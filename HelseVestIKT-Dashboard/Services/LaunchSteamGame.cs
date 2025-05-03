using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace HelseVestIKT_Dashboard.Services
{
	public static class SteamLauncher
	{
		public static void LaunchSteamGame(string appId)
		{
			try
			{
				ProcessStartInfo psi = new ProcessStartInfo($"steam://run/{appId}")
				{
					UseShellExecute = true
				};
				Process.Start(psi);
			}
			catch (Exception ex)
			{
				System.Windows.MessageBox.Show("Error launching game: " + ex.Message);
			}
		}

		public static string? GetProcessNameFromSteam(string steamPath, string appId)
		{
			// 1) Les manifest
			var manifest = Path.Combine(steamPath, "steamapps", $"appmanifest_{appId}.acf");
			if (!File.Exists(manifest)) return null;

			string content = File.ReadAllText(manifest);
			// 2) Trekk ut installdir
			var m = Regex.Match(content, "\"installdir\"\\s*\"(?<dir>.*?)\"");
			if (!m.Success) return null;

			string installDir = m.Groups["dir"].Value;
			// 3) Gå til common‑mappen og let etter exe
			var gameFolder = Path.Combine(steamPath, "steamapps", "common", installDir);
			if (!Directory.Exists(gameFolder)) return null;

			// Finn alle exe i roten (du kan snevre mer inn hvis du vet mønster)
			var exes = Directory.GetFiles(gameFolder, "*.exe", SearchOption.TopDirectoryOnly);
			if (exes.Length == 0) return null;

			// F.eks. velg den største exe (antakelse: launcheren er stor)
			var chosen = exes.OrderByDescending(f => new FileInfo(f).Length).First();
			return Path.GetFileNameWithoutExtension(chosen);
		}
	}
}
