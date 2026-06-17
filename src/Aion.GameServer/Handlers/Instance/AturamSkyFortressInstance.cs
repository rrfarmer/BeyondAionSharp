using System.Threading;
using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Manager;
using Aion.GameServer.Instance.Handlers;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.GameObjects.State;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services.Abyss;
using Aion.GameServer.Utils;
using Aion.GameServer.World;
using Aion.GameServer.World.Zone;
using LOG = Aion.GameServer.Network.Aion.ServerPackets.SmAttackStatus.LOG;
using TYPE = Aion.GameServer.Network.Aion.ServerPackets.SmAttackStatus.TYPE;

namespace Aion.GameServer.Handlers.Instance;

/// <summary>Java parity: instance/AturamSkyFortressInstance (xTz) : GeneralInstanceHandler. @InstanceID(300240000). AtomicBoolean/Int→int+Interlocked; onDie boss/officer/chief/generator webs incl. walker events (WalkManager.StartWalking((NpcAI)ai), SM_EMOTION CHANGE_SPEED); onInstanceCreate boss charge skill; onEnterZone doping msg; onPlayMovieEnd door. 1:1.</summary>
[InstanceID(300240000)]
public class AturamSkyFortressInstance : GeneralInstanceHandler
{
    private int msgIsSent;
    private int officerKilled;
    private int chiefKilled;
    private int generators;
    private bool isInstanceDestroyed;

    public AturamSkyFortressInstance(WorldMapInstance instance) : base(instance)
    {
    }

