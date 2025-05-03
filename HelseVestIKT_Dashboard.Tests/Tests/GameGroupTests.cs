using NUnit.Framework;
using System.Threading;
using HelseVestIKT_Dashboard.Models;   // ditt hoved-namespace

namespace HelseVestIKT_Dashboard.Tests
{
	/// <summary>
	/// Tester funksjonaliteten i GameGroup-klassen, spesielt HasGame-metoden
	/// for å sikre at den oppdager når spill er lagt til eller ikke.
	/// </summary>

	[TestFixture, Apartment(ApartmentState.STA)]
	public class GameGroupTests
	{

		/// <summary>
		/// Skal returnere true når et spill er lagt til i gruppen.
		/// </summary>
		[Test]
		public void HasGame_ReturnsTrue_WhenGameIsAdded()
		{
			// Arrange
			var game = new Game { AppID = "1", Title = "TestGame" };
			var group = new GameGroup { GroupName = "MinGruppe" };
			group.Games.Add(game);

			// Act
			bool finnes = group.HasGame(game);

			// Assert
			Assert.IsTrue(finnes, "HasGame skal returnere true når spillet er lagt til.");
		}

		/// <summary>
		/// Skal returnere false når gruppen er tom og ikke inneholder spillet.
		/// </summary>
		[Test]
		public void HasGame_ReturnsFalse_WhenGroupIsEmpty()
		{
			// Arrange
			var group = new GameGroup { GroupName = "TomGruppe" };
			var game = new Game { AppID = "2", Title = "AnnetSpill" };

			// Act
			bool finnes = group.HasGame(game);

			// Assert
			Assert.IsFalse(finnes, "HasGame skal returnere false når gruppen er tom.");
		}
	}
}