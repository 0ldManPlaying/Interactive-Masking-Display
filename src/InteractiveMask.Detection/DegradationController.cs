using System;
using System.Collections.Generic;

namespace InteractiveMask.Detection;

/// <summary>
/// Adaptive load manager for the v2.0 AI pipeline (roadmap F3). Watches the rolling
/// p95 inference latency reported by each completed detection and walks a small
/// degradation ladder when the detector is consistently behind its budget. Steps
/// recover (in reverse order) when latency consistently drops below the restore
/// threshold. Hysteresis - separate degrade and restore thresholds, plus N consecutive
/// samples required to step in either direction - prevents flapping on borderline
/// loads.
///
/// <para>Implemented in v2.0.x F3 (subset of architecture-v2-ai §8):</para>
/// <list type="number">
///   <item><b>FrameSkip</b>: each tile's submission cadence doubles per step
///   (every 3rd → 6th → 12th frame). Tiles read the multiplier from
///   <see cref="FrameSkipMultiplier"/> on every OnFrameDecoded.</item>
///   <item><b>PerCameraDisable</b>: AI is turned off on individual cameras in
///   priority order (highest stream ID first). The render pipeline on those
///   tiles falls back to live image without overlay.</item>
///   <item><b>GlobalFallback</b>: AI is disabled on every camera. Tile keeps
///   the live image; manual full-tile blur from v1.x is unaffected.</item>
/// </list>
///
/// <para>Out of scope for F3 (deferred):</para>
/// <list type="bullet">
///   <item>Motion-gating (architecture §8 step 2).</item>
///   <item>Inference-resolution down-scaling (step 3) - YOLO26s-seg input
///   is baked at 640 by the model export.</item>
///   <item>Polygon → bbox mask-type degrade (step 4) - cosmetic.</item>
/// </list>
///
/// Thread-safety: all mutable state is guarded by a single lock. The coordinator
/// worker feeds <see cref="RecordLatency"/>; the UI thread reads
/// <see cref="State"/> / <see cref="FrameSkipMultiplier"/> / <see cref="DisabledStreams"/>.
/// <see cref="StateChanged"/> fires from the thread that observed the transition;
/// subscribers must marshal to the UI thread themselves if they touch WPF state.
/// </summary>
public sealed class DegradationController
{
    // ---------------- Tunables -----------------------------------------------
    // Picked to stay quiet on normal load and react within ~3 s on real overload
    // assuming ~10 Hz combined inference cadence across the active streams.

    /// <summary>p95 inference latency that triggers a degrade step (ms).</summary>
    private const double DegradeThresholdMs = 80.0;

    /// <summary>p95 inference latency that allows a restore step (ms). Below
    /// the degrade threshold; the gap is the hysteresis band.</summary>
    private const double RestoreThresholdMs = 40.0;

    /// <summary>Consecutive over-budget samples required to escalate one step.</summary>
    private const int ConsecutiveSamplesToDegrade = 10;

    /// <summary>Consecutive in-budget samples required to recover one step.</summary>
    private const int ConsecutiveSamplesToRestore = 30;

    /// <summary>Rolling window size used to compute p95.</summary>
    private const int RollingWindowSize = 30;

    /// <summary>FrameSkip multiplier ceiling before we move to PerCameraDisable.</summary>
    private const int MaxFrameSkipMultiplier = 8;

    // ---------------- State (all guarded by _lock) ---------------------------
    private readonly object _lock = new();
    private readonly Queue<double> _latencySamples = new(RollingWindowSize);
    private int _consecutiveOverBudget;
    private int _consecutiveInBudget;

    private DegradationState _state = DegradationState.Normal;
    private int _frameSkipMultiplier = 1;
    private HashSet<int> _disabledStreams = new();
    private HashSet<int> _eligibleStreams = new();

    /// <summary>Raised on every transition. Fires from the calling thread of
    /// the trigger (<see cref="RecordLatency"/> or <see cref="SetEligibleStreams"/>);
    /// subscribers must marshal to the UI thread themselves.</summary>
    public event EventHandler<DegradationStateChange>? StateChanged;

    /// <summary>Current high-level state. Reads take the lock briefly for
    /// consistency with <see cref="FrameSkipMultiplier"/> and
    /// <see cref="DisabledStreams"/>.</summary>
    public DegradationState State { get { lock (_lock) return _state; } }

