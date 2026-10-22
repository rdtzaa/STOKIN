using Stokin.Application.Common;
using Stokin.Testing;
namespace Stokin.Domain.Tests;

public class RecoveryTests
{
    [Test] public void Overflow_is_definite_failure_so_editor_can_be_corrected() => Assert.True(PostingFailure.IsDefinite(new OverflowException()));
    [Test] public void Transport_errors_remain_unknown() => Assert.True(!PostingFailure.IsDefinite(new IOException()));
    [Test]
    public async Task Restore_validation_failure_keeps_access_closed()
    {
        bool canWrite = false, retained = false;
        await Assert.ThrowsAsync<InvalidOperationException>(() => MaintenanceWorkflow.RunAsync(() => Task.CompletedTask, () => throw new InvalidOperationException("reconciliation mismatch"), () => { canWrite = true; return Task.CompletedTask; }, () => { retained = true; canWrite = false; return Task.CompletedTask; }));
        Assert.True(!canWrite); Assert.True(retained);
    }
    [Test]
    public async Task Restore_reopens_only_after_verification()
    {
        bool restored = false, verified = false, open = false;
        await MaintenanceWorkflow.RunAsync(() => { restored = true; return Task.CompletedTask; }, () => { Assert.True(restored); verified = true; return Task.CompletedTask; }, () => { Assert.True(verified); open = true; return Task.CompletedTask; }, () => { open = false; return Task.CompletedTask; }); Assert.True(open);
    }
}
