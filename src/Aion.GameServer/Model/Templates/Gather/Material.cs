using System;
using System.Xml.Serialization;
using Aion.GameServer.Model.Templates;

namespace Aion.GameServer.Model.Templates.Gather;

/// <summary>Java parity: model/templates/gather/Material (ATracer).</summary>
[XmlType("Material")]
public class Material : IComparable<Material>, IL10n
{
    [XmlAttribute("name")] public string name;
    [XmlAttribute("itemid")] public int itemid;
    [XmlAttribute("nameid")] public int nameid;
    [XmlAttribute("rate")] public int rate;

    public string GetName()
    {
        return name;
    }

    public int GetItemId()
    {
        return itemid;
    }

    public int GetL10nId()
    {
        return nameid;
    }

    public int GetRate()
    {
        return rate;
    }

    public int CompareTo(Material o)
    {
        return o.rate - rate;
    }
}
