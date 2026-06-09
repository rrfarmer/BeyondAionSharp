using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;
using Aion.GameServer.Model;

namespace Aion.GameServer.Model.Templates.Tribe;

/// <summary>Java parity: model/templates/tribe/Tribe (ATracer).</summary>
[XmlType("Tribe")]
public class Tribe
{
    // Java parity: @XmlList List<TribeClass> (element content, space-separated).
    protected List<TribeClass> aggro;
    protected List<TribeClass> hostile;
    protected List<TribeClass> friend;
    protected List<TribeClass> neutral;
    protected List<TribeClass> none;
    protected List<TribeClass> support;

    [XmlElement("aggro")]
    public string AggroRaw { get => Join(aggro); set => aggro = Parse(value); }

    [XmlElement("hostile")]
    public string HostileRaw { get => Join(hostile); set => hostile = Parse(value); }

    [XmlElement("friend")]
    public string FriendRaw { get => Join(friend); set => friend = Parse(value); }

    [XmlElement("neutral")]
    public string NeutralRaw { get => Join(neutral); set => neutral = Parse(value); }

    [XmlElement("none")]
    public string NoneRaw { get => Join(none); set => none = Parse(value); }

    [XmlElement("support")]
    public string SupportRaw { get => Join(support); set => support = Parse(value); }

    [XmlAttribute("base")] protected TribeClass @base = TribeClass.NONE;
    [XmlAttribute("name")] protected TribeClass name;

    private static string Join(List<TribeClass> list) => list == null ? null : string.Join(" ", list);

    private static List<TribeClass> Parse(string value) => value == null
        ? null
        : value.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(s => (TribeClass) Enum.Parse(typeof(TribeClass), s)).ToList();

    public List<TribeClass> GetAggro()
    {
        if (aggro == null)
        {
            aggro = new List<TribeClass>();
        }
        return this.aggro;
    }

    public List<TribeClass> GetHostile()
    {
        if (hostile == null)
        {
            hostile = new List<TribeClass>();
        }
        return this.hostile;
    }

    public List<TribeClass> GetFriend()
    {
        if (friend == null)
        {
            friend = new List<TribeClass>();
        }
        return this.friend;
    }

    public List<TribeClass> GetNeutral()
    {
        if (neutral == null)
        {
            neutral = new List<TribeClass>();
        }
        return this.neutral;
    }

    public List<TribeClass> GetNone()
    {
        if (none == null)
        {
            none = new List<TribeClass>();
        }
        return this.none;
    }

    public List<TribeClass> GetSupport()
    {
        if (support == null)
        {
            support = new List<TribeClass>();
        }
        return this.support;
    }

    public TribeClass GetBase()
    {
        return @base == TribeClass.NONE ? name : @base;
    }

    public TribeClass GetName()
    {
        return name;
    }

    public bool IsGuard()
    {
        return name.IsGuard();
    }

    public override string ToString()
    {
        return name + " (" + @base + ")";
    }
}
