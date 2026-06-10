using System.Collections.Generic;
using Aion.GameServer.Dataholders;
using Aion.GameServer.GeoEngine.Collision;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Templates.Materials;
using Aion.GameServer.World.Geo;

namespace Aion.GameServer.Controllers.Observer;

/// <summary>Java parity: controllers/observer/TerrainZoneCollisionMaterialActor : AbstractMaterialSkillActor. Collections.emptyList()→new List; onMoved empty; moved() queries terrain material at position; synchronized(skills)→lock. GeoService/MaterialTemplate/DataManager red-tolerated.</summary>
public class TerrainZoneCollisionMaterialActor : AbstractMaterialSkillActor
{
    private volatile int lastMatId = 0;

    public TerrainZoneCollisionMaterialActor(Creature creature)
        : base(creature, null, (sbyte)CollisionIntention.MATERIAL.GetId(), CheckType.TOUCH, TaskId.TERRAIN_MATERIAL_ACTION, new List<MaterialSkill>())
    {
    }

    public override void OnMoved(CollisionResults collisionResults)
    {
    }

    public override void Moved()
    {
        if (GeoService.GetInstance().WorldHasTerrainMaterials(creature.GetWorldId()))
        {
            int matId = GeoService.GetInstance().GetTerrainMaterialAt(creature.GetWorldId(), creature.GetX(), creature.GetY(), creature.GetZ(), creature.GetInstanceId());
            if (matId != lastMatId || !isTouched)
            {
                lastMatId = matId;
                isTouched = true;
                MaterialTemplate template = matId == 0 ? null : DataManager.MATERIAL_DATA.GetTemplate(matId);
                if (template != null)
                {
                    List<MaterialSkill> matchingSkills = new List<MaterialSkill>();
                    foreach (MaterialSkill skill in template.GetSkills())
                    {
                        if (skill.GetTarget().Matches(creature))
                            matchingSkills.Add(skill);
                    }
                    if (matchingSkills.Count != 0)
                    {
                        skills = matchingSkills;
                        Act();
                        return;
                    }
                }
            }
        }
        if (skills.Count != 0)
        {
            lock (skills)
            {
                skills.Clear();
                isTouched = false;
                Abort();
            }
        }
    }
}
