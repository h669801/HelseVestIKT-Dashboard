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
		public string CurrentPlayer { get; private set; }
		public string CurrentStatus { get; private set; }

		public Game? _currentGame;

		public GameStatusManager(IEnumerable<Game> allGames)
		{
			_allGames = allGames;
		}

		private Game? GetCurrentlyRunningGame()
		{
			foreach(var game in _allGames)
					{
				var processes = Process.GetProcessesByName(game.ProcessName);
				if (processes.Length > 0)
				{
					return game;
				}
			}
				return null;
			}

		public void UpdateCurrentGameAndStatus()
		{
			Game? currentGame = GetCurrentlyRunningGame();
			if (currentGame != null)
			{
				// Update the game title
				CurrentPlayer = currentGame.Title;

				// Check performance (example: using the process's Responding property)
				var processes = Process.GetProcessesByName(currentGame.ProcessName);
				if (processes.Length > 0 && processes[0].Responding)
				{
					CurrentStatus = "OK";
				}
				else
				{
					CurrentStatus = "!OK";
				}
			}
			else
			{
				CurrentPlayer = "Ingen spill kjører";
				CurrentStatus = "";
			}
		}

		
	}
}
