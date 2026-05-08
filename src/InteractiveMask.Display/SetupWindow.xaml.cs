using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace InteractiveMask.Display;

public partial class SetupWindow : Window
{
    private readonly ConfigService _configService;
    private readonly AdminPinService _adminPin;
    private readonly AuditLog _audit;
    private readonly ObservableCollection<CameraSlotSettings> _cameras = new();
    private readonly ObservableCollection<NvrSettings> _nvrs = new();
    private readonly ObservableCollection<AuditViewRow> _auditRows = new();
    private readonly StreamChoices _streamChoices = new();

    public bool ConfigChanged { get; private set; }

    public SetupWindow(ConfigService configService, AdminPinService adminPin, AuditLog audit)
    {
        InitializeComponent();
        _configService = configService;
        _adminPin = adminPin;
        _audit = audit;
        CameraGrid.ItemsSource = _cameras;
        NvrGrid.ItemsSource = _nvrs;
        AuditGrid.ItemsSource = _auditRows;
        AuditPath.Text = audit.Path;
        // DataGridComboBoxColumn lives outside the visual tree until rendered, so
        // it doesn't pick up DataContext or Window resources via the usual binding
        // mechanisms. Wire ItemsSource directly to the live-language collection.
        StreamColumn.ItemsSource = _streamChoices;
        // NVR-id ComboBox shows NVR names, stores the int id. Bound to the same
        // ObservableCollection that the NVR DataGrid edits, so adding/removing
        // an NVR row updates the cameras dropdown immediately.
        NvrColumn.ItemsSource = _nvrs;
        Loaded += (_, _) => Populate();
    }

    private void Populate()
    {
        var settings = _configService.Load();

        _nvrs.Clear();
        foreach (var n in settings.Nvrs)
        {
            _nvrs.Add(new NvrSettings
            {
                Id = n.Id,
                Name = n.Name,
                Ip = n.Ip,
                Port = n.Port,
                User = n.User,
                Password = n.Password,
            });
        }

        _cameras.Clear();
        foreach (var c in settings.Cameras)
        {
            _cameras.Add(new CameraSlotSettings
            {
                Slot = c.Slot,
                NvrId = c.NvrId,
                CameraIndex = c.CameraIndex,
                StreamId = c.StreamId,
                Label = c.Label,
            });
        }

        SelectGridChoice(settings.Grid.Rows);

        AutoUnmaskMinutes.Text = settings.Privacy.AutoUnmaskMinutes.ToString(CultureInfo.InvariantCulture);
        WarningMinutes.Text = settings.Privacy.WarningMinutes.ToString(CultureInfo.InvariantCulture);
        RequireSessionPin.IsChecked = settings.Privacy.RequireSessionPin;

        HttpPort.Text = settings.Web.HttpPort.ToString(CultureInfo.InvariantCulture);
        HttpsPort.Text = settings.Web.HttpsPort?.ToString(CultureInfo.InvariantCulture) ?? "";
        CertPath.Text = settings.Web.CertPath ?? "";
        CertPassword.Password = settings.Web.CertPassword ?? "";
        BindAllInterfaces.IsChecked = settings.Web.BindAllInterfaces;

        LangNl.IsChecked = !string.Equals(settings.Language, "en", StringComparison.OrdinalIgnoreCase);
        LangEn.IsChecked = string.Equals(settings.Language, "en", StringComparison.OrdinalIgnoreCase);

        KioskEnabled.IsChecked = settings.Kiosk.Enabled;
        UseActiveDirectory.IsChecked = settings.Auth.UseActiveDirectory;
        AuthDomain.Text = settings.Auth.Domain;

        SelectComboItemByTag(WebAccessMode, settings.Web.Access.Mode);
        WebAccessPin.Password = settings.Web.Access.Pin ?? "";
        WebAccessDomain.Text = settings.Web.Access.Domain ?? "";
        UpdateWebAccessVisibility();

        SyslogEnabled.IsChecked = settings.AuditForward.SyslogEnabled;
        SyslogHost.Text = settings.AuditForward.SyslogHost;
        SyslogPort.Text = settings.AuditForward.SyslogPort.ToString(CultureInfo.InvariantCulture);
        SyslogAppName.Text = settings.AuditForward.SyslogAppName;
        SyslogFacility.Text = settings.AuditForward.SyslogFacility.ToString(CultureInfo.InvariantCulture);
        SelectComboItemByTag(SyslogProtocol, settings.AuditForward.SyslogProtocol);
        SyslogTestResult.Text = "";

        ConnDot.Background = (System.Windows.Media.Brush)FindResource("green");
        ConnText.Text = Strings.Instance.Current.SetupConfigLoaded;

        LoadAuditRows();
    }

