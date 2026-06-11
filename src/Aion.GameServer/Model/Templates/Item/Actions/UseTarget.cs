using System;
using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Items.Actions;

/// <summary>Java parity: model/templates/item/actions/UseTarget.</summary>
[XmlType("UseTarget")]
public enum UseTarget
{
    ACCESSORY,
    ARMOR,
    EQUIPMENT,
    WEAPON,
    WING,
    OTHER,
    ALL,
}

public static class UseTargetExtensions
{
    public static string Value(this UseTarget target)
    {
        return target.ToString();
    }

    public static UseTarget FromValue(string v)
    {
        return (UseTarget)Enum.Parse(typeof(UseTarget), v);
    }
}
