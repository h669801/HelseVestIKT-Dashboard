using System.Windows;

namespace HelseVestIKT_Dashboard.Views
{
    public partial class SettingsWindow : Window
    {
        public SettingsWindow()
        {
            InitializeComponent();
        }

        // 1) Åpne ProfileManagerWindow når brukeren klikker "Endre bruker"
        private async void OnChangeUser(object sender, RoutedEventArgs e)
        {
            var profileMgr = new ProfileManagerWindow
            {
                Owner = this
            };

            if (profileMgr.ShowDialog() == true && profileMgr.SelectedProfile != null)
            {
                // Bruk den valgte profilen:
                var prof = profileMgr.SelectedProfile;

                // Lagre som sist brukte profil:
                Properties.Settings.Default.LastProfileName = prof.Name;
                Properties.Settings.Default.Save();

                // Oppdater SteamApi i MainWindow (antatt at MainWindow har en metode for dette)
                if (Owner is MainWindow mw)
                {
                    await mw.SetProfileAsync(prof);
                }
            }
        }

        // 2) Et eksempel på en annen innstilling
        private void OnOtherSettings(object sender, RoutedEventArgs e)
        {
            System.Windows.MessageBox.Show("Her kan du vise andre innstillinger!",
                            "Andre innstillinger",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);
        }

        // 3) Lukk vinduet
        private void OnClose(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
