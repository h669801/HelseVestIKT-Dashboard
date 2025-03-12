using System;
using System.ComponentModel;
using System.Windows.Media;
using Valve.VR;

namespace HelseVestIKT_Dashboard
{
	public class VREquipmentStatusViewModel : INotifyPropertyChanged
	{
		private bool _isHeadsetConnected;
		public bool IsHeadsetConnected
		{
			get => _isHeadsetConnected;
			set
			{
				if (_isHeadsetConnected != value)
				{
					_isHeadsetConnected = value;
					OnPropertyChanged(nameof(IsHeadsetConnected));
				}
			}
		}

		private double _headsetBatteryPercentage;
		public double HeadsetBatteryPercentage
		{
			get => _headsetBatteryPercentage;
			set
			{
				if (_headsetBatteryPercentage != value)
				{
					_headsetBatteryPercentage = value;
					OnPropertyChanged(nameof(HeadsetBatteryPercentage));
				}
			}
		}

		// Similarly for controllers
		private bool _isLeftControllerConnected;
		public bool IsLeftControllerConnected
		{
			get => _isLeftControllerConnected;
			set
			{
				if (_isLeftControllerConnected != value)
				{
					_isLeftControllerConnected = value;
					OnPropertyChanged(nameof(IsLeftControllerConnected));
				}
			}
		}

		private double _leftControllerBatteryPercentage;
		public double LeftControllerBatteryPercentage
		{
			get => _leftControllerBatteryPercentage;
			set
			{
				if (_leftControllerBatteryPercentage != value)
				{
					_leftControllerBatteryPercentage = value;
					OnPropertyChanged(nameof(LeftControllerBatteryPercentage));
				}
			}
		}

		private bool _isRightControllerConnected;
		public bool IsRightControllerConnected
		{
			get => _isRightControllerConnected;
			set
			{
				if (_isRightControllerConnected != value)
				{
					_isRightControllerConnected = value;
					OnPropertyChanged(nameof(IsRightControllerConnected));
				}
			}
		}

		private double _rightControllerBatteryPercentage;
		public double RightControllerBatteryPercentage
		{
			get => _rightControllerBatteryPercentage;
			set
			{
				if (_rightControllerBatteryPercentage != value)
				{
					_rightControllerBatteryPercentage = value;
					OnPropertyChanged(nameof(RightControllerBatteryPercentage));
				}
			}
		}

		public event PropertyChangedEventHandler? PropertyChanged;
		protected void OnPropertyChanged(string propName) =>
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
	}
}
