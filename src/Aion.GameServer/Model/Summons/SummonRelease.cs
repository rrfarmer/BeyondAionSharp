using Aion.GameServer.Utils;

namespace Aion.GameServer.Model.Summons;

/// <summary>
/// Java parity: model/summons/SummonRelease. Scheduled or already running release of a summon, see
/// Summon.RegisterRelease(SummonRelease).
/// </summary>
public class SummonRelease
{
    private readonly UnsummonType unsummonType;
    private ScheduledTask task;
    private bool started;

    public SummonRelease(UnsummonType unsummonType)
    {
        this.unsummonType = unsummonType;
    }

    public UnsummonType GetUnsummonType()
    {
        return unsummonType;
    }

    public void SetTask(ScheduledTask task)
    {
        this.task = task;
    }

    public void MarkStarted()
    {
        started = true;
    }

    public bool HasStarted()
    {
        return started;
    }

    public bool IsCancelableByMaster()
    {
        return !started && unsummonType.IsCancelableByMaster();
    }

    public bool Cancel()
    {
        if (started)
            return false;
        return task == null || task.Cancel(false);
    }
}
