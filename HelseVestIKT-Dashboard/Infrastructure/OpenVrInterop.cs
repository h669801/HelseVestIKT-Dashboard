using System;
using Valve.VR;

namespace HelseVestIKT_Dashboard.Infrastructure
{
	/// <summary>
	/// Wrapper for OpenVR-initiering og chaperone-kall.
	/// </summary>
	public static class OpenVrInterop
	{
		private static CVRSystem? _system;

		/// <summary>Initialiserer OpenVR og lagrer CVRSystem-instansen.</summary>
		public static bool Initialize()
		{
			OpenVR.Shutdown();
			EVRInitError error = EVRInitError.None;
			OpenVR.Init(ref error, EVRApplicationType.VRApplication_Background);
			if (error != EVRInitError.None)
			{
				Console.WriteLine($"OpenVR-init feilet: {error}");
				_system = null;
				return false;
			}
			_system = OpenVR.System;
			return true;
		}

		/// <summary>Stenger den aktive OpenVR-sesjonen.</summary>
		public static void Shutdown()
		{
			OpenVR.Shutdown();
			_system = null;
		}

		/// <summary>
		/// Setter stående “zero pose” (høydekalibrering).
		/// Positivt meters–argument løfter verden nedover, så spilleren oppleves høyere.
		/// </summary>
		public static void SetHeightOffset(float meters)
		{
			if (_system == null) throw new InvalidOperationException("OpenVR ikke initialisert");

			// Hent raw zero-pose som standing-pose
			var pose = _system.GetRawZeroPoseToStandingAbsoluteTrackingPose();
			// Inverter Y-komponenten
			pose.m7 = -meters;
			// Skriv tilbake via ChaperoneSetup
			OpenVR.ChaperoneSetup.SetWorkingStandingZeroPoseToRawTrackingPose(ref pose);
			OpenVR.ChaperoneSetup.CommitWorkingCopy(EChaperoneConfigFile.Live);
		}

		/// <summary>Hjelpemetode for å sjekke om HMD er tilkoblet.</summary>
		public static bool IsHeadsetConnected()
			=> _system?.IsTrackedDeviceConnected(0) ?? false;
	}
}
