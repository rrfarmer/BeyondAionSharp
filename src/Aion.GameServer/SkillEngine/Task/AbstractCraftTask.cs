using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.SkillEngine.Task;

/// <summary>Java parity: skillengine/task/AbstractCraftTask (ATracer, synchro2).</summary>
public abstract class AbstractCraftTask : AbstractInteractionTask
{
    protected const int fullBarValue = 1000;
    protected int currentSuccessValue;
    protected int currentFailureValue;
    protected int skillLvlDiff;
    protected CraftType craftType = CraftType.NORMAL;

    /// <summary>Java parity: protected enum CraftType (per-instance progressId) → protected class-enum.</summary>
    protected sealed class CraftType
    {
        public static readonly CraftType NORMAL = new CraftType(1);
        public static readonly CraftType CRIT_BLUE = new CraftType(2);
        public static readonly CraftType CRIT_PURPLE = new CraftType(3);

        private readonly int progressId;

        private CraftType(int progressId)
        {
            this.progressId = progressId;
        }

        public int GetProgressId()
        {
            return progressId;
        }
    }

    public AbstractCraftTask(Aion.GameServer.Model.GameObjects.Players.Player requester, VisibleObject responder, int skillLvlDiff)
        : base(requester, responder)
    {
        this.skillLvlDiff = skillLvlDiff;
    }

    protected override bool OnInteraction()
    {
        if (currentSuccessValue == fullBarValue)
        {
            return OnSuccessFinish();
        }
        if (currentFailureValue == fullBarValue)
        {
            OnFailureFinish();
            return true;
        }

        AnalyzeInteraction();

        SendInteractionUpdate();
        return false;
    }

    /// <summary>Perform interaction calculation.</summary>
    protected abstract void AnalyzeInteraction();

    protected abstract void SendInteractionUpdate();

    protected abstract bool OnSuccessFinish();

    protected abstract void OnFailureFinish();
}
