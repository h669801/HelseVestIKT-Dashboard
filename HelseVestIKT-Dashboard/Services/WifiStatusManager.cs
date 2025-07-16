using SimpleWifi;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using SimpleWifi;
using System.Diagnostics;

namespace HelseVestIKT_Dashboard.Services
{

	public class WifiStatusManager
	{
		private readonly System.Windows.Controls.Image _icon;
		private Wifi? _wifi = new Wifi();
		private DispatcherTimer? _wifiSignalTimer;

		/// <summary>
		/// Initialiserer manager med bildet som skal vise signalstyrke.
		/// </summary>
		/// <param name="iconControl">Image-kontroll for Wi-Fi-ikon.</param>
		public WifiStatusManager(System.Windows.Controls.Image iconControl)
		{
			_icon = iconControl;
		}

		/// <summary>
		/// Starter overvåkning av Wi-Fi-signal.
		/// </summary>
		public void StartMonitoringWifiSignal()
		{
			try
			{
				_wifi = new Wifi();
			}
			catch (Exception ex)
			{
				Debug.WriteLine($"Kunne ikke opprette Wifi-klient: {ex.Message}");
				UpdateWifiIcon(0);
				return;
			}
				_wifiSignalTimer = new DispatcherTimer
			{
				Interval = TimeSpan.FromSeconds(5) // update every 2 seconds
			};
			_wifiSignalTimer.Tick += WifiSignalTimer_Tick;
			_wifiSignalTimer.Start();
		}

		/// <summary>
		/// Oppdaterer ikonet basert på signalstyrke.
		/// </summary>
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


		/// <summary>
		/// Timer-hendelse: henter signalstyrke i bakgrunnstråd og oppdaterer UI.
		/// </summary>
		private void WifiSignalTimer_Tick(object? sender, EventArgs e)
		{
			if (_wifi == null)
				return;
			Task.Run(() =>
			{
				try
				{
					var connected = _wifi.GetAccessPoints().FirstOrDefault(ap => ap.IsConnected);
					uint strength = connected?.SignalStrength ?? 0;
					_icon.Dispatcher.Invoke(() => UpdateWifiIcon(strength));
				}
				catch (Exception ex)
				{
					Debug.WriteLine($"Feil ved henting av Wi-Fi-status: {ex.Message}");
				}
			});
		}

		/// <summary>
		/// Stopper overvåkningen av Wi-Fi-signal.
		/// </summary>
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
