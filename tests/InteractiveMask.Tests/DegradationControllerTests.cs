using InteractiveMask.Detection;
using Xunit;

namespace InteractiveMask.Tests;

public sealed class DegradationControllerTests
{
    [Fact]
    public void RecordLatency_escalates_frame_skip_after_sustained_over_budget_samples()
    {
        var controller = new DegradationController();
        controller.SetEligibleStreams(new[] { 0, 1, 2 });

        DegradationStateChange? lastChange = null;
        controller.StateChanged += (_, change) => lastChange = change;

        for (int i = 0; i < 10; i++)
        {
            controller.RecordLatency(100);
        }

        Assert.Equal(DegradationState.FrameSkip, controller.State);
        Assert.Equal(2, controller.FrameSkipMultiplier);
        Assert.NotNull(lastChange);
        Assert.Equal(DegradationDirection.Escalate, lastChange!.Direction);
    }

    [Fact]
    public void RecordLatency_emits_recover_transition_after_sustained_in_budget_window()
    {
        var controller = new DegradationController();
        controller.SetEligibleStreams(new[] { 0, 1 });

        DegradationStateChange? lastChange = null;
        controller.StateChanged += (_, change) => lastChange = change;

        for (int i = 0; i < 10; i++)
        {
            controller.RecordLatency(100);
        }

        Assert.Equal(DegradationState.FrameSkip, controller.State);
        Assert.True(controller.FrameSkipMultiplier > 1);

        for (int i = 0; i < 120; i++)
        {
            controller.RecordLatency(25);
        }

        Assert.NotNull(lastChange);
        Assert.Equal(DegradationDirection.Recover, lastChange!.Direction);
        Assert.True(controller.FrameSkipMultiplier < lastChange.FromMultiplier);
    }

    [Fact]
    public void RecordLatency_walks_to_per_camera_disable_when_frame_skip_maxed()
    {
        var controller = new DegradationController();
        controller.SetEligibleStreams(new[] { 0, 1, 2 });

        // FrameSkip: 1->2->4->8, then PerCameraDisable on the fifth escalation.
        for (int step = 0; step < 4; step++)
        {
            for (int i = 0; i < 10; i++)
            {
                controller.RecordLatency(100);
            }
        }

        Assert.Equal(DegradationState.PerCameraDisable, controller.State);
        Assert.Single(controller.DisabledStreams);
        Assert.Contains(2, controller.DisabledStreams);
    }
}
