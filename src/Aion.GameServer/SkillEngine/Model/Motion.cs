using System.Xml.Serialization;

namespace Aion.GameServer.SkillEngine.Model;

/// <summary>Skill cast motion (animation name/speed/instant/delay). Java parity: skillengine/model/Motion (@XmlType("Motion")).</summary>
[XmlType("Motion")]
public class Motion
{
    /// <remarks>name is null when instant_skill is true.</remarks>
    [XmlAttribute("name")] public string? Name { get; set; }
    [XmlAttribute("speed")] public int Speed { get; set; } = 100;
    [XmlAttribute("instant_skill")] public bool InstantSkill { get; set; }
    [XmlAttribute("delay")] public int Delay { get; set; }

    // Java parity: afterUnmarshal — intern name to save RAM (invoked post-load by the loader).
    public void AfterUnmarshal()
    {
        if (Name != null)
            Name = string.Intern(Name);
    }

    public string? GetName() => Name;
    // Java parity: getSpeed() — animation play speed in percent
    public int GetSpeed() => Speed;
    public bool IsInstantSkill() => InstantSkill;
    public int GetDelay() => Delay;
}