    private void OnWebAccessModeChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        // Defensive — change-handler can fire during InitializeComponent before
        // the panels are created.
        if (WebAccessPinPanel is null || WebAccessAdPanel is null) return;
        UpdateWebAccessVisibility();
    }

    private void UpdateWebAccessVisibility()
    {
        var mode = (WebAccessMode.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Tag as string ?? "off";
        WebAccessPinPanel.Visibility = string.Equals(mode, "pin", StringComparison.OrdinalIgnoreCase)
            ? System.Windows.Visibility.Visible
            : System.Windows.Visibility.Collapsed;
        WebAccessAdPanel.Visibility = string.Equals(mode, "ad", StringComparison.OrdinalIgnoreCase)
            ? System.Windows.Visibility.Visible
            : System.Windows.Visibility.Collapsed;
    }

    private static void SelectComboItemByTag(System.Windows.Controls.ComboBox combo, string tag)
    {
        for (int i = 0; i < combo.Items.Count; i++)
        {
            if (combo.Items[i] is System.Windows.Controls.ComboBoxItem item &&
                string.Equals(item.Tag as string, tag, StringComparison.OrdinalIgnoreCase))
            {
                combo.SelectedIndex = i;
                return;
            }
        }
        if (combo.Items.Count > 0) combo.SelectedIndex = 0;
    }

    private static StringsTable T => Strings.Instance.Current;

    private void SelectGridChoice(int rows)
    {
        Grid1.IsChecked = rows == 1;
        Grid2.IsChecked = rows == 2;
        Grid3.IsChecked = rows == 3;
        Grid4.IsChecked = rows == 4 || rows < 1 || rows > 4;
    }

    private int CurrentGridChoice() =>
        Grid1.IsChecked == true ? 1 :
        Grid2.IsChecked == true ? 2 :
        Grid3.IsChecked == true ? 3 :
        4;

    // ---- Window chrome ----------------------------------------------------

    private void OnTitleBarMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left && e.ButtonState == MouseButtonState.Pressed)
        {
            try { DragMove(); } catch { /* DragMove can throw if state changes mid-flight */ }
        }
    }

    private void OnMinimize(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void OnClose(object sender, RoutedEventArgs e) => Close();
    private void OnCancel(object sender, RoutedEventArgs e) => Close();

    // ---- Sidebar nav -> panel switching ------------------------------------

    private void OnNavChecked(object sender, RoutedEventArgs e)
    {
        if (sender is not RadioButton rb || rb.Tag is not string targetName) return;

        foreach (var name in new[] { "NvrPanel", "CamerasPanel", "GridPanel", "PrivacyPanel", "WebPanel", "AuditPanel", "AdminPanel" })
        {
            if (FindName(name) is FrameworkElement fe)
            {
                fe.Visibility = name == targetName ? Visibility.Visible : Visibility.Collapsed;
            }
        }
    }

    // ---- Web tab -----------------------------------------------------------

    private void OnBrowseCert(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title = T.SetupBrowseCertTitle,
            Filter = T.SetupBrowseCertFilter,
        };
        if (dlg.ShowDialog(this) == true)
        {
            CertPath.Text = dlg.FileName;
        }
    }

    private void OnGenerateSelfSigned(object sender, RoutedEventArgs e)
    {
        try
        {
            var (path, password) = CertificateHelper.GenerateSelfSigned();
            CertPath.Text = path;
            CertPassword.Password = password;
            StatusLine.Foreground = (System.Windows.Media.Brush)FindResource("green");
            StatusLine.Text = string.Format(T.SetupCertCreatedFormat, path);
        }
        catch (Exception ex)
        {
            ShowError(string.Format(T.SetupCertCreateFailedFormat, ex.Message));
        }
    }

    // ---- Cameras tab -------------------------------------------------------

    private void OnAddCameraRow(object sender, RoutedEventArgs e)
    {
        int nextSlot = _cameras.Count == 0 ? 0 : _cameras.Max(c => c.Slot) + 1;
        // Default new cameras to the first NVR in the list (Id=0 if migrated).
        int defaultNvrId = _nvrs.Count > 0 ? _nvrs[0].Id : 0;
        _cameras.Add(new CameraSlotSettings
        {
            Slot = nextSlot,
            NvrId = defaultNvrId,
            CameraIndex = nextSlot,
            StreamId = 1,
            Label = $"Camera {nextSlot + 1}",
        });
    }

    private void OnDeleteCameraRow(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn) return;
        if (btn.DataContext is not CameraSlotSettings row) return;
        _cameras.Remove(row);
    }

    // ---- Cameras: drag/drop reorder ----------------------------------------
    //
    // Beheerder kan rijen verslepen via de grip-kolom om de tegelvolgorde aan
    // te passen. De Slot-waarden worden na elke drop opnieuw genummerd zodat
    // ze 1-op-1 met de lijstpositie overeenkomen.

    private CameraSlotSettings? _cameraDragRow;
    private Point _cameraDragStart;

    private void OnCameraGripMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement fe) return;
        if (fe.DataContext is not CameraSlotSettings row) return;
        _cameraDragRow = row;
        _cameraDragStart = e.GetPosition(null);
    }

    private void OnCameraGripMouseMove(object sender, MouseEventArgs e)
    {
        if (_cameraDragRow is null) return;
        if (e.LeftButton != MouseButtonState.Pressed) return;
        if (sender is not DependencyObject d) return;

        var pt = e.GetPosition(null);
        if (Math.Abs(pt.X - _cameraDragStart.X) < SystemParameters.MinimumHorizontalDragDistance
            && Math.Abs(pt.Y - _cameraDragStart.Y) < SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        // Capture the row to drag, then clear our tracking field. WPF takes
        // over with its modal drag loop until Drop or Cancel.
        var dragged = _cameraDragRow;
        _cameraDragRow = null;
        DragDrop.DoDragDrop(d, dragged, DragDropEffects.Move);
    }

    private void OnCameraGridDragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(typeof(CameraSlotSettings))
            ? DragDropEffects.Move
            : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnCameraGridDrop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(typeof(CameraSlotSettings)) is not CameraSlotSettings src) return;

        int oldIdx = _cameras.IndexOf(src);
        if (oldIdx < 0) return;

        // Walk up from the drop target until we find the DataGridRow we are
        // hovering over. If we drop in empty space below the last row, fall
        // through to "move to the end".
        var target = FindCameraRowFromVisual(e.OriginalSource as DependencyObject);
        int newIdx = target is null ? _cameras.Count - 1 : _cameras.IndexOf(target);
        if (newIdx < 0 || newIdx == oldIdx) return;

        _cameras.Move(oldIdx, newIdx);

        // Slot is the visual position in the grid. After a reorder we want
        // slot N to mean "row N", so renumber from the top. The model has no
        // INotifyPropertyChanged, so trigger a manual refresh on the grid.
        for (int i = 0; i < _cameras.Count; i++)
        {
            _cameras[i].Slot = i;
        }
        CameraGrid.Items.Refresh();
        e.Handled = true;
    }

    private CameraSlotSettings? FindCameraRowFromVisual(DependencyObject? d)
    {
        while (d is not null and not DataGridRow)
        {
            d = VisualTreeHelper.GetParent(d);
        }
        return (d as DataGridRow)?.DataContext as CameraSlotSettings;
    }

    // ---- NVR tab -----------------------------------------------------------

    private void OnAddNvrRow(object sender, RoutedEventArgs e)
    {
        // Pick a fresh Id one above the current max. Never reuse a deleted Id
        // so any stale state.json or audit entry referring to that Id stays
        // unambiguous.
        int nextId = _nvrs.Count == 0 ? 0 : _nvrs.Max(n => n.Id) + 1;
        var draft = new NvrSettings
        {
            Id = nextId,
            Name = $"NVR {nextId + 1}",
            Ip = "",
            Port = 8016,
            User = "",
            Password = "",
        };

        // Show the edit modal immediately so the user can fill in IP / user /
        // password in one go. On Save the row is appended to the list; on
        // Cancel nothing is added (avoids ghost-rows).
        var dlg = new NvrEditDialog(this, draft, isNew: true);
        if (dlg.ShowDialog() == true)
        {
            _nvrs.Add(draft);
        }
    }

    private void OnEditNvrRow(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn) return;
        if (btn.DataContext is not NvrSettings row) return;
        OpenNvrEditModal(row);
    }

    private void OnNvrRowDoubleClick(object sender, MouseButtonEventArgs e)
    {
        // Double-clicking a row opens the same edit modal as the pencil icon.
        if (NvrGrid.SelectedItem is NvrSettings row)
        {
            OpenNvrEditModal(row);
            e.Handled = true;
        }
    }

    private void OpenNvrEditModal(NvrSettings row)
    {
        var dlg = new NvrEditDialog(this, row, isNew: false);
        if (dlg.ShowDialog() == true)
        {
            // The DataGrid binds to the same NvrSettings instance the dialog
            // mutated, but the columns aren't INotifyPropertyChanged so we
            // force a refresh by removing+reinserting the row at the same index.
            int idx = _nvrs.IndexOf(row);
            if (idx >= 0)
            {
                _nvrs.RemoveAt(idx);
                _nvrs.Insert(idx, row);
            }
        }
    }

    private void OnDeleteNvrRow(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn) return;
        if (btn.DataContext is not NvrSettings row) return;
        // Block delete when cameras still reference this NVR id; the validation
        // would catch it on save anyway, but failing fast here is friendlier.
        if (_cameras.Any(c => c.NvrId == row.Id))
        {
            ShowError(string.Format(T.SetupErrNvrInUseFormat, row.Name));
            return;
        }
        _nvrs.Remove(row);
    }

    // ---- Save / cancel -----------------------------------------------------

    private void OnSave(object sender, RoutedEventArgs e)
    {
        // Commit any in-flight NVR-grid edits first; otherwise the latest typed
        // value in Name / Ip / Port etc. wouldn't be observable yet.
        NvrGrid.CommitEdit(DataGridEditingUnit.Row, true);

        if (_nvrs.Count == 0)
        {
            ShowError(T.SetupErrNoNvr);
            return;
        }

        // Each NVR's Port field is a string in the bound text column; we round-trip
        // it through int via the binding, so it's already int. Validate >0.
        var badPort = _nvrs.FirstOrDefault(n => n.Port <= 0);
        if (badPort is not null)
        {
            ShowError(T.SetupErrPort);
            return;
        }
        var dupNvrId = _nvrs.GroupBy(n => n.Id).FirstOrDefault(g => g.Count() > 1);
        if (dupNvrId is not null)
        {
            ShowError(string.Format(T.SetupErrDuplicateNvrFormat, dupNvrId.Key));
            return;
        }
        if (!int.TryParse(AutoUnmaskMinutes.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var autoMinutes) || autoMinutes < 0)
        {
            ShowError(T.SetupErrAutoMinutes);
            return;
        }
        if (!int.TryParse(WarningMinutes.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var warnMinutes) || warnMinutes < 0)
        {
            ShowError(T.SetupErrWarningMinutes);
            return;
        }
        if (!int.TryParse(HttpPort.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var httpPort) || httpPort <= 0)
        {
            ShowError(T.SetupErrHttpPort);
            return;
        }
        int? httpsPort = null;
        if (!string.IsNullOrWhiteSpace(HttpsPort.Text))
        {
            if (!int.TryParse(HttpsPort.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var hp) || hp <= 0)
            {
                ShowError(T.SetupErrHttpsPort);
                return;
            }
            httpsPort = hp;
        }

        var gridRows = CurrentGridChoice();

        CameraGrid.CommitEdit(DataGridEditingUnit.Row, true);

        // Validate the camera list before save. Two errors that would crash the
        // running app on next start: same slot used twice, or same camera index
        // referenced from two slots. Surface them early with a clear message
        // pointing at the offending row.
        var cameraRows = _cameras.Where(c => c.Slot >= 0).ToList();
        var dupSlot = cameraRows.GroupBy(c => c.Slot).FirstOrDefault(g => g.Count() > 1);
        if (dupSlot is not null)
        {
            // Show 1-based numbering so the message lines up with what the user sees.
            ShowError(string.Format(T.SetupErrDuplicateSlotFormat, dupSlot.Key + 1));
            return;
        }
        var dupCamera = cameraRows.GroupBy(c => (c.NvrId, c.CameraIndex)).FirstOrDefault(g => g.Count() > 1);
        if (dupCamera is not null)
        {
            ShowError(string.Format(T.SetupErrDuplicateCameraFormat, dupCamera.Key.CameraIndex + 1));
            return;
        }
        // Catch cameras whose NvrId no longer corresponds to a configured NVR
        // (can happen if user deletes an NVR row while cameras still reference it).
        var validNvrIds = _nvrs.Select(n => n.Id).ToHashSet();
        var orphan = cameraRows.FirstOrDefault(c => !validNvrIds.Contains(c.NvrId));
        if (orphan is not null)
        {
            ShowError(string.Format(T.SetupErrCameraOrphanFormat, orphan.Slot + 1));
            return;
        }

        var settings = new AppSettings
        {
            Nvrs = _nvrs
                .OrderBy(n => n.Id)
                .Select(n => new NvrSettings
                {
                    Id = n.Id,
                    Name = string.IsNullOrWhiteSpace(n.Name) ? $"NVR {n.Id + 1}" : n.Name.Trim(),
                    Ip = (n.Ip ?? "").Trim(),
                    Port = n.Port,
                    User = (n.User ?? "").Trim(),
                    Password = n.Password ?? "",
                })
                .ToList(),
            Grid = { Rows = gridRows, Columns = gridRows },
            Privacy =
            {
                AutoUnmaskMinutes = autoMinutes,
                WarningMinutes = warnMinutes,
                RequireSessionPin = RequireSessionPin.IsChecked == true,
            },
            Cameras = _cameras
                .Where(c => c.Slot >= 0)
                .OrderBy(c => c.Slot)
                .Select(c => new CameraSlotSettings
                {
                    Slot = c.Slot,
                    NvrId = c.NvrId,
                    CameraIndex = c.CameraIndex,
                    StreamId = c.StreamId,
                    Label = c.Label ?? "",
                })
                .ToList(),
            Kiosk = { Enabled = KioskEnabled.IsChecked == true },
            Auth =
            {
                UseActiveDirectory = UseActiveDirectory.IsChecked == true,
                Domain = AuthDomain.Text?.Trim() ?? "",
            },
            Web =
            {
                HttpPort = httpPort,
                HttpsPort = httpsPort,
                CertPath = string.IsNullOrWhiteSpace(CertPath.Text) ? null : CertPath.Text.Trim(),
                CertPassword = string.IsNullOrEmpty(CertPassword.Password) ? null : CertPassword.Password,
                BindAllInterfaces = BindAllInterfaces.IsChecked == true,
                Access =
                {
                    Mode = (WebAccessMode.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Tag as string ?? "off",
                    Pin = string.IsNullOrEmpty(WebAccessPin.Password) ? null : WebAccessPin.Password,
                    Domain = WebAccessDomain.Text?.Trim() ?? "",
                },
            },
            AuditForward = ReadAuditForwardFromUi(),
            Language = (LangEn.IsChecked == true) ? "en" : "nl",
        };

        try
        {
            _configService.Save(settings);
            _audit.Write(AuditEventType.PinSet, source: "admin", detail: "config saved");
        }
        catch (Exception ex)
        {
            ShowError(string.Format(T.SetupErrSaveFailedFormat, ex.Message));
            return;
        }

        ConfigChanged = true;
        // No more "restart required" blocking dialog. The owner (MainWindow) decides
        // whether the change is structural and prompts the user only when it really
        // is. Live-applicable changes (labels, language, privacy timers, session-PIN
        // toggle, admin-PIN) take effect immediately on close.
        Close();
    }

    private void ShowError(string message)
    {
        StatusLine.Foreground = (System.Windows.Media.Brush)FindResource("danger");
        StatusLine.Text = message;
    }

    private void OnOpenHelp(object sender, RoutedEventArgs e)
    {
        try
        {
            var help = new HelpWindow { Owner = this };
            help.ShowDialog();
        }
        catch (Exception ex)
        {
            ShowError($"Help kon niet worden geopend: {ex.Message}");
        }
    }

    private void OnChangeAdminPin(object sender, RoutedEventArgs e)
    {
        var pin = PinDialog.PromptForNewPin(
            this,
            title: T.SetupAdminPinNewTitle,
            subtitle: T.SetupAdminPinNewSub);
        if (string.IsNullOrEmpty(pin)) return;
        _adminPin.SetPin(pin);
        _audit.Write(AuditEventType.PinSet, source: "admin", detail: "admin-pin changed");
        StatusLine.Foreground = (System.Windows.Media.Brush)FindResource("green");
        StatusLine.Text = T.SetupAdminPinUpdated;
    }

    // ---- Audit tab ---------------------------------------------------------

    private void OnAuditRefresh(object sender, RoutedEventArgs e) => LoadAuditRows();

    private void OnAuditExport(object sender, RoutedEventArgs e)
    {
        var dlg = new SaveFileDialog
        {
            Title = T.SetupAuditExportTitle,
            FileName = $"interactivemask-audit-{DateTime.Now:yyyy-MM-dd}.csv",
            Filter = T.SetupAuditExportFilter,
            DefaultExt = ".csv",
        };
        if (dlg.ShowDialog(this) != true) return;

        try
        {
            var rows = ReadAuditRows();
            using var writer = new StreamWriter(dlg.FileName, append: false, Encoding.UTF8);
            writer.WriteLine("timestamp,type,slot,cameraIndex,label,source,detail");
            foreach (var r in rows)
            {
                writer.Write(Csv(r.TimestampDisplay)); writer.Write(',');
                writer.Write(Csv(r.Type));             writer.Write(',');
                writer.Write(Csv(r.Slot?.ToString(CultureInfo.InvariantCulture) ?? "")); writer.Write(',');
                writer.Write(Csv(r.CameraIndex?.ToString(CultureInfo.InvariantCulture) ?? "")); writer.Write(',');
                writer.Write(Csv(r.Label ?? ""));      writer.Write(',');
                writer.Write(Csv(r.Source ?? ""));     writer.Write(',');
                writer.WriteLine(Csv(r.Detail ?? ""));
            }
            StatusLine.Foreground = (System.Windows.Media.Brush)FindResource("green");
            StatusLine.Text = string.Format(T.SetupExportedFormat, dlg.FileName);
        }
        catch (Exception ex)
        {
            ShowError(string.Format(T.SetupExportFailedFormat, ex.Message));
        }
    }

    private void LoadAuditRows()
    {
        _auditRows.Clear();
        foreach (var r in ReadAuditRows()) _auditRows.Add(r);
    }

    private AuditForwardSettings ReadAuditForwardFromUi()
    {
        int.TryParse(SyslogPort.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var port);
        int.TryParse(SyslogFacility.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var facility);
        var protocol = (SyslogProtocol.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Tag as string ?? "udp";
        return new AuditForwardSettings
        {
            SyslogEnabled = SyslogEnabled.IsChecked == true,
            SyslogHost    = SyslogHost.Text?.Trim() ?? "",
            SyslogPort    = port > 0 ? port : 514,
            SyslogProtocol= protocol,
            SyslogFacility= Math.Clamp(facility, 0, 23),
            SyslogAppName = string.IsNullOrWhiteSpace(SyslogAppName.Text) ? "InteractiveMask" : SyslogAppName.Text.Trim(),
        };
    }

    private async void OnSyslogTest(object sender, RoutedEventArgs e)
    {
        var s = ReadAuditForwardFromUi();
        if (string.IsNullOrWhiteSpace(s.SyslogHost))
        {
            SyslogTestResult.Text = T.AuditSyslogTestNoHost;
            SyslogTestResult.Foreground = (System.Windows.Media.Brush)FindResource("amber");
            return;
        }
        SyslogTestResult.Text = T.AuditSyslogTesting;
        SyslogTestResult.Foreground = (System.Windows.Media.Brush)FindResource("text.muted");
        var (ok, error) = await SyslogForwarder.SendTestAsync(s);
        if (ok)
        {
            SyslogTestResult.Text = T.AuditSyslogTestOk;
            SyslogTestResult.Foreground = (System.Windows.Media.Brush)FindResource("green");
        }
        else
        {
            SyslogTestResult.Text = string.Format(T.AuditSyslogTestFailFormat, error ?? "");
            SyslogTestResult.Foreground = (System.Windows.Media.Brush)FindResource("red");
        }
    }

    private List<AuditViewRow> ReadAuditRows()
    {
        var path = _audit.Path;
        var rows = new List<AuditViewRow>();
        if (!File.Exists(path)) return rows;
        try
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(fs);
            string? line;
            while ((line = reader.ReadLine()) is not null)
            {
                if (line.Length == 0) continue;
                AuditEvent? ev;
                try { ev = JsonSerializer.Deserialize<AuditEvent>(line, AuditViewRow.JsonOpts); }
                catch { continue; }
                if (ev is null) continue;
                rows.Add(AuditViewRow.From(ev));
            }
        }
        catch { /* best effort */ }
        rows.Reverse();
        return rows;
    }

    private static string Csv(string value)
    {
        bool needsQuote = value.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0;
        if (!needsQuote) return value;
        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }

    public sealed class AuditViewRow
    {
        internal static readonly JsonSerializerOptions JsonOpts = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
        };

        public string TimestampDisplay { get; init; } = "";
        public string Type { get; init; } = "";
        public int? Slot { get; init; }
        public int? CameraIndex { get; init; }
        public string? Label { get; init; }
        public string? Source { get; init; }
        public string? Detail { get; init; }

        public static AuditViewRow From(AuditEvent ev) => new()
        {
            TimestampDisplay = ev.Timestamp.LocalDateTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
            Type = ev.Type.ToString(),
            Slot = ev.Slot,
            CameraIndex = ev.CameraIndex,
            Label = ev.Label,
            Source = ev.Source,
            Detail = ev.Detail,
        };
    }
}

