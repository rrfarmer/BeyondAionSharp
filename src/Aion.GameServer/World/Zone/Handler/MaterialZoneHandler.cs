using System.Collections.Generic;
using System.Collections.Concurrent;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Controllers.Observer;
using Aion.GameServer.GeoEngine.Scene;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Player;
using Aion.GameServer.Model.Templates.Materials;
using Aion.GameServer.Utils;
using Aion.GameServer.World.Zone;
using CheckType = Aion.GameServer.Controllers.Observer.AbstractCollisionObserver.CheckType;

namespace Aion.GameServer.World.Zone.Handler;

/// <summary>
/// Java parity: world/zone/handler/MaterialZoneHandler (Rolandas). Attaches a ZoneCollisionMaterialActor when a non-owner-race
/// creature enters a material geometry zone. ConcurrentHashMap -> ConcurrentDictionary; instanceof+staff pattern -> is/is Player.
/// Spatial/MaterialTemplate/actor types are faithfully ported. MaterialTarget.Matches is an extension method.
/// </summary>
public class MaterialZoneHandler : IZoneHandler
{
    private readonly ConcurrentDictionary<int, AbstractMaterialSkillActor> observed = new ConcurrentDictionary<int, AbstractMaterialSkillActor>();
    private readonly Spatial geometry;
    private readonly MaterialTemplate template;
    private Race ownerRace = Race.NONE;

    public MaterialZoneHandler(Spatial geometry, MaterialTemplate template)
    {
        this.geometry = geometry;
        this.template = template;
        string name = geometry.GetName();
        if (name.StartsWith("BU_AB_DARKSP"))
            ownerRace = Race.ASMODIANS;
        else if (name.StartsWith("BU_AB_LIGHTSP"))
            ownerRace = Race.ELYOS;
    }

    public void OnEnterZone(Creature creature, ZoneInstance zone)
    {
        if (ownerRace == creature.GetRace())
            return;
        List<MaterialSkill> matchingSkills = new List<MaterialSkill>();
        foreach (MaterialSkill skill in template.GetSkills())
        {
            if (skill.GetTarget().Matches(creature))
                matchingSkills.Add(skill);
        }
        if (matchingSkills.Count == 0)
            return;
        // Teminon/Primum Landing shield 14 & 15, abyss core 16
        CheckType checkType = geometry.GetMaterialId() >= 14 && geometry.GetMaterialId() <= 16 ? CheckType.PASS : CheckType.TOUCH;
        ZoneCollisionMaterialActor actor = new ZoneCollisionMaterialActor(creature, geometry, matchingSkills, checkType);
        creature.GetObserveController().AddObserver(actor);
        observed[creature.GetObjectId()] = actor;
        if (GeoDataConfig.GEO_MATERIALS_SHOWDETAILS && creature is Player player && player.IsStaff())
            PacketSendUtility.SendMessage(player, "Entered material zone " + geometry.GetName());
        actor.Moved();
    }

    public void OnLeaveZone(Creature creature, ZoneInstance zone)
    {
        observed.TryRemove(creature.GetObjectId(), out AbstractMaterialSkillActor actor);
        if (actor != null)
        {
            creature.GetObserveController().RemoveObserver(actor);
            actor.Abort();
        }
        if (GeoDataConfig.GEO_MATERIALS_SHOWDETAILS && creature is Player player && player.IsStaff())
        {
            PacketSendUtility.SendMessage(player, "Left material zone " + geometry.GetName());
        }
    }
}
