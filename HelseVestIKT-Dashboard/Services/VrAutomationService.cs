using HelseVestIKT_Dashboard.Infrastructure;
using HelseVestIKT_Dashboard.Services.HelseVestIKT_Dashboard.Services;
using System;

namespace HelseVestIKT_Dashboard.Services
{
	/// <summary>
	/// Concrete implementation of IVrAutomationService,  
	/// built on top of your existing SteamVrAutomation helper class.
	/// </summary>
	public class VrAutomationService : IVrAutomationService
	{
		private readonly SteamVrAutomation _automation;

		public VrAutomationService()
		{
			_automation = new SteamVrAutomation();
		}

		public void StartWatching()
			=> _automation.Start();

		public void EnsureVrViewVisible()
			=> _automation.EnsureVrViewVisible();

		public void Dispose()
			=> (_automation as IDisposable)?.Dispose();
	}
}
