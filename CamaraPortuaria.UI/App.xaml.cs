using System.IO;
using System.Windows;
using CamaraPortuaria.UI.Data;
using CamaraPortuaria.UI.Logging;
using CamaraPortuaria.UI.Services;
using CamaraPortuaria.UI.ViewModels;
using CamaraPortuaria.UI.Views;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PuertoBB.Core.Interfaces.Services;
using PuertoBB.Infrastructure;
using PuertoBB.Infrastructure.Data;
using PuertoBB.Services;
using PuertoBB.Services.Security;
using Wpf.Ui;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;
using Wpf.Ui.DependencyInjection;

namespace CamaraPortuaria.UI;

/// <summary>Modo de integración AFIP para esta app.</summary>
public enum AfipModo
{
    /// <summary>Afip.Net.Mock — mapper/WsfeService/caché reales, sin red ni certificado.</summary>
    Mock,
    /// <summary>WSAA + WSFE reales. Requiere certificado .p12 configurado.</summary>
    Real,
}

public partial class App : Application
{
    private IHost _host = null!;
    private ILogger<App>? _logger;

    /// <summary>
    /// Modo desarrollo/demo: usa servicio de Mail falso y siembra datos de ejemplo.
    /// En producción poner en false.
    /// </summary>
    public const bool ModoDemo = true;

    /// <summary>
    /// Modo de integración AFIP. Cambiar aquí para alternar entre Fake, Mock y Real.
    /// Fake = sin red (default). Mock = stack Afip.Net completo sin red ni cert. Real = WSAA+WSFE reales.
    /// </summary>
    public const AfipModo Afip = AfipModo.Mock;

    private static string AppDataDir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PuertoBB", "CamaraPortuaria");

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        Directory.CreateDirectory(AppDataDir);
        ConfigurarManejadoresGlobales();

        _host = Host.CreateDefaultBuilder()
            .ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.SetMinimumLevel(LogLevel.Debug);
                logging.AddProvider(new FileLoggerProvider(Path.Combine(AppDataDir, "Logs")));
            })
            .ConfigureServices(ConfigureServices)
            .Build();

        _logger = _host.Services.GetRequiredService<ILogger<App>>();

        await InicializarBaseDeDatosAsync();
        RestaurarTema();

        var main = _host.Services.GetRequiredService<MainWindow>();
        main.Show();
    }

    private void ConfigureServices(IServiceCollection services)
    {
        var dbPath = Path.Combine(AppDataDir, "camara-portuaria.db");

        services.AddCamaraPortuariaInfrastructure(dbPath);
        services.AddCamaraPortuariaServices();
        services.AddPuertoBBPdf();
#pragma warning disable CS0162 // rama inalcanzable por diseño — cambiar la constante Afip para activar otro modo
        if (Afip == AfipModo.Real)
            services.AddPuertoBBAfip(ticketCacheDir: Path.Combine(AppDataDir, "afip-ticket-cache"));
        else
            services.AddPuertoBBAfipMock();
#pragma warning restore CS0162
        services.AddPuertoBBMail(usarFake: ModoDemo);

        services.AddSingleton<ISecretProtector, DpapiSecretProtector>();
        services.AddTransient<IAfipConfigProvider, AfipConfigProvider>();
        services.AddTransient<IMailConfigProvider, MailConfigProvider>();
        services.AddTransient<IBackupService, BackupService>();

        services.AddNavigationViewPageProvider();
        services.AddSingleton<INavigationService, NavigationService>();
        services.AddSingleton<DialogService>();
        services.AddSingleton<IDialogService>(sp => sp.GetRequiredService<DialogService>());
        services.AddSingleton<ISnackbarService, SnackbarService>();

        services.AddSingleton<MainWindow>();
        services.AddSingleton<MainWindowViewModel>();

        services.AddTransient<DashboardPage>();       services.AddTransient<DashboardViewModel>();
        services.AddTransient<ControlPagosPage>();   services.AddTransient<ControlPagosViewModel>();
        services.AddTransient<RecibosPage>();        services.AddTransient<RecibosViewModel>();
        services.AddTransient<EmisionMasivaPage>();  services.AddTransient<EmisionMasivaViewModel>();
        services.AddTransient<EmpresasPage>();       services.AddTransient<EmpresasViewModel>();
        services.AddTransient<ConceptosReciboPage>(); services.AddTransient<ConceptosReciboViewModel>();
        services.AddTransient<GruposPage>();         services.AddTransient<GruposViewModel>();
        services.AddTransient<ConfiguracionPage>();  services.AddTransient<ConfiguracionViewModel>();
    }

    private async Task InicializarBaseDeDatosAsync()
    {
        var db = _host.Services.GetRequiredService<CamaraPortuariaDbContext>();
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
            _logger?.LogCritical(e.Exception, "Excepción no manejada en hilo UI");
            MostrarErrorCritico(e.Exception.Message);
            e.Handled = true;
        };

        AppDomain.CurrentDomain.UnhandledException += (s, e) =>
        {
            _logger?.LogCritical(e.ExceptionObject as Exception, "Excepción no manejada en hilo background");
        };

        TaskScheduler.UnobservedTaskException += (s, e) =>
        {
            _logger?.LogError(e.Exception, "Task exception no observada");
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
        if (_host is not null) await _host.StopAsync();
        base.OnExit(e);
    }
}
