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
/// Java parity: ai/instance/unstableSplinterpath/UnstableYamennesAI (@author Ritsu, Luzien, Cheatkiller),
/// with the portal cadence corrected against retail patterns <c>IDAbRe_Core_NamedD_02</c> and
/// <c>IDAbRe_Core_NamedD_Hard_02</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. The alternating portals were already right in shape
/// — upstairs, then downstairs, then upstairs — because retail builds that alternation out of one flag
/// var toggled by two branches, and this class had arrived at the same behaviour independently. Only
/// the timing was off: retail arms the portal timer at <b>30s</b> and re-arms it at <b>65s</b>, where
/// this waited a flat 60 both times.
/// <para>
/// The gate npc_ids are deliberately left alone. Retail's patterns name 283203/283222/283223 upstairs
/// and 283233 downstairs, and those look like missing adds — but they bind to the same retail patterns
/// as the 219567/219579/219580 this class already spawns, and only ours carry the
/// <c>unstableyamenessportal</c> AI that makes a gate do anything. Swapping to the ids the pattern
/// names would replace working portals with inert scenery.
/// </para>
/// </remarks>
[AIName("unstableyamennes")]
public class UnstableYamennesAI : AggressiveNpcAI
{
    /// <summary>Retail's battle timer 1: armed at 30s on entering combat, re-armed at 65s.</summary>
    private const long FirstPortalMillis = 30000L;
    private const long PortalIntervalMillis = 65000L;

    /// <summary>Retail's <c>live_time</c> on each gate.</summary>
    private const long GateLifeMillis = 70000L;

    private ScheduledTask? portalTask;
    private ScheduledTask? enrageTask;
    private readonly AtomicBoolean isStart = new AtomicBoolean();

    public UnstableYamennesAI(Npc owner)
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
        enrageTask = ThreadPoolManager.GetInstance().Schedule(_ => { GetOwner().QueueSkill(19098, 55, 0); return ValueTask.CompletedTask; }, 600000L);
        portalTask = ThreadPoolManager.GetInstance().Schedule(_ => { SpawnPortals(false); return ValueTask.CompletedTask; }, FirstPortalMillis);
    }

    private void OnHealingDebuff()
    {
        WorldMapInstance instance = GetPosition().GetWorldMapInstance();
        DeleteNpcs(instance.GetNpcs(219586));
        GetOwner().QueueSkill(19282, 55);
        Spawn(219586, GetOwner().GetX() + 10, GetOwner().GetY() - 10, GetOwner().GetZ(), (sbyte)0);
        Spawn(219586, GetOwner().GetX() - 10, GetOwner().GetY() + 10, GetOwner().GetZ(), (sbyte)0);
        Spawn(219586, GetOwner().GetX() + 10, GetOwner().GetY() + 10, GetOwner().GetZ(), (sbyte)0);
        GetOwner().ClearAttackedCount();
        PacketSendUtility.BroadcastToMap(GetOwner(), SM_SYSTEM_MESSAGE.STR_MSG_IDAbRe_Core_NmdD_ResetAggro());
    }

    private void SpawnPortals(bool isTopSpawn)
    {
        PacketSendUtility.BroadcastToMap(GetOwner(), SM_SYSTEM_MESSAGE.STR_MSG_IDAbRe_Core_NmdD_SummonStart());
        if (isTopSpawn)
        {
            OpenGate(219567, 288.10f, 741.95f, 216.81f, 3);
            OpenGate(219579, 375.05f, 750.67f, 216.82f, 59);
            OpenGate(219580, 341.33f, 699.38f, 216.86f, 59);
        }
        else
        {
            OpenGate(219567, 303.69f, 736.35f, 198.7f, 0);
            OpenGate(219579, 335.19f, 708.92f, 198.9f, 35);
            OpenGate(219580, 360.23f, 741.07f, 198.7f, 0);
        }
        ThreadPoolManager.GetInstance().Schedule(_ => { OnHealingDebuff(); return ValueTask.CompletedTask; }, 3000L);
        portalTask = ThreadPoolManager.GetInstance().Schedule(_ => { SpawnPortals(!isTopSpawn); return ValueTask.CompletedTask; }, PortalIntervalMillis);
    }

    /// <summary>
    /// Opens one gate, which closes on its own after seventy seconds.
    /// </summary>
    /// <remarks>
    /// The lifetime is retail's (<c>live_time=70</c>) and it is what makes the alternation work. This
    /// used to spawn a wave only when none of the three gates were still standing, and gave them no
    /// lifetime at all — so a group that ignored the portals rather than killing them saw the first
    /// wave and never another. Retail spawns unconditionally and lets the gates time out, which
    /// bounds them at two overlapping sets for the five seconds the 70s life exceeds the 65s cycle.
    /// </remarks>
    private void OpenGate(int npcId, float x, float y, float z, sbyte heading)
    {
        if (Spawn(npcId, x, y, z, heading) is not Npc gate)
            return;

        ThreadPoolManager.GetInstance().Schedule(_ =>
        {
            gate.GetController().DeleteIfAliveOrCancelRespawn();
            return ValueTask.CompletedTask;
        }, GateLifeMillis);
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
        DeleteNpcs(GetPosition().GetWorldMapInstance().GetNpcs(219586));
        base.HandleDespawned();
    }

    protected override void HandleDied()
    {
        CancelTasks();
        DeleteNpcs(GetPosition().GetWorldMapInstance().GetNpcs(219586));
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
