using InteractiveMask.Ipc;

namespace InteractiveMask.WebHost;

/// <summary>
/// Singleton in-memory mirror of what the desktop Display.exe is currently
/// rendering. Populated by <see cref="IpcMirrorService"/> from the IPC stream
/// and read by Razor Pages / the /api/state endpoint.
/// </summary>
public sealed class StateMirror
{
    private readonly object _lock = new();
    private int _rows;
    private int _columns;
    private bool _ipcConnected;
    private readonly Dictionary<int, TileStateDto> _tiles = new();

    public void ApplyHello(HelloDto hello)
    {
        lock (_lock)
        {
            _rows = hello.Rows;
            _columns = hello.Columns;
            _tiles.Clear();
            foreach (var tile in hello.Tiles)
            {
                _tiles[tile.Slot] = tile;
            }
        }
    }

    public void ApplyTileUpdate(TileStateDto tile)
    {
        lock (_lock)
        {
            _tiles[tile.Slot] = tile;
        }
    }

    public void SetConnected(bool connected)
    {
        lock (_lock) _ipcConnected = connected;
    }

    public StateSnapshot Snapshot()
    {
        lock (_lock)
        {
            // Tiles ordered by slot so the grid layout is deterministic.
            return new StateSnapshot(
                IpcConnected: _ipcConnected,
                Rows: _rows,
                Columns: _columns,
                Tiles: _tiles.Values.OrderBy(t => t.Slot).ToList());
        }
    }
}

public sealed record StateSnapshot(
    bool IpcConnected,
    int Rows,
    int Columns,
    List<TileStateDto> Tiles);
