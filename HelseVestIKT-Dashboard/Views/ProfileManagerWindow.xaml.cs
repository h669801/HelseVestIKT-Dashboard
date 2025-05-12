using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace HelseVestIKT_Dashboard.Views
{
    public partial class ProfileManagerWindow : Window
    {
        // Holder både listen av profiler og siste valgte profil
        private ProfilesFile _data;
        private List<SteamProfile> _profiles;

        public SteamProfile SelectedProfile { get; private set; }

        public ProfileManagerWindow()
        {
            InitializeComponent();
            ProfilesListBox.SelectionChanged += ProfilesListBox_SelectionChanged;

            // Last all data fra JSON: profiler + sist brukte navn
            _data = ProfileStore.Load();
            _profiles = _data.Profiles;
            ProfilesListBox.ItemsSource = _profiles;

            // Pre-select sist brukte profil om den finnes
            var last = _data.LastProfileName;
            var lastProfile = _profiles.FirstOrDefault(p => p.Name == last);
            if (lastProfile != null)
                ProfilesListBox.SelectedItem = lastProfile;
        }

        private void OnChangeUser(object sender, RoutedEventArgs e)
        {
            if (ProfilesListBox.SelectedItem is SteamProfile p)
            {
                SelectedProfile = p;
                // Oppdater og lagre sist valgte profil
                _data.LastProfileName = p.Name;
                ProfileStore.Save(_data);
                DialogResult = true;
            }
        }

        private void OnNewUser(object sender, RoutedEventArgs e)
        {
            var editor = new ProfileEditorWindow { Owner = this };
            if (editor.ShowDialog() == true)
            {
                // Legg til ny profil, marker den som sist brukte, og lagre
                _profiles.Add(editor.CreatedProfile);
                _data.LastProfileName = editor.CreatedProfile.Name;
                ProfileStore.Save(_data);

                // Oppdater UI
                ProfilesListBox.Items.Refresh();
                ProfilesListBox.SelectedItem = editor.CreatedProfile;
            }
        }

        private void OnDeleteUser(object sender, RoutedEventArgs e)
        {
            if (ProfilesListBox.SelectedItem is SteamProfile p)
            {
                var answer = System.Windows.MessageBox.Show(
                    $"Er du sikker på at du vil slette profilen «{p.Name}»?",
                    "Bekreft sletting",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (answer != MessageBoxResult.Yes)
                    return;

                // Fjern profilen
                _profiles.Remove(p);

                // Hvis den var sist brukte, fjern markeringen
                if (_data.LastProfileName == p.Name)
                    _data.LastProfileName = string.Empty;

                ProfileStore.Save(_data);

                // Oppdater UI
                ProfilesListBox.Items.Refresh();
                SelectedProfile = null;
                EditProfileButton.IsEnabled = false;
                DeleteProfileButton.IsEnabled = false;
            }
        }

        private void OnCancel(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private void ProfilesListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            bool hasSelection = ProfilesListBox.SelectedItem != null;
            EditProfileButton.IsEnabled = hasSelection;
            DeleteProfileButton.IsEnabled = hasSelection;
        }
    }
}
