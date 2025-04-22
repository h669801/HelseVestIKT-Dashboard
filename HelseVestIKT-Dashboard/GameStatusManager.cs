using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Diagnostics;

namespace HelseVestIKT_Dashboard
{
	public class GameStatusManager
	{
		// You can either return the values or use events to notify MainWindow
		private readonly IEnumerable<Game> _allGames;
		public string CurrentPlayer { get; private set; } = "Ingen spill kjører";
		public string CurrentStatus { get; private set; } = "";

		public Game? CurrentGame { get; private set; }

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
				if (procs.Any(p => !p.HasExited))
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
			CurrentGame = GetCurrentlyRunningGame();

			if (CurrentGame != null)
			{
				CurrentPlayer = CurrentGame.Title;

				// Sjekk om prosessen tikker
				bool responding = Process
					.GetProcesses()
					.Where(p => !string.IsNullOrEmpty(p.MainWindowTitle))
					.Any(p => p.MainWindowTitle
							   .IndexOf(CurrentGame.Title, StringComparison.OrdinalIgnoreCase) >= 0
							   && p.Responding);

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
