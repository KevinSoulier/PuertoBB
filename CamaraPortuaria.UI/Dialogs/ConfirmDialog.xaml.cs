using System.Windows.Controls;

namespace CamaraPortuaria.UI.Dialogs;

public partial class ConfirmDialog : UserControl
{
    private readonly TaskCompletionSource<bool> _tcs = new();
    public Task<bool> Result => _tcs.Task;

    public ConfirmDialog(string title, string message, string confirmText, string cancelText)
    {
        InitializeComponent();
        TitleText.Text = title;
        MessageText.Text = message;
        ConfirmButton.Content = confirmText;
        CancelButton.Content = cancelText;
    }

    private void Confirm_Click(object sender, System.Windows.RoutedEventArgs e) => _tcs.TrySetResult(true);
    private void Cancel_Click(object sender, System.Windows.RoutedEventArgs e) => _tcs.TrySetResult(false);
}
