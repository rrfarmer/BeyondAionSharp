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

    private void OnHealingDebuff()
    {
        WorldMapInstance instance = GetPosition().GetWorldMapInstance();
        DeleteNpcs(instance.GetNpcs(282107));
        GetOwner().QueueSkill(19282, 55);
        SpawnFor(282107, GetOwner().GetX() + 10, GetOwner().GetY() - 10, GetOwner().GetZ(), (sbyte)0, GolemLife);
        SpawnFor(282107, GetOwner().GetX() - 10, GetOwner().GetY() + 10, GetOwner().GetZ(), (sbyte)0, GolemLife);
        SpawnFor(282107, GetOwner().GetX() + 10, GetOwner().GetY() + 10, GetOwner().GetZ(), (sbyte)0, GolemLife);
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
