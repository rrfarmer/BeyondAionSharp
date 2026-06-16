using System.Collections.Generic;
using System.Xml.Serialization;
using Aion.GameServer.Model;

namespace Aion.GameServer.Model.Templates.Portal;

/// <summary>Java parity: model/templates/portal/PortalPath (xTz).</summary>
[XmlType("PortalPath")]
public class PortalPath
{
    [XmlAttribute("dialog")] public int dialog;
    [XmlAttribute("loc_id")] public int locId;
    [XmlAttribute("siege_id")] public int siegeId;
    [XmlAttribute("race")] public Race race = Race.PC_ALL;
    [XmlAttribute("min_level")] public int minLevel;
    [XmlAttribute("min_rank")] public int minRank;
    [XmlAttribute("kinah")] public int kinah;
    [XmlAttribute("title_id")] public int titleId;
    [XmlAttribute("err_group")] public int errGroup;
    [XmlAttribute("err_level")] public int errLevel;
    [XmlElement("quest_req")] public List<QuestReq> questReq;
    [XmlElement("item_req")] public List<ItemReq> itemReq;

    public int GetDialog()
    {
        return dialog;
    }

    public int GetLocId()
    {
        return locId;
    }

    public int GetSiegeId()
    {
        return siegeId;
    }

    public Race GetRace()
    {
        return race;
    }

    public int GetMinLevel()
    {
        return minLevel;
    }

    public int GetMinRank()
    {
        return minRank;
    }

    public int GetKinah()
    {
        return kinah;
    }

    public int GetTitleId()
    {
        return titleId;
    }

    public int GetErrGroup()
    {
        return errGroup;
    }

    public int GetErrLevel()
    {
        return errLevel;
    }

    public List<QuestReq> GetQuestReq()
    {
        return questReq;
    }

    public List<ItemReq> GetItemReq()
    {
        return itemReq;
    }
}
