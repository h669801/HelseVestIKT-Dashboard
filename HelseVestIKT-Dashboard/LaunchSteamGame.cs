using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HelseVestIKT_Dashboard
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
    }
}
