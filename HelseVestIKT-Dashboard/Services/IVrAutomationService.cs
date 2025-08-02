using HelseVestIKT_Dashboard.Infrastructure;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HelseVestIKT_Dashboard.Services
{
	namespace HelseVestIKT_Dashboard.Services
	{
		/// <summary>
		/// Automates opening/keeping-steamVR’s “VR View” window visible.
		/// </summary>
		public interface IVrAutomationService : IDisposable
		{
			/// <summary>Start the background watcher that will re-open “VR View” whenever it disappears.</summary>
			void StartWatching();

			/// <summary>Immediately open or restore the “VR View” window.</summary>
			void EnsureVrViewVisible();
		}
	}
}
