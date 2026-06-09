using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Quest;

/// <summary>Java parity: model/templates/quest/QuestCategory (antness).</summary>
[XmlType("QuestCategory")]
public enum QuestCategory
{
    QUEST,
    EVENT,
    MISSION,
    SIGNIFICANT,
    IMPORTANT,
    NON_COUNT,
    SEEN_MARKER,
    TASK,
    FACTION,
    CHALLENGE_TASK,
    PUBLIC,
    LEGION,
    PRIMARY
}
