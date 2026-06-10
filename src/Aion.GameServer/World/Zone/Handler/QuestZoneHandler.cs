using System;
using System.Collections.Generic;
using System.Reflection;
using Aion.GameServer.Controllers.Observer;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Player;
using Aion.GameServer.Model.Templates.Zone;

namespace Aion.GameServer.World.Zone.Handler;

/// <summary>Java parity: world/zone/handler/QuestZoneHandler (Rolandas) : GeneralZoneHandler. getClass().getAnnotation(ZoneNameAnnotation.class)→GetType().GetCustomAttribute&lt;ZoneNameAnnotation&gt;(); annotation.questId()→.QuestId; IncompleteAnnotationException→InvalidOperationException; Map→Dictionary; instanceof→is. AbstractQuestZoneObserver red-tolerated.</summary>
public abstract class QuestZoneHandler : GeneralZoneHandler
{
    protected readonly Dictionary<int, AbstractQuestZoneObserver> observed = new();
    protected readonly int questId;

    public QuestZoneHandler()
    {
        ZoneNameAnnotation annotation = GetType().GetCustomAttribute<ZoneNameAnnotation>();
        if (annotation == null || annotation.QuestId == 0 || DataManager.QUEST_DATA.GetQuestById(annotation.QuestId) == null)
            throw new InvalidOperationException("Incomplete annotation " + nameof(ZoneNameAnnotation) + ": questId"); // Java IncompleteAnnotationException(ZoneNameAnnotation.class, "questId")
        questId = annotation.QuestId;
    }

    public override void OnEnterZone(Creature creature, ZoneInstance zone)
    {
        if (!(creature is Player))
            return;
        AbstractQuestZoneObserver observer = CreateObserver((Player)creature, zone.GetZoneTemplate());
        creature.GetObserveController().AddObserver(observer);
        observed[creature.GetObjectId()] = observer;
    }

    public override void OnLeaveZone(Creature creature, ZoneInstance zone)
    {
        if (!(creature is Player))
            return;
        AbstractQuestZoneObserver observer = observed.GetValueOrDefault(creature.GetObjectId());
        if (observer != null)
        {
            creature.GetObserveController().RemoveObserver(observer);
            observed.Remove(creature.GetObjectId());
        }
    }

    public abstract AbstractQuestZoneObserver CreateObserver(Player player, ZoneTemplate zoneTemplate);
}
