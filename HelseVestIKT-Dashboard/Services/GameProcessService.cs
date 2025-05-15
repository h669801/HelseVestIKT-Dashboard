using System;
using System.IO;
using System.Linq;
using System.Diagnostics;                      // For Process
using System.Text.RegularExpressions;         // For Regex
using Microsoft.Win32;             // For Registry
using HelseVestIKT_Dashboard.Models; // For Game

namespace HelseVestIKT_Dashboard.Services
{
	public class GameProcessService
	{
		private readonly GameStatusManager _gameStatusManager;

		public GameProcessService(GameStatusManager gameStatusManager)
		{
			_gameStatusManager = gameStatusManager;
		}

		/// <summary>
		/// Henter den for øyeblikket kjørende spillprosessen,
		/// basert på det Game-objektet GameStatusManager holder på.
		/// </summary>
		public Process? GetRunningGameProcess()
		{
			var game = _gameStatusManager.CurrentGame;
			if (game == null) return null;

			// 1) grab all procs with that simple name
			var procs = Process.GetProcessesByName(game.ProcessName);
			Console.WriteLine($"[DEBUG] Funnet {procs.Length} prosesser med navn {game.ProcessName}");

			// 2) if we know the exact install-path, match on MainModule.FileName
			//    (avoid throwing if we can’t open MainModule by wrapping in try/catch)
			string steamPath = GetSteamInstallPathFromRegistry();
			string? exePath = GetSteamExePath(steamPath, game.AppID);
			if (!string.IsNullOrEmpty(exePath))
			{
				var byPath = procs.FirstOrDefault(p =>
				{
					try
					{
						return string.Equals(
							p.MainModule.FileName,
							exePath,
							StringComparison.OrdinalIgnoreCase
						);
					}
					catch
					{
						return false;
					}
				});
				if (byPath != null) return byPath;
			}

			// 3) fallback: pick the one whose window title contains the game title
			var byTitle = procs.FirstOrDefault(p =>
			{
				try { return p.MainWindowTitle?.IndexOf(game.Title, StringComparison.OrdinalIgnoreCase) >= 0; }
				catch { return false; }
			});
			if (byTitle != null) return byTitle;

			// 4) ultimate fallback
			return procs.FirstOrDefault();
		}

		/// <summary>
		/// Reads Steam’s install path from the registry (HKCU\Software\Valve\Steam\SteamPath),
		/// or falls back to the default Program Files location if not found.
		/// </summary>
		public static string GetSteamInstallPathFromRegistry()
		{
			const string steamKey = @"Software\Valve\Steam";
			using (var key = Registry.CurrentUser.OpenSubKey(steamKey))
			{
				if (key != null)
				{
					var path = key.GetValue("SteamPath") as string;
					if (!string.IsNullOrEmpty(path))
						return path;
				}
			}

			// fallback if the registry lookup fails
			return Path.Combine(
				Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
				"Steam");
		}

		public static string? GetSteamExePath(string steamPath, string appId)
		{
			// katalogen der SteamVR og andre Steam-spill ligger
			var common = Path.Combine(steamPath, "steamapps", "common");

			// finn undermappen til dette appId
			var manifest = Path.Combine(steamPath, "steamapps", $"appmanifest_{appId}.acf");
			if (!File.Exists(manifest))
				return null;

			// les ut “installdir”
			var text = File.ReadAllText(manifest);
			var m = Regex.Match(text, "\"installdir\"\\s*\"(?<d>.*?)\"");
			if (!m.Success) return null;
			var dir = m.Groups["d"].Value;

			var folder = Path.Combine(common, dir);
			if (!Directory.Exists(folder)) return null;

			// let etter exe i hele treet, velg største
			var exes = Directory.GetFiles(folder, "*.exe", SearchOption.AllDirectories);
			if (exes.Length == 0) return null;
			return exes
				.OrderByDescending(f => new FileInfo(f).Length)
				.First();
		}
	}
}
