using System;
using System.Windows.Threading;
using System.Windows;
using NAudio.CoreAudioApi;
using NAudio.Gui;
using System.Windows.Media;
using HelseVestIKT_Dashboard.Models;

namespace HelseVestIKT_Dashboard.Services
{
	/// <summary>
	/// Behandler lesing og skriving av systemvolum via NAudio.
	/// </summary>
	public class AudioService : IDisposable
	{
		private readonly MMDeviceEnumerator _enumerator;
		private readonly MMDevice _device;
		private DispatcherTimer? volumeStatusTimer = null;
		public ImageSource VolumeIcon => StockIcons.GetVolumeIcon();

		/// <summary>
		/// Hendelse som utløses når systemvolum endres utenfra.
		/// </summary>
		public event EventHandler<float>? VolumeChanged;

		/// <summary>
		/// Henter eller setter gjeldende systemvolum som 0.0–1.0.
		/// </summary>
		/// 


		public AudioService()
		{
			_enumerator = new MMDeviceEnumerator();
			_device = _enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
			_device.AudioEndpointVolume.OnVolumeNotification += OnVolumeNotification;
			VolumeChanged?.Invoke(this, _device.AudioEndpointVolume.MasterVolumeLevelScalar);
		}

		public float CurrentVolume
		{
			get => _device.AudioEndpointVolume.MasterVolumeLevelScalar;
			set
			{
				_device.AudioEndpointVolume.MasterVolumeLevelScalar = Math.Clamp(value, 0f, 1f);
				VolumeChanged?.Invoke(this, _device.AudioEndpointVolume.MasterVolumeLevelScalar);
			}
			}

		private void OnVolumeNotification(AudioVolumeNotificationData data)
		{
			VolumeChanged?.Invoke(this, data.MasterVolume);
		}

		public void Dispose()
		{
			_device.AudioEndpointVolume.OnVolumeNotification -= OnVolumeNotification;
		}

	}
}