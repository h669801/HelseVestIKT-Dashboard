using NUnit.Framework;
using System.ComponentModel;
using HelseVestIKT_Dashboard.Models;

namespace HelseVestIKT_Dashboard.Tests
{

	/// <summary>
	/// Tester SpillFilter-klassen for å verifisere at PropertyChanged-eventet
	/// skytes når navnet endres og at riktig PropertyName rapporteres.
	/// </summary>
	[TestFixture]
	public class SpillFilterTests
	{

		/// <summary>
		/// Skal utløse PropertyChanged når Name-verdien endres.
		/// </summary>
		[Test]
		public void Name_PropertyChanged_EventFires()
		{
			// Arrange
			var filter = new SpillFilter();
			string changedProperty = null!;
			filter.PropertyChanged += (s, e) => changedProperty = e.PropertyName;

			// Act
			filter.Name = "NyttNavn";

			// Assert
			Assert.AreEqual(nameof(filter.Name), changedProperty, "PropertyChanged skal varsle når Name endres.");
			Assert.AreEqual("NyttNavn", filter.Name, "Name skal ha den nye verdien etter endring.");
		}
	}
}
