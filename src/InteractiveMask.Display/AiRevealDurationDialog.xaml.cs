using System;
using System.Windows;
using System.Windows.Input;

namespace InteractiveMask.Display;

/// <summary>
/// Modal duration picker for the AI-reveal flow (v2.0.x F2). Returns a
/// <see cref="TimeSpan"/> for one of the four canonical choices or
/// <see cref="TimeSpan.MaxValue"/> for the "until I remask" option;
/// returns null on Cancel/Escape.
///
/// Auth happens in <see cref="MaskController"/> *before* this dialog is shown,
/// so the reviewer has already authenticated by the time these buttons appear.
/// That mirrors the v1.x flow where the PIN/AD prompt comes first and the
/// follow-up action (mask, unmask) is a non-modal confirmation.
/// </summary>
public partial class AiRevealDurationDialog : Window
{
    /// <summary>Sentinel for the "no timer, until I remask" choice.</summary>
    public static readonly TimeSpan UntilRemask = TimeSpan.MaxValue;

    /// <summary>Selected duration after a successful ShowDialog(); null on cancel.</summary>
    public TimeSpan? SelectedDuration { get; private set; }

    public AiRevealDurationDialog(Window owner, string cameraDescription)
    {
        InitializeComponent();
        Owner = owner;
        // Re-purpose the subtitle slot to also show which camera this reveal
        // applies to, so a reviewer juggling several tiles can't accidentally
        // suppress the wrong one. The localised "How long should..." line is
        // kept as a leading sentence.
        var t = Strings.Instance.Current;
        SubtitleText.Text = $"{t.AiRevealDialogSubtitle}\n{cameraDescription}";
    }

    /// <summary>
    /// Prompt the reviewer and block until a duration is chosen or the
    /// dialog is dismissed. Returns null if the dialog was cancelled.
    /// </summary>
    public static TimeSpan? Prompt(Window owner, string cameraDescription)
    {
        var dlg = new AiRevealDurationDialog(owner, cameraDescription);
        return dlg.ShowDialog() == true ? dlg.SelectedDuration : null;
    }

    private void On30s(object sender, RoutedEventArgs e)     => Commit(TimeSpan.FromSeconds(30));
    private void On1m(object sender, RoutedEventArgs e)      => Commit(TimeSpan.FromMinutes(1));
    private void On5m(object sender, RoutedEventArgs e)      => Commit(TimeSpan.FromMinutes(5));
    private void OnUntilRemask(object sender, RoutedEventArgs e) => Commit(UntilRemask);

    private void Commit(TimeSpan duration)
    {
        SelectedDuration = duration;
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
