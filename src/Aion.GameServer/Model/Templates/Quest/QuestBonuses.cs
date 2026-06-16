using System.Xml.Serialization;
using Aion.GameServer.Model.Templates.Rewards;

namespace Aion.GameServer.Model.Templates.Quest;

/// <summary>Java parity: model/templates/quest/QuestBonuses (Rolandas).</summary>
[XmlType("QuestBonuses")]
public class QuestBonuses
{
    [XmlAttribute("type")] public BonusType type;
    [XmlAttribute("level")] public int level;

    public BonusType GetType_()
    {
        return type;
    }

    public int GetLevel()
    {
        return level;
    }
}
