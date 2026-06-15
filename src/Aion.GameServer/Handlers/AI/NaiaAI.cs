using System.Collections.Generic;
using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Handler;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Templates.Npcshout;
using Aion.GameServer.Services;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/walkers/NaiaAI (Rolandas).</summary>
[AIName("naia")]
public class NaiaAI : GeneralNpcAI
{
    private bool saidCannon = false;
    private bool saidQydro = false;

    public NaiaAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleMoveArrived()
    {
        MoveEventHandler.OnMoveArrived(this);

        Npc cannon = GetPosition().GetWorldMapInstance().GetNpc(203145);
        Npc qydro = GetPosition().GetWorldMapInstance().GetNpc(203125);
        bool isCannonNear = PositionUtil.IsInRange(GetOwner(), cannon, GetOwner().GetAggroRange());
        bool isQydroNear = PositionUtil.IsInRange(GetOwner(), qydro, GetOwner().GetAggroRange());

        List<NpcShout> shouts = null;
        if (!saidCannon && isCannonNear)
        {
            saidCannon = true;
            // TODO: she should get closer and turn to Cannon
            // getOwner().getPosition().setH((byte)60);
            shouts = DataManager.NPC_SHOUT_DATA.GetNpcShouts(GetPosition().GetMapId(), GetNpcId(), ShoutEventType.WALK_WAYPOINT, "2", 0);
            NpcShoutsService.GetInstance().ShoutRandom(GetOwner(), null, shouts, 10);
        }
        else if (saidCannon && !isCannonNear)
        {
            saidCannon = false;
        }
        if (!saidQydro && isQydroNear)
        {
            saidQydro = true;
            shouts = DataManager.NPC_SHOUT_DATA.GetNpcShouts(GetPosition().GetMapId(), GetNpcId(), ShoutEventType.WALK_WAYPOINT, "1", 0);
            NpcShoutsService.GetInstance().ShoutRandom(GetOwner(), null, shouts, 0);
        }
        else if (saidQydro && !isQydroNear)
        {
            saidQydro = false;
        }
    }
}
