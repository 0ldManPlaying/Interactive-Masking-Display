using System.Globalization;
using System.Windows;
using System.Windows.Input;

namespace InteractiveMask.Display;

/// <summary>
/// Modal NVR-edit dialog. Used by SetupWindow for both "Add NVR" (blank values)
/// and "Edit existing NVR" (pre-filled). Mutates a passed-in <see cref="NvrSettings"/>
/// in place on Save and reports back via DialogResult.
/// </summary>
public partial class NvrEditDialog : Window
{
    private readonly NvrSettings _target;

    public NvrEditDialog(Window owner, NvrSettings target, bool isNew)
    {
        InitializeComponent();
        Owner = owner;
        _target = target;

        var s = Strings.Instance.Current;
        Header.Text = isNew ? s.NvrDialogAddTitle : s.NvrDialogEditTitle;
        Subheader.Text = isNew ? s.NvrDialogSubtitle : s.NvrDialogEditSubtitle;

        NameBox.Text = target.Name;
        IpBox.Text = target.Ip;
        PortBox.Text = target.Port.ToString(CultureInfo.InvariantCulture);
        UserBox.Text = target.User;
        PasswordBoxField.Password = target.Password ?? "";

        Loaded += (_, _) =>
        {
            // Auto-focus the first empty field; for an existing NVR being edited
            // that's usually the password (most common reason to re-open).
            if (string.IsNullOrEmpty(NameBox.Text)) NameBox.Focus();
            else if (string.IsNullOrEmpty(IpBox.Text)) IpBox.Focus();
            else PasswordBoxField.Focus();
        };
    }

    private void OnOk(object sender, RoutedEventArgs e)
    {
        var s = Strings.Instance.Current;

        var name = NameBox.Text?.Trim() ?? "";
        var ip = IpBox.Text?.Trim() ?? "";
        if (!int.TryParse(PortBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var port) || port <= 0 || port > 65535)
        {
            ErrorText.Text = s.SetupErrPort;
            PortBox.Focus();
            return;
        }
        if (string.IsNullOrWhiteSpace(ip))
        {
            ErrorText.Text = s.NvrDialogErrIpEmpty;
            IpBox.Focus();
            return;
        }

        _target.Name = string.IsNullOrEmpty(name) ? $"NVR {_target.Id + 1}" : name;
        _target.Ip = ip;
        _target.Port = port;
        _target.User = UserBox.Text?.Trim() ?? "";
        _target.Password = PasswordBoxField.Password;

        DialogResult = true;
        Close();
    }

    private void OnCancel(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape) { DialogResult = false; Close(); }
    }
}
