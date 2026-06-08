using Aion.GameServer.Model.Stats.Calc;
using Aion.GameServer.Model.Templates;
using Aion.GameServer.Model.Templates.Stats;

namespace Aion.GameServer.Model;

/// <summary>
/// Class a player may belong to.
/// Java parity: model/PlayerClass (@XmlEnum, implements L10n).
/// </summary>
/// <remarks>
/// Java's <c>PlayerClass implements L10n</c>; C# enums cannot implement interfaces (a C#-vs-Java
/// foundational difference), so the L10n contract is exposed via <see cref="PlayerClassExtensions.GetL10nId"/>
/// per the conflict-resolution doctrine (closest-to-1:1). Per-constant data lives in the extensions class.
/// SCREAMING_SNAKE_CASE preserved for XML.
/// </remarks>
public enum PlayerClass
{
    WARRIOR,
    GLADIATOR, // fighter
    TEMPLAR, // knight
    SCOUT,
    ASSASSIN,
    RANGER,
    MAGE,
    SORCERER, // wizard
    SPIRIT_MASTER, // elementalist
    PRIEST,
    CLERIC,
    CHANTER,
    ENGINEER,
    RIDER,
    GUNNER,
    ARTIST,
    BARD,
}

public static class PlayerClassExtensions
{
    private sealed record Data(
        byte ClassId, int NameId, PlayerClass StartingClass,
        int Power, int Health, int Agility, int Accuracy, int Knowledge, int Will,
        int HealthMultiplier, int WillMultiplier, int MagicalCriticalResist);

    private static readonly Dictionary<PlayerClass, Data> Table = new()
    {
        [PlayerClass.WARRIOR] = new(0, 240000, PlayerClass.WARRIOR, 110, 110, 100, 100, 90, 90, 400, 400, 0),
        [PlayerClass.GLADIATOR] = new(1, 240001, PlayerClass.WARRIOR, 115, 115, 100, 100, 90, 90, 440, 400, 0),
        [PlayerClass.TEMPLAR] = new(2, 240002, PlayerClass.WARRIOR, 115, 100, 100, 100, 90, 105, 460, 400, 0),
        [PlayerClass.SCOUT] = new(3, 240003, PlayerClass.SCOUT, 100, 100, 110, 110, 90, 90, 360, 400, 0),
        [PlayerClass.ASSASSIN] = new(4, 240004, PlayerClass.SCOUT, 110, 100, 110, 110, 90, 90, 360, 400, 0),
        [PlayerClass.RANGER] = new(5, 240005, PlayerClass.SCOUT, 100, 100, 115, 115, 90, 90, 280, 400, 0),
        [PlayerClass.MAGE] = new(6, 240006, PlayerClass.MAGE, 90, 90, 95, 95, 115, 115, 260, 600, 0),
        [PlayerClass.SORCERER] = new(7, 240007, PlayerClass.MAGE, 90, 90, 100, 100, 120, 110, 260, 600, 50),
        [PlayerClass.SPIRIT_MASTER] = new(8, 240008, PlayerClass.MAGE, 90, 90, 100, 100, 115, 115, 280, 600, 50),
        [PlayerClass.PRIEST] = new(9, 240009, PlayerClass.PRIEST, 95, 95, 100, 100, 100, 100, 360, 600, 0),
        [PlayerClass.CLERIC] = new(10, 240010, PlayerClass.PRIEST, 105, 110, 90, 90, 105, 110, 320, 600, 50),
        [PlayerClass.CHANTER] = new(11, 240011, PlayerClass.PRIEST, 110, 105, 90, 90, 105, 110, 360, 600, 0),
        [PlayerClass.ENGINEER] = new(12, 904314, PlayerClass.ENGINEER, 100, 100, 110, 110, 90, 90, 360, 400, 0),
        [PlayerClass.RIDER] = new(13, 904315, PlayerClass.ENGINEER, 100, 100, 100, 100, 105, 105, 420, 480, 0),
        [PlayerClass.GUNNER] = new(14, 904316, PlayerClass.ENGINEER, 100, 105, 105, 100, 100, 100, 360, 400, 0),
        [PlayerClass.ARTIST] = new(15, 904317, PlayerClass.ARTIST, 95, 95, 100, 100, 100, 105, 320, 600, 0),
        [PlayerClass.BARD] = new(16, 904318, PlayerClass.ARTIST, 90, 100, 100, 100, 110, 110, 320, 520, 50),
    };

    private static Data Of(PlayerClass pc) => Table[pc];

    // Java parity: createStatsTemplate(int level)
    public static StatsTemplate CreateStatsTemplate(this PlayerClass pc, int level)
    {
        var d = Of(pc);
        var statsTemplate = new PlayerStatsTemplate(d);
        statsTemplate.SetMaxHp(PlayerStatCalculator.CalculateMaxHp(pc, level));
        statsTemplate.SetMaxMp(PlayerStatCalculator.CalculateMaxMp(pc, level));
        statsTemplate.SetBlock(PlayerStatCalculator.CalculateBlockEvasionOrParry(level));
        statsTemplate.SetParry(PlayerStatCalculator.CalculateBlockEvasionOrParry(level));
        statsTemplate.SetEvasion(PlayerStatCalculator.CalculateBlockEvasionOrParry(level));
        statsTemplate.SetAccuracy(PlayerStatCalculator.CalculatePhysicalAccuracy(level));
        statsTemplate.SetMacc(PlayerStatCalculator.CalculateMagicalAccuracy(level));
        statsTemplate.SetAttack(18);
        statsTemplate.SetPcrit(2);
        statsTemplate.SetMcrit(50);
        statsTemplate.SetStrikeResist(PlayerStatCalculator.CalculateStrikeResist(level));
        statsTemplate.SetSpellResist(d.MagicalCriticalResist);
        return statsTemplate;
    }

