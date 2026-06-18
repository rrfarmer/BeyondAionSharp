using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Aion.GameServer.Instance.Handlers;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils;
using Aion.GameServer.World;
using Rnd = Aion.GameServer.Commons.Utils.Rnd;

namespace Aion.GameServer.Handlers.Instance;

/// <summary>
/// Java parity: instance/abyss/AbstractInnerUpperAbyssInstance (Estrayl, AION 4.8) : GeneralInstanceHandler.
/// Basic handler for all versions of the siege-keep instances in the inner upper abyss. Base for Krotan/Kysis/Miren
/// Barracks+Chamber. AtomicBoolean/AtomicInteger→Utils; Future→ScheduledTask; scheduleAtFixedRate→ScheduleAtFixedRateTask
/// (Func+TimeSpan idiom); AbyssPointsService fully-qualified Services.Abyss. 1:1.
/// </summary>
public abstract class AbstractInnerUpperAbyssInstance : GeneralInstanceHandler
{
    /// <summary>
    /// All final spawn locations behind the end boss. Currently it is only a safety mechanism due to players can jump through doors.
    /// So it can be removed if this issue will be fixed in future.
    /// </summary>
    private List<WorldPosition> chestLocations = new List<WorldPosition>();

    /// <summary>For final fight trigger</summary>
    private readonly AtomicBoolean isFlyringPassed = new AtomicBoolean();

    /// <summary>
    /// The instance will start in hard mode and can be switched to an easier version due the killing of the artifact in the
    /// room in front of the end boss (Only available in legion and level 40+ version).
    /// </summary>
    private readonly AtomicBoolean isEasyMode = new AtomicBoolean();

    /// <summary>
    /// Needs to count passed time to reduce chest spawns. This is currently a custom feature to bring difficulty. On retail
    /// it is not necessary to be fast to get all chests.
    /// </summary>
    private readonly AtomicInteger failCounter = new AtomicInteger();

    private ScheduledTask chestReductionTask;

    public AbstractInnerUpperAbyssInstance(WorldMapInstance instance) : base(instance)
    {
    }

    /// <summary>the awakened boss id</summary>
    protected abstract int GetBossId();

    /// <summary>the ordinary chest id</summary>
    protected abstract int GetChestId();

    /// <summary>the id of the door NPC</summary>
    protected abstract int GetDoorId();

    /// <summary>the smallest id of the four key masters</summary>
    protected abstract int GetKeymasterId();

    /// <summary>the id of the invisible NPC which is used to trigger the final timer event</summary>
    protected abstract int GetTimerNpcId();

    public override void OnInstanceCreate()
    {
        Spawn(GetKeymasterId() + Rnd.NextInt(4), 527.64f, 212.0511f, 178.4134f, (byte)90);

        chestLocations.Add(new WorldPosition(mapId, 575.664f, 853.248f, 199.3737f, (byte)63));
        chestLocations.Add(new WorldPosition(mapId, 571.560f, 869.936f, 199.3737f, (byte)69));
        chestLocations.Add(new WorldPosition(mapId, 560.082f, 882.979f, 199.3737f, (byte)76));
        chestLocations.Add(new WorldPosition(mapId, 545.404f, 892.116f, 199.3737f, (byte)83));
        chestLocations.Add(new WorldPosition(mapId, 528.269f, 895.106f, 199.3737f, (byte)89));
        chestLocations.Add(new WorldPosition(mapId, 511.364f, 891.941f, 199.3737f, (byte)96));
        chestLocations.Add(new WorldPosition(mapId, 496.222f, 883.099f, 199.3737f, (byte)103));
        chestLocations.Add(new WorldPosition(mapId, 484.933f, 869.897f, 199.3747f, (byte)109));
        chestLocations.Add(new WorldPosition(mapId, 479.122f, 853.576f, 199.3727f, (byte)116));
        chestLocations.Add(new WorldPosition(mapId, 478.966f, 836.407f, 199.3737f, (byte)2));
        chestLocations.Add(new WorldPosition(mapId, 485.365f, 820.350f, 199.4596f, (byte)9));
        chestLocations.Add(new WorldPosition(mapId, 576.463f, 837.337f, 199.7000f, (byte)56)); // Bonus Chest
    }

    public override void OnCreatureDetected(Npc detector, Creature detected)
    {
        if (detector.GetNpcId() == GetTimerNpcId() && detected is Player && isFlyringPassed.CompareAndSet(false, true))
        {
            chestReductionTask = ThreadPoolManager.GetInstance().ScheduleAtFixedRateTask(_ =>
            {
                if (failCounter.IncrementAndGet() >= 11)
                {
                    DeleteAliveNpcs(GetBossId());
                    if (chestReductionTask != null && !chestReductionTask.IsCancelled)
                        chestReductionTask.Cancel(true);
                }
                return ValueTask.CompletedTask;
            }, TimeSpan.FromMilliseconds(300000), TimeSpan.FromMilliseconds(30000));

            PacketSendUtility.BroadcastToMap(instance, SM_SYSTEM_MESSAGE.STR_MSG_INSTANCE_START_IDABRE());
            PacketSendUtility.BroadcastToMap(instance, new SM_QUEST_ACTION(0, 600));
            detector.GetController().Delete();
        }
    }

    /// <summary>Called to delete static door NPCs.</summary>
    /// <param name="staticId">of the door NPC to be deleted</param>
    protected void OpenDoor(int staticId)
    {
        foreach (Npc n in instance.GetNpcs(GetDoorId()).Where(n => n.GetSpawn().GetStaticId() == staticId))
            n.GetController().Delete();
    }

    /// <summary>Called after destruction of the artifact to replace the boss with his weaker version.</summary>
    protected void SwitchToEasyMode()
    {
        if (isEasyMode.CompareAndSet(false, true))
        {
            Npc boss = GetNpc(GetBossId());
            if (boss != null && !boss.IsDead())
            {
                Spawn(GetBossId() - 1, boss.GetX(), boss.GetY(), boss.GetZ(), boss.GetHeading());
                boss.GetController().Delete();
            }
        }
    }

    /// <summary>
    /// Called after death of the end boss to interrupts the time task and spawns the reward chests depending on the past time.
    /// </summary>
    protected void SpawnChests()
    {
        if (chestReductionTask != null && !chestReductionTask.IsCancelled)
            chestReductionTask.Cancel(true);

        int chestCount = 11 - failCounter.Get();
        for (int i = 0; i < chestCount; i++)
        {
            WorldPosition chestLoc = chestLocations[i];
            Spawn(GetChestId(), chestLoc.GetX(), chestLoc.GetY(), chestLoc.GetZ(), chestLoc.GetHeading());
        }
        if (!isEasyMode.Get())
        {
            WorldPosition bChestLoc = chestLocations[chestLocations.Count - 1];
            Spawn(GetChestId() + 1, bChestLoc.GetX(), bChestLoc.GetY(), bChestLoc.GetZ(), bChestLoc.GetHeading());
        }
        chestLocations.Clear();
        chestLocations = null;
    }

    /// <summary>Called after destruction of statues to distribute a certain amount of ap.</summary>
    /// <param name="ap">the amount of abyss points the destroyed statue should reward</param>
    protected virtual void RewardStatueKill(int ap)
    {
        int rewardAp = ap / instance.GetPlayerCount();
        instance.ForEachPlayer(p => Aion.GameServer.Services.Abyss.AbyssPointsService.AddAp(p, rewardAp));
    }

    public override bool IsBoss(Npc npc)
    {
        return npc.GetNpcId() == GetBossId();
    }
}
