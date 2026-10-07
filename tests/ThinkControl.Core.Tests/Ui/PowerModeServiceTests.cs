using ThinkControl.UI.Services;
using Xunit;

namespace ThinkControl.Core.Tests.Ui;

public sealed class PowerModeServiceTests
{
    private static readonly Guid Efficiency = new("961cc777-2547-4f9d-8174-7d86181b8a7a");
    private static readonly Guid BalancedPlan = new("381b4222-f694-41f0-9685-ff5bb260df2e");

    [Fact]
    public void PersistedPreferenceCannotMaskFailedEffectiveReadback()
    {
        var service = new PowerModeService((_, _) => true, _ => 0,
            () => (true, Guid.Empty), () => BalancedPlan);
        int applied = 0;
        service.ModeApplied += _ => applied++;

        Assert.False(service.SetForSource(ThinkControlPowerMode.Quiet, true, true));
        Assert.Contains("Windows still reports Balanced", service.LastEffectiveError);
        Assert.Equal(0, applied);
    }

    [Fact]
    public void InactiveSourceCanBeConfiguredWithoutChangingEffectiveMode()
    {
        bool? configuredSource = null;
        var service = new PowerModeService((_, battery) => { configuredSource = battery; return true; },
            _ => throw new InvalidOperationException("Must not write the active overlay"),
            () => (true, Guid.Empty), () => BalancedPlan);

        Assert.True(service.SetForSource(ThinkControlPowerMode.Quiet, true, false));
        Assert.Equal(true, configuredSource);
    }

    [Fact]
    public void EffectiveSuccessDoesNotHideFailedSourcePersistence()
    {
        var service = new PowerModeService((_, _) => false,
            _ => throw new InvalidOperationException("Do not apply an unsaved source preference"),
            () => (true, Efficiency), () => BalancedPlan);
        Assert.False(service.SetForSource(ThinkControlPowerMode.Quiet, true, true));
        Assert.Contains("could not save", service.LastEffectiveError);
    }

    [Theory]
    [InlineData("8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c", "High performance")]
    [InlineData("a1841308-3541-4fab-bc81-f71556f20b4a", "Power saver")]
    public void IncompatiblePlanIsRejectedBeforeOverlayWrite(string plan, string name)
    {
        var service = new PowerModeService((_, _) => true,
            _ => throw new InvalidOperationException("Preflight must prevent writes"),
            () => (true, Guid.Empty), () => new Guid(plan));

        Assert.False(service.SetEffective(ThinkControlPowerMode.Quiet));
        Assert.Contains(name, service.LastEffectiveError);
        Assert.Contains("Balanced power plan", service.LastEffectiveError);
    }

    [Fact]
    public void UnknownEffectiveOverlayIsNotReportedAsBalanced()
    {
        var service = new PowerModeService((_, _) => true, _ => 0,
            () => (true, new Guid("00000000-0000-0000-0000-000000000123")), () => BalancedPlan);
        Assert.Null(service.GetCurrent(true));
        Assert.False(service.SetEffective(ThinkControlPowerMode.Quiet));
        Assert.Contains("unknown power overlay", service.LastEffectiveError);
    }

    [Fact]
    public void ModePreparesBalancedAndRestoresOriginalPlanOnce()
    {
        Guid original = new("8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c");
        Guid plan = original;
        int writes = 0;
        var service = new PowerModeService((_, _) => true, _ => 0,
            () => (true, Efficiency), () => plan,
            value => { plan = value; writes++; return 0; });
        Assert.True(service.SetEffective(ThinkControlPowerMode.Quiet, prepareBalancedPlan: true));
        Assert.Equal(BalancedPlan, plan);
        Assert.True(service.SetEffective(ThinkControlPowerMode.Quiet, prepareBalancedPlan: true));
        Assert.True(service.RestoreModePlan());
        Assert.Equal(original, plan);
        Assert.True(service.RestoreModePlan());
        Assert.Equal(2, writes);
    }

    [Fact]
    public void FailedOverlayRestoresOriginalPlanAndDoesNotAcquireLease()
    {
        Guid original = new("a1841308-3541-4fab-bc81-f71556f20b4a");
        Guid plan = original;
        var service = new PowerModeService((_, _) => true, _ => 5,
            () => (true, Guid.Empty), () => plan,
            value => { plan = value; return 0; });
        Assert.False(service.SetEffective(ThinkControlPowerMode.Quiet, prepareBalancedPlan: true));
        Assert.Equal(original, plan);
        Assert.False(service.HasModePlan);
    }

    [Fact]
    public void ModeExitPreservesAnExternallySelectedPlan()
    {
        Guid plan = new("8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c");
        var service = new PowerModeService((_, _) => true, _ => 0,
            () => (true, Efficiency), () => plan,
            value => { plan = value; return 0; });
        Assert.True(service.SetEffective(ThinkControlPowerMode.Quiet, prepareBalancedPlan: true));
        Guid external = Guid.NewGuid();
        plan = external;
        Assert.True(service.RestoreModePlan());
        Assert.Equal(external, plan);
    }

    [Fact]
    public void BalancedModeExitRestoresActualOverlayInsteadOfSavedPreference()
    {
        Guid original = new("ded574b5-45a0-4f42-8737-46345c09c238");
        Guid overlay = original;
        var service = new PowerModeService((_, _) => true,
            value => { overlay = value; return 0; }, () => (true, overlay), () => BalancedPlan,
            _ => throw new InvalidOperationException("Balanced must not be rewritten"));
        Assert.True(service.SetEffective(ThinkControlPowerMode.Quiet, prepareBalancedPlan: true));
        Assert.True(service.SetEffective(ThinkControlPowerMode.Balanced, prepareBalancedPlan: true));
        Assert.True(service.RestoreModePlan());
        Assert.Equal(original, overlay);
        Assert.False(service.HasModePlan);
    }

    [Fact]
    public void FailedBalancedRestoreKeepsBaselineForRetry()
    {
        Guid overlay = Guid.Empty;
        bool reject = false;
        var service = new PowerModeService((_, _) => true,
            value => { if (reject) return 5; overlay = value; return 0; },
            () => (true, overlay), () => BalancedPlan);
        Assert.True(service.SetEffective(ThinkControlPowerMode.Quiet, prepareBalancedPlan: true));
        reject = true;
        Assert.False(service.RestoreModePlan());
        Assert.True(service.HasModePlan);
        reject = false;
        Assert.True(service.RestoreModePlan());
        Assert.Equal(Guid.Empty, overlay);
    }

    [Fact]
    public void ConfirmedEffectiveWritePublishesExactlyOneApplication()
    {
        var service = new PowerModeService((_, _) => true, _ => 0,
            () => (true, Efficiency), () => BalancedPlan);
        int applied = 0;
        service.ModeApplied += _ => applied++;
        Assert.True(service.SetForSource(ThinkControlPowerMode.Quiet, true, true));
        Assert.Equal(1, applied);
        Assert.Null(service.LastEffectiveError);
    }
}
