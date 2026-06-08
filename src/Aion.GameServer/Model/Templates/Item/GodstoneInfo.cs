using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Item;

/// <summary>
/// Godstone proc parameters embedded in an item.
/// Java parity: model/templates/item/GodstoneInfo (@XmlRootElement("Godstone")).
/// </summary>
[XmlRoot("Godstone")]
public class GodstoneInfo
{
    [XmlAttribute("skillid")] public int Skillid { get; set; }
    [XmlAttribute("skilllvl")] public int Skilllvl { get; set; }
    [XmlAttribute("probability")] public int Probability { get; set; }
    [XmlAttribute("probabilityleft")] public int Probabilityleft { get; set; }
    [XmlAttribute("breakprob")] public int Breakprob { get; set; }
    [XmlAttribute("nonbreakcount")] public int Nonbreakcount { get; set; }

    public int GetSkillId() => Skillid;
    public int GetSkillLevel() => Skilllvl;
    public int GetProbability() => Probability;
    public int GetProbabilityLeft() => Probabilityleft;
    public int GetBreakProb() => Breakprob;
    public int GetNonBreakCount() => Nonbreakcount;
}
