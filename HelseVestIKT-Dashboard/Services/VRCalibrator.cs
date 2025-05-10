using HelseVestIKT_Dashboard.Infrastructure;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Valve.VR;
using System.IO;


namespace HelseVestIKT_Dashboard.Services
{
	public class VRCalibrator
	{
		private CVRSystem? vrSystem;

		// Felles helper-metode som skal resentrere VR-visningen
		// Shared helper you can call for both seated and standing
		public void Recenter(ETrackingUniverseOrigin origin)
		{
			try
			{
				// 1) Hvis du allerede har en session, steng den
				OpenVR.Shutdown();

				// 2) Initier ny overlay-session
				EVRInitError initError = EVRInitError.None;
				OpenVR.Init(ref initError, EVRApplicationType.VRApplication_Overlay);
				if (initError != EVRInitError.None)
				{
					Console.WriteLine($"[VRCalibrator] Init feilet: {initError}");
					return;
				}


				// Nå er både OpenVR.Compositor og OpenVR.Chaperone ikke-null

				// 3) Sett ønsket tracking-space
				OpenVR.Compositor.SetTrackingSpace(origin);

				// 4) Nullstill zero-pose
				OpenVR.Chaperone.ResetZeroPose(origin);

				// 5) Hent oppdaterte poser (du kan eventuelt sende tomme arrays i stedet for null)
				var renderPoses = new Valve.VR.TrackedDevicePose_t[OpenVR.k_unMaxTrackedDeviceCount];
				var gamePoses = new Valve.VR.TrackedDevicePose_t[OpenVR.k_unMaxTrackedDeviceCount];
				OpenVR.Compositor.WaitGetPoses(renderPoses, gamePoses);

				Console.WriteLine($"[VRCalibrator] Recenter fullført: {origin}");
			}
			catch (Exception ex)
			{
				// Logg detalj, unngå at kallende tråd kollapser
				Console.WriteLine($"[VRCalibrator] Recenter unntak: {ex.Message}");
			}
		}


			/// <summary>
			/// Justerer verdens Y-offset slik at brukeren oppleves høyere/lavere.
			/// </summary>
		public void ApplyHeight(double heightMeters)
		{
			// dersom vi ikke har system fra før, hent det fra OpenVR
			if (vrSystem == null)
				vrSystem = OpenVR.System;
			if (vrSystem == null)
			{
				Console.WriteLine("[VRCalibrator] VR.System ikke tilgjengelig");
				return;
			}

			try
			{

				HmdMatrix34_t rawPose = vrSystem.GetRawZeroPoseToStandingAbsoluteTrackingPose();
				rawPose.m7 = -(float)heightMeters;
				var chaperoneSetup = OpenVR.ChaperoneSetup;

				chaperoneSetup.SetWorkingStandingZeroPoseToRawTrackingPose(ref rawPose);
				chaperoneSetup.CommitWorkingCopy(EChaperoneConfigFile.Live);
				Console.WriteLine($"[VRCalibrator] Høyde satt til {heightMeters}m");
			}
			catch (Exception ex)
			{
				Console.WriteLine($"[VRCalibrator] ApplyHeight unntak: {ex.Message}");
			}
			}

		/// <summary>
		/// Eksempel: henter ut gjeldende kalibrering fra ChaperoneSetup.
		/// </summary>
		public float LoadCurrentHeightCalibration()
		{
			if (vrSystem == null)
				throw new InvalidOperationException("VR‐system ikke klart");

			HmdMatrix34_t pose = new HmdMatrix34_t();
			bool ok = OpenVR.ChaperoneSetup.GetWorkingStandingZeroPoseToRawTrackingPose(ref pose);
			if (!ok)
				throw new InvalidOperationException("Kunne ikke hente zero‐pose");

			// m7 er Y‐offset i matrisen, negativ av øyehøyde
			return -(float)pose.m7;
		}

		public void Initialize(CVRSystem system)
		{
			vrSystem = system;
		}
	}
}
