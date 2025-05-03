using SimpleWifi;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using SimpleWifi;

namespace HelseVestIKT_Dashboard.Services
{

	public class WifiStatusManager
	{
		private readonly System.Windows.Controls.Image _icon;
		private readonly Wifi _wifi = new Wifi();
		private DispatcherTimer? _wifiSignalTimer;

		public WifiStatusManager(System.Windows.Controls.Image iconControl)
		{
			_icon = iconControl;
		}

		public void StartMonitoringWifiSignal()
		{
			_wifiSignalTimer = new DispatcherTimer
			{
				Interval = TimeSpan.FromSeconds(2) // update every 2 seconds
			};
			_wifiSignalTimer.Tick += WifiSignalTimer_Tick;
			_wifiSignalTimer.Start();
		}

		private void UpdateWifiIcon(uint signalStrength)
		{
			string iconPath = signalStrength switch
			{
				>= 78 => "pack://application:,,,/Assets/Bilder/wifi_3_bar.png",
				>= 52 => "pack://application:,,,/Assets/Bilder/wifi_2_bar.png",
				>= 1 => "pack://application:,,,/Assets/Bilder/wifi_1_bar.png",
				_ => "pack://application:,,,/Assets/Bilder/wifi_0_bar.png",
			};
			_icon.Source = new BitmapImage(new Uri(iconPath, UriKind.Absolute));
		}

		private void WifiSignalTimer_Tick(object? sender, EventArgs e)
		{
			var connected = _wifi.GetAccessPoints().FirstOrDefault(ap => ap.IsConnected);
			UpdateWifiIcon(connected?.SignalStrength ?? 0);
		}

		public void StopMonitoringWifiSignal()
		{
			_wifiSignalTimer?.Stop();
		}

		/*
		public void WifiSignalTimer_Tick(object? sender, EventArgs e)
		{
			var accessPoints = wifi.GetAccessPoints();
			var connected = accessPoints.FirstOrDefault(ap => ap.IsConnected);

			if (connected != null)
			{
				// Optionally, cast the uint signal strength to int if needed
				int signalQuality = (int)connected.SignalStrength;
				// Set text to blank when a connection is present
				// Update the icon based on the signal strength
				UpdateWifiIcon(connected.SignalStrength);
			}
			else
			{
				// Optionally show the "no signal" icon
				UpdateWifiIcon(0);
			}
		}
		*/
	}
}
