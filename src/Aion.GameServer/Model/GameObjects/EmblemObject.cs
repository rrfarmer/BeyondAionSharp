using Aion.GameServer.Model.House;
using Aion.GameServer.Model.Templates.Housing;

namespace Aion.GameServer.Model.GameObjects;

/// <summary>Java parity: model/gameobjects/EmblemObject (Rolandas).</summary>
public class EmblemObject : HouseObject<HousingEmblem>
{
    public EmblemObject(HouseRegistry registry, int objId, int templateId)
        : base(registry, objId, templateId)
    {
    }

    public override bool CanExpireNow()
    {
        return false;
    }
}
