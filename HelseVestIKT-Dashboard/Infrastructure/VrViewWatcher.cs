using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Automation;

namespace HelseVestIKT_Dashboard.Infrastructure
{
	public class VrViewWatcher
	{
		private readonly SteamVrAutomation _steamVrAutomation;
		private readonly AutomationEventHandler _openedHandler;
		private readonly AutomationEventHandler _closedHandler;
		
		public event Action VrViewReopened;


		public VrViewWatcher(SteamVrAutomation steamAutomation)
		{
			_steamVrAutomation = steamAutomation ?? throw new ArgumentNullException(nameof(steamAutomation));
			_openedHandler = new AutomationEventHandler(OnWindowOpened);
			_closedHandler = new AutomationEventHandler(OnWindowClosed);
		}

		public void Start()
		{
			var root = AutomationElement.RootElement;

			Automation.AddAutomationEventHandler(WindowPattern.WindowOpenedEvent, root, TreeScope.Subtree,_openedHandler);
			Automation.AddAutomationEventHandler(WindowPattern.WindowClosedEvent, root, TreeScope.Subtree, _closedHandler);

		}

		public void Stop()
		{
			var root = AutomationElement.RootElement;

			Automation.RemoveAutomationEventHandler(WindowPattern.WindowOpenedEvent, root,_openedHandler);
			Automation.RemoveAutomationEventHandler(WindowPattern.WindowClosedEvent, root, _closedHandler);

		}

		private void OnWindowOpened(object src, AutomationEventArgs e)
		{
			var el = src as AutomationElement;
			if (el == null) return;
			var name = el.Current.Name ?? "";
			if (name.Contains("VR-View") || name.Contains("VR-visning"))
			{
				VrViewReopened?.Invoke();
			}

		}

		private void OnWindowClosed(object src, AutomationEventArgs e)
		{
			if (!(src is AutomationElement el)) 
				return;

			var name = el.Current.Name ?? "";
			//Sjekk om det er VR-View/VR-tilskuervisning som lukker seg
			if (name.Contains("VR-View") || name.Contains("VR-visning"))
			{
				_steamVrAutomation.EnsureVrViewVisible();
			}
		}
	}
}
