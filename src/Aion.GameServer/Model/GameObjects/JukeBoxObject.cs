using Aion.GameServer.Model.House;
using Aion.GameServer.Model.Templates.Housing;

namespace Aion.GameServer.Model.GameObjects;

/// <summary>Java parity: model/gameobjects/JukeBoxObject (Rolandas).</summary>
public class JukeBoxObject : HouseObject<HousingJukeBox>
{
    public JukeBoxObject(HouseRegistry registry, int objId, int templateId)
        : base(registry, objId, templateId)
    {
    }
}
