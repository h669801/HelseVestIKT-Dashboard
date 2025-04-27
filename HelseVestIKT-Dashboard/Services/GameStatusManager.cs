using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Diagnostics;
using Valve.VR;
using System.IO;
using HelseVestIKT_Dashboard.Models;


namespace HelseVestIKT_Dashboard.Services
{

	public class GameStatusManager
	{
		// You can either return the values or use events to notify MainWindow
		private readonly IEnumerable<Game> _allGames;
		public string CurrentPlayer { get; private set; } = "Ingen spill kjører";
		public string CurrentStatus { get; private set; } = "";

		public Game? CurrentGame { get; private set; }

		private Process? _launchedProcess;
		private Game? _launchedGame;


		public GameStatusManager(IEnumerable<Game> allGames)
		{
			_allGames = allGames;
		}
		private Game? GetCurrentlyRunningGame()
		{
			// 1) Forsøk match på prosessnavn (raskest, mest pålitelig for VR‑apper uten vindu)
			foreach (var game in _allGames.Where(g => !string.IsNullOrEmpty(g.ProcessName)))
			{
				var procs = Process.GetProcessesByName(game.ProcessName);
				if (procs.Any(p => !String.IsNullOrEmpty(p.MainWindowTitle)))
					return game;
			}
			// 2) Fallback til vindustittel‑match
			foreach (var proc in Process.GetProcesses().Where(p => !string.IsNullOrEmpty(p.MainWindowTitle)))
			{
				string title = proc.MainWindowTitle;
				var match = _allGames.FirstOrDefault(g =>
					!string.IsNullOrEmpty(g.Title) &&
					title.IndexOf(g.Title, StringComparison.OrdinalIgnoreCase) >= 0);
				if (match != null)
					return match;
			}

			return null;
		}



		public void UpdateCurrentGameAndStatus()
		{
			// —————— NY LØSNING START ——————
			// Spør OpenVR om den aktive scene-processId
			uint pid = OpenVR.Applications?.GetCurrentSceneProcessId() ?? 0;
			if (pid != 0)
			{
				try
				{
					var p = Process.GetProcessById((int)pid);
					// Prøv å matche exe-navnet mot InstallPath i listen
					var match = _allGames.FirstOrDefault(g =>
						Path.GetFileNameWithoutExtension(g.InstallPath)
							.Equals(
								Path.GetFileNameWithoutExtension(p.MainModule?.FileName),
								StringComparison.OrdinalIgnoreCase
							)
					);

					if (match != null)
					{
						// Vi fant et SteamVR-scene-spill – bruk det!
						CurrentGame = match;
						CurrentPlayer = match.Title;
						CurrentStatus = p.Responding ? "OK" : "!OK";
						return;
					}
				}
				catch
				{
					// ignorer eventuelle tilgangs-errors på MainModule
				}
			}
			// —————— NY LØSNING SLUTT ——————

			// Deretter din gamle logikk:
			CurrentGame = GetCurrentlyRunningGame();
			if (CurrentGame != null)
			{
				CurrentPlayer = CurrentGame.Title;
				bool responding = !string.IsNullOrEmpty(CurrentGame.ProcessName)
					&& Process
						.GetProcessesByName(CurrentGame.ProcessName)
						.Any(p => p.Responding);
				CurrentStatus = responding ? "OK" : "!OK";
			}
			else
			{
				CurrentPlayer = "Ingen spill kjører";
				CurrentStatus = "";
			}
		}
	}
}
