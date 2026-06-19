using System.Windows;
using System.Windows.Controls;
using CamaraPortuaria.UI.Services;
using CamaraPortuaria.UI.ViewModels;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace CamaraPortuaria.UI.Views;

public partial class ConfiguracionPage : Page
{
    private readonly ConfiguracionViewModel _vm;
    private bool _cargando = true;

    public ConfiguracionPage(ConfiguracionViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
        _vm = vm;
        Loaded   += OnLoaded;
        Unloaded += (_, _) => _vm.PropertyChanged -= OnViewModelPropertyChanged;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        CertPasswordBox.Password = _vm.EdCertificadoPassword ?? string.Empty;
        SmtpPasswordBox.Password = _vm.CtaSmtpPassword ?? string.Empty;
        OAuthClientSecretBox.Password = _vm.CtaOAuthClientSecret ?? string.Empty;
        ThemeSelector.SelectedIndex = PreferenciasUsuario.GetTema() switch
        {
            "Light" => 0,
            "Dark"  => 1,
            _        => 2
        };
        _vm.PropertyChanged += OnViewModelPropertyChanged;
        _cargando = false;
    }

    // PasswordBox no soporta binding: sincroniza desde VM → UI cuando cambia EdCertificadoPassword
    // (al cargar un punto en edición, revertir con Cancelar) o SmtpPassword (al revertir Correo).
    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(_vm.EdCertificadoPassword))
        {
            var actual = _vm.EdCertificadoPassword ?? string.Empty;
            if (CertPasswordBox.Password != actual) CertPasswordBox.Password = actual;
        }
        else if (e.PropertyName == nameof(_vm.CtaSmtpPassword))
        {
            var actual = _vm.CtaSmtpPassword ?? string.Empty;
            if (SmtpPasswordBox.Password != actual) SmtpPasswordBox.Password = actual;
        }
        else if (e.PropertyName == nameof(_vm.CtaOAuthClientSecret))
        {
            var actual = _vm.CtaOAuthClientSecret ?? string.Empty;
            if (OAuthClientSecretBox.Password != actual) OAuthClientSecretBox.Password = actual;
        }
    }

    /// <summary>Inserta la variable del botón en el cuerpo (TextBox).</summary>
    private void InsertarVariable_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement fe || fe.Tag is not string token) return;
        var i = MailCuerpoBox.SelectionStart;
        MailCuerpoBox.Text = MailCuerpoBox.Text.Insert(i, token);
        MailCuerpoBox.SelectionStart = i + token.Length;
        MailCuerpoBox.Focus();
    }

    private void CertPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        => _vm.EdCertificadoPassword = CertPasswordBox.Password;

    private void SmtpPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        => _vm.CtaSmtpPassword = SmtpPasswordBox.Password;

    private void OAuthClientSecretBox_PasswordChanged(object sender, RoutedEventArgs e)
        => _vm.CtaOAuthClientSecret = OAuthClientSecretBox.Password;

    private void ThemeSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_cargando) return;
        var persist = ThemeSelector.SelectedIndex switch
        {
            0 => "Light",
            1 => "Dark",
            _ => "System"
        };

        var window = Window.GetWindow(this);
        if (persist == "System")
        {
            ApplicationThemeManager.ApplySystemTheme();
            SystemThemeWatcher.Watch(window, WindowBackdropType.Mica);
        }
        else
        {
            SystemThemeWatcher.UnWatch(window);
            ApplicationThemeManager.Apply(
                persist == "Dark" ? ApplicationTheme.Dark : ApplicationTheme.Light,
                WindowBackdropType.Mica);   // acento del sistema (updateAccent por defecto)
        }

        PreferenciasUsuario.SetTema(persist);
    }
}
