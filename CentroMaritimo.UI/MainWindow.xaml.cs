using System.Windows;
using CentroMaritimo.UI.Services;
using CentroMaritimo.UI.ViewModels;
using CentroMaritimo.UI.Views;
using Wpf.Ui;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace CentroMaritimo.UI;

public partial class MainWindow : FluentWindow
{
    private readonly INavigationService _nav;

    public MainWindow(MainWindowViewModel vm, INavigationService nav, DialogService dialogService, ISnackbarService snackbarService)
    {
        InitializeComponent();
        DataContext = vm;
        _nav = nav;
        if (App.ModoDemo) Title += " — MODO DEMO";

        // En modo "System" seguimos el tema y el acento del SO en vivo.
        if (PreferenciasUsuario.GetTema() == "System")
            SystemThemeWatcher.Watch(this, WindowBackdropType.Mica);

        _nav.SetNavigationControl(RootNavigation);
        dialogService.Initialize(DialogOverlay, DialogHost);
        snackbarService.SetSnackbarPresenter(RootSnackbar);
        SnackbarHost.Service = snackbarService;

        RootNavigation.Navigated += (_, e) =>
        {
            // El contenido vive en un Frame (frontera de herencia): fijamos el color de texto del
            // tema en cada página para que sus TextBlock sigan el estilo (claro/oscuro).
            if (e.Page is System.Windows.Controls.Page page)
                page.SetResourceReference(System.Windows.Controls.Page.ForegroundProperty, "TextFillColorPrimaryBrush");

            // Botón de back de la barra de ventana: habilitado según el historial del NavigationView.
            BackButton.IsEnabled = RootNavigation.CanGoBack;
        };

        Loaded += (_, _) => _nav.Navigate(typeof(DashboardPage));
    }

    private void BackButton_Click(object sender, RoutedEventArgs e) => RootNavigation.GoBack();
}
