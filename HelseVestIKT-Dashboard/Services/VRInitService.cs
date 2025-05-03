using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Valve.VR;
using System.Threading;
using System.IO;
using System.Windows;
using MessageBox = System.Windows.MessageBox;

namespace HelseVestIKT_Dashboard.Services
{
	public class VRInitService
	{

		private CVRSystem _system;
		/// <summary>
		/// Starter SteamVR, venter på vrserver, og initierer OpenVR.
		/// </summary>
		public bool InitializeOpenVR()
		{
			// 1) Start SteamVR hvis det ikke allerede kjører
			if (!Process.GetProcessesByName("vrserver").Any())
			{
				var steamPath = Path.Combine(
					Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
					"Steam", "Steam.exe");
				Process.Start(new ProcessStartInfo(steamPath, "-applaunch 250820")
				{
					UseShellExecute = true
				});
			}

			// 2) Vent til vrserver er oppe (max 10 s)
			var sw = Stopwatch.StartNew();
			while (sw.Elapsed < TimeSpan.FromSeconds(10))
			{
				if (Process.GetProcessesByName("vrserver").Any())
					break;
				Thread.Sleep(200);
			}

			// 3) Initier OpenVR som bakgrunnsapp
			EVRInitError error = EVRInitError.None;
			OpenVR.Init(ref error, EVRApplicationType.VRApplication_Background);

			if (error != EVRInitError.None)
			{
				// Logg feilen
				Debug.WriteLine($"OpenVR init feilet: {error}");

				// Gi brukeren beskjed og deaktiver VR-funksjonalitet
				MessageBox.Show(
					$"Kunne ikke koble til VR-headset:\n{error}\n\n" +
					"Sørg for at SteamVR kjører og at headset er tilkoblet.",
					"VR-initialisering mislyktes",
					MessageBoxButton.OK,
					MessageBoxImage.Warning);

				// Kall Shutdown for å rydde opp eventuelle delvis init
				OpenVR.Shutdown();

				return false;
			}

			_system = OpenVR.System;
			return true;
		}

		/// <summary>
		/// Sjekk om vrSystem lever — ellers forsøk re-init.
		/// </summary>
		public void EnsureVrSystemAlive()
		{
			try
			{
				// Dette kaster hvis systemet ikke er gyldig
				bool onDesktop = _system != null && _system.IsDisplayOnDesktop();
				if (!onDesktop)
					InitializeOpenVR();
			}
			catch
			{
				InitializeOpenVR();
			}
		}

		public void Shutdown()
		{
			OpenVR.Shutdown(); // Stenger ned OpenVR og alle tilknyttede ressurser
			_system = null; // Nullstiller systemreferansen
		}

		public CVRSystem System => _system; // Gir tilgang til CVRSystem-instansen

		/// <summary>
		/// Asynkron restart av SteamVR (venter uten å blokkere UI-tråden).
		/// </summary>
		public async Task RestartSteamVRAsync()
		{
			// 1) Stopp SteamVR-prosessene
			Process.Start("cmd.exe", "/C taskkill /F /IM vrserver.exe /IM vrmonitor.exe");
			// 2) Vent asynkront
			await Task.Delay(3000);
			// 3) Start Steam med SteamVR
			var steamExe = Path.Combine(
				Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
				"Steam", "Steam.exe");
			Process.Start(new ProcessStartInfo(steamExe, "-applaunch 250820")
			{
				UseShellExecute = true
			});
		}
	}
}
