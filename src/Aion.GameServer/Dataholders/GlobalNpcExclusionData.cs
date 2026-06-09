using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;
using Aion.GameServer.Model;
using Aion.GameServer.Model.Templates.Npc;

namespace Aion.GameServer.Dataholders;

/// <summary>Java parity: dataholders/GlobalNpcExclusionData (bobobear). @XmlRootElement(global_npc_exclusions); @XmlList element Set→Raw space-sep string property; afterUnmarshal→AfterUnmarshal(object).</summary>
[XmlRoot("global_npc_exclusions")]
public class GlobalNpcExclusionData
{
    private HashSet<int> excludedNpcIds;
    private HashSet<string> excludedNpcNames;
    private HashSet<NpcTemplateType> excludedTypes;
    private HashSet<TribeClass> excludedTribes;
    private HashSet<AbyssNpcType> excludedAbyssTypes;

    [XmlElement("npc_ids")]
    public string ExcludedNpcIdsRaw
    {
        get => excludedNpcIds == null ? null : string.Join(" ", excludedNpcIds);
        set => excludedNpcIds = value == null ? null : new HashSet<int>(value.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse));
    }

    [XmlElement("npc_names")]
    public string ExcludedNpcNamesRaw
    {
        get => excludedNpcNames == null ? null : string.Join(" ", excludedNpcNames);
        set => excludedNpcNames = value == null ? null : new HashSet<string>(value.Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    [XmlElement("npc_types")]
    public string ExcludedTypesRaw
    {
        get => excludedTypes == null ? null : string.Join(" ", excludedTypes);
        set => excludedTypes = value == null ? null : new HashSet<NpcTemplateType>(value.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(t => (NpcTemplateType)Enum.Parse(typeof(NpcTemplateType), t)));
    }

    [XmlElement("npc_tribes")]
    public string ExcludedTribesRaw
    {
        get => excludedTribes == null ? null : string.Join(" ", excludedTribes);
        set => excludedTribes = value == null ? null : new HashSet<TribeClass>(value.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(t => (TribeClass)Enum.Parse(typeof(TribeClass), t)));
    }

    [XmlElement("npc_abyss_types")]
    public string ExcludedAbyssTypesRaw
    {
        get => excludedAbyssTypes == null ? null : string.Join(" ", excludedAbyssTypes);
        set => excludedAbyssTypes = value == null ? null : new HashSet<AbyssNpcType>(value.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(t => (AbyssNpcType)Enum.Parse(typeof(AbyssNpcType), t)));
    }

    [XmlIgnore] private bool isEmpty;

    public void AfterUnmarshal(object parent)
    {
        isEmpty = excludedNpcIds == null && excludedNpcNames == null && excludedTypes == null && excludedTribes == null && excludedAbyssTypes == null;
    }

    public ISet<int> GetNpcIds()
    {
        return excludedNpcIds == null ? new HashSet<int>() : excludedNpcIds;
    }

    public ISet<string> GetNpcNames()
    {
        return excludedNpcNames == null ? new HashSet<string>() : excludedNpcNames;
    }

    public ISet<NpcTemplateType> GetNpcTemplateTypes()
    {
        return excludedTypes == null ? new HashSet<NpcTemplateType>() : excludedTypes;
    }

    public ISet<TribeClass> GetNpcTribes()
    {
        return excludedTribes == null ? new HashSet<TribeClass>() : excludedTribes;
    }

    public ISet<AbyssNpcType> GetNpcAbyssTypes()
    {
        return excludedAbyssTypes == null ? new HashSet<AbyssNpcType>() : excludedAbyssTypes;
    }

    public bool IsEmpty()
    {
        return isEmpty;
    }
}
