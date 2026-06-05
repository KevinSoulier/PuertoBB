using System.Windows.Controls;

namespace CamaraPortuaria.UI.Dialogs;

public partial class AlertDialog : UserControl
{
    private readonly TaskCompletionSource<bool> _tcs = new();
    public Task Result => _tcs.Task;

    public AlertDialog(string title, string message, string closeText)
    {
        InitializeComponent();
        TitleText.Text = title;
        MessageText.Text = message;
        CloseButton.Content = closeText;
    }

    private void Close_Click(object sender, System.Windows.RoutedEventArgs e) => _tcs.TrySetResult(true);
}
