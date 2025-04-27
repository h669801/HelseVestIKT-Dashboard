using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace HelseVestIKT_Dashboard.Views
{
    public partial class ProfileManagerWindow : Window
    {
        private List<SteamProfile> _profiles;

        public SteamProfile SelectedProfile { get; private set; }

        public ProfileManagerWindow()
        {
            InitializeComponent();

            ProfilesListBox.SelectionChanged += ProfilesListBox_SelectionChanged;

            // Last alle profiler
            _profiles = ProfileStore.Load();
            ProfilesListBox.ItemsSource = _profiles;

            // Pre-select sist brukte profil om den finnes
            var last = Properties.Settings.Default.LastProfileName;
            var lastProfile = _profiles.FirstOrDefault(p => p.Name == last);
            if (lastProfile != null)
                ProfilesListBox.SelectedItem = lastProfile;
        }

        private void OnChangeUser(object sender, RoutedEventArgs e)
        {
            if (ProfilesListBox.SelectedItem is SteamProfile p)
            {
                SelectedProfile = p;
                DialogResult = true;
            }
        }

        private void OnNewUser(object sender, RoutedEventArgs e)
        {
            // Åpne editor-vinduet for ny profil
            var editor = new ProfileEditorWindow();
            editor.Owner = this;
            if (editor.ShowDialog() == true)
            {
                // Legg til ny profil i listen og velg den
                _profiles.Add(editor.CreatedProfile);
                ProfileStore.Save(_profiles);
                ProfilesListBox.Items.Refresh();

                ProfilesListBox.SelectedItem = editor.CreatedProfile;
            }
        }

        private void OnCancel(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private void ProfilesListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Aktiver "Endre bruker"-knappen kun dersom noe er valgt
            EditProfileButton.IsEnabled = ProfilesListBox.SelectedItem != null;
        }
    }
}
