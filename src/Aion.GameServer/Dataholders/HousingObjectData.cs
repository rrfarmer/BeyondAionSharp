using System.Collections.Generic;
using System.Xml.Serialization;
using Aion.GameServer.Model.Templates.Housing;

namespace Aion.GameServer.Dataholders;

/// <summary>Java parity: dataholders/HousingObjectData (Rolandas). @XmlRootElement(housing_objects); @XmlElements(11 subtypes)→stacked [XmlElement]; afterUnmarshal→AfterUnmarshal(object).</summary>
[XmlRoot("housing_objects")]
public class HousingObjectData
{
    [XmlElement("postbox", typeof(HousingPostbox))]
    [XmlElement("use_item", typeof(HousingUseableItem))]
    [XmlElement("move_item", typeof(HousingMoveableItem))]
    [XmlElement("chair", typeof(HousingChair))]
    [XmlElement("picture", typeof(HousingPicture))]
    [XmlElement("passive", typeof(HousingPassiveItem))]
    [XmlElement("npc", typeof(HousingNpc))]
    [XmlElement("storage", typeof(HousingStorage))]
    [XmlElement("jukebox", typeof(HousingJukeBox))]
    [XmlElement("moviejukebox", typeof(HousingMovieJukeBox))]
    [XmlElement("emblem", typeof(HousingEmblem))]
    public List<PlaceableHouseObject> housingObjects;

    [XmlIgnore] private readonly Dictionary<int, PlaceableHouseObject> objectTemplatesById = new();

    public void AfterUnmarshal(object parent)
    {
        foreach (PlaceableHouseObject obj in housingObjects)
        {
            objectTemplatesById[obj.GetTemplateId()] = obj;
        }

        housingObjects = null;
    }

    public int Size()
    {
        return objectTemplatesById.Count;
    }

    public PlaceableHouseObject GetTemplateById(int templateId)
    {
        return objectTemplatesById.TryGetValue(templateId, out var v) ? v : null;
    }
}
