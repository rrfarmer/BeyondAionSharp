using Aion.GameServer.Model.House;
using Aion.GameServer.Model.Templates.Housing;

namespace Aion.GameServer.Model.GameObjects;

/// <summary>Java parity: model/gameobjects/PassiveObject (Rolandas).</summary>
public class PassiveObject : HouseObject<HousingPassiveItem>
{
    public PassiveObject(HouseRegistry registry, int objId, int templateId)
        : base(registry, objId, templateId)
    {
    }
}
