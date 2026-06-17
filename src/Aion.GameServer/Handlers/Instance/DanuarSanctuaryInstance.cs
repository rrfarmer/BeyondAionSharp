using System.Threading;
using Aion.GameServer.Commons.Utils;
using Aion.GameServer.Instance.Handlers;
using Aion.GameServer.Model.Animations;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services.Players;
using Aion.GameServer.Services.Teleport;
using Aion.GameServer.SkillEngine;
using Aion.GameServer.Utils;
using Aion.GameServer.World;

namespace Aion.GameServer.Handlers.Instance;

/// <summary>Java parity: instance/DanuarSanctuaryInstance (Ritsu, Tibald) : GeneralInstanceHandler. @InstanceID(301380000); AtomicBoolean cannonUsed compareAndSet→int+Interlocked; onInstanceCreate rnd boss; onDie; handleUseItemFinish teleports+cannon skill+die; onReviveEvent. 1:1.</summary>
[InstanceID(301380000)]
public class DanuarSanctuaryInstance : GeneralInstanceHandler
{
    private int cannonUsed;

    public DanuarSanctuaryInstance(WorldMapInstance instance) : base(instance)
    {
    }

    public override void OnInstanceCreate()
    {
        SpawnRndBoss();
    }

    public override void OnDie(Npc npc)
    {
        base.OnDie(npc);
        switch (npc.GetNpcId())
        {
            case 233187:
                Spawn(233087, 906.4945f, 861.5854f, 280.5441f, (byte)73, 1699);
                npc.GetController().Delete();
                break;
            case 235624:
            case 235625:
            case 235626:
                Spawn(701876, 1071.9772f, 682.4911f, 282.0391f, (byte)60); // Emergency Exit
                break;
            case 730866:
            case 233448:
            case 233447:
                npc.GetController().Delete();
                break;
        }
    }

    public override void HandleUseItemFinish(Player player, Npc npc)
    {
        switch (npc.GetNpcId())
        {
            case 701873:
                TeleportService.TeleportTo(player, instance, 1029.273f, 362.651f, 297.89f, (byte)30, TeleportAnimation.FADE_OUT_BEAM);
                break;
            case 701871:
                TeleportService.TeleportTo(player, instance, 1006.0412f, 1366.468f, 337.26f, (byte)105, TeleportAnimation.FADE_OUT_BEAM);
                break;
            case 701872:
                TeleportService.TeleportTo(player, instance, 846.172f, 991.731f, 300.04f, (byte)110, TeleportAnimation.FADE_OUT_BEAM);
                break;
            case 730863:
                if (Interlocked.CompareExchange(ref cannonUsed, 1, 0) == 0)
                {
                    Npc target = instance.GetNpc(730866);
                    Aion.GameServer.SkillEngine.SkillEngine.GetInstance().GetSkill(npc, 20385, 1, target).UseWithoutPropSkill();
                    if (target != null)
                    {
                        ThreadPoolManager.GetInstance().Schedule(() => target.GetController().Die(), 5700L);
                    }
                }
                break;
            case 701876:
                TeleportService.MoveToInstanceExit(player, mapId, player.GetRace());
                break;
        }
    }

    public override bool OnReviveEvent(Player player)
    {
        PlayerReviveService.Revive(player, 25, 25, true, 0);
        PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_REBIRTH_MASSAGE_ME());
        player.GetGameStats().UpdateStatsAndSpeedVisually();
        TeleportService.TeleportTo(player, instance, 388.6437f, 1184.639f, 55.30134f);
        return true;
    }

    private void SpawnRndBoss()
    {
        /*
         * 235624 Warmage Suyaroka
         * 235625 Chief Medic Tagnu
         * 235626 Virulent Ukahim
         */
        Spawn((235624 + Rnd.Get(0, 2)), 1056.6105f, 694.0836f, 282.04f, (byte)30);
    }
}
