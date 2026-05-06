namespace InteractiveMask.Gdk;

public enum TileStatus
{
    /// <summary>No camera bound to this tile.</summary>
    Empty,
    /// <summary>Camera bound; waiting for the NVR connection or first frame.</summary>
    Pending,
    /// <summary>Receiving frames.</summary>
    Live,
    /// <summary>Camera reports video loss (signal lost upstream of the NVR).</summary>
    VideoLoss,
    /// <summary>NVR connection is down or the camera is unreachable; auto-retry pending.</summary>
    Disconnected,
    /// <summary>Decode pipeline failure; surfaces in the UI for diagnostics.</summary>
    Error,
}
