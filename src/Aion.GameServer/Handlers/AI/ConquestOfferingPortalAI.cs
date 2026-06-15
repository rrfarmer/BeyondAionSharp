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

/// <summary>Java parity: ai/ConquestOfferingPortalAI (Yeats, Sykra).</summary>
[AIName("conquest_offering_portal")]
public class ConquestOfferingPortalAI : ActionItemNpcAI
{
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
        }, System.TimeSpan.FromMilliseconds(65000)));
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
