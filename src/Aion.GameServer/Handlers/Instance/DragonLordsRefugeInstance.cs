using System;
using System.Collections.Generic;
using System.Threading;
using Aion.GameServer.Ai;
using Aion.GameServer.Instance.Handlers;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services.Players;
using Aion.GameServer.Services.Teleport;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Utils;
using Aion.GameServer.World;
using Rnd = Aion.GameServer.Commons.Utils.Rnd;
using SkillEngineSvc = Aion.GameServer.SkillEngine.SkillEngine;

namespace Aion.GameServer.Handlers.Instance;

/// <summary>Java parity: instance/DragonLordsRefugeInstance (bobobear, Luzien, Estrayl) : GeneralInstanceHandler. @InstanceID(300520000). AtomicInteger→int+Interlocked (raceId/incarnationKills/progress); onSpawn boss-phase msgs+spawns; onStartEffect/onEndEffect skill webs; onDie multi-phase Tiamat fight; handleDialogueEvent/handlePhaseProgress; onCreatureDetected; handleUseItemFinish teleports; onReviveEvent random respawn. 1:1.</summary>
[InstanceID(300520000)]
public class DragonLordsRefugeInstance : GeneralInstanceHandler
{
    private readonly List<WorldPosition> respawnLocations = new();
    protected int raceId = 10;
    protected int incarnationKills;
    protected int progress;
    protected ScheduledTask failTask;

    public DragonLordsRefugeInstance(WorldMapInstance instance) : base(instance)
    {
        // Retail positions
        respawnLocations.Add(new WorldPosition(300520000, 493.72f, 507.67f, 240.5f, (byte)0));
        respawnLocations.Add(new WorldPosition(300520000, 497.52f, 526.03f, 240.5f, (byte)0));
        respawnLocations.Add(new WorldPosition(300520000, 513.02f, 524.57f, 240.5f, (byte)0));
        respawnLocations.Add(new WorldPosition(300520000, 499.57f, 510.19f, 240.5f, (byte)0));
        respawnLocations.Add(new WorldPosition(300520000, 513.22f, 508.36f, 240.5f, (byte)0));
        respawnLocations.Add(new WorldPosition(300520000, 505.05f, 525.03f, 240.5f, (byte)0));
        respawnLocations.Add(new WorldPosition(300520000, 498.65f, 519.93f, 240.5f, (byte)0));
        respawnLocations.Add(new WorldPosition(300520000, 511.44f, 515.36f, 240.5f, (byte)0));
        respawnLocations.Add(new WorldPosition(300520000, 495.27f, 517.07f, 240.5f, (byte)0));
        respawnLocations.Add(new WorldPosition(300520000, 492.62f, 525.37f, 240.5f, (byte)0));
        respawnLocations.Add(new WorldPosition(300520000, 507.56f, 520.36f, 240.5f, (byte)0));
        respawnLocations.Add(new WorldPosition(300520000, 508.20f, 506.85f, 240.5f, (byte)0));
    }

