using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.House;
using Aion.GameServer.Model.Templates.Housing;

namespace Aion.GameServer.Model.GameObjects;

/// <summary>Java parity: model/gameobjects/MoveableObject (Rolandas).</summary>
public class MoveableObject : HouseObject<HousingMoveableItem>
{
    public MoveableObject(HouseRegistry registry, int objId, int templateId)
        : base(registry, objId, templateId)
    {
    }

    public override void OnUse(Player player)
    {
    }
}
