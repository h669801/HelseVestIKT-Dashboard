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

		public VRDashboardService(GameProcessService processService, GameStatusManager gameStatusManager)
		{
			_processService = processService;
			_gameStatusManager = gameStatusManager;
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
		public void OpenDashboard()
		{
			var err = OpenVR.Applications.LaunchDashboardOverlay("");
			if (err != EVRApplicationError.None)
				throw new InvalidOperationException(err.ToString());
		}


		public void PauseKnapp_Click(object sender, RoutedEventArgs e)
		{
			// 1) Finn spillprosessen slik du allerede gjør
			var proc = _processService.GetRunningGameProcess();

			if (proc == null)
			{
				MessageBox.Show("Fant ingen spill å pause.", "Pause", MessageBoxButton.OK, MessageBoxImage.Warning);
				return;
			}

			// 2) Sjekk at OpenVR.Applications er initialisert
			var applications = OpenVR.Applications;
			if (applications == null)
			{
				MessageBox.Show("OpenVR.Applications er ikke tilgjengelig.", "Pause", MessageBoxButton.OK, MessageBoxImage.Error);
				return;
			}

			// 3) Prøv å åpne SteamVR‐dashboard overlay
			//    Argumentet er appKey for det SteamVR-dashboard‐overlayet du vil vise.
			//    Her bruker vi tom streng for å vise brukerens standard dashboard.
			EVRApplicationError err = applications.LaunchDashboardOverlay(string.Empty);

			// 4) Håndter eventuelle feil
			if (err != EVRApplicationError.None)
			{
				MessageBox.Show(
					$"Kunne ikke åpne SteamVR-dashboard: {err}",
					"Pause",
					MessageBoxButton.OK,
					MessageBoxImage.Error
				);
			}
		}
	}
}
