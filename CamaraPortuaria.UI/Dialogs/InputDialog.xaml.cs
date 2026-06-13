using System.Windows.Controls;

namespace CamaraPortuaria.UI.Dialogs;

public partial class InputDialog : UserControl
{
    private readonly TaskCompletionSource<string?> _tcs = new();
    public Task<string?> Result => _tcs.Task;

    public InputDialog(string title, string placeholder, string? initialValue, string? description = null)
    {
        InitializeComponent();
        TitleText.Text = title;
        InputBox.Text = initialValue ?? string.Empty;
        InputBox.Tag = placeholder;
        if (!string.IsNullOrWhiteSpace(description))
        {
            DescriptionText.Text = description;
            DescriptionText.Visibility = System.Windows.Visibility.Visible;
        }
        Loaded += (_, _) => { InputBox.Focus(); InputBox.SelectAll(); };
    }

    private void Ok_Click(object sender, System.Windows.RoutedEventArgs e)
        => _tcs.TrySetResult(string.IsNullOrWhiteSpace(InputBox.Text) ? null : InputBox.Text.Trim());

    private void Cancel_Click(object sender, System.Windows.RoutedEventArgs e) => _tcs.TrySetResult(null);
}
