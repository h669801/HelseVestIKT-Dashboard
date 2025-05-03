using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Collections.ObjectModel;
using HelseVestIKT_Dashboard.Models;
using SteamKit2.WebUI.Internal;

namespace HelseVestIKT_Dashboard.Services
{
	/// <summary>
	/// Håndterer søkefunksjonalitet i spilloversikten.
	/// </summary>
	public class SearchService
	{
		private const string Placeholder = "Søk etter spill...";

		// Intern timer for "debounce"
		private readonly DispatcherTimer _debounceTimer;
		private readonly System.Windows.Controls.TextBox _searchBox;
		private readonly ObservableCollection<Game> _viewCollection;
		private readonly IEnumerable<Game> _allGames;

		public SearchService(
			System.Windows.Controls.TextBox searchBox,
			ObservableCollection<Game> viewCollection,
			IEnumerable<Game> allGames)
		{
			_searchBox = searchBox;
			_viewCollection = viewCollection;
			_allGames = allGames;

			// Opprett og konfigurer intern debounce-timer
			_debounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
			_debounceTimer.Tick += (_, __) => ApplySearch();

			// Hver gang tekst endres: restart timer
			_searchBox.TextChanged += (_, __) =>
			{
				_debounceTimer.Stop();
				_debounceTimer.Start();
			};
		}

		private void OnGotFocus(object sender, RoutedEventArgs e)
		{
			if (_searchBox.Text == Placeholder)
				_searchBox.Text = "";
		}

		private void OnLostFocus(object sender, RoutedEventArgs e)
		{
			if (string.IsNullOrWhiteSpace(_searchBox.Text))
				_searchBox.Text = Placeholder;
		}

	
		/// <summary>
		/// Filtrerer spill basert på _searchBox.Text.
		/// </summary>
		public void ApplySearch()
		{
			_debounceTimer.Stop();

			var query = _searchBox.Text.Trim();
			var resultat = string.IsNullOrEmpty(query)
				? _allGames
				: _allGames.Where(g =>
					g.Title.Contains(query, StringComparison.OrdinalIgnoreCase));

			// Oppdaterer visnings-samling
			_viewCollection.Clear();
			foreach (var g in resultat)
				_viewCollection.Add(g);
		}
	}
}
