using System;
using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services.Panesterra.Ahserion;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/worlds/panesterra/ahserionsflight/AdvanceCorridorShield (@author Yeats).</summary>
[AIName("ahserion_advance_corridor_shield")]
public class AdvanceCorridorShield : AhserionConstructAI
{
    private readonly AtomicBoolean canShout = new AtomicBoolean(true);

    public AdvanceCorridorShield(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleAttack(Creature creature)
    {
        base.HandleAttack(creature);
        BroadcastAttack();
    }

    private void BroadcastAttack()
    {
        if (canShout.CompareAndSet(true, false))
        {
            if (!IsDead() && GetOwner().GetWorldId() == 400030000)
            {
                switch (GetNpcId())
                {
                    case 297306: SendPacket(SM_SYSTEM_MESSAGE.STR_MSG_GAB1_SUB_PORTAL_DEST_69_ATTACKED()); break;
                    case 297307: SendPacket(SM_SYSTEM_MESSAGE.STR_MSG_GAB1_SUB_PORTAL_DEST_70_ATTACKED()); break;
                    case 297308: SendPacket(SM_SYSTEM_MESSAGE.STR_MSG_GAB1_SUB_PORTAL_DEST_71_ATTACKED()); break;
                    case 297309: SendPacket(SM_SYSTEM_MESSAGE.STR_MSG_GAB1_SUB_PORTAL_DEST_72_ATTACKED()); break;
                }
                ThreadPoolManager.GetInstance().Schedule(_ => { AllowShout(); return ValueTask.CompletedTask; }, 3000L);
            }
        }
    }

    private void AllowShout()
    {
        if (!IsDead())
            canShout.Set(true);
    }

    private void SendPacket(SM_SYSTEM_MESSAGE msg)
    {
        World.World.GetInstance().GetWorldMap(400030000).GetMainWorldMapInstance().ForEachPlayer(p => PacketSendUtility.SendPacket(p, msg));
    }

    protected override void HandleDied()
    {
        if (GetOwner().GetWorldId() == 400030000 && AhserionRaid.GetInstance().IsStarted())
        {
            switch (GetNpcId())
            {
                case 297306:
                    SendPacket(SM_SYSTEM_MESSAGE.STR_MSG_GAB1_SUB_PORTAL_DEST_69_BROKEN());
                    break;
                case 297307:
                    SendPacket(SM_SYSTEM_MESSAGE.STR_MSG_GAB1_SUB_PORTAL_DEST_70_BROKEN());
                    break;
                case 297308:
                    SendPacket(SM_SYSTEM_MESSAGE.STR_MSG_GAB1_SUB_PORTAL_DEST_71_BROKEN());
                    break;
                case 297309:
                    SendPacket(SM_SYSTEM_MESSAGE.STR_MSG_GAB1_SUB_PORTAL_DEST_72_BROKEN());
                    break;
            }
            AhserionRaid.GetInstance().HandleCorridorShieldDestruction(GetNpcId());
        }
        base.HandleDied();
    }
}
