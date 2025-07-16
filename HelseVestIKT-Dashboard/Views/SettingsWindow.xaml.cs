using HelseVestIKT_Dashboard.Infrastructure;
using HelseVestIKT_Dashboard.Models;
using NAudio.Utils;
using SteamKit2.GC.CSGO.Internal;
using System.Runtime.InteropServices;
using System.Windows;
using MessageBox = System.Windows.MessageBox;

namespace HelseVestIKT_Dashboard.Views
{
    public partial class SettingsWindow : Window
    {

        private readonly MainWindow _main;
		public SettingsWindow()
        {
            InitializeComponent();

            _main = (MainWindow)System.Windows.Application.Current.MainWindow!;
            UpdateLockButton();

        }

        private void UpdateLockButton()
        {
            if (_main.IsLocked)
            {
                LockToggleButton.Content = "Lås opp applikasjon";
            }
            else
            {
                LockToggleButton.Content = "Lås applikasjon";
            }
		}


		// 1) Åpne ProfileManagerWindow når brukeren klikker "Endre bruker"
		private async void ChangeUserButton_Click(object sender, RoutedEventArgs e)
        {
            var profileMgr = new ProfileManagerWindow
            {
                Owner = this
            };

            if (profileMgr.ShowDialog() == true && profileMgr.SelectedProfile != null)
            {
                // Les eksisterende data fra JSON
                var data = ProfileStore.Load();

                // Bruk den valgte profilen:
                var prof = profileMgr.SelectedProfile;

                // Oppdater sist brukte profil og lagre alt
                data.LastProfileName = prof.Name;
                ProfileStore.Save(data);

                // Oppdater SteamApi i MainWindow
                if (Owner is MainWindow mw)
                {
                    await mw.SetProfileAsync(prof);
                }
            }
        }


        private void PinButton_Click(object sender, RoutedEventArgs e)
		{
            var pinWin = new PinWindow { Owner = this, WindowStartupLocation = WindowStartupLocation.CenterOwner };
            bool? ok = pinWin.ShowDialog();
            if (ok == true)
            {
                EditProfileButton.Visibility = Visibility.Visible;
                UpdateLockButton();
			}
		}

        private void LockToggleButton_Click(object sender, RoutedEventArgs e)
        {
            _main.ToggleLock();
            UpdateLockButton();
		}


        // 3) Lukk vinduet
        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void RestartPCButton_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show("Er du sikker på at du vil restarte PC?", "Bekreft restart", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes)
                return;

			try
            {
                Win32.RestartWindows();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Kunne ikke starte PC på nytt: {ex.Message}", "Feil", MessageBoxButton.OK, MessageBoxImage.Error);
			}
        }
	}
}
