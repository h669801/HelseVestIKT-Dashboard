using HelseVestIKT_Dashboard.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using WpfCheckBox = System.Windows.Controls.CheckBox;

namespace HelseVestIKT_Dashboard.Services
{
	public class FilterService
	{

		// Oversettelse fra norsk UI-label til intern sjanger-/type-nøkkel
		private readonly Dictionary<string, string> _translation = new()
		{
			["Action"] = "Action",
			["Eventyr"] = "Adventure",
			["Indie"] = "Indie",
			["Lettbeint"] = "Casual",
			["Massivt flerspill"] = "Massively Multiplayer",
			["Racing"] = "Racing",
			["Rollespill"] = "RPG",
			["Simulering"] = "Simulation",
			["Sport"] = "Sports",
			["Strategi"] = "Strategy",

			["VR-spill"] = "VR",
			["Steam-spill"] = "Steam",
			["Vis kun favoritter"] = "Favorite",
			["Vis kun nylig spilt"] = "Recent",
			["Andre spill"] = "Other"
		};

		/// <summary>
		/// Filtrerer en liste av spill basert på valgte sjangre, typer og grupper.
		/// </summary>
		public IEnumerable<Game> ApplyFilters(
			IEnumerable<string> genres,
			IEnumerable<string> types,
			IEnumerable<GameGroup> groups,
			IEnumerable<Game> allGames)
		{
			var gList = genres.ToList();
			var tList = types.ToList();
			var grpList = groups.ToList();

			return allGames.Where(game =>
				MatchesGenre(gList, game)
				&& MatchesType(tList, game)
				&& MatchesGroup(grpList, game)
			);
		}

		// Sjekker sjanger-filter
		private bool MatchesGenre(List<string> genres, Game game)
		{
			if (genres.Count == 0)
				return true;

			// Oversett hver valgt norsk sjanger til engelsk og sjekk mot game.Genres
			return genres.Any(norsk =>
			{
				if (!_translation.TryGetValue(norsk, out var eng))
					return false;
				return game.Genres.Contains(eng, StringComparer.OrdinalIgnoreCase);
			});
		}

		// Sjekker type-filter
		private bool MatchesType(List<string> types, Game game)
		{
			if (types.Count == 0)
				return true;

			return types.Any(norsk =>
			{
				if (!_translation.TryGetValue(norsk, out var key))
					return false;

				return key switch
				{
					"VR" => game.IsVR,
					"Steam" => game.IsSteamGame,
					"Favorite" => game.IsFavorite,
					"Recent" => game.IsRecentlyPlayed,
					"Other" => !game.IsVR && !game.IsSteamGame,
					_ => false
				};
			});
		}

		// Sjekker gruppe-filter
		private bool MatchesGroup(List<GameGroup> groups, Game game)
		{
			if (groups.Count == 0)
				return true;

			return groups.Any(g => g.Games.Contains(game));
		}

		/*
		private bool FilterByGenre(IList<WpfCheckBox> boxes, Game game)
		{
			bool noneChecked = true;
			foreach (var box in boxes)
			{
				if (box.IsChecked == true)
				{
					noneChecked = false;
					var key = box.Content?.ToString();
					if (key != null
						&& _genreTranslation.TryGetValue(key, out var translated)
						&& game.Genres.Contains(translated))
					{
						return true;
					}
				}
			}
			// hvis ingen er krysset av, ta med alt
			return noneChecked;
		}

		/*
		private bool FilterByType(IList<WpfCheckBox> boxes, Game game)
		{
			bool noneChecked = true;
			foreach (var box in boxes)
			{
				if (box.IsChecked == true)
				{
					noneChecked = false;
					switch (box.Content?.ToString())
					{
						case "Vis kun nylig spilt":
							if (game.IsRecentlyPlayed) return true;
							break;
						case "VR-spill":
							if (game.IsVR) return true;
							break;
						case "Steam spill":
							if (game.IsSteamGame) return true;
							break;
						case "Andre spill":
							if (!game.IsSteamGame) return true;
							break;
						case "Flerspiller":
							if (!game.IsSinglePlayer) return true;
							break;
						case "Vis kun favoritter":
							if (game.IsFavorite) return true;
							break;
					}
				}
			}
			return noneChecked;
		}
		
		private bool FilterByGroups(
			IList<(WpfCheckBox box, GameGroup group)> groups,
			Game game)
		{
			bool noneChecked = true;
			foreach (var (box, grp) in groups)
			{
				if (box.IsChecked == true)
				{
					noneChecked = false;
					if (grp.HasGame(game))
						return true;
				}
			}
			return noneChecked;
		}
		*/
	}
}
