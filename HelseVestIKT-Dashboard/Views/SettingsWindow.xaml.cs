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
			var pinWin = new PinWindow
			{
				Owner = this,
				WindowStartupLocation = WindowStartupLocation.CenterOwner
			};

			// ShowDialog returnerer DialogResult fra PinWindow
			bool? ok = pinWin.ShowDialog();
			if (ok == true)
			{
                EditProfileButton.Visibility = Visibility.Visible; // PIN var korrekt – vis Endre bruker‐knappen

			}
			// etter at PinWindow lukkes, er du fortsatt i SettingsWindow
		}

        private void OnToggleLock(object sender, RoutedEventArgs e)
        {
            if (Owner is MainWindow mw)
            {
                mw.ToggleLock();

                // Oppdater knappetekst basert på ny tilstand
                if (mw.IsLocked)
                {
                    LockToggleButton.Content = "Lås opp applikasjon";
                }
                else
                {
                    LockToggleButton.Content = "Lås applikasjon";
                }
            }
        }


        // 3) Lukk vinduet
        private void OnClose(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
