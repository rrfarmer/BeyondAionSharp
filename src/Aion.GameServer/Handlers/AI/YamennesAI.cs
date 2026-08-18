using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Poll;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils;
using Aion.GameServer.World;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// Java parity: ai/instance/abyssal_splinter/YamennesAI (@author Ritsu, Luzien).
/// </summary>
[AIName("yamennes")]
public class YamennesAI : AggressiveNpcAI
{
    private ScheduledTask? portalTask;
    private ScheduledTask? enrageTask;
    private ScheduledTask? golemTask;
    private readonly AtomicBoolean isStart = new AtomicBoolean();

    public YamennesAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleAttack(Creature creature)
    {
        base.HandleAttack(creature);
        if (isStart.CompareAndSet(false, true))
        {
            PacketSendUtility.BroadcastMessage(GetOwner(), 1500013); // Those who threaten the artefact shall be returned to the flow of Aether!
            StartTasks();
        }
    }

    private void StartTasks()
    {
        enrageTask = ThreadPoolManager.GetInstance().Schedule(_ => { GetOwner().QueueSkill(19098, 55); return ValueTask.CompletedTask; }, 600000L);
        golemTask = ThreadPoolManager.GetInstance().ScheduleAtFixedRateTask(
            _ => { SpawnGolems(); return ValueTask.CompletedTask; },
            System.TimeSpan.FromMilliseconds(GolemCycleMillis),
            System.TimeSpan.FromMilliseconds(GolemCycleMillis));
        portalTask = ThreadPoolManager.GetInstance().Schedule(_ => { SpawnPortals(false); return ValueTask.CompletedTask; }, 60000L);
    }

    /// <summary>
    /// Retail <c>IDAbRe_Core_NamedD_Hard</c>: the portals carry <c>live_time</c> 70 on a timer re-armed
    /// at 70 seconds, so a set expires exactly as the next arrives. Ours waited a flat 60.
    /// </summary>
    private const int PortalLife = 70;
    private const int PortalCycleMillis = 70000;

    /// <summary>
    /// Retail gives the ametgolems three minutes on a timer of their own.
    /// </summary>
    /// <remarks>
    /// <b>Ours are still driven by the healing-debuff chain rather than by retail's independent
    /// 180-second timer</b>, and are cleared explicitly at the start of each debuff, so this lifetime
    /// rarely fires. It is set anyway because the explicit clear is not a lifetime -- if the chain ever
    /// stops, the golems standing at that moment would otherwise stay forever. <b>The cadence itself is
    /// a known structural divergence and is not fixed here.</b>
    /// </remarks>
    private const int GolemLife = 180;

    /// <summary>Retail re-arms the golem branch's own timer at three minutes.</summary>
    private const long GolemCycleMillis = 180_000L;

    /// <summary>
    /// Retail's three marks for the ametgolems, from <c>IDAbRe_Core_NamedD_Hard</c>.
    /// </summary>
    /// <remarks>
    /// <b>They are absolute in retail and were relative here</b> — this class placed them at ten metres
    /// diagonally off Yamennes, so they followed him around the room instead of standing where the fight
    /// expects them.
    /// </remarks>
    private static readonly (float X, float Y, float Z)[] GolemMarks =
    [
        (361.53f, 741.54f, 198.31f),
        (302.85f, 735.30f, 198.15f),
        (334.30f, 709.31f, 198.81f),
    ];

    /// <summary>
    /// Retail hangs the golems off a timer of their own, re-armed at three minutes, and lets each expire
    /// on its own three-minute <c>live_time</c>.
    /// </summary>
    /// <remarks>
    /// <b>This class drove them from the healing-debuff chain instead</b>, clearing the previous three at
    /// the start of every debuff — so their cadence was the debuff's, not theirs, and the lifetime added
    /// in an earlier pass almost never fired because the explicit clear got there first. That divergence
    /// was recorded at the time and left standing; this is it corrected.
    /// </remarks>
    private void SpawnGolems()
    {
        if (IsDead() || !GetOwner().IsSpawned())
            return;

        foreach ((float x, float y, float z) in GolemMarks)
            SpawnFor(282107, x, y, z, 0, GolemLife);
    }

    private void OnHealingDebuff()
    {
        WorldMapInstance instance = GetPosition().GetWorldMapInstance();
        GetOwner().QueueSkill(19282, 55);
        GetOwner().ClearAttackedCount();
        PacketSendUtility.BroadcastToMap(GetOwner(), SM_SYSTEM_MESSAGE.STR_MSG_IDAbRe_Core_NmdD_ResetAggro());
    }

    private void SpawnPortals(bool isTopSpawn)
    {
        // Retail spawns unconditionally and lets the portals time out. This used to spawn only when
        // none of the three were still standing and gave them no lifetime at all, so a group that
        // ignored the portals rather than killing them saw the first wave and never another -- the same
        // shape the unstable variant was corrected for, and this class kept.
        PacketSendUtility.BroadcastToMap(GetOwner(), SM_SYSTEM_MESSAGE.STR_MSG_IDAbRe_Core_NmdD_SummonStart());
        if (isTopSpawn)
        {
            SpawnFor(282014, 288.10f, 741.95f, 216.81f, (sbyte)3, PortalLife);
            SpawnFor(282015, 375.05f, 750.67f, 216.82f, (sbyte)59, PortalLife);
            SpawnFor(282131, 341.33f, 699.38f, 216.86f, (sbyte)59, PortalLife);
        }
        else
        {
            SpawnFor(282014, 303.69f, 736.35f, 198.7f, (sbyte)0, PortalLife);
            SpawnFor(282015, 335.19f, 708.92f, 198.9f, (sbyte)35, PortalLife);
            SpawnFor(282131, 360.23f, 741.07f, 198.7f, (sbyte)0, PortalLife);
        }
        ThreadPoolManager.GetInstance().Schedule(_ => { OnHealingDebuff(); return ValueTask.CompletedTask; }, 3000L);
        portalTask = ThreadPoolManager.GetInstance().Schedule(_ => { SpawnPortals(!isTopSpawn); return ValueTask.CompletedTask; }, PortalCycleMillis);
    }

    private void DeleteNpcs(List<Npc> npcs)
    {
        npcs.Where(n => n != null).ToList().ForEach(n => n.GetController().Delete());
    }

    private void CancelTasks()
    {
        if (portalTask != null && !portalTask.IsDone())
            portalTask.Cancel(true);
        if (enrageTask != null && !enrageTask.IsDone())
            enrageTask.Cancel(true);
        // The golem clock repeats, so it has to be cancelled and not merely guarded: a repeating task
        // that only checks IsDead() keeps running forever, which is what StopsEveryTimerWhenItDies was
        // written for on Stormwing.
        if (golemTask != null && !golemTask.IsDone())
            golemTask.Cancel(true);
    }

    protected override void HandleBackHome()
    {
        CancelTasks();
        base.HandleBackHome();
        GetOwner().GetController().Delete();
    }

    protected override void HandleDespawned()
    {
        CancelTasks();
        DeleteNpcs(GetPosition().GetWorldMapInstance().GetNpcs(282107));
        base.HandleDespawned();
    }

    protected override void HandleDied()
    {
        CancelTasks();
        DeleteNpcs(GetPosition().GetWorldMapInstance().GetNpcs(282107));
        base.HandleDied();
    }

    public override bool Ask(AIQuestion question)
    {
        return question switch
        {
            AIQuestion.REWARD_LOOT or AIQuestion.REWARD_AP => false,
            _ => base.Ask(question),
        };
    }
}