/// <summary>
/// User-facing stream labels for the cameras DataGrid. The stored value stays
/// an int (0/1/2) so the existing config schema is unchanged; only the display
/// is friendly. Labels follow the camera-stream convention: 0 = main / highest,
/// 1 = secondary / default for multi-channel viewing, 2 = third / lowest.
/// </summary>
public sealed class StreamChoices : ObservableCollection<StreamChoice>
{
    public StreamChoices()
    {
        Add(new StreamChoice { Value = 0, Display = Strings.Instance.Current.StreamHigh });
        Add(new StreamChoice { Value = 1, Display = Strings.Instance.Current.StreamDefault });
        Add(new StreamChoice { Value = 2, Display = Strings.Instance.Current.StreamLow });
        Strings.Instance.PropertyChanged += (_, _) => Refresh();
    }

    private void Refresh()
    {
        if (Count != 3) return;
        var s = Strings.Instance.Current;
        this[0] = new StreamChoice { Value = 0, Display = s.StreamHigh };
        this[1] = new StreamChoice { Value = 1, Display = s.StreamDefault };
        this[2] = new StreamChoice { Value = 2, Display = s.StreamLow };
    }
}

public sealed class StreamChoice
{
    public int Value { get; init; }
    public string Display { get; init; } = "";
}
