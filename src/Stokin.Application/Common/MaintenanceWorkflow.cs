namespace Stokin.Application.Common;

public static class MaintenanceWorkflow
{
    public static async Task RunAsync(Func<Task> restore, Func<Task> verify, Func<Task> release, Func<Task> retain)
    {
        try { await restore(); await verify(); await release(); }
        catch { try { await retain(); } catch { } throw; }
    }
}
