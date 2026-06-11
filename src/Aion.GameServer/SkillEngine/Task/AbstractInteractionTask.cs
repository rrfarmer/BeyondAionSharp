using System;
using System.Threading.Tasks;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Utils;

namespace Aion.GameServer.SkillEngine.Task;

/// <summary>Java parity: skillengine/task/AbstractInteractionTask (ATracer).</summary>
public abstract class AbstractInteractionTask
{
    private ScheduledTask task;
    protected int interval = 2500;
    protected int delay = 1000;

    protected readonly Aion.GameServer.Model.GameObjects.Players.Player requester;
    protected readonly VisibleObject responder;

    public AbstractInteractionTask(Aion.GameServer.Model.GameObjects.Players.Player requester, VisibleObject responder)
    {
        this.requester = requester;
        if (responder == null)
            this.responder = requester;
        else
            this.responder = responder;
    }

    /// <summary>Called on each interaction.</summary>
    protected abstract bool OnInteraction();

    /// <summary>Called when interaction is complete.</summary>
    protected abstract void OnInteractionFinish();

    /// <summary>Called before interaction is started.</summary>
    protected abstract void OnInteractionStart();

    /// <summary>Called when interaction is not complete and needs to be aborted.</summary>
    protected abstract void OnInteractionAbort();

    /// <summary>Interaction scheduling method.</summary>
    public void Start()
    {
        OnInteractionStart();

        task = ThreadPoolManager.GetInstance().ScheduleAtFixedRateTask(ct =>
        {
            bool stopTask = !requester.IsOnline() || OnInteraction();
            if (stopTask)
                Stop();
            return ValueTask.CompletedTask;
        }, TimeSpan.FromMilliseconds(delay), TimeSpan.FromMilliseconds(interval));
    }

    /// <summary>Stop current interaction.</summary>
    public void Stop()
    {
        OnInteractionFinish();

        if (task != null && !task.IsCancelled)
        {
            task.Cancel();
            task = null;
        }
    }

    /// <summary>Abort current interaction.</summary>
    public void Abort()
    {
        OnInteractionAbort();
        Stop();
    }

    public bool IsInProgress()
    {
        return task != null && !task.IsCancelled;
    }

    public void SetInterval(int interval)
    {
        this.interval = interval;
    }
}
