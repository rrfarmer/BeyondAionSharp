using System.Collections.Generic;
using System.Xml.Serialization;
using Aion.GameServer.Model;
using Aion.GameServer.Model.Stats.Calc;
using Aion.GameServer.Model.Stats.Calc.Functions;
using Aion.GameServer.Model.Templates.Stats;

namespace Aion.GameServer.Model.Templates;

/// <summary>Java parity: model/templates/TitleTemplate (xavier).</summary>
public class TitleTemplate : IStatOwner, IL10n
{
    [XmlAttribute("id")] private int titleId;
    [XmlElement("modifiers")] protected ModifiersTemplate modifiers;
    [XmlAttribute("race")] private Race race;
    [XmlAttribute("nameId")] private int nameId;
    [XmlAttribute("desc")] private string description;

    public int GetTitleId()
    {
        return titleId;
    }

    public Race GetRace()
    {
        return race;
    }

    public int GetL10nId()
    {
        return nameId;
    }

    public string GetDesc()
    {
        return description;
    }

    public List<StatFunction> GetModifiers()
    {
        return modifiers == null ? null : modifiers.GetModifiers();
    }
}