    // Java parity: getClassId()
    public static byte GetClassId(this PlayerClass pc) => Of(pc).ClassId;

    // Java parity: static getPlayerClassById(byte)
    public static PlayerClass GetPlayerClassById(byte classId) => GetPlayerClassById(classId, false) ?? throw new ArgumentException("There is no player class with id " + classId);

    // Java parity: static getPlayerClassById(byte, boolean ignoreInvalidClassId)
    public static PlayerClass? GetPlayerClassById(byte classId, bool ignoreInvalidClassId)
    {
        foreach (var (pc, d) in Table)
        {
            if (d.ClassId == classId)
                return pc;
        }
        if (ignoreInvalidClassId)
            return null;
        throw new ArgumentException("There is no player class with id " + classId);
    }

    // Java parity: getL10nId() (L10n contract — see remarks on PlayerClass)
    public static int GetL10nId(this PlayerClass pc) => Of(pc).NameId;

    // Java parity: isStartingClass()
    public static bool IsStartingClass(this PlayerClass pc) => Of(pc).StartingClass == pc;

    // Java parity: getStartingClass()
    public static PlayerClass GetStartingClass(this PlayerClass pc) => Of(pc).StartingClass;

    // Java parity: isPhysicalClass()
    public static bool IsPhysicalClass(this PlayerClass pc) => pc switch
    {
        PlayerClass.WARRIOR or PlayerClass.GLADIATOR or PlayerClass.TEMPLAR or PlayerClass.SCOUT
            or PlayerClass.ASSASSIN or PlayerClass.RANGER or PlayerClass.CHANTER => true,
        _ => false,
    };

    // Java parity: getIconImage()
    public static string GetIconImage(this PlayerClass pc) => pc switch
    {
        PlayerClass.WARRIOR => "textures/ui/EMBLEM/icon_emblem_warrior.dds",
        PlayerClass.GLADIATOR => "textures/ui/EMBLEM/icon_emblem_fighter.dds",
        PlayerClass.TEMPLAR => "textures/ui/EMBLEM/icon_emblem_knight.dds",
        PlayerClass.SCOUT => "textures/ui/EMBLEM/icon_emblem_scout.dds",
        PlayerClass.ASSASSIN => "textures/ui/EMBLEM/icon_emblem_assassin.dds",
        PlayerClass.RANGER => "textures/ui/EMBLEM/icon_emblem_ranger.dds",
        PlayerClass.MAGE => "textures/ui/EMBLEM/icon_emblem_mage.dds",
        PlayerClass.SORCERER => "textures/ui/EMBLEM/icon_emblem_wizard.dds",
        PlayerClass.SPIRIT_MASTER => "textures/ui/EMBLEM/icon_emblem_elementalist.dds",
        PlayerClass.PRIEST => "textures/ui/EMBLEM/icon_emblem_cleric.dds", // cleric and priest images are switched in client
        PlayerClass.CLERIC => "textures/ui/EMBLEM/icon_emblem_priest.dds", // cleric and priest images are switched in client
        PlayerClass.CHANTER => "textures/ui/EMBLEM/icon_emblem_chanter.dds",
        PlayerClass.ENGINEER => "textures/ui/EMBLEM/Icon_emblem_Engineer.dds",
        PlayerClass.RIDER => "textures/ui/EMBLEM/Icon_emblem_Rider.dds",
        PlayerClass.GUNNER => "textures/ui/EMBLEM/Icon_emblem_Gunner.dds",
        PlayerClass.ARTIST => "textures/ui/EMBLEM/Icon_emblem_Artist.dds",
        PlayerClass.BARD => "textures/ui/EMBLEM/Icon_emblem_Bard.dds",
        _ => throw new ArgumentOutOfRangeException(nameof(pc)),
    };

    public static int GetPower(this PlayerClass pc) => Of(pc).Power;
    public static int GetHealth(this PlayerClass pc) => Of(pc).Health;
    public static int GetAgility(this PlayerClass pc) => Of(pc).Agility;
    public static int GetAccuracy(this PlayerClass pc) => Of(pc).Accuracy;
    public static int GetKnowledge(this PlayerClass pc) => Of(pc).Knowledge;
    public static int GetWill(this PlayerClass pc) => Of(pc).Will;
    public static int GetWillMultiplier(this PlayerClass pc) => Of(pc).WillMultiplier;
    public static int GetHealthMultiplier(this PlayerClass pc) => Of(pc).HealthMultiplier;
    public static int GetAgilityMultiplier(this PlayerClass pc) => 310;
    public static int GetAccuracyMultiplier(this PlayerClass pc) => 200;
    public static int GetNoWeaponPowerMultiplier(this PlayerClass pc) => 70;

    // Java parity: private class PlayerStatsTemplate extends StatsTemplate
    private sealed class PlayerStatsTemplate : StatsTemplate
    {
        private readonly Data _d;
        public PlayerStatsTemplate(Data d) => _d = d;

        public override int GetPower() => _d.Power;
        public override int GetHealth() => _d.Health;
        public override int GetAgility() => _d.Agility;
        public override int GetBaseAccuracy() => _d.Accuracy;
        public override int GetKnowledge() => _d.Knowledge;
        public override int GetWill() => _d.Will;
        public override float GetWalkSpeed() => 1.5f;
        public override float GetRunSpeed() => 6f;
        public override float GetFlySpeed() => 9f;
    }
}
