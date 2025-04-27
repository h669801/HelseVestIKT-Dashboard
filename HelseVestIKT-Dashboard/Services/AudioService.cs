using System;
using NAudio.CoreAudioApi;

namespace HelseVestIKT_Dashboard.Services
{
	/// <summary>
	/// Behandler lesing og skriving av systemvolum via NAudio.
	/// </summary>
	public class AudioService : IDisposable
	{
		private readonly MMDeviceEnumerator _enumerator;
		private readonly MMDevice _device;

		/// <summary>
		/// Hendelse som utløses når systemvolum endres utenfra.
		/// </summary>
		public event EventHandler<float>? VolumeChanged;

		/// <summary>
		/// Henter eller setter gjeldende systemvolum som 0.0–1.0.
		/// </summary>
		public float CurrentVolume
		{
			get => _device.AudioEndpointVolume.MasterVolumeLevelScalar;
			set => _device.AudioEndpointVolume.MasterVolumeLevelScalar = Math.Clamp(value, 0f, 1f);
		}

		public AudioService()
		{
			_enumerator = new MMDeviceEnumerator();
			_device = _enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
			_device.AudioEndpointVolume.OnVolumeNotification += OnVolumeNotification;
		}

		private void OnVolumeNotification(AudioVolumeNotificationData data)
		{
			VolumeChanged?.Invoke(this, data.MasterVolume);
		}

		public void Dispose()
		{
			_device.AudioEndpointVolume.OnVolumeNotification -= OnVolumeNotification;
			_enumerator.Dispose();
		}
	}
}