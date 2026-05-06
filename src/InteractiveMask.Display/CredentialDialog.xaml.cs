using System.Windows;
using System.Windows.Input;

namespace InteractiveMask.Display;

public partial class CredentialDialog : Window
{
    private readonly Func<string, string, WindowsAuth.Result> _verifier;

    public string AuthenticatedUsername { get; private set; } = "";

    private CredentialDialog(Func<string, string, WindowsAuth.Result> verifier)
    {
        InitializeComponent();
        _verifier = verifier;
        Loaded += (_, _) => UserBox.Focus();
    }

    /// <summary>
    /// Show a modal credential dialog. The verifier delegate is called when the
    /// user presses Enter / OK; on success the dialog closes and
    /// <see cref="AuthenticatedUsername"/> is set to the canonical name.
    /// </summary>
    public static (bool ok, string username) Prompt(
        Window? owner,
        string? title,
        string? subtitle,
        Func<string, string, WindowsAuth.Result> verifier)
    {
        var dlg = new CredentialDialog(verifier) { Owner = owner };
        if (title is not null) dlg.Header.Text = title;
        if (subtitle is not null) dlg.Subheader.Text = subtitle;
        bool ok = dlg.ShowDialog() == true;
        return (ok, dlg.AuthenticatedUsername);
    }

    private void OnOk(object sender, RoutedEventArgs e)
    {
        var result = _verifier(UserBox.Text.Trim(), PasswordBoxField.Password);
        if (result.Success)
        {
            AuthenticatedUsername = result.Username ?? UserBox.Text.Trim();
            DialogResult = true;
        }
        else
        {
            ErrorText.Text = result.Error ?? "Aanmelden mislukt.";
            PasswordBoxField.Clear();
            PasswordBoxField.Focus();
        }
    }

    private void OnCancel(object sender, RoutedEventArgs e) => DialogResult = false;

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape) { DialogResult = false; e.Handled = true; }
    }
}
