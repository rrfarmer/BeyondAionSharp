using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Commons.Utils;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;
using Aion.GameServer.Model.Animations;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Templates.Spawns;
using Aion.GameServer.Services.Teleport;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// The conquest offering portals (833018, 833021). Retail patterns
/// <c>LF4_Rotation_Dinamic_Portal</c> and <c>DF4_Rotation_Dinamic_Portal</c>.
/// </summary>
/// <remarks>
/// Java parity: ai/ConquestOfferingPortalAI (Yeats, Sykra). Retail-sourced correction below; see
/// docs/retail-ai-fidelity.md. Found by <c>audit_timer_drift.py</c>.
/// <para>
/// <b>It closed after sixty-five seconds and retail gives it three minutes.</b> The portal's whole
/// pattern is two rungs: <c>on_wake_up</c> sets an idle timer of <b>180000</b>, and
/// <c>on_idle_timer</c> is a bare <c>despawn_self</c>. The rotation monster that drops it spawns it
/// with <c>live_time=0</c> — permanent — so the three minutes are the portal's own and the only clock
/// on it.
/// </para>
/// <para>
/// Sixty-five seconds is a third of that. A portal left by a conquest kill is meant to be something a
/// group can finish the fight, loot, and then walk into; at sixty-five it is gone before most of that.
/// </para>
/// <para>
/// <b>Not translated:</b> retail's <c>on_talked_by_user</c> is a ladder of rungs each carrying
/// <c>test_probability percent=2</c> and a <c>teleport_target_alias</c> — a weighted table of
/// destinations. This port picks a destination from the spawn data instead, excluding anything within
/// fifty metres of the creator, which is a different mechanism reaching a similar place.
/// </para>
/// </remarks>
[AIName("conquest_offering_portal")]
public class ConquestOfferingPortalAI : ActionItemNpcAI
{
    /// <summary>Retail's <c>set_idle_timer</c> on waking, whose rung is a bare <c>despawn_self</c>.</summary>
    public const long PortalLifeMillis = 180_000L;

    private SpawnTemplate targetLocation;

    public ConquestOfferingPortalAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleSpawned()
    {
        base.HandleSpawned();
        targetLocation = FindTargetLocation();
        GetOwner().GetController().AddTask(TaskId.DESPAWN, ThreadPoolManager.GetInstance().Schedule(_ =>
        {
            GetOwner().GetController().Delete();
            return ValueTask.CompletedTask;
        }, System.TimeSpan.FromMilliseconds(PortalLifeMillis)));
    }

    protected override void HandleUseItemFinish(Player player)
    {
        if (targetLocation != null)
            TeleportService.TeleportTo(player, targetLocation.GetWorldId(), targetLocation.GetX(), targetLocation.GetY(), targetLocation.GetZ(),
                targetLocation.GetHeading(), TeleportAnimation.FADE_OUT_BEAM);
    }

    private SpawnTemplate FindTargetLocation()
    {
        int npcId = GetNpcId() == 833018 ? 856412 : 856433;
        SpawnGroup spawnGroup = Rnd.Get(DataManager.SPAWNS_DATA.GetSpawnsForNpc(GetOwner().GetWorldId(), npcId));
        if (spawnGroup != null)
        {
            SpawnTemplate targetLocation = null;
            Npc creator = FindCreatorNpc();
            if (creator != null)
            {
                SpawnTemplate creatorTemplate = creator.GetSpawn();
                // exclude all teleport templates within a 50m range around the creator spawn template
                // to prevent teleportation to the killed conquest npc (creator of this npc)
                List<SpawnTemplate> spawnTemplates = spawnGroup.GetSpawnTemplates().Where(teleportTemplate =>
                    !PositionUtil.IsInRange(teleportTemplate.GetX(), teleportTemplate.GetY(), teleportTemplate.GetZ(),
                        creatorTemplate.GetX(), creatorTemplate.GetY(), creatorTemplate.GetZ(), 50)).ToList();
                targetLocation = Rnd.Get(spawnTemplates);
            }

            if (targetLocation != null)
                return targetLocation;
            return Rnd.Get(spawnGroup.GetSpawnTemplates());
        }

        return null;
    }

    private Npc FindCreatorNpc()
    {
        if (GetCreatorId() != 0 && GetPosition().GetWorldMapInstance().GetObject(GetCreatorId()) is Npc npc)
            return npc;
        return null;
    }
}
