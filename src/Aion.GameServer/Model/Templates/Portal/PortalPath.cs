using System.Collections.Generic;
using System.Xml.Serialization;
using Aion.GameServer.Model;

namespace Aion.GameServer.Model.Templates.Portal;

/// <summary>Java parity: model/templates/portal/PortalPath (xTz).</summary>
[XmlType("PortalPath")]
public class PortalPath
{
    [XmlAttribute("dialog")] private int dialog;
    [XmlAttribute("loc_id")] private int locId;
    [XmlAttribute("siege_id")] private int siegeId;
    [XmlAttribute("race")] private Race race = Race.PC_ALL;
    [XmlAttribute("min_level")] private int minLevel;
    [XmlAttribute("min_rank")] private int minRank;
    [XmlAttribute("kinah")] private int kinah;
    [XmlAttribute("title_id")] private int titleId;
    [XmlAttribute("err_group")] private int errGroup;
    [XmlAttribute("err_level")] private int errLevel;
    [XmlElement("quest_req")] private List<QuestReq> questReq;
    [XmlElement("item_req")] private List<ItemReq> itemReq;

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
