using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Forms;

namespace HelseVestIKT_Dashboard.Services
{
	/// <summary>
	/// Starter Steam-spill ved å bruke steam://-protokollen.
	/// </summary>
	public static class SteamLauncher
	{
		/// <param name="appId">Steam AppID for spillet.</param>
		public static void Launch(string appId)
		{
			try
			{
				var steamUri = new Uri($"steam://run/{appId}");
				var psi = new ProcessStartInfo
				{
					FileName = steamUri.ToString(),
					UseShellExecute = true
				};
				Process.Start(psi);
			}
			catch (Exception ex)
			{
				System.Windows.MessageBox.Show($"Kunne ikke starte Steam-spill (AppID={appId}): {ex.Message}");
			}
		}
	}
}