using System;
using System.Configuration;
using System.Data;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;

namespace HelseVestIKT_Dashboard
{
	public partial class App : Application
	{
		private string _logPath;

		protected override void OnStartup(StartupEventArgs e)
		{
			base.OnStartup(e);

			// SKRUDD AV FEILLOGGING -MELDINGSBOKS
			// 1) Sett opp loggfil i "Dokumenter"
			var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
			_logPath = Path.Combine(docs, "HelseVestIKT-crash.log");
			Directory.CreateDirectory(Path.GetDirectoryName(_logPath)!);

			// 2) Registrer global logg + visning
			AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
			TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
			this.DispatcherUnhandledException += OnDispatcherUnhandledException;

		/*
			// Valgfritt: informer brukeren om hvor logg legges én gang
			MessageBox.Show(
				$"All unntakslogging skrives til:\n{_logPath}",
				"Loggplassering",
				MessageBoxButton.OK,
				MessageBoxImage.Information);
		*/
			}

		private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
		{
			// Logg hele exception-tekst + stack trace
			File.AppendAllText(_logPath,
				$"[UI] {DateTime.Now}: {e.Exception}\n{new string('-', 40)}\n");

			// Vis stabelspor i dialog (kan fjerne i produksjon)
			MessageBox.Show(
				$"Uventet feil i UI-tråd:\n\n{e.Exception}",
				"Feil",
				MessageBoxButton.OK,
				MessageBoxImage.Error);

			e.Handled = true;
		}


		private void OnDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
		{
			// Logg
			File.AppendAllText(_logPath,
				$"[DOMENE] {DateTime.Now}: {e.ExceptionObject}\n{new string('-', 40)}\n");

			// Vis dialog
			MessageBox.Show(
				$"Uventet feil i bakgrunnstråd:\n{e.ExceptionObject}",
				"Feil",
				MessageBoxButton.OK,
				MessageBoxImage.Error);
		}

		private void OnUnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
		{
			// Logg
			File.AppendAllText(_logPath,
				$"[TASK] {DateTime.Now}: {e.Exception}\n{new string('-', 40)}\n");

			// Vis dialog
			MessageBox.Show(
				$"Uventet oppgave-unntak:\n{e.Exception.Message}",
				"Feil",
				MessageBoxButton.OK,
				MessageBoxImage.Error);

			e.SetObserved();
		}
	}
}
