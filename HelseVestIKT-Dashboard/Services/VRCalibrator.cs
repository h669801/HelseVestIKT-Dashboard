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
	public class VRCalibrator : IDisposable
	{
		private CVRSystem? _vrSystem;
		private bool _overlaySessionActive;
		private bool _disposed;
		private bool _initialized;

		/// <summary>
		/// Initialiserer VR-systemet. Kalles én gang ved oppstart.
		/// </summary>
		public void Initialize(CVRSystem system)
		{
			_vrSystem = system ?? throw new ArgumentNullException(nameof(system));
			_initialized = true;
		}


		/// <summary>
		/// Initialiserer en kort overlay‐session for kalibrering.
		/// </summary>
		/// 
		private async Task<bool> InitOverlaySessionAsync()
		{
			if (_overlaySessionActive)
				return true;

			return await Task.Run(() =>
			{
				EVRInitError err = EVRInitError.None;
				OpenVR.Shutdown();
				OpenVR.Init(ref err, EVRApplicationType.VRApplication_Overlay);
				_overlaySessionActive = (err == EVRInitError.None);
				if (!_overlaySessionActive)
					System.Diagnostics.Debug.WriteLine($"[VRCalivrator] Init overlay feilet: {err}");
				return _overlaySessionActive;

			});
		}

		// Felles helper-metode som skal resentrere VR-visningen
		// Shared helper you can call for both seated and standing
		public async Task RecenterAsync(ETrackingUniverseOrigin origin)
		{

			if (!await InitOverlaySessionAsync())
				return;

			try
			{

				// 3) Sett ønsket tracking-space
				OpenVR.Compositor.SetTrackingSpace(origin);

				// 4) Nullstill zero-pose
				OpenVR.Chaperone.ResetZeroPose(origin);

				// 5) Hent oppdaterte poser (du kan eventuelt sende tomme arrays i stedet for null)
				var renderPoses = new Valve.VR.TrackedDevicePose_t[OpenVR.k_unMaxTrackedDeviceCount];
				var gamePoses = new Valve.VR.TrackedDevicePose_t[OpenVR.k_unMaxTrackedDeviceCount];
				OpenVR.Compositor.WaitGetPoses(renderPoses, gamePoses);

				System.Diagnostics.Debug.WriteLine($"[VRCalibrator] Recenter({origin}) fullført.");
			}
			catch (Exception ex)
			{
				// Logg detalj, unngå at kallende tråd kollapser
				System.Diagnostics.Debug.WriteLine($"[VRCalibrator] Recenter unntak: {ex}");
			}
		}


		/*

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
		
		*/

		/// <summary>
		/// Justerer høydeoffset slik at brukerens hodehav ønsket meters over gulvet.
		/// </summary>
		public void ApplyHeight(float heightMeters)
		{
			try
			{
				// Hent rå pose‐matrise for standing‐mod
				var raw = _vrSystem.GetRawZeroPoseToStandingAbsoluteTrackingPose();
				// m7 er Y‐offset, negativ av ønsket høyde
				raw.m7 = -heightMeters;
				var setup = OpenVR.ChaperoneSetup;
				setup.SetWorkingStandingZeroPoseToRawTrackingPose(ref raw);
				setup.CommitWorkingCopy(EChaperoneConfigFile.Live);
				System.Diagnostics.Debug.WriteLine($"[VRCalibrator] Høyde satt til {heightMeters:F2} m");
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"[VRCalibrator] ApplyHeight unntak: {ex}");
			}
		}


		/*
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
		*/

		/// <summary>
		/// Leser tilbake gjeldende høyde‐kalibrering (i meter).
		/// </summary>
		public float LoadCurrentHeightCalibration()
		{
			// Henter den sist committede zero‐pose
			var pose = new HmdMatrix34_t();
			if (!OpenVR.ChaperoneSetup.GetWorkingStandingZeroPoseToRawTrackingPose(ref pose))
				throw new InvalidOperationException("Kunne ikke hente kalibreringspose.");

			return -(float)pose.m7;
		}

		/// <summary>
		/// Rydder opp ressursene—stenger overlay‐session om aktiv.
		/// </summary>
		public void Dispose()
		{
			if (_disposed) return;
			_disposed = true;

			try
			{
				if (_overlaySessionActive)
				{
					OpenVR.Shutdown();
					_overlaySessionActive = false;
				}
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"[VRCalibrator] Dispose unntak: {ex}");
			}
		}
	}
}
