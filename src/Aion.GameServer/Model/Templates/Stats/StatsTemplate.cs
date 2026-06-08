using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Stats;

/// <summary>
/// Container for a creature's base stats (loaded from XML or built programmatically).
/// Java parity: model/templates/stats/StatsTemplate (@XmlRootElement("stats_template")).
/// </summary>
/// <remarks>
/// Getters overridden by <c>PlayerClass.PlayerStatsTemplate</c> (power/health/agility/baseAccuracy/
/// knowledge/will and the speed getters) are <c>virtual</c>. The base stat getters return the Java
/// constant 100; speed getters delegate to <see cref="Speeds"/> (null → 0).
/// </remarks>
[XmlRoot("stats_template")]
public class StatsTemplate
{
    [XmlAttribute("maxHp")] public int MaxHp { get; set; }
    [XmlAttribute("maxMp")] public int MaxMp { get; set; }

    [XmlAttribute("evasion")] public int Evasion { get; set; }
    [XmlAttribute("block")] public int Block { get; set; }
    [XmlAttribute("parry")] public int Parry { get; set; }
    [XmlAttribute("pdef")] public int Pdef { get; set; }
    [XmlAttribute("mdef")] public int Mdef { get; set; }
    [XmlAttribute("mresist")] public int Mresist { get; set; }
    [XmlAttribute("msup")] public int Msup { get; set; }
    [XmlAttribute("strike_resist")] public int StrikeResist { get; set; }
    [XmlAttribute("spell_resist")] public int SpellResist { get; set; }

    [XmlAttribute("attack")] public int Attack { get; set; }
    [XmlAttribute("accuracy")] public int Accuracy { get; set; }
    [XmlAttribute("pcrit")] public int Pcrit { get; set; }

    [XmlAttribute("matk")] public int Matk { get; set; }
    [XmlAttribute("macc")] public int Macc { get; set; }
    [XmlAttribute("mcrit")] public int Mcrit { get; set; }
    [XmlAttribute("mboost")] public int MagicBoost { get; set; }

    [XmlAttribute("abnormal_resist")] public int AbnormalResistance { get; set; }

    // Java parity: @XmlTransient int stunLikeResistance
    [XmlIgnore] public int StunLikeResistanceField { get; set; }

    [XmlElement("speeds")] public CreatureSpeeds? Speeds { get; set; }

    public int GetMaxHp() => MaxHp;
    public void SetMaxHp(int maxHp) => MaxHp = maxHp;

    public int GetMaxMp() => MaxMp;
    public void SetMaxMp(int maxMp) => MaxMp = maxMp;

    /* ======================================= */

    public int GetEvasion() => Evasion;
    public void SetEvasion(int evasion) => Evasion = evasion;

    public int GetBlock() => Block;
    public void SetBlock(int block) => Block = block;

    public int GetParry() => Parry;
    public void SetParry(int parry) => Parry = parry;

    public int GetPdef() => Pdef;
    public void SetPdef(int pdef) => Pdef = pdef;

    public int GetMdef() => Mdef;
    public void SetMdef(int mdef) => Mdef = mdef;

    public int GetMresist() => Mresist;
    public void SetMresist(int mresist) => Mresist = mresist;

    public int GetMsup() => Msup;

    public int GetStrikeResist() => StrikeResist;
    public void SetStrikeResist(int strikeResist) => StrikeResist = strikeResist;

    public int GetSpellResist() => SpellResist;
    public void SetSpellResist(int spellResist) => SpellResist = spellResist;

    /* ======================================= */

    public int GetAttack() => Attack;
    public void SetAttack(int attack) => Attack = attack;

    public int GetAccuracy() => Accuracy;
    public void SetAccuracy(int accuracy) => Accuracy = accuracy;

    public int GetPcrit() => Pcrit;
    public void SetPcrit(int pcrit) => Pcrit = pcrit;

    /* ======================================= */

    public int GetMagicalAttack() => Matk;
    public void SetMagicalAttack(int matk) => Matk = matk;

    public int GetMacc() => Macc;
    public void SetMacc(int macc) => Macc = macc;

    public int GetMcrit() => Mcrit;
    public void SetMcrit(int mcrit) => Mcrit = mcrit;

    public int GetMagicBoost() => MagicBoost;

    /* ======================================= */

    public virtual float GetWalkSpeed() => Speeds == null ? 0 : Speeds.GetWalkSpeed();
    public virtual float GetRunSpeed() => Speeds == null ? 0 : Speeds.GetRunSpeed();
    public float GetGroupWalkSpeed() => Speeds == null ? 0 : Speeds.GetGroupWalkSpeed();
    public float GetRunSpeedFight() => Speeds == null ? 0 : Speeds.GetRunSpeedFight();
    public float GetGroupRunSpeedFight() => Speeds == null ? 0 : Speeds.GetGroupRunSpeedFight();
    public virtual float GetFlySpeed() => Speeds == null ? 0 : Speeds.GetFlySpeed();

    public int GetAbnormalResistance() => AbnormalResistance;

    public int GetStunLikeResistance() => StunLikeResistanceField;
    public void SetStunLikeResistance(int stunLikeResistance) => StunLikeResistanceField = stunLikeResistance;

    public virtual int GetPower() => 100;
    public virtual int GetHealth() => 100;
    public virtual int GetAgility() => 100;
    public virtual int GetBaseAccuracy() => 100;
    public virtual int GetKnowledge() => 100;
    public virtual int GetWill() => 100;
}
