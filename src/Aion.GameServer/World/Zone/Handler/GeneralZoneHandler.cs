using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.World.Zone.Handler;

/// <summary>Java parity: world/zone/handler/GeneralZoneHandler.</summary>
public class GeneralZoneHandler : IZoneHandler
{
    // virtual: Java QuestZoneHandler (extends GeneralZoneHandler) overrides these.
    public virtual void OnEnterZone(Creature player, ZoneInstance zone)
    {
    }

    public virtual void OnLeaveZone(Creature player, ZoneInstance zone)
    {
    }
}