    /// <summary>How many frames the per-tile submission path should skip
    /// between inference calls. 1 = use the configured per-camera cadence
    /// unchanged, 2 = halve it, etc. Read once per OnFrameDecoded.</summary>
    public int FrameSkipMultiplier { get { lock (_lock) return _frameSkipMultiplier; } }

    /// <summary>Snapshot of stream IDs the controller has currently taken offline
    /// (in addition to whatever the operator switched off in Setup). The snapshot
    /// is replaced wholesale on each transition; callers cache the reference for
    /// the duration of one OnFrameDecoded so a mid-call mutation can't tear.</summary>
    public IReadOnlySet<int> DisabledStreams { get { lock (_lock) return _disabledStreams; } }

    // ---------------- Inputs --------------------------------------------------

    /// <summary>
    /// Feed one completed inference's latency to the controller. Cheap (~µs)
    /// and called from the InferenceCoordinator worker after each detection.
    /// Drives the rolling p95 + the consecutive-sample counters that decide
    /// when to step in either direction.
    /// </summary>
    public void RecordLatency(double latencyMs)
    {
        DegradationStateChange? change = null;
        lock (_lock)
        {
            if (_latencySamples.Count >= RollingWindowSize) _latencySamples.Dequeue();
            _latencySamples.Enqueue(latencyMs);

            double p95 = ComputeP95Locked();

            // Hysteresis: a sample in the band (RestoreThreshold < p95 <
            // DegradeThreshold) resets both counters - "OK but not great",
            // neither escalate nor recover.
            if (p95 >= DegradeThresholdMs)
            {
                _consecutiveOverBudget++;
                _consecutiveInBudget = 0;
            }
            else if (p95 <= RestoreThresholdMs)
            {
                _consecutiveInBudget++;
                _consecutiveOverBudget = 0;
            }
            else
            {
                _consecutiveOverBudget = 0;
                _consecutiveInBudget = 0;
            }

            // At most one transition per RecordLatency call so each step gets
            // its own audit-log entry and the UI badge gets time to render.
            if (_consecutiveOverBudget >= ConsecutiveSamplesToDegrade)
            {
                change = EscalateLocked(p95);
                _consecutiveOverBudget = 0;
            }
            else if (_consecutiveInBudget >= ConsecutiveSamplesToRestore
                     && _state != DegradationState.Normal)
            {
                change = RecoverLocked(p95);
                _consecutiveInBudget = 0;
            }
        }

        // Raise outside the lock so a handler that calls back into the
        // controller can't deadlock.
        if (change is not null) StateChanged?.Invoke(this, change);
    }

    /// <summary>
    /// Register the set of streams that are currently AI-eligible (i.e. the
    /// operator has AI=on in Setup for those cameras). The controller picks
    /// disable victims from this set in priority order. Display calls this
    /// whenever the per-camera AI configuration changes via Setup → Apply.
    /// </summary>
    public void SetEligibleStreams(IReadOnlyCollection<int> streamIds)
    {
        lock (_lock)
        {
            _eligibleStreams = new HashSet<int>(streamIds);
            // If the operator just turned AI off on a stream that the
            // controller had auto-disabled, drop it from our set too so the
            // bookkeeping stays consistent. Doesn't change the state.
            var trimmed = new HashSet<int>();
            foreach (var s in _disabledStreams)
                if (_eligibleStreams.Contains(s)) trimmed.Add(s);
            if (trimmed.Count != _disabledStreams.Count) _disabledStreams = trimmed;
        }
    }

    // ---------------- State transitions --------------------------------------

    private DegradationStateChange EscalateLocked(double p95)
    {
        var prevState = _state;
        var prevMult = _frameSkipMultiplier;

        // Step 1: FrameSkip - double the multiplier until we hit the ceiling.
        if (_frameSkipMultiplier < MaxFrameSkipMultiplier)
        {
            _frameSkipMultiplier *= 2;
            _state = DegradationState.FrameSkip;
        }
        // Step 5: PerCameraDisable - take one more stream offline. Highest
        // stream ID first; matches the "last tiles in the grid usually less
        // important" heuristic. Operator can re-order via Setup.
        else if (TryDisableOneMoreStreamLocked())
        {
            _state = DegradationState.PerCameraDisable;
        }
        // Step 6: GlobalFallback - everything off. Tiles continue to show
        // live image; manual full-tile blur still works.
        else
        {
            _disabledStreams = new HashSet<int>(_eligibleStreams);
            _state = DegradationState.GlobalFallback;
        }

        return new DegradationStateChange(
            FromState: prevState, ToState: _state,
            FromMultiplier: prevMult, ToMultiplier: _frameSkipMultiplier,
            DisabledStreams: _disabledStreams,
            P95LatencyMs: p95,
            Direction: DegradationDirection.Escalate);
    }

