using System.Collections.Generic;
using System.Threading;
using Aion.GameServer.Ai;
using Aion.GameServer.Instance.Handlers;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services.Teleport;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Utils;
using Aion.GameServer.World;
using SkillEngineSvc = Aion.GameServer.SkillEngine.SkillEngine;

namespace Aion.GameServer.Handlers.Instance;

/// <summary>Java parity: instance/AnguishedDragonLordsRefugeInstance (Estrayl) : DragonLordsRefugeInstance. @InstanceID(300630000). Same fight shape as base with the anguished (236xxx/856xxx) NPC ids; overrides onSpawn/onStartEffect/onEndEffect/onDie/handleUseItemFinish/spawnTiamat/spawnCalindi; private endInstance/handlePhaseProgress. 1:1.</summary>
[InstanceID(300630000)]
public class AnguishedDragonLordsRefugeInstance : DragonLordsRefugeInstance
{
    public AnguishedDragonLordsRefugeInstance(WorldMapInstance instance) : base(instance)
    {
    }

    public override void OnSpawn(VisibleObject obj)
    {
        if (obj is Npc npc)
        {
            switch (npc.GetNpcId())
            {
                case 236276: // Tiamat Dragon
                    ThreadPoolManager.GetInstance().Schedule(() =>
                    {
                        Spawn(730673, 459.548f, 456.849f, 417.405f, (byte)21);
                        Spawn(730674, 547.909f, 456.568f, 417.405f, (byte)45);
                        Spawn(730675, 460.082f, 571.978f, 417.405f, (byte)98);
                        Spawn(730676, 547.822f, 571.876f, 417.405f, (byte)74);
                    }, 60000L);
                    break;
                case 236277: // Tiamat Weakened Dragon
                    SendMsg(SM_SYSTEM_MESSAGE.IDTIAMAT_TIAMAT_COUNTDOWN_START(), 2000);
                    PacketSendUtility.BroadcastToMap(instance, new SM_QUEST_ACTION(0, 1800));
                    failTask = ThreadPoolManager.GetInstance().Schedule(() =>
                    {
                        Npc tiamat = GetNpc(236277);
                        if (tiamat != null && !tiamat.IsDead())
                        {
                            EndInstance(new List<int> { 730625, 730633, 730634, 730635, 730636 });
                            SendMsg(SM_SYSTEM_MESSAGE.IDTIAMAT_TIAMAT_COUNTDOWN_OVER());
                        }
                    }, 1800000L); // 30'
                    break;
                case 856020: // Kaisinel 1st Phase
                    SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_IDTIAMAT_TIAMAT_2PHASE_START_LIGHT(), 20000);
                    break;
                case 856021: // Kaisinel 2nd Phase
                    SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_IDTIAMAT_KAISINEL_2PHASE_DEADLYATK(), 3000);
                    break;
                case 856022: // Kaisinel 3rd Phase
                    SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_IDTIAMAT_KAISINEL_2PHASE_GROGGY(), 15000);
                    break;
                case 856023: // Marchutan 1st Phase
                    SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_IDTIAMAT_TIAMAT_2PHASE_START_DARK(), 20000);
                    break;
                case 856024: // Marchutan 2nd Phase
                    SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_IDTIAMAT_MARCHUTAN_2PHASE_DEADLYATK(), 3000);
                    break;
                case 856025: // Marchutan 3rd Phase
                    SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_IDTIAMAT_MARCHUTAN_2PHASE_GROGGY(), 15000);
                    break;
                case 856026:
                    SendMsg(SM_SYSTEM_MESSAGE.STR_IDTIAMAT_TIAMAT_SPAWN_BLACKHOLE(), 2500);
                    break;
                case 730673:
                    ThreadPoolManager.GetInstance().Schedule(() =>
                    {
                        Spawn(856483, 463f, 568f, 417.405f, (byte)105);
                        Spawn(856484, 545f, 568f, 417.405f, (byte)78);
                        Spawn(856485, 545f, 461f, 417.405f, (byte)46);
                        Spawn(856486, 463f, 461f, 417.405f, (byte)17);
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
                    ThreadPoolManager.GetInstance().Schedule(() => Spawn(856020 + raceId * 3, 504f, 515f, 417.405f, (byte)60), 6000L);
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
                if (((Npc)effect.GetEffector()).GetNpcId() == 236276)
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
        Spawn(833482, 548.18683f, 514.54523f, 420f, (byte)0, 23);
    }

    public override void OnDie(Npc npc)
    {
        base.OnDie(npc);
        switch (npc.GetNpcId())
        {
            case 236274: // Calindi Flamelord
                DeleteAliveNpcs(730695, 730696); // Surkana
                ThreadPoolManager.GetInstance().Schedule(() => AIActions.UseSkill((NpcAI)GetNpc(236275).GetAi(), 20919), 4000L);
                ThreadPoolManager.GetInstance().Schedule(() => DeleteAliveNpcs(730694), 6000L); // Aetheric field
                break;
            case 236276: // Tiamat Dragon - killed by Empyrean Lord
                GetNpc(730699).GetController().Die(); // Animates roof destruction
                GetNpc(730700).GetController().Die();
                Spawn(283134, 451.97f, 514.55f, 417.40436f, (byte)0);
                Spawn(236277, 451.97f, 514.55f, 417.40436f, (byte)0);
                Spawn(730704, 437.541f, 513.487f, 415.824f, (byte)0, 17); // Collapsed Debris impaling Tiamat
                break;
            case 236277:
                failTask.Cancel(true);
                PacketSendUtility.BroadcastToMap(instance, new SM_QUEST_ACTION(0, 0));
                // TODO: play movie
                EndInstance(new List<int> { 219490, 219493, 219362, 730625, 730633, 730634, 730635, 730636 });
                Spawn(702729, 480f, 514f, 417.405f, (byte)0); // Treasure Chest
                Spawn(800430, 506.8f, 511.4f, 417.405f, (byte)60); // Kahrun
                Spawn(800350 + raceId * 6, 506.7f, 518.4f, 417.405f, (byte)60); // human Kaisinel/Marchutan
                Spawn(800464, 544.964f, 517.898f, 417.405f, (byte)113);
                Spawn(800465, 545.605f, 510.325f, 417.405f, (byte)17);
                break;
            case 236278: // Incarnations
            case 236279:
            case 236280:
            case 236281:
                if (Interlocked.Increment(ref incarnationKills) == 4)
                    HandlePhaseProgress();
                SkillEngineSvc.GetInstance().ApplyEffectDirectly(npc.GetNpcId() - 215299, npc, GetNpc(236276)); // 20979 - 20982
                break;
            case 856483: // Balaur Spiritualist
            case 856484:
            case 856485:
            case 856486:
                Npc empyreanLord = GetNpc(856020 + raceId * 3);
                if (empyreanLord != null)
                    SkillEngineSvc.GetInstance().ApplyEffectDirectly(npc.GetNpcId() - 835490, npc, empyreanLord); // 20993 - 20996
                npc.GetController().Delete();
                break;
            case 856020: // Kaisinel 1st Phase
            case 856023: // Marchutan 1st Phase
                if (Volatile.Read(ref incarnationKills) == 4) // in rare cases the empyrean lord dies while the instance have to wait for progress
                    return;
                SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_IDTIAMAT_TIAMAT_DEADLYHOWLING());
                for (int i = 283163; i <= 283166; i++)
                {
                    Npc incarnation = GetNpc(i);
                    if (incarnation != null)
                        AIActions.UseSkill((NpcAI)incarnation.GetAi(), 20983);
                }
                AIActions.UseSkill((NpcAI)GetNpc(236276).GetAi(), 20983);
                ThreadPoolManager.GetInstance().Schedule(() => EndInstance(new List<int> { 730625, 730633, 730634, 730635, 730636, 730699, 730700 }), 7000L);
                break;
        }
    }

    public override void HandleUseItemFinish(Player player, Npc npc)
    {
        base.HandleUseItemFinish(player, npc);
        if (npc.GetNpcId() == 833482) // Exit
            TeleportService.MoveToInstanceExit(player, mapId, player.GetRace());
    }

    private void HandlePhaseProgress()
    {
        SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_IDTIAMAT_TIAMAT_2PHASE_CLOSE_ALL());

        int empyreanLordId = 856020 + raceId * 3;
        GetNpc(236276).GetEffectController().RemoveEffect(20984); // Dispel Unbreakable Wing

        // schedule spawn of empyrean lords for final attack to tiamat before getting exhausted
        ThreadPoolManager.GetInstance().Schedule(() =>
        {
            DeleteAliveNpcs(empyreanLordId);
            Spawn(856021 + raceId * 3, 516.285f, 514.84f, 417.405f, (byte)60);
        }, 30000L);
    }

    protected override void SpawnTiamat()
    {
        Spawn(236275, 452, 514, 432, (byte)0);
    }

    protected override void SpawnCalindi()
    {
        Spawn(236274, 483.463f, 514.519f, 417.404f, (byte)0);
    }
}
