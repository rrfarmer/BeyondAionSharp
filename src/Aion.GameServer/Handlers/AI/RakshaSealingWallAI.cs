using Aion.GameServer.Ai;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Utils;
using Aion.GameServer.World;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/raksang/RakshaSealingWallAI (xTz).</summary>
[AIName("raksha_sealing_wall")]
public class RakshaSealingWallAI : GeneralNpcAI
{
    private AtomicBoolean startedEvent = new AtomicBoolean(false);

    public RakshaSealingWallAI(Npc owner)
        : base(owner)
    {
    }

    public override bool CanThink()
    {
        return false;
    }

    protected override void HandleCreatureMoved(Creature creature)
    {
        if (creature is Player)
        {
            Player player = (Player)creature;
            if (PositionUtil.GetDistance(GetOwner(), player) <= 35)
            {
                if (startedEvent.CompareAndSet(false, true))
                {
                    WorldMapInstance instance = GetPosition().GetWorldMapInstance();
                    Npc sharik = instance.GetNpc(217425);
                    Npc flamelord = instance.GetNpc(217451);
                    Npc sealguard = instance.GetNpc(217456);
                    int bossId;
                    if ((sharik == null || sharik.IsDead()) && (flamelord == null || flamelord.IsDead()) && (sealguard == null || sealguard.IsDead()))
                    {
                        bossId = 217475;
                    }
                    else
                    {
                        bossId = 217647;
                    }
                    Spawn(bossId, 1063.08f, 903.13f, 138.744f, (sbyte)29);
                    AIActions.DeleteOwner(this);
                }
            }
        }
    }
}
