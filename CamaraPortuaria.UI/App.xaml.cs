using System.Globalization;
using System.IO;
using System.Linq;
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
using Wpf.Ui;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;
using Wpf.Ui.DependencyInjection;

namespace CamaraPortuaria.UI;

public partial class App : Application
{
    private IHost _host = null!;
    private ILogger<App>? _logger;

    /// <summary>
    /// Usa FakeMailService en lugar de MailKit (no envía correo real).
    /// Configurable en appsettings.json → PuertoBB:MailMockService. Default: false (correo real).
    /// </summary>
    public static bool MailMockService { get; private set; }

    /// <summary>
    /// Usa el stack AFIP mock (Afip.Net.Mock, sin red ni certificado) en lugar de WSAA/WSFE reales.
    /// Configurable en appsettings.json → PuertoBB:AfipMockService. Default: false (AFIP real).
    /// </summary>
    public static bool AfipMockService { get; private set; }

    /// <summary>
    /// Modo demo = cualquiera de los dos mocks activo. Controla la siembra de datos de ejemplo
    /// (SeedData) y el rótulo "— MODO DEMO" en el título de la ventana.
    /// </summary>
    public static bool ModoDemo => MailMockService || AfipMockService;

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
            MailMockService = cfg.GetValue("PuertoBB:MailMockService", false);
            AfipMockService = cfg.GetValue("PuertoBB:AfipMockService", false);

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

            // Modo generador de datos de stress (sin abrir la ventana): --seed-stress N.
            if (ParseSeedStress(e.Args) is int seedN)
            {
                await EjecutarSeedStressAsync(seedN);
                Shutdown(0);
                return;
            }

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
        if (AfipMockService)
            services.AddPuertoBBAfipMock();
        else
            services.AddPuertoBBAfip(ticketCacheDir: Path.Combine(AppDataDir, "afip-ticket-cache"));
        services.AddPuertoBBMail(usarFake: MailMockService);

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
        services.AddTransient<ClientesPage>();       services.AddTransient<ClientesViewModel>();
        services.AddTransient<ConceptosReciboPage>(); services.AddTransient<ConceptosReciboViewModel>();
        services.AddTransient<GruposPage>();         services.AddTransient<GruposViewModel>();
        services.AddTransient<ConfiguracionPage>();  services.AddTransient<ConfiguracionViewModel>();
    }

    private async Task InicializarBaseDeDatosAsync()
    {
        await using var scope = _host.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CamaraPortuariaDbContext>();
        var backup = scope.ServiceProvider.GetRequiredService<IBackupService>();

        // Backup defensivo antes de aplicar migraciones sobre una base existente: una migración
        // fallida en una actualización no debe dejar la base sin un respaldo previo.
        var hayBasePrevia = File.Exists(Path.Combine(AppDataDir, "camara-portuaria.db"));
        if (hayBasePrevia && (await db.Database.GetPendingMigrationsAsync()).Any())
        {
            _logger?.LogInformation("Hay migraciones pendientes; generando backup defensivo antes de migrar.");
            var pre = await backup.BackupAutomaticoAsync();
            if (!pre.Success)
                _logger?.LogWarning("No se pudo generar el backup previo a la migración: {Error}", pre.ErrorMessage);
        }

        try
        {
            await db.Database.MigrateAsync();
        }
        catch (Exception ex)
        {
            _logger?.LogCritical(ex, "Falló la migración de la base de datos");
            throw new InvalidOperationException(
                "No se pudo actualizar la base de datos. Si había una base anterior, quedó respaldada en la " +
                "carpeta de backups. Contactá al equipo de desarrollo.\n\nDetalle: " + ex.Message, ex);
        }

        if (ModoDemo)
            await SeedData.EnsureSeededAsync(db);

        DispararBackupDiario();
    }

    /// <summary>
    /// Genera un backup automático diario (a lo sumo uno por día) sin demorar el arranque de la UI.
    /// Usa su propio scope porque corre después de que el scope de inicialización se libera.
    /// </summary>
    private void DispararBackupDiario() => _ = Task.Run(async () =>
    {
        try
        {
            await using var scope = _host.Services.CreateAsyncScope();
            var backup = scope.ServiceProvider.GetRequiredService<IBackupService>();
            if (backup.FechaUltimoBackup()?.Date == DateTime.Now.Date) return; // ya hay copia de hoy
            var res = await backup.BackupAutomaticoAsync();
            if (res.Success) _logger?.LogInformation("Backup automático diario: {Ruta}", res.Data);
            else _logger?.LogWarning("El backup automático diario falló: {Error}", res.ErrorMessage);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error en el backup automático diario");
        }
    });

    /// <summary>Devuelve N si los argumentos incluyen "--seed-stress N" (N &gt; 0); si no, null.</summary>
    private static int? ParseSeedStress(string[] args)
    {
        for (var i = 0; i < args.Length - 1; i++)
            if (args[i] == "--seed-stress" && int.TryParse(args[i + 1], out var n) && n > 0)
                return n;
        return null;
    }

    /// <summary>Genera datos de stress contra la base real de la app (migra + siembra base + N recibos) y
    /// deja un resumen en <c>stress-seed.txt</c> (esta app es WinExe: no hay consola para el output).</summary>
    private async Task EjecutarSeedStressAsync(int cantidad)
    {
        await using var scope = _host.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CamaraPortuariaDbContext>();
        await db.Database.MigrateAsync();
        await SeedData.EnsureSeededAsync(db); // garantiza empresas para los recibos
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var total = await StressSeedData.GenerarRecibosAsync(db, cantidad, _logger);
        sw.Stop();
        var resumen = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Stress seed: {cantidad} pedidos · {total} recibos en base · {sw.Elapsed:mm\\:ss\\.fff}";
        await File.WriteAllTextAsync(Path.Combine(AppDataDir, "stress-seed.txt"), resumen);
        _logger?.LogInformation("{Resumen}", resumen);
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
        // Para que los bindings WPF (StringFormat) usen es-AR y no la cultura del sistema.
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
