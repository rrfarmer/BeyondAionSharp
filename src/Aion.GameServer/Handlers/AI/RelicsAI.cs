using Aion.GameServer.Ai;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Instance.Instancescore;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/pvpArenas/RelicsAI (@author xTz).</summary>
[AIName("pvparenarelics")]
public class RelicsAI : ActionItemNpcAI
{
    private bool isRewarded;

    public RelicsAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleDialogStart(Player player)
    {
        InstanceScore instance = GetPosition().GetWorldMapInstance().GetInstanceHandler().GetInstanceScore();
        if (instance != null && !instance.IsStartProgress())
        {
            return;
        }
        base.HandleDialogStart(player);
    }

    protected override void HandleUseItemFinish(Player player)
    {
        if (!isRewarded)
        {
            isRewarded = true;
            AIActions.HandleUseItemFinish(this, player);
            int npcId = GetNpcId();
            if (npcId != 701187 && npcId != 701188)
            {
                AIActions.ScheduleRespawn(this);
            }
            AIActions.DeleteOwner(this);
        }
    }
}
