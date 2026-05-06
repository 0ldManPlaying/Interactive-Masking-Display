using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace InteractiveMask.Display;

public enum PinDialogMode
{
    /// <summary>First mask of a session: ask the caregiver to set a new PIN. No cancel.</summary>
    SetNew,
    /// <summary>Existing session: caregiver must enter the PIN to unmask. Cancel allowed.</summary>
    Verify,
}

public partial class PinDialog : Window
{
    private const int PinLength = 4;
    private readonly StringBuilder _entered = new();
    private readonly PinDialogMode _mode;
    private readonly Func<string, bool>? _verifier;
    private readonly Func<TimeSpan>? _lockoutCheck;

    public string EnteredPin { get; private set; } = "";

    private PinDialog(PinDialogMode mode, Func<string, bool>? verifier, Func<TimeSpan>? lockoutCheck)
    {
        InitializeComponent();
        _mode = mode;
        _verifier = verifier;
        _lockoutCheck = lockoutCheck;

        var s = Strings.Instance.Current;
        switch (mode)
        {
            case PinDialogMode.SetNew:
                Header.Text = s.PinSetTitle;
                Subheader.Text = s.PinSetSubtitle;
                CancelButton.Visibility = Visibility.Collapsed;
                break;
            case PinDialogMode.Verify:
                Header.Text = s.PinVerifyTitle;
                Subheader.Text = s.PinVerifySubtitle;
                CancelButton.Visibility = Visibility.Visible;
                break;
        }

        UpdateDots();
        UpdateLockoutMessage();
    }

    /// <summary>
    /// Show a dialog asking the user to set a brand-new 4-digit PIN. Returns the
    /// entered PIN, or null if the dialog was closed without completion.
    /// </summary>
    public static string? PromptForNewPin(Window? owner, string? title = null, string? subtitle = null)
    {
        var dlg = new PinDialog(PinDialogMode.SetNew, verifier: null, lockoutCheck: null) { Owner = owner };
        if (title is not null) dlg.Header.Text = title;
        if (subtitle is not null) dlg.Subheader.Text = subtitle;
        return dlg.ShowDialog() == true ? dlg.EnteredPin : null;
    }

    /// <summary>
    /// Show a verification dialog. The verifier delegate is called for each
    /// fully-entered PIN; if it returns true the dialog closes with success,
    /// otherwise the entry is cleared and the user can try again until lockout.
    /// </summary>
    public static bool PromptToVerify(
        Window? owner,
        Func<string, bool> verifier,
        Func<TimeSpan> lockoutCheck,
        string? title = null,
        string? subtitle = null)
    {
        var dlg = new PinDialog(PinDialogMode.Verify, verifier, lockoutCheck) { Owner = owner };
        if (title is not null) dlg.Header.Text = title;
        if (subtitle is not null) dlg.Subheader.Text = subtitle;
        return dlg.ShowDialog() == true;
    }

    private void OnDigitClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string tag && tag.Length == 1)
        {
            AppendDigit(tag[0]);
        }
    }

    private void OnBackspaceClick(object sender, RoutedEventArgs e)
    {
        if (_entered.Length > 0)
        {
            _entered.Length--;
            UpdateDots();
            ErrorText.Text = "";
        }
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        if (_mode == PinDialogMode.Verify)
        {
            DialogResult = false;
        }
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key >= Key.D0 && e.Key <= Key.D9)
        {
            AppendDigit((char)('0' + (e.Key - Key.D0)));
            e.Handled = true;
        }
        else if (e.Key >= Key.NumPad0 && e.Key <= Key.NumPad9)
        {
            AppendDigit((char)('0' + (e.Key - Key.NumPad0)));
            e.Handled = true;
        }
        else if (e.Key == Key.Back || e.Key == Key.Delete)
        {
            OnBackspaceClick(this, new RoutedEventArgs());
            e.Handled = true;
        }
        else if (e.Key == Key.Escape && _mode == PinDialogMode.Verify)
        {
            DialogResult = false;
            e.Handled = true;
        }
    }

    private void AppendDigit(char digit)
    {
        if (UpdateLockoutMessage()) return; // currently locked out
        if (_entered.Length >= PinLength) return;
        _entered.Append(digit);
        UpdateDots();

        if (_entered.Length == PinLength)
        {
            EnteredPin = _entered.ToString();
            HandleComplete();
        }
    }

    private void HandleComplete()
    {
        if (_mode == PinDialogMode.SetNew)
        {
            DialogResult = true;
            return;
        }

        if (_verifier is null)
        {
            DialogResult = true;
            return;
        }

        if (_verifier(EnteredPin))
        {
            DialogResult = true;
        }
        else
        {
            // Wrong PIN - clear input, show feedback, possibly lockout.
            _entered.Clear();
            UpdateDots();
            if (!UpdateLockoutMessage())
            {
                ErrorText.Text = Strings.Instance.Current.PinWrong;
            }
        }
    }

    /// <summary>
    /// Refresh the lockout message; returns true if currently locked out so callers
    /// can short-circuit further input handling.
    /// </summary>
    private bool UpdateLockoutMessage()
    {
        var remaining = _lockoutCheck?.Invoke() ?? TimeSpan.Zero;
        if (remaining > TimeSpan.Zero)
        {
            ErrorText.Text = string.Format(
                Strings.Instance.Current.PinLockedOutFormat,
                Math.Ceiling(remaining.TotalSeconds));
            return true;
        }
        return false;
    }

    private void UpdateDots()
    {
        Ellipse[] dots = { Dot0, Dot1, Dot2, Dot3 };
        for (int i = 0; i < dots.Length; i++)
        {
            dots[i].Fill = i < _entered.Length ? Brushes.White : Brushes.Transparent;
        }
    }
}
