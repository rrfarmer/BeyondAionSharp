using System.Xml.Serialization;

namespace Aion.GameServer.SkillEngine.Model;

/// <summary>Per-weapon animation timing for a skill. Java parity: skillengine/model/Times (@XmlType("Times")).</summary>
[XmlType("Times")]
public class Times
{
    [XmlAttribute("weapon")] public string? Weapon { get; set; }
    [XmlAttribute("id")] public int Id { get; set; }
    [XmlAttribute("min")] public float MinTime { get; set; }
    [XmlAttribute("max")] public float MaxTime { get; set; }
    [XmlAttribute("animation_length")] public float AnimationLength { get; set; }

    // Java parity: afterUnmarshal — intern weapon string (invoked post-load by the loader).
    public void AfterUnmarshal()
    {
        Weapon = string.Intern(Weapon!);
    }

    public int GetId() => Id;
    public float GetMinTime() => MinTime;
    public float GetMaxTime() => MaxTime;
    public float GetAnimationLength() => AnimationLength;
    public string? GetWeapon() => Weapon;
}
