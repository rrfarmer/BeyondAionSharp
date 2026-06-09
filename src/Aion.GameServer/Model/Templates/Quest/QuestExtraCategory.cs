using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Quest;

/// <summary>Java parity: model/templates/quest/QuestExtraCategory (Cheatkiller).</summary>
[XmlType("QuestExtraCategory")]
public enum QuestExtraCategory
{
    NONE,
    COIN_QUEST,
    DRACONIC_RECIPE_QUEST, // not use 3.9
    DEVANION_QUEST,
    GOLD_QUEST
}
