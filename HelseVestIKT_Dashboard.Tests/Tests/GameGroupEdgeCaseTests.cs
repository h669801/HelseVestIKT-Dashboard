using HelseVestIKT_Dashboard.Models;

namespace HelseVestIKT_Dashboard.Tests
{

	/// <summary>
	/// Testklasse for at en ny GameGroup uten spill alltid returnerer true på ingen filter.
	/// </summary>

	[TestFixture]
	public class GameGroupEdgeCaseTests
	{

		/// <summary>
		/// Skal returnere false når en annen instans med samme AppID sjekkes,
		/// fordi HasGame bruker objektreferanse, ikke kun AppID.
		/// </summary>
		[Test]
		public void HasGame_WithNullOrDifferentInstance_ReturnsFalse()
		{
			var group = new GameGroup { GroupName = "G" };
			// Samme AppID men ulik instans
			var g1 = new Game { AppID = "x", Title = "T" };
			var g2 = new Game { AppID = "x", Title = "T" };
			group.Games.Add(g1);

			bool result = group.HasGame(g2);

			Assert.IsFalse(result, "HasGame bør bruke referanselikhet, ikke kun AppID.");
		}

		/// <summary>
		/// Skal sikre at Games-listen er korrekt initialisert og ikke er null
		/// selv før noen spill er lagt til.
		/// </summary>
		[Test]
		public void NewGroup_GamesListInitialized_NotNull()
		{
			// Legger til påkrevd GroupName for at objekt-initieringen skal kompileres
			var group = new GameGroup { GroupName = string.Empty };
			Assert.IsNotNull(group.Games, "Games-lista skal aldri være null selv om ingen spill legges til.");
		}

	}
}