    private bool TryDisableOneMoreStreamLocked()
    {
        // Need at least two enabled streams; with one left a further disable
        // would be equivalent to GlobalFallback and the caller handles that.
        int stillEnabled = 0;
        int highestEnabled = int.MinValue;
        foreach (var s in _eligibleStreams)
        {
            if (_disabledStreams.Contains(s)) continue;
            stillEnabled++;
            if (s > highestEnabled) highestEnabled = s;
        }
        if (stillEnabled <= 1) return false;

        var next = new HashSet<int>(_disabledStreams) { highestEnabled };
        _disabledStreams = next;
        return true;
    }

    private DegradationStateChange RecoverLocked(double p95)
    {
        var prevState = _state;
        var prevMult = _frameSkipMultiplier;

        if (_state == DegradationState.GlobalFallback)
        {
            // Re-enable the lowest-ID disabled stream first.
            ReEnableOneStreamLocked();
            _state = _disabledStreams.Count == 0
                ? DegradationState.FrameSkip
                : DegradationState.PerCameraDisable;
        }
        else if (_state == DegradationState.PerCameraDisable)
        {
            ReEnableOneStreamLocked();
            if (_disabledStreams.Count == 0) _state = DegradationState.FrameSkip;
        }
        else if (_state == DegradationState.FrameSkip)
        {
            _frameSkipMultiplier = Math.Max(1, _frameSkipMultiplier / 2);
            if (_frameSkipMultiplier == 1) _state = DegradationState.Normal;
        }

        return new DegradationStateChange(
            FromState: prevState, ToState: _state,
            FromMultiplier: prevMult, ToMultiplier: _frameSkipMultiplier,
            DisabledStreams: _disabledStreams,
            P95LatencyMs: p95,
            Direction: DegradationDirection.Recover);
    }

    private void ReEnableOneStreamLocked()
    {
        int? recovered = null;
        foreach (var s in _disabledStreams)
            if (recovered is null || s < recovered.Value) recovered = s;
        if (recovered is null) return;

        var next = new HashSet<int>(_disabledStreams);
        next.Remove(recovered.Value);
        _disabledStreams = next;
    }

    private double ComputeP95Locked()
    {
        if (_latencySamples.Count == 0) return 0.0;
        // Tiny window (30) - allocation here is fine; method runs at most
        // once per inference (~10-30 Hz total).
        var arr = _latencySamples.ToArray();
        Array.Sort(arr);
        int idx = (int)Math.Ceiling(0.95 * (arr.Length - 1));
        return arr[idx];
    }
}

/// <summary>Coarse degradation ladder step. Maps to the audit-event detail field.</summary>
public enum DegradationState
{
    /// <summary>No degradation active; every eligible stream at full cadence.</summary>
    Normal = 0,
    /// <summary>Frame-skip multiplier raised; per-tile cadence reduced.</summary>
    FrameSkip = 1,
    /// <summary>One or more streams have AI disabled to relieve load.</summary>
    PerCameraDisable = 2,
    /// <summary>All AI disabled; tiles show live image without overlay.</summary>
    GlobalFallback = 3,
}

/// <summary>Direction of a single state transition.</summary>
public enum DegradationDirection { Escalate, Recover }

/// <summary>
/// Snapshot of a single degradation transition. Passed to subscribers of
/// <see cref="DegradationController.StateChanged"/>; carries before/after
/// state plus the p95 reading that triggered the change so the audit trail
/// records why the kiosk dropped (or recovered) a step.
/// </summary>
public sealed record DegradationStateChange(
    DegradationState FromState,
    DegradationState ToState,
    int FromMultiplier,
    int ToMultiplier,
    IReadOnlySet<int> DisabledStreams,
    double P95LatencyMs,
    DegradationDirection Direction);