    public override void OnSpawn(VisibleObject obj)
    {
        if (obj is Npc)
        {
            switch (((Npc)obj).GetNpcId())
            {
                case 219361: // Tiamat Dragon
                    ThreadPoolManager.GetInstance().Schedule(() =>
                    {
                        Spawn(730673, 459.548f, 456.849f, 417.405f, (byte)21);
                        Spawn(730674, 547.909f, 456.568f, 417.405f, (byte)45);
                        Spawn(730675, 460.082f, 571.978f, 417.405f, (byte)98);
                        Spawn(730676, 547.822f, 571.876f, 417.405f, (byte)74);
                    }, 60000L);
                    break;
                case 219362: // Tiamat Weakened Dragon
                    SendMsg(SM_SYSTEM_MESSAGE.IDTIAMAT_TIAMAT_COUNTDOWN_START(), 2000);
                    PacketSendUtility.BroadcastToMap(instance, new SM_QUEST_ACTION(0, 1800));
                    failTask = ThreadPoolManager.GetInstance().Schedule(() =>
                    {
                        Npc tiamat = GetNpc(219362);
                        if (tiamat != null && !tiamat.IsDead())
                        {
                            EndInstance(new List<int> { 730625, 730633, 730634, 730635, 730636 });
                            SendMsg(SM_SYSTEM_MESSAGE.IDTIAMAT_TIAMAT_COUNTDOWN_OVER());
                        }
                    }, 1800000L); // 30'
                    break;
                case 219488: // Kaisinel 1st Phase
                    SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_IDTIAMAT_TIAMAT_2PHASE_START_LIGHT(), 20000);
                    break;
                case 219489: // Kaisinel 2nd Phase
                    SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_IDTIAMAT_KAISINEL_2PHASE_DEADLYATK(), 3000);
                    break;
                case 219490: // Kaisinel 3rd Phase
                    SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_IDTIAMAT_KAISINEL_2PHASE_GROGGY(), 15000);
                    break;
                case 219491: // Marchutan 1st Phase
                    SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_IDTIAMAT_TIAMAT_2PHASE_START_DARK(), 20000);
                    break;
                case 219492: // Marchutan 2nd Phase
                    SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_IDTIAMAT_MARCHUTAN_2PHASE_DEADLYATK(), 3000);
                    break;
                case 219493: // Marchutan 3rd Phase
                    SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_IDTIAMAT_MARCHUTAN_2PHASE_GROGGY(), 15000);
                    break;
                case 283140:
                    SendMsg(SM_SYSTEM_MESSAGE.STR_IDTIAMAT_TIAMAT_SPAWN_BLACKHOLE(), 2500);
                    break;
                case 730673:
                    ThreadPoolManager.GetInstance().Schedule(() =>
                    {
                        Spawn(283163, 463f, 568f, 417.405f, (byte)105);
                        Spawn(283164, 545f, 568f, 417.405f, (byte)78);
                        Spawn(283165, 545f, 461f, 417.405f, (byte)46);
                        Spawn(283166, 463f, 461f, 417.405f, (byte)17);
                    }, 10000L);
                    break;
                case 730695: // Surkana
                    SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_IDTIAMAT_KALYNDI_SURKANA_SPAWN(), 2500);
                    break;
            }
        }
    }

    public override void OnStartEffect(Effect effect)
    {
        switch (effect.GetSkillId())
        {
            case 20918:
                PacketSendUtility.BroadcastMessage(GetNpc(800341), 1500612);
                break;
            case 20920:
                if (Interlocked.CompareExchange(ref progress, 2, 1) == 1)
                {
                    ThreadPoolManager.GetInstance().Schedule(() => Spawn(219488 + raceId * 3, 504f, 515f, 417.405f, (byte)60), 6000L);
                    Spawn(219532, 469f, 563f, 417.41f, (byte)103);
                    Spawn(219535, 466f, 560f, 417.41f, (byte)103);
                    Spawn(219533, 542f, 559f, 417.41f, (byte)79);
                    Spawn(219538, 538f, 562f, 417.41f, (byte)79);
                    Spawn(219534, 537f, 466f, 417.41f, (byte)42);
                    Spawn(219537, 541f, 469f, 417.41f, (byte)42);
                    Spawn(219536, 466f, 471f, 417.41f, (byte)18);
                    Spawn(219539, 470f, 467f, 417.41f, (byte)18);
                }
                break;
            case 20993:
            case 20994:
            case 20995:
            case 20996:
                SendMsg(SM_SYSTEM_MESSAGE.IDTIAMAT_TIAMAT_DRAKAN_ON_DIE());
                break;
        }
    }

    public override void OnEndEffect(Effect effect)
    {
        switch (effect.GetSkillId())
        {
            case 20975:
                SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_IDTIAMAT_TIAMAT_2PHASE_CLOSE_CRACK());
                break;
            case 20976:
                SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_IDTIAMAT_TIAMAT_2PHASE_CLOSE_RAGE());
                break;
            case 20977:
                SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_IDTIAMAT_TIAMAT_2PHASE_CLOSE_GRAVITY());
                break;
            case 20978:
                SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_IDTIAMAT_TIAMAT_2PHASE_CLOSE_CRYSTAL());
                break;
            case 20983:
                if (((Npc)effect.GetEffector()).GetNpcId() == 219361)
                    EndInstance(new List<int> { 730625, 730633, 730634, 730635, 730636 });
                break;
        }
    }

    private void EndInstance(List<int> despawnExceptions)
    {
        instance.ForEachNpc(npc =>
        {
            if (!despawnExceptions.Contains(npc.GetNpcId()))
                npc.GetController().Delete();
        });
        Spawn(730630, 548.18683f, 514.54523f, 420f, (byte)0, 23);
    }

    public override void OnDie(Npc npc)
    {
        base.OnDie(npc);
        switch (npc.GetNpcId())
        {
            case 219359: // Calindi Flamelord
                DeleteAliveNpcs(730695, 730696); // Surkana
                ThreadPoolManager.GetInstance().Schedule(() => AIActions.UseSkill((NpcAI)GetNpc(219360).GetAi(), 20919), 4000L);
                ThreadPoolManager.GetInstance().Schedule(() => DeleteAliveNpcs(730694), 6000L); // Aetheric field
                break;
            case 219361: // Tiamat Dragon - killed by Empyrean Lord
                GetNpc(730699).GetController().Die(); // Animates roof destruction
                GetNpc(730700).GetController().Die();
                Spawn(283134, 451.97f, 514.55f, 417.40436f, (byte)0);
                Spawn(219362, 451.97f, 514.55f, 417.40436f, (byte)0);
                Spawn(730704, 437.541f, 513.487f, 415.824f, (byte)0, 17); // Collapsed Debris impaling Tiamat
                break;
            case 219362:
                failTask.Cancel(true);
                PacketSendUtility.BroadcastToMap(instance, new SM_QUEST_ACTION(0, 0));
                // TODO: play movie
                EndInstance(new List<int> { 219490, 219493, 219362, 730625, 730633, 730634, 730635, 730636 });
                Spawn(701542, 480f, 514f, 417.405f, (byte)0); // Treasure Chest
                Spawn(800430, 506.8f, 511.4f, 417.405f, (byte)60); // Kahrun
                Spawn(800350 + raceId * 6, 506.7f, 518.4f, 417.405f, (byte)60); // human Kaisinel/Marchutan
                Spawn(800464, 544.964f, 517.898f, 417.405f, (byte)113);
                Spawn(800465, 545.605f, 510.325f, 417.405f, (byte)17);
                break;
            case 219365: // Incarnations
            case 219366:
            case 219367:
            case 219368:
                if (Interlocked.Increment(ref incarnationKills) == 4)
                    HandlePhaseProgress();
                SkillEngineSvc.GetInstance().ApplyEffectDirectly(npc.GetNpcId() - 198386, npc, GetNpc(219361)); // 20979 - 20982
                break;
            case 283163: // Balaur Spiritualist
            case 283164:
            case 283165:
            case 283166:
                Npc empyreanLord = GetNpc(219488 + raceId * 3);
                if (empyreanLord != null)
                    SkillEngineSvc.GetInstance().ApplyEffectDirectly(npc.GetNpcId() - 262170, npc, empyreanLord); // 20993 - 20996
                break;
            case 219488: // Kaisinel 1st Phase
            case 219491: // Marchutan 1st Phase
                if (Volatile.Read(ref incarnationKills) == 4) // in rare cases the empyrean lord dies while the instance have to wait for progress
                    return;
                SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_IDTIAMAT_TIAMAT_DEADLYHOWLING());
                for (int i = 219365; i <= 219368; i++)
                {
                    Npc incarnation = GetNpc(i);
                    if (incarnation != null)
                        AIActions.UseSkill((NpcAI)incarnation.GetAi(), 20983);
                }
                AIActions.UseSkill((NpcAI)GetNpc(219361).GetAi(), 20983);
                ThreadPoolManager.GetInstance().Schedule(() => EndInstance(new List<int> { 730625, 730633, 730634, 730635, 730636, 730699, 730700 }), 5000L);
                break;
        }
    }

    private void HandlePhaseProgress()
    {
        SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_IDTIAMAT_TIAMAT_2PHASE_CLOSE_ALL());

        int empyreanLordId = 219488 + raceId * 3;
        GetNpc(219361).GetEffectController().RemoveEffect(20984); // Dispel Unbreakable Wing

        // schedule spawn of empyrean lords for final attack to tiamat before became exhausted
        ThreadPoolManager.GetInstance().Schedule(() =>
        {
            DeleteAliveNpcs(empyreanLordId);
            Spawn(219489 + raceId * 3, 516.285f, 514.84f, 417.405f, (byte)60);
        }, 30000L);
    }

    /// <summary>Smalltalk between Tiamat and Kahrun</summary>
    private void HandleDialogueEvent(Npc tiamat)
    {
        Npc kahrun = GetNpc(800341);
        AIActions.TargetCreature((NpcAI)tiamat.GetAi(), kahrun);
        PacketSendUtility.BroadcastMessage(tiamat, 1500613, 5000);
        PacketSendUtility.BroadcastMessage(kahrun, 1500609, 10000);
        PacketSendUtility.BroadcastMessage(tiamat, 1500614, 15000);
        PacketSendUtility.BroadcastMessage(kahrun, 1500610, 20000);
        PacketSendUtility.BroadcastMessage(tiamat, 1500615, 26500);
        PacketSendUtility.BroadcastMessage(kahrun, 1500611, 31500);
        ThreadPoolManager.GetInstance().Schedule(() => AIActions.UseSkill((NpcAI)kahrun.GetAi(), 20597), 35500L);
        PacketSendUtility.BroadcastMessage(tiamat, 1500616, 40000);
        ThreadPoolManager.GetInstance().Schedule(() => AIActions.UseSkill((NpcAI)tiamat.GetAi(), 20918), 49000L);
        PacketSendUtility.BroadcastMessage(tiamat, 1500617, 44500);
        ThreadPoolManager.GetInstance().Schedule(() =>
        {
            kahrun.GetController().Delete();
            tiamat.GetController().Delete();
            SpawnTiamat();
            SpawnCalindi();
        }, 54500L);
    }

    protected virtual void SpawnTiamat()
    {
        Spawn(219360, 452, 514, 432, (byte)0);
    }

    protected virtual void SpawnCalindi()
    {
        Spawn(219359, 483.463f, 514.519f, 417.404f, (byte)0);
    }

    public override void OnEnterInstance(Player player)
    {
        Interlocked.CompareExchange(ref raceId, player.GetRace().GetRaceId(), 10);
    }

    public override void OnCreatureDetected(Npc detector, Creature detected)
    {
        if (detector.GetNpcId() == 219360 && detected is Player && Interlocked.CompareExchange(ref progress, 1, 0) == 0)
            HandleDialogueEvent(detector);
    }

    public override void HandleUseItemFinish(Player player, Npc npc)
    {
        switch (npc.GetNpcId())
        {
            case 730625:
                float distance = Rnd.Get(250, 450) * 0.1f;
                double radian = Math.PI / 180.0 * Rnd.Get(-45, 60);
                float x = (float)(Math.Cos(radian) * distance);
                float y = (float)(Math.Sin(radian) * distance);
                TeleportService.TeleportTo(player, instance.GetMapId(), 503.389f + x, 514.661f + y, 417.404f);
                Npc tiamat = GetNpc(219360);
                if (tiamat != null)
                    tiamat.OverrideNpcType(CreatureType.PEACE);
                break;
            case 730630: // Exit
                TeleportService.MoveToInstanceExit(player, mapId, player.GetRace());
                break;
            case 730673:
                TeleportService.TeleportTo(player, instance.GetMapId(), 217.144f, 195.616f, 246.0712f);
                break;
            case 730674:
                TeleportService.TeleportTo(player, instance.GetMapId(), 785.866f, 197.713f, 246.0712f);
                break;
            case 730675:
                TeleportService.TeleportTo(player, instance.GetMapId(), 217.947f, 832.552f, 246.0712f);
                break;
            case 730676:
                TeleportService.TeleportTo(player, instance.GetMapId(), 779.178f, 833.055f, 246.0712f);
                break;
            case 730633:
                TeleportService.TeleportTo(player, instance.GetMapId(), 461.493f, 459.308f, 417.405f);
                break;
            case 730634:
                TeleportService.TeleportTo(player, instance.GetMapId(), 546f, 458.934f, 417.405f);
                break;
            case 730635:
                TeleportService.TeleportTo(player, instance.GetMapId(), 462.188f, 568.913f, 417.405f);
                break;
            case 730636:
                TeleportService.TeleportTo(player, instance.GetMapId(), 545.773f, 569.225f, 417.405f);
                break;
        }
    }

    public override void OnInstanceDestroy()
    {
        if (failTask != null && !failTask.IsCancelled)
            failTask.Cancel(true);
    }

    public override bool OnReviveEvent(Player player)
    {
        PlayerReviveService.Revive(player, 25, 25, true, 0);
        player.GetGameStats().UpdateStatsAndSpeedVisually();
        PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_REBIRTH_MASSAGE_ME());
        WorldPosition p = Rnd.Get(respawnLocations);
        TeleportService.TeleportTo(player, instance, p.GetX(), p.GetY(), p.GetZ(), p.GetHeading());
        return true;
    }
}
