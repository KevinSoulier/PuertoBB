using System.IO;
using System.Windows;
using CentroMaritimo.UI.Data;
using CentroMaritimo.UI.Services;
using CentroMaritimo.UI.ViewModels;
using CentroMaritimo.UI.Views;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PuertoBB.Core.Interfaces.Services;
using PuertoBB.Infrastructure;
using PuertoBB.Infrastructure.Data;
using PuertoBB.Services;
using Serilog;
using Wpf.Ui;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;
using Wpf.Ui.DependencyInjection;

namespace CentroMaritimo.UI;

public partial class App : Application
{
    private IHost _host = null!;

    /// <summary>Modo desarrollo/demo: servicios falsos de AFIP/Mail y datos sembrados.</summary>
    public const bool ModoDemo = true;

    private static string AppDataDir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PuertoBB", "CentroMaritimo");

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        Directory.CreateDirectory(AppDataDir);
        ConfigurarSerilog();
        ConfigurarManejadoresGlobales();

        _host = Host.CreateDefaultBuilder()
            .UseSerilog()
            .ConfigureServices(ConfigureServices)
            .Build();

        await InicializarBaseDeDatosAsync();
        RestaurarTema();

        var main = _host.Services.GetRequiredService<MainWindow>();
        main.Show();
    }

    private void ConfigureServices(IServiceCollection services)
    {
        var dbPath = Path.Combine(AppDataDir, "centro-maritimo.db");

        services.AddCentroMaritimoInfrastructure(dbPath);
        services.AddCentroMaritimoServices();
        services.AddPuertoBBPdf();
        services.AddPuertoBBAfip(usarFake: ModoDemo);
        services.AddPuertoBBMail(usarFake: ModoDemo);

        services.AddTransient<IAfipConfigProvider, AfipConfigProvider>();
        services.AddTransient<IMailConfigProvider, MailConfigProvider>();
        services.AddTransient<IBackupService, BackupService>();

        services.AddNavigationViewPageProvider();
        services.AddSingleton<INavigationService, NavigationService>();
        services.AddSingleton<DialogService>();
        services.AddSingleton<IDialogService>(sp => sp.GetRequiredService<DialogService>());

        services.AddSingleton<MainWindow>();
        services.AddSingleton<MainWindowViewModel>();

        services.AddTransient<DashboardPage>();      services.AddTransient<DashboardViewModel>();
        services.AddTransient<RecibosPage>();        services.AddTransient<RecibosViewModel>();
        services.AddTransient<VouchersPage>();       services.AddTransient<VouchersViewModel>();
        services.AddTransient<CierrePeriodoPage>();  services.AddTransient<CierrePeriodoViewModel>();
        services.AddTransient<EmisionMasivaPage>();  services.AddTransient<EmisionMasivaViewModel>();
        services.AddTransient<AgenciasPage>();       services.AddTransient<AgenciasViewModel>();
        services.AddTransient<BarcosPage>();         services.AddTransient<BarcosViewModel>();
        services.AddTransient<GruposPage>();         services.AddTransient<GruposViewModel>();
        services.AddTransient<ConfiguracionPage>();  services.AddTransient<ConfiguracionViewModel>();
    }

    private void ConfigurarSerilog()
    {
        var logPath = Path.Combine(AppDataDir, "Logs", "app-.log");
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.File(
                path: logPath,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 30,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}")
            .Enrich.FromLogContext()
            .Enrich.WithMachineName()
            .CreateLogger();
    }

    private async Task InicializarBaseDeDatosAsync()
    {
        var db = _host.Services.GetRequiredService<CentroMaritimoDbContext>();
        await db.Database.MigrateAsync();
        if (ModoDemo)
            await SeedData.EnsureSeededAsync(db);
    }

    private void RestaurarTema()
    {
        var pref = PreferenciasUsuario.GetTema();   // "Light" | "Dark" | "System"
        if (pref == "System")
            ApplicationThemeManager.ApplySystemTheme();              // tema + acento del sistema
        else
            ApplicationThemeManager.Apply(
                pref == "Dark" ? ApplicationTheme.Dark : ApplicationTheme.Light,
                WindowBackdropType.Mica);                            // updateAccent: true (default) → acento del sistema
    }

    private void ConfigurarManejadoresGlobales()
    {
        DispatcherUnhandledException += (s, e) =>
        {
            Log.Fatal(e.Exception, "Excepción no manejada en hilo UI");
            MostrarErrorCritico(e.Exception.Message);
            e.Handled = true;
        };

        AppDomain.CurrentDomain.UnhandledException += (s, e) =>
        {
            Log.Fatal(e.ExceptionObject as Exception, "Excepción no manejada en hilo background");
            Log.CloseAndFlush();
        };

        TaskScheduler.UnobservedTaskException += (s, e) =>
        {
            Log.Error(e.Exception, "Task exception no observada");
            e.SetObserved();
        };
    }

    private void MostrarErrorCritico(string mensaje)
    {
        try
        {
            var dialog = _host.Services.GetRequiredService<IDialogService>();
            _ = dialog.ShowAlertAsync("Ocurrió un error",
                $"{mensaje}\n\nEl detalle quedó registrado en el archivo de log:\n{Path.Combine(AppDataDir, "Logs")}");
        }
        catch
        {
            System.Windows.MessageBox.Show(mensaje, "Error",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        Log.CloseAndFlush();
        if (_host is not null) await _host.StopAsync();
        base.OnExit(e);
    }
}
