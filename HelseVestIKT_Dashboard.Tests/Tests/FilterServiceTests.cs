using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;
using HelseVestIKT_Dashboard.Models;
using HelseVestIKT_Dashboard.Services;

namespace HelseVestIKT_Dashboard.Tests
{

	/// <summary>
	/// Tester FilterService sin ApplyFilters-metode for å sikre korrekt filtrering
	/// basert på sjanger, type og grupper.
	/// </summary>
	[TestFixture]
	public class FilterServiceTests
	{
		private FilterService _service = null!;
		private List<Game> _allGames = null!;


		/// <summary>
		/// Initialiserer et sett med spill med ulike egenskaper før hver test.
		/// </summary>
		[SetUp]
		public void Setup()
		{
			_service = new FilterService();
			_allGames = new List<Game>
			{
				new Game
				{
					AppID = "1",
					Title = "ActionGame",
					Genres = new List<string> { "Action" },
					IsVR = false,
					IsSteamGame = true,
					IsRecentlyPlayed = false,
					IsFavorite = false,
					IsSinglePlayer = false
				},
				new Game
				{
					AppID = "2",
					Title = "RPGGame",
					Genres = new List<string> { "RPG" },
					IsVR = true,
					IsSteamGame = false,
					IsRecentlyPlayed = true,
					IsFavorite = false,
					IsSinglePlayer = false
				},
				new Game
				{
					AppID = "3",
					Title = "StrategyGame",
					Genres = new List<string> { "Strategy" },
					IsVR = false,
					IsSteamGame = false,
					IsRecentlyPlayed = false,
					IsFavorite = true,
					IsSinglePlayer = false
				}
			};
		}


		/// <summary>
		/// Skal returnere alle spill når ingen filtre er angitt.
		/// </summary>
		[Test]
		public void ApplyFilters_UtenNoGenresTypesGroups_ReturnsAlleSpill()
		{
			var result = _service.ApplyFilters(
				genres: Enumerable.Empty<string>(),
				types: Enumerable.Empty<string>(),
				groups: Enumerable.Empty<GameGroup>(),
				allGames: _allGames)
				.ToList();

			Assert.AreEqual(3, result.Count, "Alle 3 spill skal returneres når ingen filtre er valgt.");
		}

		/// <summary>
		/// Skal filtrere på sjanger "Rollespill" og returnere kun det matchende spillet.
		/// </summary>
		[Test]
		public void ApplyFilters_FiltersByGenre_ReturnOnlyMatching()
		{
			var result = _service.ApplyFilters(
				genres: new[] { "Rollespill" },
				types: Enumerable.Empty<string>(),
				groups: Enumerable.Empty<GameGroup>(),
				allGames: _allGames)
				.ToList();

			Assert.AreEqual(1, result.Count, "Kun ett spill med sjanger RPG skal returneres.");
			Assert.AreEqual("2", result[0].AppID, "AppID for RPG-spillet skal være '2'.");
		}

		/// <summary>
		/// Skal filtrere på type "Vis kun nylig spilt" og returnere nylig spilte spill.
		/// </summary>
		[Test]
		public void ApplyFilters_FiltersByType_ReturnOnlyRecent()
		{
			var result = _service.ApplyFilters(
				genres: Enumerable.Empty<string>(),
				types: new[] { "Vis kun nylig spilt" },
				groups: Enumerable.Empty<GameGroup>(),
				allGames: _allGames)
				.ToList();

			Assert.AreEqual(1, result.Count, "Kun nylig spilte spill skal returneres.");
			Assert.AreEqual("2", result[0].AppID, "AppID for nylig spilt-spillet skal være '2'.");
		}


		/// <summary>
		/// Skal kombinere filter for VR-spill og favoritter, og returnere spill som
		/// tilfredsstiller minst ett av kriteriene.
		/// </summary>
		[Test]
		public void ApplyFilters_FiltersByType_VRAndFavorite()
		{
			var result = _service.ApplyFilters(
				genres: Enumerable.Empty<string>(),
				types: new[] { "VR-spill", "Vis kun favoritter" },
				groups: Enumerable.Empty<GameGroup>(),
				allGames: _allGames)
				.ToList();

			CollectionAssert.AreEquivalent(
				new[] { "2", "3" },
				result.Select(g => g.AppID),
				"AppID-ene som returneres skal være 2 og 3.");
		}

		/// <summary>
		/// Skal filtrere basert på en spesifikk GameGroup og returnere kun spill i den.
		/// </summary>
		[Test]
		public void ApplyFilters_WithGroups_ReturnOnlyInGroup()
		{
			var grp = new GameGroup { GroupName = "MinGruppe" };
			grp.Games.Add(_allGames.First(g => g.AppID == "1"));
			grp.Games.Add(_allGames.First(g => g.AppID == "3"));

			var result = _service.ApplyFilters(
				genres: Enumerable.Empty<string>(),
				types: Enumerable.Empty<string>(),
				groups: new[] { grp },
				allGames: _allGames)
				.ToList();

			CollectionAssert.AreEquivalent(
				new[] { "1", "3" },
				result.Select(g => g.AppID),
				"AppID-ene som returneres skal være 1 og 3.");
		}
	}
}
