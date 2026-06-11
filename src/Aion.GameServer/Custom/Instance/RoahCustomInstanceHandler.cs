using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Aion.Commons.Utils;
using Aion.GameServer.Custom.Instance.Neuralnetwork;
using Aion.GameServer.Dao;
using Aion.GameServer.Instance.Handlers;
using Aion.GameServer.Model;
using Aion.GameServer.Model.Drop;
using Aion.GameServer.Model.Flyring;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Geometry;
using Aion.GameServer.Model.Stats.Calc.Functions;
using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.Model.Templates.Flyring;
using Aion.GameServer.Network.Aion.Serverpackets;
using Aion.GameServer.Services.Drop;
using Aion.GameServer.Services.Event;
using Aion.GameServer.Services.Item;
using Aion.GameServer.Services.Players;
using Aion.GameServer.Skillengine.Model;
using Aion.GameServer.Utils;
using Aion.GameServer.World;

namespace Aion.GameServer.Custom.Instance;

/// <summary>Java parity: custom/instance/RoahCustomInstanceHandler (Jo, Estrayl) : GeneralInstanceHandler. AtomicLong→long+Interlocked (compareAndSet→CompareExchange==0, get→Read); AtomicBoolean→int+Interlocked/Volatile; Future&lt;?&gt;→ScheduledTask (cancel(true)→Cancel()); schedule/scheduleAtFixedRate→Schedule/ScheduleAtFixedRateTask(TimeSpan, ct=>{...;return ValueTask.CompletedTask;}); switch arrows/exprs→C# switch statement/expression; instanceof X x→is X x; Math.round(float)→(int)Math.Floor(+0.5f); String.format %d/%s/%b/%.3f→string.Format {N}/{N:F3}; Map.get→GetValueOrDefault. Base members (instance/mapId/log/spawn/deleteAliveNpcs) + many services red-tolerated.</summary>
public class RoahCustomInstanceHandler : GeneralInstanceHandler
{
    private const int CENTER_ARTIFACT_ID = 234029;
    private const int TRASH_MOB_ID = 218483;
    private const int BULKY_MOB_ID = 282757;
    private const int DOMINATOR_MOB_ID = 284203;
    private const int BOSS_MOB_A_M_ID = 231919; // asmodian male
    private const int BOSS_MOB_A_F_ID = 231747; // asmodian female
    private const int BOSS_MOB_E_M_ID = 231918; // elyos male
    private const int BOSS_MOB_E_F_ID = 231717; // elyos female
    private const int BOSS_MOB_AT_ID = 217221;

    private const int TIME_LIMIT = 900; // 15 minutes

    private const float REWARD_SCALE = 2f;

    private long startTime;
    private int isInitialized;
    private int isCompleted;

    private int avgClassDps, achievedDps, playerObjId;
    private PlayerModel model;
    private List<int> skillSet;

    private bool isBossPhase, isPrebuffed;
    private int rank;
    private ScheduledTask despawnTask, trashMobSpawnTask, bulkyMobSpawnTask, dominatorMobSpawnTask;

    public RoahCustomInstanceHandler(WorldMapInstance instance) : base(instance)
    {
    }

    public override void OnInstanceCreate()
    {
        SpawnRings();
        Spawn(730588, 505.2431f, 377.0414f, 93.8944f, (byte)30, 33); // Exit
    }

    public override void OnInstanceDestroy()
    {
        CancelAllTasks();
    }

    private void SpawnRings()
    {
        // transparent entry gate
        new FlyRing(new FlyRingTemplate("ROAH_WING_1", mapId, new Point3D(501.77, 409.53, 94.12), new Point3D(503.93, 409.65, 98.9),
            new Point3D(506.26, 409.7, 94.15), 10), instance.GetInstanceId()).Spawn();
    }

