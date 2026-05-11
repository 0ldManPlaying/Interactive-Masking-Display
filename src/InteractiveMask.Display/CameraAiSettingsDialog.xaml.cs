using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using InteractiveMask.Detection;

namespace InteractiveMask.Display;

/// <summary>
/// Per-camera AI-masking configuration modal: an Enabled toggle plus a checkbox
/// for each masking category. Mutates a passed-in <see cref="CameraSlotSettings"/>
/// in place on Save and signals acceptance via <c>DialogResult</c>, matching the
/// pattern of NvrEditDialog.
/// </summary>
public partial class CameraAiSettingsDialog : Window
{
    private readonly CameraSlotSettings _target;

    public CameraAiSettingsDialog(Window owner, CameraSlotSettings target, string cameraDescription)
    {
        InitializeComponent();
        Owner = owner;
        _target = target;

        SubheaderText.Text = cameraDescription;

        EnableBox.IsChecked = target.AiEnabled;
        PersonBox.IsChecked     = target.AiClasses.Contains(ObjectClass.Person);
        TwoWheelerBox.IsChecked = target.AiClasses.Contains(ObjectClass.TwoWheeler);
        VehicleBox.IsChecked    = target.AiClasses.Contains(ObjectClass.Vehicle);

        UpdateClassesEnabledState();
    }

    private void OnEnableChanged(object sender, RoutedEventArgs e) => UpdateClassesEnabledState();

    /// <summary>
    /// Grey out the per-category checkboxes when the master toggle is off; the
    /// values still round-trip through the dialog so a user toggling off then on
    /// does not lose their per-category selection.
    /// </summary>
    private void UpdateClassesEnabledState()
    {
        bool enabled = EnableBox.IsChecked == true;
        ClassesPanel.IsEnabled = enabled;
        ClassesPanel.Opacity = enabled ? 1.0 : 0.45;
    }

    private void OnOk(object sender, RoutedEventArgs e)
    {
        _target.AiEnabled = EnableBox.IsChecked == true;

        var picked = new HashSet<ObjectClass>();
        if (PersonBox.IsChecked == true)     picked.Add(ObjectClass.Person);
        if (TwoWheelerBox.IsChecked == true) picked.Add(ObjectClass.TwoWheeler);
        if (VehicleBox.IsChecked == true)    picked.Add(ObjectClass.Vehicle);
        _target.AiClasses = picked;

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
