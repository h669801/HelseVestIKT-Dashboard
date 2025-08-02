// Fil: Services/VRCalibrator.cs
using HelseVestIKT_Dashboard.Infrastructure;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Valve.VR;

namespace HelseVestIKT_Dashboard.Services
{
	/// <summary>
	/// Håndterer init og kalibrerings-operasjoner mot OpenVR/SteamVR.
	/// Initieres én gang, og Dispose kalles når applikasjonen terminerer.
	/// </summary>
	public class VRCalibrator : IDisposable
	{
		private CVRSystem _vrSystem = null!;
		private bool _initialized;
		private bool _disposed;

		/// <summary>
		/// Standardkonstruktør. Kall Initialize() etter at SteamVR er startet.
		/// </summary>
		public VRCalibrator() { }

		/// <summary>
		/// Initialiserer VR-systemet. Kalles én gang ved oppstart av applikasjonen.
		/// </summary>
		public void Initialize()
		{
			if (_initialized)
				return;

			// Sjekk at SteamVR-runtime finnes
			if (!OpenVR.IsRuntimeInstalled())
				throw new InvalidOperationException("SteamVR-runtime ikke installert.");

			// Init OpenVR i Scene-modus
			EVRInitError err = EVRInitError.None;
			OpenVR.Init(ref err, EVRApplicationType.VRApplication_Scene);
			if (err != EVRInitError.None)
				throw new InvalidOperationException($"OpenVR.Init feilet: {err}");

			// Hent system-instans
			_vrSystem = OpenVR.System ?? throw new InvalidOperationException("Kunne ikke hente CVRSystem.");
			_initialized = true;
			Debug.WriteLine("[VRCalibrator] VR-system initialisert.");
		}

		/// <summary>
		/// Resentrerer scenen (seated/standing) uten å re-initiere hele VR-runtime.
		/// </summary>
		public async Task RecenterAsync(ETrackingUniverseOrigin origin)
		{
			if (!_initialized)
				throw new InvalidOperationException("VR-systemet er ikke initialisert.");

			try
			{
				// Sett ønsket tracking-space
				OpenVR.Compositor.SetTrackingSpace(origin);

				// Nullstill zero-pose
				OpenVR.Chaperone.ResetZeroPose(origin);

				// Vent litt før vi henter nye poser
				await Task.Delay(50);

				// Hent de oppdaterte posene (må sende inn matriser av riktig størrelse)
				var renderPoses = new TrackedDevicePose_t[OpenVR.k_unMaxTrackedDeviceCount];
				var gamePoses = new TrackedDevicePose_t[OpenVR.k_unMaxTrackedDeviceCount];
				OpenVR.Compositor.WaitGetPoses(renderPoses, gamePoses);

				Debug.WriteLine($"[VRCalibrator] Recenter({origin}) fullført.");
			}
			catch (Exception ex)
			{
				Debug.WriteLine($"[VRCalibrator] Recenter unntak: {ex}");
			}
		}

		/// <summary>
		/// Justerer høydeoffset (øyehøyde) i meter.
		/// </summary>
		public void ApplyHeight(float heightMeters)
		{
			if (!_initialized)
				throw new InvalidOperationException("VR-systemet er ikke initialisert.");

			try
			{
				// Hent rå pose-matrise for standing-mode
				var raw = _vrSystem.GetRawZeroPoseToStandingAbsoluteTrackingPose();
				raw.m7 = -heightMeters; // negasjon: VR forventer minus-verdi

				// Sett ny pose og commit
				var setup = OpenVR.ChaperoneSetup;
				setup.SetWorkingStandingZeroPoseToRawTrackingPose(ref raw);
				setup.CommitWorkingCopy(EChaperoneConfigFile.Live);

				Debug.WriteLine($"[VRCalibrator] Høyde satt til {heightMeters:F2} m");
			}
			catch (Exception ex)
			{
				Debug.WriteLine($"[VRCalibrator] ApplyHeight unntak: {ex}");
			}
		}

		/// <summary>
		/// Leser tilbake gjeldende høydekalibrering i meter.
		/// </summary>
		public float LoadCurrentHeightCalibration()
		{
			if (!_initialized)
				throw new InvalidOperationException("VR-systemet er ikke initialisert.");

			var pose = new HmdMatrix34_t();
			bool success = OpenVR.ChaperoneSetup.GetWorkingStandingZeroPoseToRawTrackingPose(ref pose);
			if (!success)
				throw new InvalidOperationException("Kunne ikke hente kalibreringspose.");

			return -(float)pose.m7;
		}

		/// <summary>
		/// Rydder opp: Kaller Shutdown én gang når applikasjonen avsluttes.
		/// </summary>
		public void Dispose()
		{
			if (_disposed)
				return;

			_disposed = true;
			if (_initialized)
			{
				try
				{
					OpenVR.Shutdown();
					Debug.WriteLine("[VRCalibrator] OpenVR.Shutdown kalt.");
				}
				catch (Exception ex)
				{
					Debug.WriteLine($"[VRCalibrator] Dispose unntak: {ex}");
				}
			}
		}
	}
}
