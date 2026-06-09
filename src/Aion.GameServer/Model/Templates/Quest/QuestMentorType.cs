using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Quest;

/// <summary>Java parity: model/templates/quest/QuestMentorType (MrPoke).</summary>
[XmlType(AnonymousType = true)]
public enum QuestMentorType
{
    NONE,
    MENTOR,
    MENTE
}
