using Aion.GameServer.Ai;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/illuminaryObelisk/DynatoumAI (@author Estrayl).</summary>
[AIName("dynatoum")]
public class DynatoumAI : SummonerAI
{
    private readonly AtomicBoolean isStarted = new AtomicBoolean();
    private readonly AtomicBoolean areEntriesRemoved = new AtomicBoolean();
    protected ScheduledTask? despawnTask;

    public DynatoumAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleAttack(Creature creature)
    {
        base.HandleAttack(creature);
        if (isStarted.CompareAndSet(false, true))
            ScheduleDespawn(1);
        if (GetLifeStats().GetHpPercentage() <= 70 && areEntriesRemoved.CompareAndSet(false, true))
            RemoveBossEntries();
    }

    protected void RemoveBossEntries()
    {
        PacketSendUtility.BroadcastToMap(GetOwner(), SM_SYSTEM_MESSAGE.STR_MSG_IDF5_U3_BOSS_PORTAL_DESTROY());
        foreach (Npc p in GetPosition().GetWorldMapInstance().GetNpcs(702216))
            if (p != null)
                p.GetController().Delete();
    }

    protected void ScheduleDespawn(int delayInSec)
    {
        despawnTask = ThreadPoolManager.GetInstance().Schedule(_ =>
        {
            if (!IsDead())
            {
                switch (delayInSec)
                {
                    case 1:
                        PacketSendUtility.BroadcastToMap(GetOwner(), SM_SYSTEM_MESSAGE.STR_MSG_IDF5_U3_BOSS_TIMER_01());
                        ScheduleDespawn(300);
                        break;
                    case 300:
                        PacketSendUtility.BroadcastToMap(GetOwner(), SM_SYSTEM_MESSAGE.STR_MSG_IDF5_U3_BOSS_TIMER_02());
                        ScheduleDespawn(240);
                        break;
                    case 240:
                        PacketSendUtility.BroadcastToMap(GetOwner(), SM_SYSTEM_MESSAGE.STR_MSG_IDF5_U3_BOSS_TIMER_03());
                        ScheduleDespawn(60);
                        break;
                    case 60:
                        GetOwner().QueueSkill(21534, 1, 3000);
                        break;
                }
            }
            return System.Threading.Tasks.ValueTask.CompletedTask;
        }, (long)delayInSec * 1000);
    }

    public override void OnEndUseSkill(SkillTemplate skillTemplate, int skillLevel)
    {
        switch (skillTemplate.GetSkillId())
        {
            case 21534:
                GetOwner().GetController().Delete();
                PacketSendUtility.BroadcastToMap(GetOwner(), SM_SYSTEM_MESSAGE.STR_MSG_IDF5_U3_BOSS_TIMER_04());
                break;
        }
    }

    protected void CancelDespawnTask()
    {
        if (despawnTask != null && !despawnTask.IsCancelled)
            despawnTask.Cancel(true);
    }

    protected override void HandleDespawned()
    {
        CancelDespawnTask();
        base.HandleDespawned();
    }

    protected override void HandleDied()
    {
        CancelDespawnTask();
        base.HandleDied();
    }

    protected override void HandleBackHome()
    {
        base.HandleBackHome();
    }
}