    public override bool OnPassFlyingRing(Player player, string flyingRing)
    {
        if (flyingRing.Equals("ROAH_WING_1") && Interlocked.CompareExchange(ref startTime, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), 0) == 0)
        {
            Npc artifact = (Npc)Spawn(CENTER_ARTIFACT_ID, 504.1977f, 481.5051f, 87.2790f, (byte)30);
            if (artifact != null)
                AdaptNPC(artifact, rank);

            PacketSendUtility.BroadcastToMap(instance, SM_SYSTEM_MESSAGE.STR_MSG_INSTANCE_START_IDABRE());
            PacketSendUtility.BroadcastToMap(instance, new SM_QUEST_ACTION(0, TIME_LIMIT));
            PacketSendUtility.BroadcastToMap(instance, new SM_MESSAGE(0, null,
                "You have 15 minutes to demolish the central Aether Stone. Beware for its protectors!", ChatType.BRIGHT_YELLOW_CENTER));

            despawnTask = ThreadPoolManager.GetInstance().Schedule(ct =>
            {
                SetResult(false);
                DeleteAliveNpcs(CENTER_ARTIFACT_ID, TRASH_MOB_ID, BULKY_MOB_ID, DOMINATOR_MOB_ID, BOSS_MOB_A_M_ID, BOSS_MOB_A_F_ID, BOSS_MOB_E_M_ID,
                    BOSS_MOB_E_F_ID, BOSS_MOB_AT_ID);
                return ValueTask.CompletedTask;
            }, TimeSpan.FromMilliseconds(TIME_LIMIT * 1000));

            // spawn bulky mob: 1/min after 1:20min
            bulkyMobSpawnTask = ThreadPoolManager.GetInstance().ScheduleAtFixedRateTask(ct =>
            {
                Npc bulky = null;
                foreach (Npc n in instance.GetNpcs(BULKY_MOB_ID))
                {
                    if (!n.IsDead() && !n.GetLifeStats().IsAboutToDie())
                    {
                        bulky = n;
                        break;
                    }
                }
                if (bulky != null)
                {
                    bulky.GetLifeStats().SetCurrentHp(bulky.GetLifeStats().GetMaxHp());
                    PacketSendUtility.BroadcastToMap(instance, new SM_MESSAGE(0, null, "Protector Vord recovered energy!", ChatType.BRIGHT_YELLOW_CENTER));
                }
                else
                {
                    bulky = (Npc)Spawn(BULKY_MOB_ID, 493.923f, 488.16794f, 87.18341f, (byte)0);
                    AdaptNPC(bulky, rank);
                    bulky.GetAggroList().AddHate(player, 1000);
                    PacketSendUtility.BroadcastToMap(instance, new SM_MESSAGE(0, null, "A tough protector has appeared!", ChatType.BRIGHT_YELLOW_CENTER));
                }
                return ValueTask.CompletedTask;
            }, TimeSpan.FromMilliseconds(80000), TimeSpan.FromMilliseconds(60000));

            if (rank >= CustomInstanceRankEnum.BRONZE.GetMinRank())
            {
                // spawn trash mobs: 1 wave/min after 1min
                trashMobSpawnTask = ThreadPoolManager.GetInstance().ScheduleAtFixedRateTask(ct =>
                {
                    int newTrash = 6;
                    foreach (Npc n in instance.GetNpcs(TRASH_MOB_ID)) // always spawn until 6 total
                        if (!n.IsDead())
                            newTrash--;

                    if (newTrash > 0)
                    {
                        for (int i = 0; i < newTrash; i++)
                        {
                            Npc mob = (Npc)Spawn(TRASH_MOB_ID, 500.1f + Rnd.NextFloat(8f), 460f + Rnd.NextFloat(4f), 86.7112f, (byte)30);
                            AdaptNPC(mob, rank);
                            mob.GetAggroList().AddHate(player, 1000);
                        }
                    }
                    return ValueTask.CompletedTask;
                }, TimeSpan.FromMilliseconds(60000), TimeSpan.FromMilliseconds(60000 - rank * 1000L)); // 60s ... 30s
            }

            if (rank >= CustomInstanceRankEnum.SILVER.GetMinRank())
            {
                // spawn dominator mob: 1/min after 1:40min
                dominatorMobSpawnTask = ThreadPoolManager.GetInstance().ScheduleAtFixedRateTask(ct =>
                {
                    Npc dominator = null;
                    foreach (Npc n in instance.GetNpcs(DOMINATOR_MOB_ID))
                    {
                        if (!n.IsDead() && !n.GetLifeStats().IsAboutToDie())
                        {
                            dominator = n;
                            break;
                        }
                    }
                    if (dominator != null)
                    {
                        dominator.GetLifeStats().SetCurrentHp(dominator.GetLifeStats().GetMaxHp());
                        PacketSendUtility.BroadcastToMap(instance, new SM_MESSAGE(0, null, "Protector Vala recovered energy!", ChatType.BRIGHT_YELLOW_CENTER));
                    }
                    else
                    {
                        dominator = (Npc)Spawn(DOMINATOR_MOB_ID, 515.23114f, 487.94998f, 87.176056f, (byte)60);
                        AdaptNPC(dominator, rank);
                        dominator.GetAggroList().AddHate(player, 1000);
                        PacketSendUtility.BroadcastToMap(instance, new SM_MESSAGE(0, null, "A vile protector has appeared!", ChatType.BRIGHT_YELLOW_CENTER));
                    }
                    return ValueTask.CompletedTask;
                }, TimeSpan.FromMilliseconds(100000), TimeSpan.FromMilliseconds(60000));
            }
        }
        return false;
    }

    public override void OnDie(Npc npc)
    {
        switch (npc.GetNpcId())
        {
            case TRASH_MOB_ID:
            case BULKY_MOB_ID:
            case DOMINATOR_MOB_ID:
                npc.GetController().Delete();
                break;
            case CENTER_ARTIFACT_ID:
                {
                    CancelAllTasks();
                    // use pcd for rare cases in which the artifact got destroyed by dots/servants and the player got DC'ed before
                    PlayerCommonData pcd = PlayerService.GetOrLoadPlayerCommonData(playerObjId);
                    float usedTime = (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - Interlocked.Read(ref startTime)) / 1000f;
                    achievedDps = (int)Math.Floor(npc.GetLifeStats().GetMaxHp() / usedTime + 0.5f);
                    log.LogInformation(string.Format(
                        "[CI_ROAH] Player [id={0}, name={1}, class={2}, rank={3}({4}), isPrebuffed={5}] succeeded in destroying the artifact in {6:F3}s (DPS:{7}).",
                        pcd.GetPlayerObjId(), pcd.GetName(), pcd.GetPlayerClass(), CustomInstanceRankEnumExtensions.GetRankDescription(rank), rank, isPrebuffed, usedTime,
                        achievedDps));
                    DeleteAliveNpcs(TRASH_MOB_ID, BULKY_MOB_ID, DOMINATOR_MOB_ID);
                    PacketSendUtility.BroadcastToMap(instance,
                        new SM_MESSAGE(0, null, "You feel a shadowy presence from the throne room.", ChatType.BRIGHT_YELLOW_CENTER));
                    PacketSendUtility.BroadcastToMap(instance, new SM_QUEST_ACTION(0, 0));
                    int bossID = BOSS_MOB_AT_ID;
                    if (pcd.GetPlayerClass() != PlayerClass.RIDER)
                    {
                        switch (pcd.GetRace())
                        {
                            case Race.ASMODIANS:
                                bossID = pcd.GetGender() switch
                                {
                                    Gender.MALE => BOSS_MOB_A_M_ID,
                                    Gender.FEMALE => BOSS_MOB_A_F_ID,
                                    _ => bossID,
                                };
                                break;
                            case Race.ELYOS:
                                bossID = pcd.GetGender() switch
                                {
                                    Gender.MALE => BOSS_MOB_E_M_ID,
                                    Gender.FEMALE => BOSS_MOB_E_F_ID,
                                    _ => bossID,
                                };
                                break;
                        }
                    }
                    Spawn(bossID, 503.96774f, 631.935f, 104.548775f, (byte)90); // spawn bossMob
                    isBossPhase = true;
                    npc.GetController().Delete();
                }
                break;
            case BOSS_MOB_A_M_ID:
            case BOSS_MOB_A_F_ID:
            case BOSS_MOB_E_M_ID:
            case BOSS_MOB_E_F_ID:
            case BOSS_MOB_AT_ID:
                {
                    CalcBossDrop(npc.GetObjectId());
                    SetResult(true);
                    PacketSendUtility.BroadcastToMap(instance, new SM_QUEST_ACTION(0, 0));
                }
                break;
        }
    }

    public override void OnEnterInstance(Player player)
    {
        if (Interlocked.CompareExchange(ref isInitialized, 1, 0) == 0)
        {
            isPrebuffed = CheckForPrebuffOnEntry(player); // check for Word of Wind and Symphony of Destruction
            SetAverageDpsByPlayerClass(player.GetPlayerClass());
            playerObjId = instance.GetRegisteredObjects().First();
            rank = CustomInstanceService.GetInstance().LoadOrCreateRank(playerObjId).GetRank();
            PacketSendUtility.BroadcastToMap(instance, new SM_MESSAGE(0, null,
                "Welcome to the 'Eternal Challenge', " + CustomInstanceRankEnumExtensions.GetRankDescription(rank) + " challenger!", ChatType.BRIGHT_YELLOW_CENTER));
            SpawnUnlockableNpcs(player);
            // train player model from previous boss runs
            skillSet = PlayerModelController.GetSkillSetForPlayer(playerObjId);
            model = PlayerModelController.TrainModelForPlayer(playerObjId, skillSet);
        }
    }

    private bool CheckForPrebuffOnEntry(Player player)
    {
        return player.GetEffectController().FindBySkillId(1810) != null || player.GetEffectController().FindBySkillId(4469) != null;
    }

    private void SetAverageDpsByPlayerClass(PlayerClass playerClass)
    {
        avgClassDps = playerClass switch
        {
            PlayerClass.TEMPLAR or PlayerClass.CHANTER => 2500,
            PlayerClass.GUNNER => 4500,
            PlayerClass.SORCERER or PlayerClass.ASSASSIN => 5000,
            PlayerClass.SPIRIT_MASTER => 6000,
            _ => 4000,
        };
    }

    private void CalcBossDrop(int npcObjId)
    {
        ISet<DropItem> dropItems = DropRegistrationService.GetInstance().GetCurrentDropMap().GetValueOrDefault(npcObjId);
        if (dropItems != null) // dropItems can possibly be null if the player died right before the boss
        {
            dropItems.Clear();
            dropItems.Add(DropRegistrationService.GetInstance().RegDropItem(0, playerObjId, npcObjId, CustomInstanceService.REWARD_COIN_ID, GetRewardCoinAmount(rank)));

            if (EventService.GetInstance().IsEventActive("Ice Festival"))
                dropItems.Add(DropRegistrationService.GetInstance().RegDropItem(0, playerObjId, npcObjId, 182215803, 1));
        }
    }

    private int GetRewardCoinAmount(int rank)
    {
        CustomInstanceRankEnum rankEnum = CustomInstanceRankEnumExtensions.GetByRank(rank);
        if (rankEnum != CustomInstanceRankEnum.ANCIENT_PLUS)
            return rankEnum.GetMinReward();
        else
            return (int)(rankEnum.GetMinReward() + (rank - rankEnum.GetMinRank()) * REWARD_SCALE);
    }

    private void AdaptNPC(Npc npc, int rank)
    {
        List<StatSetFunction> functions = new List<StatSetFunction>();

        switch (npc.GetNpcId()) // IRON ... ANCIENT+
        {
            case CENTER_ARTIFACT_ID: // ~5 ... 10min
                {
                    int maxHP = 300 * avgClassDps + rank * 10 * avgClassDps;
                    if (rank > CustomInstanceRankEnum.ANCIENT.GetMinRank())
                        maxHP += (rank - CustomInstanceRankEnum.ANCIENT.GetMinRank()) * 150000;
                    functions.Add(new StatSetFunction(StatEnum.MAXHP, maxHP));
                }
                break;
            case TRASH_MOB_ID: // ~1s fix (AoE-able)
                functions.Add(new StatSetFunction(StatEnum.MAXHP, 1));
                functions.Add(new StatSetFunction(StatEnum.SPEED, 6000 + rank * 100));
                break;
            case BULKY_MOB_ID: // ~10 ... 25s
                functions.Add(new StatSetFunction(StatEnum.MAXHP, 10 * avgClassDps + rank * avgClassDps / 2));
                break;
            case DOMINATOR_MOB_ID: // ~10s fix
                functions.Add(new StatSetFunction(StatEnum.MAXHP, 10 * avgClassDps));
                functions.Add(new StatSetFunction(StatEnum.PHYSICAL_ATTACK, 0)); // only debuffs, no dmg
                functions.Add(new StatSetFunction(StatEnum.ATTACK_SPEED, 2000 - rank * 30));
                break;
        }

        npc.GetGameStats().AddEffect(null, functions);
        npc.GetLifeStats().SetCurrentHp(npc.GetLifeStats().GetMaxHp());
    }

    private void SetResult(bool success)
    {
        CancelAllTasks();
        if (Interlocked.CompareExchange(ref isCompleted, 1, 0) == 0)
        {
            int oldRank = rank;
            Player player = instance.GetPlayer(playerObjId);
            if (success)
            {
                rank++;
                PacketSendUtility.BroadcastToMap(instance,
                    new SM_MESSAGE(0, null, "Your rank has been increased to " + CustomInstanceRankEnumExtensions.GetRankDescription(rank)
                        + ". Stay steadfast for tougher challenges and higher rewards!", ChatType.BRIGHT_YELLOW_CENTER));
            }
            else
            {
                rank = Math.Max(0, --rank);

                if (player != null)
                {
                    PacketSendUtility.SendPacket(player, new SM_MESSAGE(0, null,
                        "Your rank has been decreased to " + CustomInstanceRankEnumExtensions.GetRankDescription(rank) + ".", ChatType.BRIGHT_YELLOW_CENTER));

                    if (rank > 0) // prevent suicide abuse || loss rewards
                        ItemService.AddItem(player, CustomInstanceService.REWARD_COIN_ID, GetRewardCoinAmount(oldRank), true);
                }
            }

            CustomInstanceService.GetInstance().SaveNewPlayerModelEntries(playerObjId);
            if (CustomInstanceService.GetInstance().ChangePlayerRank(playerObjId, rank, achievedDps))
            {
                string name = player != null ? player.GetName() : PlayerDAO.GetPlayerNameByObjId(playerObjId);
                log.LogInformation(string.Format("[CI_ROAH] Rank changed for Player [id={0}, name={1}, oldRank={2}({3}), newRank={4}({5})]", playerObjId, name,
                    CustomInstanceRankEnumExtensions.GetRankDescription(oldRank), oldRank, CustomInstanceRankEnumExtensions.GetRankDescription(rank), rank));
            }
        }
    }

    private void CancelAllTasks()
    {
        if (despawnTask != null)
            despawnTask.Cancel();
        if (trashMobSpawnTask != null)
            trashMobSpawnTask.Cancel();
        if (bulkyMobSpawnTask != null)
            bulkyMobSpawnTask.Cancel();
        if (dominatorMobSpawnTask != null)
            dominatorMobSpawnTask.Cancel();
    }

    private void SpawnUnlockableNpcs(Player player)
    {
        if (rank >= CustomInstanceRankEnum.BRONZE.GetMinRank()) // + General Goods Merchant
            if (player.GetRace() == Race.ASMODIANS)
                Spawn(205848, 510.84058f, 407.1751f, 93.91156f, (byte)87);
            else
                Spawn(205826, 510.84058f, 407.1751f, 93.91156f, (byte)87);

        if (rank >= CustomInstanceRankEnum.SILVER.GetMinRank()) // + Warehouse Manager
            Spawn(205711, 496.6516f, 407.25f, 93.85523f, (byte)99);

        if (rank >= CustomInstanceRankEnum.GOLD.GetMinRank()) // + Legion Warehouse
            if (player.GetRace() == Race.ASMODIANS)
                Spawn(805253, 494.753f, 405.53464f, 93.87636f, (byte)104);
            else
                Spawn(805245, 494.753f, 405.53464f, 93.87636f, (byte)104);

        if (rank >= CustomInstanceRankEnum.PLATINUM.GetMinRank()) // + Mail
            if (player.GetRace() == Race.ASMODIANS)
                Spawn(700079, 499.6871f, 406.73828f, 93.84634f, (byte)91);
            else
                Spawn(700000, 499.6871f, 406.73828f, 93.84634f, (byte)91);

        if (rank >= CustomInstanceRankEnum.MITHRIL.GetMinRank()) // + Soul Healer
            Spawn(205727, 513.6179f, 405.60223f, 93.91156f, (byte)82);

        if (rank >= CustomInstanceRankEnum.CERANIUM.GetMinRank()) // + Trade Broker
            if (player.GetRace() == Race.ASMODIANS)
                Spawn(205853, 516.24396f, 402.30844f, 93.91156f, (byte)68);
            else
                Spawn(798912, 516.24396f, 402.30844f, 93.91156f, (byte)68);

        if (rank >= CustomInstanceRankEnum.ANCIENT.GetMinRank()) // + Stigma Master
            if (player.GetRace() == Race.ASMODIANS)
                Spawn(800765, 493.39554f, 403.7841f, 93.88781f, (byte)106);
            else
                Spawn(800760, 493.39554f, 403.7841f, 93.88781f, (byte)106);

        if (rank >= CustomInstanceRankEnum.ANCIENT_PLUS.GetMinRank()) // + AP Shugo
            Spawn(804833, 508.1798f, 407.87378f, 93.8523f, (byte)89);
    }

    private int GetElapsedTimeMillis()
    {
        return (int)(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - Interlocked.Read(ref startTime));
    }

    public override bool OnReviveEvent(Player player)
    {
        PacketSendUtility.SendPacket(player, new SM_QUEST_ACTION(0, TIME_LIMIT * 1000 - GetElapsedTimeMillis()));
        return base.OnReviveEvent(player);
    }

    public override bool OnDie(Player player, Creature lastAttacker)
    {
        if (Volatile.Read(ref isCompleted) == 0)
        {
            if (isBossPhase)
            {
                PacketSendUtility.SendMessage(player, "At last! I have become .. your greatest nightmare!", ChatType.BRIGHT_YELLOW_CENTER);
                DeleteAliveNpcs(BOSS_MOB_A_M_ID, BOSS_MOB_A_F_ID, BOSS_MOB_E_M_ID, BOSS_MOB_E_F_ID, BOSS_MOB_AT_ID);
            }
            else
            {
                PacketSendUtility.SendMessage(player, "You shall not pass!", ChatType.BRIGHT_YELLOW_CENTER);
                DeleteAliveNpcs(CENTER_ARTIFACT_ID, TRASH_MOB_ID, BULKY_MOB_ID, DOMINATOR_MOB_ID);
            }
            SetResult(false);
        }
        return true;
    }

    public override void OnEndCastSkill(Skill skill)
    {
        if (isBossPhase && skill.GetEffector() is Player effector && instance.IsRegistered(effector.GetObjectId()))
            CustomInstanceService.GetInstance().RecordPlayerModelEntry(effector, skill, effector.GetTarget());
    }

    public PlayerModel GetPlayerModel()
    {
        return model;
    }

    public List<int> GetSkillSet()
    {
        return skillSet;
    }

    public override bool AllowSelfReviveBySkill()
    {
        return false;
    }

    public override bool AllowSelfReviveByItem()
    {
        return false;
    }

    public override bool AllowInstanceRevive()
    {
        return false;
    }
}
