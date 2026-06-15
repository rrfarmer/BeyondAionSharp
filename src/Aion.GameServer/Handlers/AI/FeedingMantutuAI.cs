using Aion.GameServer.Ai;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.World;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/rakes/FeedingMantutuAI (xTz).</summary>
[AIName("feeding_mantutu")]
public class FeedingMantutuAI : ShifterAI
{
    public FeedingMantutuAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleDialogStart(Player player)
    {
        WorldMapInstance instance = GetPosition().GetWorldMapInstance();
        if (instance.GetNpc(281128) == null && instance.GetNpc(281129) == null)
        {
            base.HandleDialogStart(player);
        }
    }

    protected override void HandleUseItemFinish(Player player)
    {
        base.HandleUseItemFinish(player);
        Npc boss = GetPosition().GetWorldMapInstance().GetNpc(219033);
        if (boss != null && boss.IsSpawned() && !boss.IsDead())
        {
            Npc npc = null;
            switch (GetNpcId())
            {
                case 701387: // water supply
                    npc = (Npc)Spawn(281129, 712.042f, 490.5559f, 939.7027f, (sbyte)0);
                    break;
                case 701386: // feed supply
                    npc = (Npc)Spawn(281128, 714.62634f, 504.4552f, 939.60675f, (sbyte)0);
                    break;
            }

            boss.GetAi().OnCustomEvent(1, npc);
            AIActions.DeleteOwner(this);
        }
    }
}
