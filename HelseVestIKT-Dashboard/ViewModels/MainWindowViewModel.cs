using System;
using System.Windows.Input;
using HelseVestIKT_Dashboard.Infrastructure;
using HelseVestIKT_Dashboard.Services;
using HelseVestIKT_Dashboard.Helpers.Commands;

namespace HelseVestIKT_Dashboard.ViewModels
{
	public class MainWindowViewModel : BaseViewModel
	{
		private readonly AudioService _audio;
		private double _heightSetting;
		private float _volume;
		private string _currentPlayer = "";
		private string _currentStatus = "";

		public MainWindowViewModel()
		{
			_audio = new AudioService();
			_audio.VolumeChanged += (s, v) => Volume = v;
			OpenVrInterop.Initialize();
		}

		public double HeightSetting
		{
			get => _heightSetting;
			set
			{
				if (Math.Abs(_heightSetting - value) > 0.0001)
				{
					_heightSetting = value;
					RaisePropertyChanged();
					OpenVrInterop.SetHeightOffset((float)value);
				}
			}
		}

		public float Volume
		{
			get => _volume;
			set
			{
				if (Math.Abs(_volume - value) > 0.0001f)
				{
					_volume = value;
					RaisePropertyChanged();
					_audio.CurrentVolume = value;
				}
			}
		}

		public string CurrentPlayer
		{
			get => _currentPlayer;
			set { _currentPlayer = value; RaisePropertyChanged(); }
		}

		public string CurrentStatus
		{
			get => _currentStatus;
			set { _currentStatus = value; RaisePropertyChanged(); }
		}

		public ICommand RefreshGameStatusCommand => new RelayCommand(_ =>
		{
			// kall GameStatusManager.UpdateCurrentGameAndStatus() og oppdater
		});
	}
}