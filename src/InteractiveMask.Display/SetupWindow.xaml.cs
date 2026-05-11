using InteractiveMask.Gdk;
using InteractiveMask.Hardware;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
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

    /// <summary>
    /// Optional callback that returns the NVR-side camera names for a given
    /// NvrId. Wired by MainWindow to its live <see cref="InteractiveMask.Gdk.NvrSession"/>
    /// so the sync flow reuses the existing connection (avoids second-session
    /// rejection by NVRs that allow only one login per user).
    /// </summary>
    private readonly Func<int, CancellationToken, Task<IReadOnlyDictionary<int, string>>>? _cameraNameFetcher;

    /// <summary>
    /// Optional callback fired after a successful Apply / Save. Lets MainWindow
    /// hot-reload the new config without waiting for the dialog to close, so
    /// the operator can keep tweaking after seeing the change live.
    /// </summary>
    private readonly Action? _onApplied;

    public bool ConfigChanged { get; private set; }

    public SetupWindow(ConfigService configService, AdminPinService adminPin, AuditLog audit,
                       Func<int, CancellationToken, Task<IReadOnlyDictionary<int, string>>>? cameraNameFetcher = null,
                       Action? onApplied = null)
    {
        _cameraNameFetcher = cameraNameFetcher;
        _onApplied = onApplied;
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
        // Language picker: 5-language list lives on Strings.Instance.
        LanguageBox.ItemsSource = Strings.Instance.SupportedLanguages;
        // About tab: show the runtime assembly version so the user can spot
        // which build is installed without having to open file properties.
        AboutVersionValue.Text = typeof(SetupWindow).Assembly.GetName().Version?.ToString(3) ?? "";
        Loaded += (_, _) =>
        {
            Populate();
            // Kick off the v2.0 capability probe in the background. Result lands in
            // the About > System capabilities card via the Dispatcher.
            _ = RunCapabilityProbeAsync();
        };
        // Dispose the per-window StreamChoices so its static-event subscription
        // on Strings.Instance.PropertyChanged is released. Otherwise every
        // Setup-open accumulates one dead handler on the singleton over the
        // weeks-long kiosk uptime.
        Closed += (_, _) => _streamChoices.Dispose();
    }

    private void OnAboutOpenLink(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
    {
        // Hyperlink-driven URL open: shell-execute via the default browser.
        // Used by the website link in the About > Product card.
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = e.Uri.ToString(),
                UseShellExecute = true,
            };
            System.Diagnostics.Process.Start(psi);
            e.Handled = true;
        }
        catch
        {
            // Default browser missing or blocked. The URL stays visible in
            // the card so the user can copy it.
        }
    }

    private void OnAboutBugReport(object sender, RoutedEventArgs e)
    {
        // Open the user's default email client with a pre-filled subject and
        // a body containing the runtime version + Windows version. Saves the
        // reporter from having to look that up themselves and gives support
        // the context to reproduce.
        try
        {
            var version = typeof(SetupWindow).Assembly.GetName().Version?.ToString(3) ?? "unknown";
            var os = Environment.OSVersion.VersionString;
            var subject = Uri.EscapeDataString($"InteractiveMask v{version} - bug report");
            var body = Uri.EscapeDataString(
                $"Versie / Version: {version}\r\n" +
                $"Windows: {os}\r\n" +
                $"\r\n" +
                $"--- Beschrijving / Description ---\r\n" +
                $"\r\n");
            var mailto = $"mailto:support@bnl.idisglobal.com?subject={subject}&body={body}";
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = mailto,
                UseShellExecute = true,
            };
            System.Diagnostics.Process.Start(psi);
        }
        catch
        {
            // No mail client registered or blocked. Silent fallback; the
            // address is also visible in the card so the user can copy it
            // manually.
        }
    }

    // ------------------------------------------------------------------
    // v2.0 capability probe (P2 deliverable)
    // ------------------------------------------------------------------

    private HostCapabilityProfile? _capabilityProfile;

    private void OnAboutCapsRefresh(object sender, RoutedEventArgs e)
    {
        // Fire-and-forget; the async method handles its own UI updates and
        // re-enables the button when done.
        _ = RunCapabilityProbeAsync();
    }

    private void OnAboutCapsBenchmark(object sender, RoutedEventArgs e)
    {
        _ = RunBenchmarkAsync();
    }

    private void SetCapsButtonsEnabled(bool enabled)
    {
        if (AboutCapsRefreshBtn != null) AboutCapsRefreshBtn.IsEnabled = enabled;
        if (AboutCapsBenchmarkBtn != null) AboutCapsBenchmarkBtn.IsEnabled = enabled;
    }

    private async Task RunCapabilityProbeAsync()
    {
        var T = Strings.Instance.Current;
        SetCapsButtonsEnabled(false);
        AboutCapsStatus.Text = T.AboutCapsProbing;
        AboutCapsContent.Children.Clear();

        try
        {
            // WMI calls block; keep the UI responsive while they run.
            var probed = await Task.Run(HostCapabilityProbe.Probe);
            // Preserve any benchmark result obtained earlier in the session so the
            // re-probe does not silently wipe it from view.
            var preserved = _capabilityProfile?.LastBenchmark;
            var profile = probed with { LastBenchmark = preserved };
            _capabilityProfile = profile;
            // Persist for support / diagnostics. Best-effort, never throws.
            CapabilityProfileStore.Save(profile);
            RenderCapabilityProfile(profile);
        }
        catch (Exception ex)
        {
            AboutCapsStatus.Text = $"{T.AboutCapsProbing} {ex.GetType().Name}: {ex.Message}";
        }
        finally
        {
            SetCapsButtonsEnabled(true);
        }
    }

    private async Task RunBenchmarkAsync()
    {
        var T = Strings.Instance.Current;
        SetCapsButtonsEnabled(false);

        try
        {
            using var runner = new BenchmarkRunner();
            var progress = new Progress<BenchmarkProgress>(p =>
            {
                AboutCapsStatus.Text = $"{T.AboutCapsBenchmarkRunning} {p.Completed}/{p.Total}";
            });
            AboutCapsStatus.Text = T.AboutCapsBenchmarkRunning;

            var bench = await runner.RunAsync(progress);

            // Merge into current profile. If the user hit Run benchmark before the
            // hardware probe finished, run that now too so the persisted profile is
            // complete.
            var profile = _capabilityProfile ?? await Task.Run(HostCapabilityProbe.Probe);
            profile = profile with { LastBenchmark = bench };
            _capabilityProfile = profile;
            CapabilityProfileStore.Save(profile);
            RenderCapabilityProfile(profile);
        }
        catch (Exception ex)
        {
            AboutCapsStatus.Text = $"{T.AboutCapsBenchmarkRunning} {ex.GetType().Name}: {ex.Message}";
        }
        finally
        {
            SetCapsButtonsEnabled(true);
        }
    }

    private void RenderCapabilityProfile(HostCapabilityProfile profile)
    {
        var T = Strings.Instance.Current;
        AboutCapsContent.Children.Clear();

        AppendCapsRow(T.AboutCapsCpu, FormatCpu(profile.Cpu));
        AppendCapsRow(T.AboutCapsMemory, FormatMemory(profile.Memory));

        if (profile.Gpus.Count == 0)
        {
            AppendCapsRow(T.AboutCapsGpu, T.AboutCapsNone);
        }
        else
        {
            foreach (var g in profile.Gpus)
            {
                AppendCapsRow(T.AboutCapsGpu, FormatGpu(g, T));
            }
        }

        if (profile.Npus.Count == 0)
        {
            AppendCapsRow(T.AboutCapsNpu, T.AboutCapsNone);
        }
        else
        {
            foreach (var n in profile.Npus)
            {
                AppendCapsRow(T.AboutCapsNpu, n.Name);
            }
        }

        AppendCapsRow(T.AboutCapsAiTier, profile.Tier.ToString());

        if (profile.LastBenchmark is { } bench)
        {
            AppendCapsSeparator();
            AppendCapsRow(
                T.AboutCapsBenchmarkProvider,
                $"{bench.ExecutionProvider} · {bench.ModelFileName} ({string.Join("x", bench.InputDimensions)})");
            AppendCapsRow(T.AboutCapsBenchmarkColdStart, $"{bench.ColdStartMs:0.0} ms");
            AppendCapsRow(
                T.AboutCapsBenchmarkP50,
                $"{bench.SteadyStateP50Ms:0.00} / {bench.SteadyStateP95Ms:0.00} / {bench.SteadyStateP99Ms:0.00} ms");
            AppendCapsRow(T.AboutCapsBenchmarkThroughput, $"{bench.ThroughputFps:0} fps");
        }

        AboutCapsStatus.Text = $"{profile.ProbedAt.ToLocalTime():yyyy-MM-dd HH:mm} · schema {profile.ProbeSchemaVersion}";
    }

    private void AppendCapsSeparator()
    {
        var sep = new Border
        {
            Height = 1,
            Background = (Brush)TryFindResource("border") ?? Brushes.Gray,
            Margin = new Thickness(0, 8, 0, 8),
            Opacity = 0.5,
        };
        AboutCapsContent.Children.Add(sep);
    }

    private void AppendCapsRow(string label, string value)
    {
        var grid = new Grid { Margin = new Thickness(0, 3, 0, 3) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(190) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var labelTb = new TextBlock
        {
            Text = label,
            Foreground = (Brush)TryFindResource("text.muted") ?? Brushes.Gray,
            FontSize = 13,
        };
        Grid.SetColumn(labelTb, 0);

        var valueTb = new TextBlock
        {
            Text = value,
            Foreground = (Brush)TryFindResource("text") ?? Brushes.White,
            FontSize = 13,
            TextWrapping = TextWrapping.Wrap,
        };
        Grid.SetColumn(valueTb, 1);

        grid.Children.Add(labelTb);
        grid.Children.Add(valueTb);
        AboutCapsContent.Children.Add(grid);
    }

    private static string FormatCpu(CpuInfo c)
    {
        var dot = "·"; // middle dot, language-neutral separator
        var avx = c.HasAvx2 ? "AVX2 ✓" : "AVX2 ✗";
        return $"{c.Name} {dot} {c.PhysicalCores} / {c.LogicalCores} threads {dot} {avx}";
    }

    private static string FormatMemory(MemoryInfo m)
    {
        var totalGb = m.TotalPhysicalBytes / (1024.0 * 1024 * 1024);
        var freeGb = m.AvailablePhysicalBytes / (1024.0 * 1024 * 1024);
        return $"{totalGb:0.0} GB ({freeGb:0.0} GB free)";
    }

    private static string FormatGpu(GpuInfo g, StringsTable T)
    {
        var dot = "·";
        var vram = g.DedicatedVramBytes.HasValue
            ? $"{g.DedicatedVramBytes.Value / (1024.0 * 1024 * 1024):0.0} GB"
            : "?";
        var suffix = g.IsIntegratedHeuristic ? $" {dot} {T.AboutCapsIntegrated}" : string.Empty;
        return $"{g.Name} {dot} {vram}{suffix}";
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
                NvrTitle = c.NvrTitle ?? "",
            });
        }

        SelectGridChoice(settings.Grid.Rows);

        AutoUnmaskMinutes.Text = settings.Privacy.AutoUnmaskMinutes.ToString(CultureInfo.InvariantCulture);
        WarningMinutes.Text = settings.Privacy.WarningMinutes.ToString(CultureInfo.InvariantCulture);
        RequireSessionPin.IsChecked = settings.Privacy.RequireSessionPin;
        ShowMassUnmaskConfirm.IsChecked = settings.Privacy.ShowMassUnmaskConfirm;
        ModeOversightRadio.IsChecked = settings.Privacy.Mode == PrivacyMode.OversightDefault;
        ModePrivacyRadio.IsChecked = settings.Privacy.Mode == PrivacyMode.PrivacyDefault;
        PrivacyDefaultRequireAuthOnReveal.IsChecked = settings.Privacy.PrivacyDefaultRequireAuthOnReveal;
        ShowCameraLabel.IsChecked = settings.Privacy.ShowCameraLabel;
        ShowNvrTitle.IsChecked = settings.Privacy.ShowNvrTitle;

        HttpPort.Text = settings.Web.HttpPort.ToString(CultureInfo.InvariantCulture);
        HttpsPort.Text = settings.Web.HttpsPort?.ToString(CultureInfo.InvariantCulture) ?? "";
        CertPath.Text = settings.Web.CertPath ?? "";
        CertPassword.Password = settings.Web.CertPassword ?? "";
        BindAllInterfaces.IsChecked = settings.Web.BindAllInterfaces;

        // Language picker: pick the persisted code, fall back to the running
        // UI language (set by App.OnStartup either from the picker or the OS
        // culture) if the persisted value is missing or unsupported. Avoids a
        // jarring switch back to "nl" when the user just chose another
        // language at first run but hasn't saved Setup yet.
        var savedLang = string.IsNullOrWhiteSpace(settings.Language)
            ? Strings.Instance.LanguageCode
            : settings.Language!.Trim().ToLowerInvariant();
        if (!Strings.Instance.SupportedLanguages.Any(l => string.Equals(l.Code, savedLang, StringComparison.OrdinalIgnoreCase)))
        {
            savedLang = Strings.Instance.LanguageCode;
        }
        LanguageBox.SelectedValue = savedLang;

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
        Grid4.IsChecked = rows == 4 || rows < 1 || rows > 5;
        Grid5.IsChecked = rows == 5;
    }

    private int CurrentGridChoice() =>
        Grid1.IsChecked == true ? 1 :
        Grid2.IsChecked == true ? 2 :
        Grid3.IsChecked == true ? 3 :
        Grid5.IsChecked == true ? 5 :
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

        foreach (var name in new[] { "NvrPanel", "CamerasPanel", "PrivacyPanel", "WebPanel", "AuditPanel", "AdminPanel", "AboutPanel" })
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

    /// <summary>
    /// Opens the per-camera AI-masking modal. The dialog mutates the row in place
    /// on Save; the DataGrid view is refreshed afterwards so the state-aware
    /// button styling (accent vs. muted) reflects the new AiEnabled flag without
    /// requiring Setup to be reopened. CameraSlotSettings is a plain POCO without
    /// INotifyPropertyChanged so the DataTrigger needs the explicit refresh.
    /// </summary>
    private void OnCameraAiButton(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn) return;
        if (btn.DataContext is not CameraSlotSettings row) return;

        // Best-effort description shown in the dialog subheader: prefer the
        // operator label, fall back to NVR title, then to a numeric slot id.
        var description = !string.IsNullOrWhiteSpace(row.Label)
            ? row.Label
            : !string.IsNullOrWhiteSpace(row.NvrTitle)
                ? row.NvrTitle
                : $"Slot {row.Slot + 1}";

        var dlg = new CameraAiSettingsDialog(this, row, description);
        if (dlg.ShowDialog() == true)
        {
            // Force the CameraGrid bindings to re-evaluate so the per-row AI
            // button immediately reflects the new state.
            CameraGrid.Items.Refresh();
        }
    }

    // ---- Cameras: sync names from NVR --------------------------------------
    //
    // Connects to each unique NVR referenced by the current camera rows,
    // requests its G2DEVICE_STATUS, and copies the camera-description strings
    // back into the Label column. Custom labels (anything that is not the
    // auto-generated "Camera N" default and not empty) are kept as-is so an
    // admin who has typed a meaningful name doesn't lose it on a sync.

    private bool _cameraSyncRunning;

    private async void OnSyncCameraNames(object sender, RoutedEventArgs e)
    {
        if (_cameraSyncRunning) return;
        var T = Strings.Instance.Current;

        if (_cameras.Count == 0)
        {
            ShowStatus(T.CamerasSyncNamesNoCameras, isError: true);
            return;
        }

        _cameraSyncRunning = true;
        try
        {
            ShowStatus(T.CamerasSyncNamesProgress, isError: false);

            // Group rows per NVR so we open at most one fetch session per
            // recorder, regardless of how many cameras live on it.
            var byNvr = _cameras.GroupBy(c => c.NvrId).ToList();
            int updated = 0;
            var failures = new List<string>();

            foreach (var group in byNvr)
            {
                var nvr = _nvrs.FirstOrDefault(n => n.Id == group.Key);
                if (nvr is null || string.IsNullOrWhiteSpace(nvr.Ip)) continue;

                IReadOnlyDictionary<int, string> nvrNames;
                try
                {
                    if (_cameraNameFetcher is null)
                    {
                        // No live session available (Setup opened in a stand-alone
                        // context). Fall back to a one-shot fetcher; this only
                        // works if no other client is already logged in.
                        var conn = new NvrConnectionInfo(nvr.Ip, nvr.Port, nvr.User, nvr.Password);
                        nvrNames = await NvrCameraNameFetcher
                            .FetchAsync(conn, TimeSpan.FromSeconds(8))
                            .ConfigureAwait(true);
                    }
                    else
                    {
                        // Reuse the running NvrSession's already-authenticated
                        // pipe; no second login, no concurrent-session conflict.
                        nvrNames = await _cameraNameFetcher(nvr.Id, CancellationToken.None)
                            .ConfigureAwait(true);
                    }
                }
                catch (Exception ex)
                {
                    // One NVR failing must not abort the sync for the rest;
                    // collect the error and keep going so the operator gets
                    // titles for whatever recorders are reachable.
                    failures.Add($"{nvr.Name}: {ex.Message}");
                    continue;
                }

                foreach (var cam in group)
                {
                    if (!nvrNames.TryGetValue(cam.CameraIndex, out var nvrName)) continue;

                    // Always overwrite NvrTitle: the NVR is the source of truth
                    // for that field. The operator's Label is independent and
                    // never touched here.
                    if (!string.Equals(cam.NvrTitle, nvrName, StringComparison.Ordinal))
                    {
                        cam.NvrTitle = nvrName;
                        updated++;
                    }
                }
            }

            // Force the NVR-title column to redraw — CameraSlotSettings has no INPC.
            CameraGrid.Items.Refresh();

            string message;
            bool isError;
            if (failures.Count > 0)
            {
                // Partial success: include both the count of updated rows and
                // the list of NVRs that didn't answer. Mark as error so the
                // status line goes red and the operator notices.
                message = string.Format(CultureInfo.CurrentCulture,
                    T.CamerasSyncNamesFailFormat,
                    string.Join("; ", failures))
                    + (updated > 0
                        ? " (" + string.Format(CultureInfo.CurrentCulture, T.CamerasSyncNamesDoneFormat, updated) + ")"
                        : "");
                isError = true;
            }
            else if (updated > 0)
            {
                message = string.Format(CultureInfo.CurrentCulture, T.CamerasSyncNamesDoneFormat, updated);
                isError = false;
            }
            else
            {
                message = T.CamerasSyncNamesNoneApplied;
                isError = false;
            }
            ShowStatus(message, isError);
        }
        finally
        {
            _cameraSyncRunning = false;
        }
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

    private void OnSave(object sender, RoutedEventArgs e) => SaveAndApply(closeAfter: true);

    private void OnApply(object sender, RoutedEventArgs e) => SaveAndApply(closeAfter: false);

    private void SaveAndApply(bool closeAfter)
    {
        // Commit any in-flight NVR-grid edits first; otherwise the latest typed
        // value in Name / Ip / Port etc. wouldn't be observable yet.
        NvrGrid.CommitEdit(DataGridEditingUnit.Row, true);
        // Same goes for the camera grid: a label or NVR-id edit that's still in
        // edit-mode at the moment of click would otherwise be silently dropped.
        CameraGrid.CommitEdit(DataGridEditingUnit.Row, true);

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
                ShowMassUnmaskConfirm = ShowMassUnmaskConfirm.IsChecked == true,
                Mode = ModePrivacyRadio.IsChecked == true ? PrivacyMode.PrivacyDefault : PrivacyMode.OversightDefault,
                PrivacyDefaultRequireAuthOnReveal = PrivacyDefaultRequireAuthOnReveal.IsChecked == true,
                ShowCameraLabel = ShowCameraLabel.IsChecked == true,
                ShowNvrTitle = ShowNvrTitle.IsChecked == true,
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
                    NvrTitle = c.NvrTitle ?? "",
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
            Language = LanguageBox.SelectedValue as string ?? "nl",
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
        // Live-apply via the callback so MainWindow picks up the new settings
        // without waiting for the dialog to close. The owner decides whether
        // the change is structural and prompts the user only when it really is.
        _onApplied?.Invoke();

        if (closeAfter)
        {
            Close();
        }
        else
        {
            // Stay open so the operator can keep tweaking. Reflect the success
            // on the status line and reload the form from the freshly-saved
            // config so any normalisation (trimmed strings, default ports) is
            // visible.
            ShowSuccess(T.SetupChangesApplied);
            Populate();
        }
    }

    private void ShowSuccess(string message)
    {
        StatusLine.Foreground = (System.Windows.Media.Brush)FindResource("green");
        StatusLine.Text = message;
    }

    private void ShowStatus(string message, bool isError)
    {
        if (isError) ShowError(message);
        else ShowSuccess(message);
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
public sealed class StreamChoices : ObservableCollection<StreamChoice>, IDisposable
{
    // Stored as a field (instead of an inline lambda subscribed at construction)
    // so Dispose can unsubscribe. Without that, every SetupWindow open leaked
    // one StreamChoices + its captured 'this' onto the static
    // Strings.Instance.PropertyChanged event, accumulating over the kiosk's
    // weeks-long uptime.
    private readonly System.ComponentModel.PropertyChangedEventHandler _onLanguageChanged;
    private bool _disposed;

    public StreamChoices()
    {
        Add(new StreamChoice { Value = 0, Display = Strings.Instance.Current.StreamHigh });
        Add(new StreamChoice { Value = 1, Display = Strings.Instance.Current.StreamDefault });
        Add(new StreamChoice { Value = 2, Display = Strings.Instance.Current.StreamLow });
        _onLanguageChanged = (_, _) => Refresh();
        Strings.Instance.PropertyChanged += _onLanguageChanged;
    }

    private void Refresh()
    {
        if (Count != 3) return;
        var s = Strings.Instance.Current;
        this[0] = new StreamChoice { Value = 0, Display = s.StreamHigh };
        this[1] = new StreamChoice { Value = 1, Display = s.StreamDefault };
        this[2] = new StreamChoice { Value = 2, Display = s.StreamLow };
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Strings.Instance.PropertyChanged -= _onLanguageChanged;
    }
}

public sealed class StreamChoice
{
    public int Value { get; init; }
    public string Display { get; init; } = "";
}
