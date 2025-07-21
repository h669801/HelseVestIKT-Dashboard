using System;
using System.Linq;
using System.Threading;
using System.Windows.Automation;

namespace HelseVestIKT_Dashboard.Infrastructure
{
	/// <summary>
	/// Automatiserer åpning og synliggjøring av "VR View" i SteamVR.
	/// </summary>
	public class SteamVrAutomation
	{
		private const int ExpandWaitMs = 500;         // Ventetid for menyutvidelse
		private readonly VrViewWatcher _vrWatcher;
		private bool _started;

		/// <summary>
		/// Konstruktør: Konfigurerer watcher for å håndtere manuell lukking av VR View.
		/// </summary>
		public SteamVrAutomation()
		{
			_vrWatcher = new VrViewWatcher(this);
			_vrWatcher.VrViewReopened += OnVrViewReopened;
		}

		/// <summary>
		/// Starter initial visning og overvåkning. Kalles gjerne i MainWindow.Loaded.
		/// </summary>
		public void Start()
		{
			if (_started)
				return;
			_started = true;

			// Vis VR View én gang etter at UI er klart
			EnsureVrViewVisible();

			// Start overvåkning av VR View-lukking
			_vrWatcher.Start();
		}

		/// <summary>
		/// Callback når VR View-vinduet åpnes på nytt (etter manuell lukking).
		/// </summary>
		private void OnVrViewReopened()
		{
			TryEnsureVrViewVisible();
		}

		/// <summary>
		/// Wrap for å ignorere focus-feil i event handler.
		/// </summary>
		private void TryEnsureVrViewVisible()
		{
			try
			{
				EnsureVrViewVisible();
			}
			catch (InvalidOperationException)
			{
				// Ignorer feil ved fokus-setting
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine(ex);
			}
		}

		/// <summary>
		/// Sørger for at "VR View" vises – klikker menyen kun om det ikke allerede er åpent.
		/// </summary>
		public void EnsureVrViewVisible()
		{
			// Hvis VR View allerede er synlig, restaurer og returner
			if (IsVrViewVisible())
			{
				RestoreVrView();
				return;
			}


			var statusWindow = AutomationElement.RootElement.FindFirst(TreeScope.Children,
				new PropertyCondition(AutomationElement.NameProperty, "SteamVR Status"));
			if (statusWindow == null) return;

			ExpandVersionMenu(statusWindow);

			Thread.Sleep(ExpandWaitMs);
			ClickGlobalMenuItem("Display VR View");

			Thread.Sleep(ExpandWaitMs);

			RestoreVrView();
		}

		private void ExpandVersionMenu(AutomationElement statusWindow)
		{
			var versionMenu = statusWindow.FindAll(
				TreeScope.Descendants,
				new PropertyCondition(AutomationElement.ControlTypeProperty,
				ControlType.MenuItem)).Cast<AutomationElement>().FirstOrDefault(el => el.Current.Name.StartsWith("STEAMVR ", StringComparison.OrdinalIgnoreCase));

			if (versionMenu == null) return;

			if (versionMenu.TryGetCurrentPattern(ExpandCollapsePattern.Pattern, out var expPattern))
			{
				var expand = (ExpandCollapsePattern)expPattern;
				if(expand.Current.ExpandCollapseState != ExpandCollapseState.Expanded)
					expand.Expand();
			}
			else if (versionMenu.TryGetCurrentPattern(InvokePattern.Pattern, out var invPattern))
			{
				((InvokePattern)invPattern).Invoke();
			}
		}

		/// <summary>
		/// Søker globalt etter ett menyelement og klikker det.
		/// </summary>
		private void ClickGlobalMenuItem(string name)
		{
			var item = AutomationElement.RootElement.FindFirst(
				TreeScope.Subtree,
				new AndCondition(
					new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.MenuItem),
					new PropertyCondition(AutomationElement.NameProperty, name)
				));
			if (item == null) return;

			if (item.TryGetCurrentPattern(InvokePattern.Pattern, out var inv))
			{
				((InvokePattern)inv).Invoke();
			}
		}

		/// <summary>
		/// Sjekker om "SteamVR Status"-vinduet er åpent.
		/// </summary>
		private AutomationElement FindSteamStatusWindow()
		{
			return AutomationElement.RootElement.FindFirst(
				TreeScope.Children,
				new PropertyCondition(AutomationElement.NameProperty, "SteamVR Status"));
		}

		/// <summary>
		/// Prøver å klikke på "Display VR View" under SteamVR-menyen.
		/// </summary>
		private bool TryClickDisplayVrView(AutomationElement steamStatus)
		{
			// Finn "STEAMVR x.x"-menyen
			var versionMenu = steamStatus.FindAll(
					TreeScope.Descendants,
					new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.MenuItem))
				.Cast<AutomationElement>()
				.FirstOrDefault(el => el.Current.Name.StartsWith("STEAMVR ", StringComparison.OrdinalIgnoreCase));

			if (versionMenu == null)
				return false;

			// Utvid menyen hvis støttet
			if (versionMenu.TryGetCurrentPattern(ExpandCollapsePattern.Pattern, out var expPattern))
			{
				var expand = (ExpandCollapsePattern)expPattern;
				if (expand.Current.ExpandCollapseState != ExpandCollapseState.Expanded)
					expand.Expand();
			}
			else if (versionMenu.TryGetCurrentPattern(InvokePattern.Pattern, out var invokePattern))
			{
				((InvokePattern)invokePattern).Invoke();
			}

			Thread.Sleep(ExpandWaitMs);

			// Finn "Display VR View" i hele SteamVR-status vinduet
			var displayItem = steamStatus.FindFirst(
				TreeScope.Descendants,
				new AndCondition(
					new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.MenuItem),
					new PropertyCondition(AutomationElement.NameProperty, "Display VR View")
				));

			if (displayItem == null)
				return false;

			// Utfør klikk
			if (displayItem.TryGetCurrentPattern(InvokePattern.Pattern, out var invPattern))
			{
				((InvokePattern)invPattern).Invoke();
				return true;
			}

			return false;
		}

		private bool IsVrViewVisible()
		{
			return AutomationElement.RootElement.FindFirst(TreeScope.Children,
				new PropertyCondition(AutomationElement.NameProperty, "VR View")) != null;
		}

		private void RestoreVrView()
		{
			var vrView = AutomationElement.RootElement.FindFirst(TreeScope.Children,
				new PropertyCondition(AutomationElement.NameProperty, "VR View"));

			if (vrView != null && vrView.TryGetCurrentPattern(WindowPattern.Pattern, out var wpPattern))
			{
				var window = (WindowPattern)wpPattern;
				window.SetWindowVisualState(WindowVisualState.Normal);
			}
		}
	}
}