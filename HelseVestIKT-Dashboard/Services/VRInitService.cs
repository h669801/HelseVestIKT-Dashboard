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

		private CVRSystem? _system;
		public CVRSystem? System => _system;
		/// <summary>
		/// «Trygg» initialisering av OpenVR som bakgrunns‐app.
		/// </summary>
		public bool SafeInitOpenVR()
		{
			// 1) Gjør et forsøk på å starte/vente på SteamVR
			var sw = Stopwatch.StartNew();
			while (sw.Elapsed < TimeSpan.FromSeconds(10))
			{
				if (Process.GetProcessesByName("vrserver").Any())
					break;
				Thread.Sleep(200);
			}

			// 2) Så init OpenVR som bakgrunns-app
			EVRInitError error = EVRInitError.None;
			OpenVR.Init(ref error, EVRApplicationType.VRApplication_Background);
			if (error != EVRInitError.None)
			{
				Debug.WriteLine($"[VRInitService] OpenVR-init feilet: {error}");
				return false;
			}

			_system = OpenVR.System;
			return true;
		}

		/// <summary>
		/// Original metode for å kalibrere slider, font osv.
		/// (den kan fortsatt hete InitializeOpenVR dersom du bruker den andre steder)
		/// </summary>
		public bool InitializeOpenVR()
		{
			// (valgfritt) behold dialog‐varsel her om du vil
			if (!SafeInitOpenVR())
			{
				MessageBox.Show(
					"Kunne ikke koble til VR-headset.\nSørg for at SteamVR kjører og at headset er tilkoblet.",
					"VR-initialisering mislyktes",
					MessageBoxButton.OK,
					MessageBoxImage.Warning);
				return false;
			}
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

		/// <summary>
		/// Slår av gjeldende OpenVR-session.
		/// </summary>
		/// <summary>
		/// Stenger den aktive OpenVR-session (både bakgrunn og overlay).
		/// </summary>
		public void Shutdown()
		{
			try { OpenVR.Shutdown(); } catch { /* ignorer */ }
			_system = null;
		}


		/// <summary>
		/// Asynkron restart av SteamVR (venter uten å blokkere UI-tråden).
		/// </summary>
		/// <summary>
		/// Starter eller restarter SteamVR-prosessen asynkront.
		/// </summary>
		public async Task RestartSteamVRAsync()
		{
			// 1) Start vrserver via Steam hvis det ikke kjører
			if (!Process.GetProcessesByName("vrserver").Any())
			{
				var steamExe = Path.Combine(
					Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
					"Steam", "Steam.exe");
				Process.Start(new ProcessStartInfo(steamExe, "-applaunch 250820")
				{
					UseShellExecute = true
				});
			}

			// 2) Vent til vrserver er oppe (maks 10 sek)
			var sw = Stopwatch.StartNew();
			while (sw.Elapsed < TimeSpan.FromSeconds(10))
			{
				if (Process.GetProcessesByName("vrserver").Any())
					break;
				await Task.Delay(200);
			}
		}
	}
}
