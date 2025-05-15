using System.Diagnostics;
using System.Net;
using System.Text;
using System.Windows;
using System.Windows.Navigation;
using HelseVestIKT_Dashboard.Models;

namespace HelseVestIKT_Dashboard.Views
{
    public partial class ProfileEditorWindow : Window
    {
        public SteamProfile CreatedProfile { get; private set; }

        public ProfileEditorWindow()
        {
            InitializeComponent();
        }

        private void OnSave(object sender, RoutedEventArgs e)
        {
            var name = NameBox.Text.Trim();
            var apiKey = ApiKeyBox.Text.Trim();
            var userId = UserIdBox.Text.Trim();

            if (string.IsNullOrEmpty(name))
            {
                System.Windows.MessageBox.Show("Profilnavn kan ikke være tomt.", "Validering", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(userId))
            {
                System.Windows.MessageBox.Show("Både API-nøkkel og User ID må fylles ut.", "Validering", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            CreatedProfile = new SteamProfile
            {
                Name = name,
                ApiKey = apiKey,
                UserId = userId
            };
            DialogResult = true;
        }

        private void OnCancel(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private async void OnFetchSteamId(object sender, RoutedEventArgs e)
        {
            var auth = new SteamAuthWindow { Owner = this };
            if (auth.ShowDialog() == true && auth.SteamId.HasValue)
            {
                UserIdBox.Text = auth.SteamId.Value.ToString();
            }
            else
            {
                System.Windows.MessageBox.Show("Kunne ikke hente SteamID.", "Feil", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }


        private void OnOpenSteamApiKeyPage(object sender, RoutedEventArgs e)
        {
            var win = new ApiKeyWindow
            {
                Owner = this
            };
            win.ShowDialog();
        }
    }
}
