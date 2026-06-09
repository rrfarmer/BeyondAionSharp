using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;
using Aion.GameServer.Model.Templates;

namespace Aion.GameServer.Model.Autogroup;

/// <summary>Java parity: model/autogroup/AutoGroup (MrPoke). @XmlType("AutoGroup"); implements L10n. @XmlAttribute List&lt;Integer&gt;→Raw.</summary>
[XmlType("AutoGroup")]
public class AutoGroup : L10n
{
    [XmlAttribute("id")] private int id;
    [XmlAttribute("instanceId")] private int instanceId;
    [XmlAttribute("name_id")] private int nameId;
    [XmlAttribute("title_id")] private int titleId;
    [XmlAttribute("min_lvl")] private int minLvl;
    [XmlAttribute("max_lvl")] private int maxLvl;
    [XmlAttribute("register_quick")] private bool registerQuick;
    [XmlAttribute("register_group")] private bool registerGroup;
    [XmlAttribute("register_new")] private bool registerNew;

    private List<int> npcIds;

    [XmlAttribute("npc_ids")]
    public string NpcIdsRaw
    {
        get => npcIds == null ? null : string.Join(" ", npcIds);
        set => npcIds = value == null ? null : value.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToList();
    }

    public int GetMaskId()
    {
        return id;
    }

    public int GetInstanceMapId()
    {
        return instanceId;
    }

    public int GetL10nId()
    {
        return nameId;
    }

    public int GetTitleId()
    {
        return titleId;
    }

    public int GetMinLvl()
    {
        return minLvl;
    }

    public int GetMaxLvl()
    {
        return maxLvl;
    }

    public bool CanRegisterQuickEntry()
    {
        return registerQuick;
    }

    public bool HasRegisterGroup()
    {
        return registerGroup;
    }

    public bool CanRegisterNewEntry()
    {
        return registerNew;
    }

    public List<int> GetNpcIds()
    {
        return npcIds == null ? new List<int>() : npcIds;
    }

    public bool IsRecruitableInstance()
    {
        return id >= 302 && id < 400 || instanceId == 300600000 || instanceId == 300220000;
    }
}
