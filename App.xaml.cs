using System.Windows;
using System.Windows.Threading;
using Quizmester.Services;
using Quizmester.Windows;

namespace Quizmester;

public partial class App : Application
{
    // Entry point van de WPF-app: database klaarzetten vóórdat het inlogscherm opent.
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Centrale vangnetmelding voor ONVERWACHTE fouten op de UI-thread.
        // Dit vervangt niet de gerichte validaties in de formulieren.
        DispatcherUnhandledException += OnDispatcherUnhandledException;

        try
        {
            // CREATE TABLE IF NOT EXISTS + voorbeeldaccounts, vragen en categorieën.
            DatabaseService.Initialize();
            new LoginWindow().Show();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Quizmester kon niet starten.\n\n{ex.Message}",
                "Opstartfout",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown();
        }
    }

    // WPF laat hier fouten zien in plaats van het programma zonder uitleg te sluiten.
    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show(
            "Er ging onverwacht iets mis. De actie is afgebroken zodat de applicatie niet hard crasht.\n\n" + e.Exception.Message,
            "Onverwachte fout",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        e.Handled = true;
    }
}
