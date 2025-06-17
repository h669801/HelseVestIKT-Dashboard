// Services/GameStatusManager.cs
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Valve.VR;
using HelseVestIKT_Dashboard.Models;

namespace HelseVestIKT_Dashboard.Services
{
	/// <summary>
	/// Holder oversikt over gjeldende spill og status.
	/// Forbedret VR-detektering via OpenVR applikasjonsnøkkel.
	/// </summary>
	public class GameStatusService
	{
		private readonly IEnumerable<Game> _allGames;
		public string CurrentPlayer { get; private set; } = "Ingen spill kjører";
		public string CurrentStatus { get; private set; } = string.Empty;
		public Game? CurrentGame { get; private set; }

		// Eksplisitt lansert spill
		private Process? _launchedProcess;
		private Game? _launchedGame;

		public GameStatusService(IEnumerable<Game> allGames)
		{
			_allGames = allGames;
		}

		/// <summary>
		/// Kall denne rett etter å ha startet spillet (i LaunchGameAsync)
		/// </summary>
		public void SetLaunchedGame(Game game, Process proc)
		{
			_launchedGame = game;
			_launchedProcess = proc;
		}

		/// <summary>
		/// Oppdaterer CurrentGame og CurrentStatus.
		/// 1) Eksplisitt lansert spill
		/// 2) VR-baserte applikasjonsnøkkel (mer presis enn exe-match)
		/// 3) Fallback på prosessnavn / vindustittel
		/// </summary>
		public void UpdateCurrentGameAndStatus()
		{
			// 1) Eksplisitt lansert spill
			if (_launchedProcess != null && !_launchedProcess.HasExited)
			{
				CurrentGame = _launchedGame;
				CurrentPlayer = _launchedGame?.Title ?? string.Empty;
				CurrentStatus = _launchedProcess.Responding ? "OK" : "!OK";
				return;
			}
			_launchedProcess = null;
			_launchedGame = null;

			// 2) VR-detektering vha. OpenVR applikasjonsnøkkel
			uint pid = OpenVR.Applications?.GetCurrentSceneProcessId() ?? 0;
			if (pid != 0)
			{
				try
				{
					// Hent applikasjonsnøkkel til det aktive VR-innholdet
					var buffer = new StringBuilder(128);
					EVRApplicationError err = OpenVR.Applications.GetApplicationKeyByProcessId(pid, buffer, (uint)buffer.Capacity);
					if (err == EVRApplicationError.None)
					{
						string appKey = buffer.ToString();
						// appKey er typisk "steam.app.{AppID}"
						var parts = appKey.Split('.');
						string id = parts.Last();
						var match = _allGames.FirstOrDefault(g => g.AppID == id);
						if (match != null)
						{
							CurrentGame = match;
							CurrentPlayer = match.Title;
							CurrentStatus = "OK";
							return;
						}
					}
				}
				catch
				{
					// ignorer eventuelle feil
				}
			}

			// 3) Fallback: prosessnavn eller vindustittel
			var running = GetCurrentlyRunningGame();
			if (running != null)
			{
				CurrentGame = running;
				CurrentPlayer = running.Title;
				bool responding = !string.IsNullOrEmpty(running.ProcessName)
					&& Process.GetProcessesByName(running.ProcessName)
							  .Any(p => p.Responding);
				CurrentStatus = responding ? "OK" : "!OK";
			}
			else
			{
				CurrentGame = null;
				CurrentPlayer = "Ingen spill kjører";
				CurrentStatus = string.Empty;
			}
		}

		/// <summary>
		/// Fallback-sjekk: prøver prosessnavn, deretter vindustittel.
		/// </summary>
		private Game? GetCurrentlyRunningGame()
		{
			// Prosessnavn
			foreach (var game in _allGames.Where(g => !string.IsNullOrEmpty(g.ProcessName)))
			{
				var procs = Process.GetProcessesByName(game.ProcessName);
				if (procs.Any(p => !string.IsNullOrEmpty(p.MainWindowTitle)))
					return game;
			}

			// Vindustittel
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

		
	}
}
