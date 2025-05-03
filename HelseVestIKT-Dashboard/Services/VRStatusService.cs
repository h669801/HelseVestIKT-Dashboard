using HelseVestIKT_Dashboard.Infrastructure;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Valve.VR;
using static SteamKit2.GC.Dota.Internal.CMsgSteamLearn_InferenceMetadata_Response;
using static SteamKit2.Internal.CPlayer_GetGameBadgeLevels_Response;

namespace HelseVestIKT_Dashboard.Services
{
	public class VRStatusService
	{
		private CVRSystem? _vrSystem;
		private readonly VRStatusManager _statusModel;
		DispatcherTimer? _statusTimer;

		public VRStatusService(VRStatusManager statusModel)
		{
			_statusModel = statusModel;
		}

		public void InitializeBackground()
		{
			OpenVR.Shutdown();
			EVRInitError err = EVRInitError.None;
			OpenVR.Init(ref err, EVRApplicationType.VRApplication_Background);
			if (err == EVRInitError.None)
				_vrSystem = OpenVR.System;
			else
				throw new InvalidOperationException($"OpenVR init failed: {err}");
		}

		public void StartStatusUpdates(TimeSpan interval)
		{
			if (_vrSystem == null)
			{
				Debug.WriteLine($"[VRStatus] VR-system ikke initialisert – hopper over status-timer.");
				return;  // Ikke kast: bare ikke start timer
			}

			_statusTimer = new DispatcherTimer { Interval = interval };
			_statusTimer.Tick += (_, __) => RefreshStatus();
			_statusTimer.Start();
		}

		public void StopStatusUpdates()
		{
			_statusTimer?.Stop();
		}
		private void RefreshStatus()
		{
			if (_vrSystem == null) return;
			// ... kopier koden fra UpdateVREquipmentStatus her, men oppdater på _statusModel ...
		}

		public void Shutdown()
		{
			_statusTimer?.Stop();
			OpenVR.Shutdown();
		}

		private void Start()
		{
			_statusTimer = new DispatcherTimer();
			_statusTimer.Interval = TimeSpan.FromSeconds(7);
			_statusTimer.Tick += (s, e) => Update();
			_statusTimer.Start();
		}

		private void Update()
		{
			if (_vrSystem == null)
				return;

			// Update headset status (assuming headset is device index 0)
			bool headsetConnected = _vrSystem.IsTrackedDeviceConnected(0);
			_statusModel.IsHeadsetConnected = headsetConnected;
			if (headsetConnected)
			{
				ETrackedPropertyError error = ETrackedPropertyError.TrackedProp_Success;
				float battery = _vrSystem.GetFloatTrackedDeviceProperty(0, ETrackedDeviceProperty.Prop_DeviceBatteryPercentage_Float, ref error);
				double newBatteryPercentage = battery * 100; // assuming value is between 0 and 1
				if (Math.Abs(_statusModel.HeadsetBatteryPercentage - newBatteryPercentage) > 1)
				{
					_statusModel.HeadsetBatteryPercentage = newBatteryPercentage;
				}
			}
			else
			{
				_statusModel.HeadsetBatteryPercentage = 0;
			}

			// For left controller:
			uint leftIndex = _vrSystem.GetTrackedDeviceIndexForControllerRole(ETrackedControllerRole.LeftHand);
			bool leftConnected = _vrSystem.IsTrackedDeviceConnected(leftIndex);
			_statusModel.IsLeftControllerConnected = leftConnected;
			if (leftConnected)
			{
				ETrackedPropertyError error = ETrackedPropertyError.TrackedProp_Success;
				float battery = _vrSystem.GetFloatTrackedDeviceProperty(leftIndex, ETrackedDeviceProperty.Prop_DeviceBatteryPercentage_Float, ref error);
				_statusModel.LeftControllerBatteryPercentage = battery * 100;
			}
			else
			{
				_statusModel.LeftControllerBatteryPercentage = 0;
			}

			// For right controller:
			uint rightIndex = _vrSystem.GetTrackedDeviceIndexForControllerRole(ETrackedControllerRole.RightHand);
			bool rightConnected = _vrSystem.IsTrackedDeviceConnected(rightIndex);
			_statusModel.IsRightControllerConnected = rightConnected;
			if (rightConnected)
			{
				ETrackedPropertyError error = ETrackedPropertyError.TrackedProp_Success;
				float battery = _vrSystem.GetFloatTrackedDeviceProperty(rightIndex, ETrackedDeviceProperty.Prop_DeviceBatteryPercentage_Float, ref error);
				Console.WriteLine($"Right Controller battery: {battery}, error {error}");
				_statusModel.RightControllerBatteryPercentage = battery * 100;
			}
			else
			{
				_statusModel.RightControllerBatteryPercentage = 0;
			}
		}

		public void Stop() => _statusTimer?.Stop();


	}
}