using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Markup;
using CamaraPortuaria.UI.Data;
using CamaraPortuaria.UI.Logging;
using CamaraPortuaria.UI.Services;
using CamaraPortuaria.UI.ViewModels;
using CamaraPortuaria.UI.Views;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
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
    /// Configurable en appsettings.json → PuertoBB:ModoDemo.
    /// </summary>
    public static bool ModoDemo { get; private set; } = true;

    /// <summary>
    /// Modo de integración AFIP. Configurable en appsettings.json → PuertoBB:Afip (Mock|Real).
    /// </summary>
    public static AfipModo Afip { get; private set; } = AfipModo.Mock;

    private static string AppDataDir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PuertoBB", "CamaraPortuaria");

    protected override async void OnStartup(StartupEventArgs e)
    {
        try
        {
            base.OnStartup(e);

            ConfigurarCultura();
            Directory.CreateDirectory(AppDataDir);
            ConfigurarManejadoresGlobales();

            var cfg = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: true).Build();
            ModoDemo = cfg.GetValue("PuertoBB:ModoDemo", true);
            Afip     = Enum.TryParse<AfipModo>(cfg["PuertoBB:Afip"], out var m) ? m : AfipModo.Mock;

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
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                $"Error al iniciar la aplicación:\n\n{ex.Message}",
                "Error de inicio",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    private void ConfigureServices(IServiceCollection services)
    {
        var dbPath = Path.Combine(AppDataDir, "camara-portuaria.db");

        services.AddCamaraPortuariaInfrastructure(dbPath);
        services.AddCamaraPortuariaServices();
        services.AddPuertoBBPdf();
        if (Afip == AfipModo.Real)
            services.AddPuertoBBAfip(ticketCacheDir: Path.Combine(AppDataDir, "afip-ticket-cache"));
        else
            services.AddPuertoBBAfipMock();
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
        await using var scope = _host.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CamaraPortuariaDbContext>();
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

    /// <summary>
    /// Fija la cultura es-AR en toda la app para que importes y fechas se formateen igual en código
    /// (helper <c>Formato</c>) y en los bindings XAML (StringFormat=C2/d), sin depender de la cultura del SO.
    /// </summary>
    private static void ConfigurarCultura()
    {
        var cultura = CultureInfo.GetCultureInfo("es-AR");
        CultureInfo.DefaultThreadCurrentCulture = cultura;
        CultureInfo.DefaultThreadCurrentUICulture = cultura;
        Thread.CurrentThread.CurrentCulture = cultura;
        Thread.CurrentThread.CurrentUICulture = cultura;
        FrameworkElement.LanguageProperty.OverrideMetadata(
            typeof(FrameworkElement),
            new FrameworkPropertyMetadata(XmlLanguage.GetLanguage(cultura.IetfLanguageTag)));
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
        try
        {
            if (_host is not null) await _host.StopAsync();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error en OnExit");
        }
        base.OnExit(e);
    }
}
