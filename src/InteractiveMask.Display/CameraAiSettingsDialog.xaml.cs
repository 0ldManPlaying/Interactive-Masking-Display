using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls.Primitives;
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
        PaddingSlider.Value = Math.Clamp(target.MaskPaddingPercent, 0, 50);
        UpdatePaddingText();

        UpdateClassesEnabledState();
    }

    private void OnPaddingSliderChanged(object sender, RoutedPropertyChangedEventArgs<double> e) => UpdatePaddingText();

    private void UpdatePaddingText()
    {
        // Round to int (slider is logically integer-valued; visual feedback
        // shows the discrete percentage value rather than a fractional one).
        int pct = (int)Math.Round(PaddingSlider.Value);
        PaddingValueText.Text = pct.ToString(CultureInfo.InvariantCulture) + " %";
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

        _target.MaskPaddingPercent = Math.Clamp((int)Math.Round(PaddingSlider.Value), 0, 50);

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
