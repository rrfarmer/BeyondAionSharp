using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Quest;

/// <summary>Java parity: model/templates/quest/QuestTarget (Rolandas).</summary>
[XmlType(AnonymousType = true)]
public enum QuestTarget
{
    NONE,
    AREA,
    LEAGUE,
    ALLIANCE
}