    public override void OnDie(Npc npc)
    {
        if (isInstanceDestroyed)
        {
            return;
        }

        switch (npc.GetNpcId())
        {
            case 702651:
                Spawn(282281, 524.1896f, 489.7742f, 649.916f, (byte)34);
                break;
            case 700982:
                Spawn(282279, 467.7094f, 465.6622f, 647.93896f, (byte)40);
                break;
            case 700983:
                Spawn(282278, 449.5576f, 420.7812f, 652.9143f, (byte)89);
                instance.SetDoorState(68, true);
                break;
            case 702650:
                Spawn(282277, 572.8088f, 459.4094f, 647.93896f, (byte)15);
                break;
            case 702652:
                Spawn(282280, 581.1f, 401.3544f, 648.6401f, (byte)9);
                break;
            case 217373:
                Spawn(730375, 374.85f, 424.32f, 653.52f, (byte)0);
                break;
            case 701043:
                npc.GetController().Delete();
                DeleteAliveNpcs(701030);
                SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_IDStation_HugenNM_00());
                break;
            case 217371:
                Spawn(730374, npc.GetX(), npc.GetY(), npc.GetZ(), (byte)0);
                break;
            case 217370:
                int killed1 = Interlocked.Increment(ref officerKilled);
                if (killed1 == 4)
                {
                    instance.SetDoorState(174, true);
                    SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_IDStation_3FDoor_311());
                    StartOfficerWalkerEvent();
                }
                else if (killed1 == 8)
                {
                    instance.SetDoorState(175, true);
                    StartMarbataWalkerEvent();
                }
                npc.GetController().Delete();
                break;
            case 217656:
                int killed2 = Interlocked.Increment(ref chiefKilled);
                if (killed2 == 1)
                {
                    StartOfficerWalkerEvent();
                }
                else if (killed2 == 2)
                {
                    instance.SetDoorState(178, true);
                    instance.SetDoorState(308, false); // reopen side windows
                    ThreadPoolManager.GetInstance().Schedule(() => instance.SetDoorState(307, true), 10000L); // close side windows
                }
                npc.GetController().Delete();
                break;
            case 217382:
                instance.SetDoorState(307, false); // reopen side windows
                instance.SetDoorState(230, true);
                Player player = npc.GetAggroList().GetMostPlayerDamage();
                if (player != null)
                {
                    AbyssPointsService.AddAp(player, 540);
                }
                SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_IDStation_3FDoor_322());
                break;
            case 218577:
                Spawn(217382, 258.3894f, 796.7554f, 901.6453f, (byte)80);
                break;
            case 701029:
                Npc boss = instance.GetNpc(217371);
                int used = Interlocked.Increment(ref generators);
                if (boss != null)
                {
                    if (used == 1)
                    {
                        SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_IDStation_HugenNM_01());
                    }
                    else if (used == 2)
                    {
                        boss.GetEffectController().RemoveEffect(19406);
                        Aion.GameServer.SkillEngine.SkillEngine.GetInstance().GetSkill(boss, 19407, 1, boss).UseNoAnimationSkill();
                        SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_IDStation_HugenNM_02());
                    }
                    else if (used == 3)
                    {
                        boss.GetEffectController().RemoveEffect(19407);
                        Aion.GameServer.SkillEngine.SkillEngine.GetInstance().GetSkill(boss, 19408, 1, boss).UseNoAnimationSkill();
                        SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_IDStation_HugenNM_03());
                    }
                    else if (used == 4)
                    {
                        boss.GetEffectController().RemoveEffect(19408);
                        Aion.GameServer.SkillEngine.SkillEngine.GetInstance().GetSkill(boss, 18117, 1, boss).UseNoAnimationSkill();
                        SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_IDStation_HugenNM_04());
                    }
                }
                npc.GetController().Delete();
                break;
            case 217369:
            case 217368:
            case 217655:
                npc.GetController().Delete();
                break;
        }
    }

    private void StartMarbataWalkerEvent()
    {
        SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_IDStation_3FDoor_311());
        StartWalk((Npc)Spawn(218577, 193.45583f, 802.1455f, 900.7575f, (byte)103), "3002400009");
        StartWalk((Npc)Spawn(217655, 198.34431f, 801.4107f, 900.66125f, (byte)110), "30024000010");
        StartWalk((Npc)Spawn(217655, 197.13315f, 798.7863f, 900.6499f, (byte)110), "30024000011");
    }

    private void StartWalk(Npc npc, string walkId)
    {
        ThreadPoolManager.GetInstance().Schedule(() =>
        {
            if (!isInstanceDestroyed)
            {
                npc.GetSpawn().SetWalkerId(walkId);
                WalkManager.StartWalking((NpcAI)npc.GetAi());
                npc.SetState(CreatureState.ACTIVE, true);
                PacketSendUtility.BroadcastPacket(npc, new SM_EMOTION(npc, EmotionType.CHANGE_SPEED, 0, npc.GetObjectId()));
            }
        }, 2000L);
    }

    private void StartOfficerWalkerEvent()
    {
        StartWalk((Npc)Spawn(217655, 146.53816f, 713.5974f, 901.0108f, (byte)111), "3002400003");
        StartWalk((Npc)Spawn(217655, 144.84991f, 720.9318f, 901.0604f, (byte)96), "3002400004");
        StartWalk((Npc)Spawn(217655, 146.19899f, 709.60455f, 901.0078f, (byte)110), "3002400005");
        StartWalk((Npc)Spawn(217656, 144.11845f, 716.8327f, 901.046f, (byte)100), "3002400006");
        StartWalk((Npc)Spawn(217369, 144.96825f, 712.83344f, 901.0133f, (byte)110), "3002400007");
        StartWalk((Npc)Spawn(217369, 144.75804f, 718.4293f, 901.05493f, (byte)80), "3002400008");
    }

    public override void OnInstanceCreate()
    {
        instance.SetDoorState(177, true);
        Npc npc = instance.GetNpc(217371);
        if (npc != null)
        {
            Aion.GameServer.SkillEngine.SkillEngine.GetInstance().GetSkill(npc, 19406, 1, npc).UseNoAnimationSkill();
        }
    }

    public override void OnInstanceDestroy()
    {
        isInstanceDestroyed = true;
    }

    public override void HandleUseItemFinish(Player player, Npc npc)
    {
        switch (npc.GetNpcId())
        {
            case 730398:
                player.GetLifeStats().IncreaseHp(TYPE.HP, 5205, npc);
                player.GetLifeStats().IncreaseMp(TYPE.MP, 5205, 0, LOG.REGULAR);
                SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_IDStation_Doping_02());
                npc.GetController().Delete();
                break;
            case 730397:
                Aion.GameServer.SkillEngine.SkillEngine.GetInstance().GetSkill(npc, 19520, 51, player).UseNoAnimationSkill();
                SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_IDStation_Doping_01());
                break;
            case 730410:
                instance.SetDoorState(90, true);
                break;
            case 731533:
                Aion.GameServer.SkillEngine.SkillEngine.GetInstance().GetSkill(player, 21807, 1, player).UseNoAnimationSkill();
                break;
            case 731534:
                Aion.GameServer.SkillEngine.SkillEngine.GetInstance().GetSkill(player, 21808, 1, player).UseNoAnimationSkill();
                break;
        }
    }

    public override void OnEnterZone(Player player, ZoneInstance zone)
    {
        if (zone.GetAreaTemplate().GetZoneName() == ZoneName.Get("SKY_FORTRESS_WAREHOUSE_ZONE_300240000"))
        {
            // wtf is that? Notify only one player ?
            if (Interlocked.CompareExchange(ref msgIsSent, 1, 0) == 0)
            {
                PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_IDStation_Doping_01_AD());
            }
        }
    }

    public override void OnPlayMovieEnd(Player player, int movieId)
    {
        if (movieId == 471)
            ThreadPoolManager.GetInstance().Schedule(() => instance.SetDoorState(308, true), 10000L); // close side windows
    }
}
