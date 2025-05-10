using HelseVestIKT_Dashboard.Views;
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
		private readonly GameStatusManager _gameStatusManager;
		private readonly VRInitService _initService;

		public VRDashboardService(GameProcessService processService, GameStatusManager gameStatusManager, VRInitService initService)
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
	

		public void PauseKnapp_Click(object sender, RoutedEventArgs e)
		{
			
			// 2) Åpne SteamVR Dashboard (samme som menyknappen på kontrolleren)
			EVRApplicationError err = OpenVR.Applications.LaunchDashboardOverlay("");
			if (err != EVRApplicationError.None)
			{
				MessageBox.Show(
					$" [VRDashboardService] Kunne ikke åpne SteamVR Dashboard: {err}",
					"Feil ved åpning av dashboard",
					MessageBoxButton.OK,
					MessageBoxImage.Error);
			}
		}

	}
}
