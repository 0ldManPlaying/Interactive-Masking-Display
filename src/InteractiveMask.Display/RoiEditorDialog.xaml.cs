using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using InteractiveMask.Detection;

namespace InteractiveMask.Display;

/// <summary>
/// Polygon editor for the per-camera Region of Interest. Shows a frozen
/// snapshot of the current live frame (or a placeholder if no frame is yet
/// available); clicks add polygon vertices in source-pixel coordinates. The
/// snapshot is rendered inside a <c>Viewbox</c> so coordinates inside the
/// drawing <see cref="Canvas"/> are always source-pixel space regardless of
/// the window size — WPF's hit-test pipeline rolls the Viewbox transform
/// back into <c>MouseEventArgs.GetPosition</c>, so we don't have to track
/// scale factors manually.
/// <para>
/// The dialog mutates the passed-in <see cref="CameraSlotSettings"/> in place
/// on Save (same pattern as NvrEditDialog and CameraAiSettingsDialog).
/// Cancelled / no-points / fewer-than-3-points outcomes leave the target's
/// existing polygon untouched on Cancel, or write an empty list on Save (the
/// "no ROI configured" signal).
/// </para>
/// </summary>
public partial class RoiEditorDialog : Window
{
    private readonly CameraSlotSettings _target;
    private readonly List<PolygonPoint> _points = new();

    /// <summary>
    /// Index of the polygon vertex currently being dragged, or -1 when no
    /// drag is in progress. Set on marker MouseDown, cleared on canvas MouseUp.
    /// Mouse-capture on the canvas ensures we still receive MouseMove / MouseUp
    /// even when the cursor leaves the marker (or the canvas bounds entirely).
    /// </summary>
    private int _draggingIndex = -1;

    public RoiEditorDialog(Window owner, CameraSlotSettings target, BitmapSource? snapshot, string cameraDescription)
    {
        InitializeComponent();
        Owner = owner;
        _target = target;

        SubheaderText.Text = cameraDescription;

        // Seed the working list from the persisted polygon so re-opening the
        // editor lets the user adjust the existing region instead of starting over.
        foreach (var p in target.AiRoiPolygon) _points.Add(p);

        if (snapshot != null && snapshot.PixelWidth > 0 && snapshot.PixelHeight > 0)
        {
            SnapshotImage.Source = snapshot;
            EditorContent.Width = snapshot.PixelWidth;
            EditorContent.Height = snapshot.PixelHeight;
            EditorViewbox.Visibility = Visibility.Visible;
            NoFramePlaceholder.Visibility = Visibility.Collapsed;
            RedrawPolygon();
        }
        else
        {
            // No snapshot: editor is read-only. Existing polygon (if any) stays
            // untouched on Save unless the user clicks Clear.
            EditorViewbox.Visibility = Visibility.Collapsed;
            NoFramePlaceholder.Visibility = Visibility.Visible;
            RedrawPolygon();
        }
    }

    private void OnCanvasClick(object sender, MouseButtonEventArgs e)
    {
        // Markers set e.Handled=true on their own MouseDown so we never get
        // here when the user grabs an existing vertex; this handler only fires
        // for clicks on empty canvas space. GetPosition relative to the
        // DrawCanvas returns coordinates inside the canvas's local space
        // (source-pixel coordinates, thanks to Viewbox).
        var pos = e.GetPosition(DrawCanvas);
        int x = ClampX(pos.X);
        int y = ClampY(pos.Y);
        _points.Add(new PolygonPoint(x, y));
        RedrawPolygon();
    }

    private void OnMarkerMouseDown(int pointIndex, MouseButtonEventArgs e)
    {
        if (pointIndex < 0 || pointIndex >= _points.Count) return;
        _draggingIndex = pointIndex;
        DrawCanvas.CaptureMouse();
        // Stop the event bubbling so OnCanvasClick doesn't fire and add a new
        // point on top of the one being grabbed.
        e.Handled = true;
    }

    private void OnCanvasMouseMove(object sender, MouseEventArgs e)
    {
        if (_draggingIndex < 0) return;
        if (e.LeftButton != MouseButtonState.Pressed)
        {
            // Defensive: if the system thinks the button is up (lost focus,
            // alt-tab during drag, etc.), end the drag cleanly.
            EndDrag();
            return;
        }
        var pos = e.GetPosition(DrawCanvas);
        _points[_draggingIndex] = new PolygonPoint(ClampX(pos.X), ClampY(pos.Y));
        RedrawPolygon();
    }

