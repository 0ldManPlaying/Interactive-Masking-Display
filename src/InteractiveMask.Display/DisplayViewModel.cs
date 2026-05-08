using InteractiveMask.Gdk;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Threading;

namespace InteractiveMask.Display;

public sealed class DisplayViewModel : INotifyPropertyChanged
{
    private string _statusLine = "";
    private int _rows;
    private int _columns;
    private bool _connectionLost;
    private string _connectionBanner = "";

    public ObservableCollection<TileViewModel> Tiles { get; } = new();

    public int Rows
    {
        get => _rows;
        private set => Set(ref _rows, value);
    }

    public int Columns
    {
        get => _columns;
        private set => Set(ref _columns, value);
    }

    public string StatusLine
    {
        get => _statusLine;
        set => Set(ref _statusLine, value);
    }

    /// <summary>True when the NVR session is not currently connected. Drives the
    /// visibility of the reconnect banner.</summary>
    public bool ConnectionLost
    {
        get => _connectionLost;
        set => Set(ref _connectionLost, value);
    }

    /// <summary>Localized banner text shown while disconnected, e.g. "NVR niet
    /// bereikbaar — opnieuw verbinden over 4 s...". Updated every tick by
    /// MainWindow so the countdown decreases visibly.</summary>
    public string ConnectionBanner
    {
        get => _connectionBanner;
        set => Set(ref _connectionBanner, value);
    }

    /// <summary>Build the tile collection for a given grid size, all initially empty.
    /// Disposes any previous tiles so they unsubscribe from <see cref="Strings.Instance"/>
    /// and don't pin the old view-models in memory after a grid resize.</summary>
    public void InitializeGrid(int rows, int columns, Dispatcher dispatcher)
    {
        foreach (var old in Tiles) old.Dispose();
        Rows = rows;
        Columns = columns;
        Tiles.Clear();
        int total = rows * columns;
        for (int i = 0; i < total; i++)
        {
            Tiles.Add(new TileViewModel(i, dispatcher));
        }
    }

    /// <summary>Bind a previously-registered <see cref="CameraTile"/> to a slot in the grid.</summary>
    public void BindCameraToSlot(int slotIndex, CameraTile camera)
    {
        if (slotIndex < 0 || slotIndex >= Tiles.Count)
            throw new ArgumentOutOfRangeException(nameof(slotIndex));
        Tiles[slotIndex].Attach(camera);
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        return true;
    }
}
