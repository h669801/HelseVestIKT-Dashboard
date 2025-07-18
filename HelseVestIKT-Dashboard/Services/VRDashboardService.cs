﻿using HelseVestIKT_Dashboard.Views;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Valve.VR;
using MessageBox = System.Windows.MessageBox;

namespace HelseVestIKT_Dashboard.Services
{
   public class VRDashboardService
    {
		private readonly GameProcessService _processService;
		private readonly GameStatusService _gameStatusManager;
		private readonly VRInitService _initService;

		public VRDashboardService(GameProcessService processService, GameStatusService gameStatusManager, VRInitService initService)
		{
			_processService = processService;
			_gameStatusManager = gameStatusManager;
			_initService = initService;
		}

		public void CloseCurrentGame()
		{
			var proc = _processService.GetRunningGameProcess();
			if (proc == null)
			{
				MessageBox.Show("Ingen spill å avslutte.", "Avslutt spill",
								MessageBoxButton.OK, MessageBoxImage.Warning);
				return;
			}

			// Prøv å lese modul-filnavn, men unngå crash
			string exeName = "";
			try { exeName = proc.MainModule?.FileName ?? ""; }
			catch { /* ignore */ }

			bool isSteam = exeName
				.IndexOf("Steam.exe", StringComparison.OrdinalIgnoreCase) >= 0;

			if (isSteam)
			{
				// Bruk Steam‐URI for å lukke guidet via Steam
				var appName = _gameStatusManager.CurrentPlayer;
				Process.Start(new ProcessStartInfo
				{
					FileName = $"steam://close/{appName}",
					UseShellExecute = true
				});
			}
			else
			{
				// Vanlig prosess‐lukking + ev. tvang etter 2s
				proc.CloseMainWindow();
				Task.Delay(2000).ContinueWith(_ =>
				{
					if (!proc.HasExited) proc.Kill();
				});
			}
		}


		/// <summary>
		/// Pauser den aktive VR-spillsesjonen ved å åpne SteamVR dashboard-overlay.
		/// </summary>
		/// <param name="sender">Knappen som ble klikket.</param>
		/// <param name="e">Event-args for klikket.</param>

		/// <summary>
		/// Pauser den aktive VR-spillsesjonen ved å åpne SteamVR-dashboard-overlay.
		/// </summary>
		public void PauseKnapp_Click(object sender, RoutedEventArgs e)
		{
			// Sjekk at SteamVR er oppe og at vi har en VR-system-instans:
			if (!OpenVR.IsHmdPresent() || OpenVR.System == null)
			{
				MessageBox.Show(
					"SteamVR kjører ikke eller headset er ikke tilkoblet.",
					"Pause",
					MessageBoxButton.OK,
					MessageBoxImage.Warning);
				return;
			}

			// Toggle dashboard via URI:
			try
			{
				Process.Start(new ProcessStartInfo(
					"vrmonitor://debugcommands/system_dashboard_toggle")
				{
					UseShellExecute = true
				});
			}
			catch (Exception ex)
			{
				MessageBox.Show(
					$"Kunne ikke toggle dashboard via URI:\n{ex.Message}",
					"Pause",
					MessageBoxButton.OK,
					MessageBoxImage.Error);
			}
		}
	}
}