    private void OnCanvasMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_draggingIndex < 0) return;
        EndDrag();
        // Drag-release is not a click-to-add: if the user happened to release
        // over empty canvas, we don't want a new point to appear. Marking
        // Handled prevents the bubbling MouseLeftButtonUp event from re-
        // triggering downstream click handlers in some WPF chrome layouts.
        e.Handled = true;
    }

    private void EndDrag()
    {
        _draggingIndex = -1;
        if (DrawCanvas.IsMouseCaptured) DrawCanvas.ReleaseMouseCapture();
    }

    private int ClampX(double v) => Math.Max(0, Math.Min((int)Math.Round(EditorContent.Width) - 1, (int)Math.Round(v)));
    private int ClampY(double v) => Math.Max(0, Math.Min((int)Math.Round(EditorContent.Height) - 1, (int)Math.Round(v)));

    private void OnUndoPoint(object sender, RoutedEventArgs e)
    {
        if (_points.Count == 0) return;
        _points.RemoveAt(_points.Count - 1);
        RedrawPolygon();
    }

    private void OnClearPoints(object sender, RoutedEventArgs e)
    {
        if (_points.Count == 0) return;
        _points.Clear();
        RedrawPolygon();
    }

    /// <summary>
    /// Sync the visible Polygon shape and the point-count text to the current
    /// <see cref="_points"/> list. Point markers (small circles) are added as
    /// child Ellipse elements on the Canvas alongside the Polygon shape.
    /// </summary>
    private void RedrawPolygon()
    {
        // Update polygon shape's vertices.
        var pts = new PointCollection(_points.Count);
        foreach (var p in _points) pts.Add(new Point(p.X, p.Y));
        PolygonShape.Points = pts;

        // Rebuild point markers. Keep the Polygon shape as the first child of
        // DrawCanvas so we always know where to start the remove pass.
        for (int i = DrawCanvas.Children.Count - 1; i >= 0; i--)
        {
            if (DrawCanvas.Children[i] is Ellipse) DrawCanvas.Children.RemoveAt(i);
        }
        // Marker size is in source pixels; appears scaled by the Viewbox to fit
        // the available area. 18 px diameter at source scale gives a clearly
        // visible dot on typical 1080p / 4MP feeds without dominating the view.
        const double markerSize = 18;
        for (int i = 0; i < _points.Count; i++)
        {
            var p = _points[i];
            int capturedIndex = i; // closure-safe local
            var dot = new Ellipse
            {
                Width = markerSize,
                Height = markerSize,
                Stroke = Brushes.White,
                StrokeThickness = 2,
                Fill = new SolidColorBrush(Color.FromRgb(0x4D, 0xAB, 0xF7)),
                // SizeAll cursor signals to the user that the marker is
                // grab-and-move, not just a static decoration.
                Cursor = Cursors.SizeAll,
            };
            // MouseDown on the marker starts a drag. The handler sets
            // _draggingIndex and captures the mouse on DrawCanvas so move/up
            // events keep flowing even if the cursor leaves the marker.
            dot.MouseLeftButtonDown += (s, ev) => OnMarkerMouseDown(capturedIndex, ev);
            Canvas.SetLeft(dot, p.X - markerSize / 2);
            Canvas.SetTop(dot, p.Y - markerSize / 2);
            DrawCanvas.Children.Add(dot);
        }

        var t = Strings.Instance.Current;
        PointCountText.Text = string.Format(CultureInfo.CurrentCulture, t.AiRoiPointsLabel, _points.Count);
    }

    private void OnOk(object sender, RoutedEventArgs e)
    {
        // Always commit the working list to the target, even if it's empty or
        // has < 3 points. Empty / degenerate polygons are the canonical signal
        // for "no ROI configured, the runtime filter is bypassed". The runtime
        // check (TileViewModel.PointInPolygon) only filters when polygon.Count
        // >= 3, so a 1- or 2-point list is equivalent to "no ROI" but lets
        // the admin keep their in-progress drawing across dialog closes.
        _target.AiRoiPolygon = new List<PolygonPoint>(_points);
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
