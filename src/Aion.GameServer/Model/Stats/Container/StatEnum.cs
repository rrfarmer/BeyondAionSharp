namespace Aion.GameServer.Model.Stats.Container;

/// <summary>
/// All creature/item stat identifiers.
/// Java parity: model/stats/container/StatEnum (@XmlType("StatEnum") @XmlEnum).
/// </summary>
/// <remarks>
/// SCREAMING_SNAKE_CASE member names are preserved so XML deserialization matches Java tokens.
/// Java carries two per-constant fields: <c>itemStoneMask</c> (the parenthesized value, default 0)
/// and <c>sign</c> (default 1; only ATTACK_SPEED is -1). Those live in
/// <see cref="StatEnumExtensions"/> since C# enum members cannot hold instance fields.
/// </remarks>
public enum StatEnum
{
    MAXDP, // Maximum DP
    MAXHP, // HP
    MAXMP, // MP

    AGILITY,
    BLOCK,
    EVASION,
    CONCENTRATION,
    WILL,
    HEALTH,
    ACCURACY,
    KNOWLEDGE,
    PARRY,
    POWER,
    SPEED,
    ALLSPEED,
    WEIGHT,
    HIT_COUNT,

    ATTACK_RANGE,
    ATTACK_SPEED,
    PHYSICAL_ATTACK, // Attack
    PHYSICAL_ACCURACY, // Accuracy
    PHYSICAL_CRITICAL, // Critical Strike
    PHYSICAL_DEFENSE, // Physical Def
    MAIN_HAND_HITS,
    MAIN_HAND_ACCURACY,
    MAIN_HAND_CRITICAL,
    MAIN_HAND_POWER,
    MAIN_HAND_ATTACK_SPEED,
    OFF_HAND_HITS,
    OFF_HAND_ACCURACY,
    OFF_HAND_CRITICAL,
    OFF_HAND_POWER,
    OFF_HAND_ATTACK_SPEED,

    MAGICAL_ATTACK, // Magical Attack
    MAGICAL_ACCURACY,
    MAGICAL_CRITICAL, // Critical Spell
    MAGICAL_RESIST, // Magic Resist
    MAX_DAMAGES,
    MIN_DAMAGES,

    EARTH_RESISTANCE,
    FIRE_RESISTANCE,
    WIND_RESISTANCE,
    WATER_RESISTANCE,
    DARK_RESISTANCE,
    LIGHT_RESISTANCE,

    BOOST_MAGICAL_SKILL,
    BOOST_SPELL_ATTACK,
    BOOST_CASTING_TIME, // Casting Speed
    BOOST_CASTING_TIME_HEAL,
    BOOST_CASTING_TIME_TRAP,
    BOOST_CASTING_TIME_ATTACK,
    BOOST_CASTING_TIME_SKILL,
    BOOST_CASTING_TIME_SUMMONHOMING,
    BOOST_CASTING_TIME_SUMMON,
    BOOST_HATE, // Enmity Boost

    FLY_TIME,
    FLY_SPEED,

    DAMAGE_REDUCE, // how much damage you block
    DAMAGE_REDUCE_MAX, // whats max damage to block, TODO: implement

    // resistances
    BLEED_RESISTANCE, // Bleed Resist
    BLIND_RESISTANCE, // Blind Resist
    BIND_RESISTANCE, // Bind Resist
    CHARM_RESISTANCE, // Charm Resist TODO: what is it for?
    CONFUSE_RESISTANCE, // Confusion Resist
    CURSE_RESISTANCE, // Curse Resist
    DISEASE_RESISTANCE, // Disease Resist
    DEFORM_RESISTANCE, // Deform Resist
    FEAR_RESISTANCE, // Fear Resist
    NOFLY_RESISTANCE, // Nofly Resist
    OPENAERIAL_RESISTANCE, // Aether's Hold Resist
    PARALYZE_RESISTANCE, // Paralysis Resistance
    PERIFICATION_RESISTANCE, // Petrification Resist //TODO: type
    POISON_RESISTANCE, // Poison Resist
    PULLED_RESISTANCE, // Pulled Resist
    ROOT_RESISTANCE, // Immobilization Resist
    SILENCE_RESISTANCE, // Silence Resistance
    SLEEP_RESISTANCE, // Sleep Resist
    SLOW_RESISTANCE, // Reduce Speed Resist
    SNARE_RESISTANCE, // Reduce Attack Speed Resist
    SPIN_RESISTANCE, // Spin Resist
    STAGGER_RESISTANCE, // Knock Back Resist
    STUMBLE_RESISTANCE, // Stumble Resist
    STUN_RESISTANCE, // Stun Resist

    // penetrations
    BLEED_RESISTANCE_PENETRATION, // Bleeding Penetration
    BLIND_RESISTANCE_PENETRATION, // Blindness Penetration
    BIND_RESISTANCE_PENETRATION, // Bind Penetration
    CHARM_RESISTANCE_PENETRATION, // Charm Penetration
    CONFUSE_RESISTANCE_PENETRATION, // Confusion Penetration
    CURSE_RESISTANCE_PENETRATION, // Curse Penetration
    DISEASE_RESISTANCE_PENETRATION, // Disease Penetration
    DEFORM_RESISTANCE_PENETRATION, // Deform Penetration
    FEAR_RESISTANCE_PENETRATION, // Fear Penetration
    NOFLY_RESISTANCE_PENETRATION, // NoFly Penetration
    OPENAERIAL_RESISTANCE_PENETRATION, // Aether's Hold Penetration
    PARALYZE_RESISTANCE_PENETRATION, // Paralysis Resistance Penetration
    PERIFICATION_RESISTANCE_PENETRATION, // Petrification Penetration
    POISON_RESISTANCE_PENETRATION, // Poisoning Penetration
    PULLED_RESISTANCE_PENETRATION, // Pulled Penetration
    ROOT_RESISTANCE_PENETRATION, // Immobilization Penetration
    SILENCE_RESISTANCE_PENETRATION, // Silence Resistance Penetration
    SLEEP_RESISTANCE_PENETRATION, // Sleep Penetration
    SLOW_RESISTANCE_PENETRATION, // Reduce Movement Speed Penetration
    SNARE_RESISTANCE_PENETRATION, // Reduce Attack Speed Penetration
    SPIN_RESISTANCE_PENETRATION, // Spin Penetration
    STAGGER_RESISTANCE_PENETRATION, // Knock Back Penetration
    STUMBLE_RESISTANCE_PENETRATION, // Stumble Penetration
    STUN_RESISTANCE_PENETRATION, // Stun Penetration

    REGEN_MP, // Natural Mana Treatment
    REGEN_HP, // Natural Healing
    REGEN_FP, // Natural Flight Serum

    HEAL_BOOST, // Healing Boost, not BOOST_CASTING_TIME_HEAL ?
    ALLRESIST, // All Stats ?
    STUNLIKE_RESISTANCE,
    ELEMENTAL_RESISTANCE_DARK,
    ELEMENTAL_RESISTANCE_LIGHT,
    MAGICAL_CRITICAL_RESIST, // Spell Resist
    MAGICAL_CRITICAL_DAMAGE_REDUCE, // Spell Fortitude
    PHYSICAL_CRITICAL_RESIST, // Strike Resist
    PHYSICAL_CRITICAL_DAMAGE_REDUCE, // Strike Fortitude
    ERFIRE,
    ERAIR,
    EREARTH,
    ERWATER,
    ABNORMAL_RESISTANCE_ALL, // All Altered State Resist ?
    ALLPARA,
    KNOWIL, // Knowledge and Will
    AGIDEX, // Accuracy and Agility
    STRVIT, // Power and Health

    MAGICAL_DEFEND, // Magical Defense
    MAGIC_SKILL_BOOST_RESIST, // Magic Supression

    // Effects stats (bossts, deboosts)
    HEAL_SKILL_BOOST,
    HEAL_SKILL_DEBOOST,
    BOOST_HUNTING_XP_RATE,
    BOOST_GROUP_HUNTING_XP_RATE,
    BOOST_QUEST_XP_RATE,

    BOOST_CRAFTING_XP_RATE, // for level xp only
    BOOST_COOKING_XP_RATE, // for skill xp
    BOOST_WEAPONSMITHING_XP_RATE, // for skill xp
    BOOST_ARMORSMITHING_XP_RATE, // for skill xp
    BOOST_TAILORING_XP_RATE, // for skill xp
    BOOST_ALCHEMY_XP_RATE, // for skill xp
    BOOST_HANDICRAFTING_XP_RATE, // for skill xp
    BOOST_MENUISIER_XP_RATE, // for skill xp

    BOOST_GATHERING_XP_RATE, // for level xp only
    BOOST_AETHERTAPPING_XP_RATE, // for skill xp
    BOOST_ESSENCETAPPING_XP_RATE, // for skill xp

    BOOST_DROP_RATE,
    BOOST_MANTRA_RANGE,
    BOOST_RESIST_DEBUFF,

    // 3.5
    ELEMENTAL_FIRE,

    // PvP and PvE
    PVP_PHYSICAL_ATTACK,
    PVP_PHYSICAL_DEFEND,
    PVP_MAGICAL_ATTACK,
    PVP_MAGICAL_DEFEND,

    PVP_ATTACK_RATIO,
    PVP_ATTACK_RATIO_MAGICAL,
    PVP_ATTACK_RATIO_PHYSICAL,
    PVP_DEFEND_RATIO,
    PVP_DEFEND_RATIO_PHYSICAL,
    PVP_DEFEND_RATIO_MAGICAL,

    PVE_ATTACK_RATIO,
    PVE_ATTACK_RATIO_MAGICAL,
    PVE_ATTACK_RATIO_PHYSICAL,
    PVE_DEFEND_RATIO,
    PVE_DEFEND_RATIO_PHYSICAL,
    PVE_DEFEND_RATIO_MAGICAL,

    AP_BOOST,
    DR_BOOST,

    // 4.3
    PROC_REDUCE_RATE,
    BOOST_CHARGE_TIME,

    // 4.7
    PVP_DODGE,
    PVP_BLOCK,
    PVP_PARRY,
    PVP_HIT_ACCURACY,
    PVP_MAGICAL_RESIST,
    PVP_MAGICAL_HIT_ACCURACY,

    // 4.8
    BLOCK_PENETRATION,
}

public static class StatEnumExtensions
{
    // Java parity: per-constant itemStoneMask (parenthesized value). Members not listed default to 0.
    private static readonly Dictionary<StatEnum, int> ItemStoneMasks = new()
    {
        [StatEnum.MAXDP] = 22, [StatEnum.MAXHP] = 18, [StatEnum.MAXMP] = 20,
        [StatEnum.AGILITY] = 9, [StatEnum.BLOCK] = 33, [StatEnum.EVASION] = 31,
        [StatEnum.CONCENTRATION] = 41, [StatEnum.WILL] = 11, [StatEnum.HEALTH] = 7,
        [StatEnum.ACCURACY] = 8, [StatEnum.KNOWLEDGE] = 10, [StatEnum.PARRY] = 32,
        [StatEnum.POWER] = 6, [StatEnum.SPEED] = 36, [StatEnum.WEIGHT] = 39,
        [StatEnum.HIT_COUNT] = 35,
        [StatEnum.ATTACK_RANGE] = 38, [StatEnum.ATTACK_SPEED] = 29,
        [StatEnum.PHYSICAL_ATTACK] = 25, [StatEnum.PHYSICAL_ACCURACY] = 30,
        [StatEnum.PHYSICAL_CRITICAL] = 34, [StatEnum.PHYSICAL_DEFENSE] = 26,
        [StatEnum.MAGICAL_ATTACK] = 27, [StatEnum.MAGICAL_ACCURACY] = 105,
        [StatEnum.MAGICAL_CRITICAL] = 40, [StatEnum.MAGICAL_RESIST] = 28,
        [StatEnum.EARTH_RESISTANCE] = 14, [StatEnum.FIRE_RESISTANCE] = 15,
        [StatEnum.WIND_RESISTANCE] = 13, [StatEnum.WATER_RESISTANCE] = 12,
        [StatEnum.DARK_RESISTANCE] = 17, [StatEnum.LIGHT_RESISTANCE] = 16,
        [StatEnum.BOOST_MAGICAL_SKILL] = 104, [StatEnum.BOOST_CASTING_TIME] = 108,
        [StatEnum.BOOST_HATE] = 109,
        [StatEnum.FLY_TIME] = 23, [StatEnum.FLY_SPEED] = 37,
        [StatEnum.BLEED_RESISTANCE] = 44, [StatEnum.BLIND_RESISTANCE] = 48,
        [StatEnum.BIND_RESISTANCE] = 63, [StatEnum.CHARM_RESISTANCE] = 49,
        [StatEnum.CONFUSE_RESISTANCE] = 54, [StatEnum.CURSE_RESISTANCE] = 53,
        [StatEnum.DISEASE_RESISTANCE] = 50, [StatEnum.DEFORM_RESISTANCE] = 64,
        [StatEnum.FEAR_RESISTANCE] = 52, [StatEnum.NOFLY_RESISTANCE] = 66,
        [StatEnum.OPENAERIAL_RESISTANCE] = 59, [StatEnum.PARALYZE_RESISTANCE] = 45,
        [StatEnum.PERIFICATION_RESISTANCE] = 56, [StatEnum.POISON_RESISTANCE] = 43,
        [StatEnum.PULLED_RESISTANCE] = 65, [StatEnum.ROOT_RESISTANCE] = 47,
        [StatEnum.SILENCE_RESISTANCE] = 51, [StatEnum.SLEEP_RESISTANCE] = 46,
        [StatEnum.SLOW_RESISTANCE] = 61, [StatEnum.SNARE_RESISTANCE] = 60,
        [StatEnum.SPIN_RESISTANCE] = 62, [StatEnum.STAGGER_RESISTANCE] = 58,
        [StatEnum.STUMBLE_RESISTANCE] = 57, [StatEnum.STUN_RESISTANCE] = 55,
        [StatEnum.BLEED_RESISTANCE_PENETRATION] = 70, [StatEnum.BLIND_RESISTANCE_PENETRATION] = 74,
        [StatEnum.BIND_RESISTANCE_PENETRATION] = 89, [StatEnum.CHARM_RESISTANCE_PENETRATION] = 75,
        [StatEnum.CONFUSE_RESISTANCE_PENETRATION] = 80, [StatEnum.CURSE_RESISTANCE_PENETRATION] = 79,
        [StatEnum.DISEASE_RESISTANCE_PENETRATION] = 76, [StatEnum.DEFORM_RESISTANCE_PENETRATION] = 90,
        [StatEnum.FEAR_RESISTANCE_PENETRATION] = 78, [StatEnum.NOFLY_RESISTANCE_PENETRATION] = 92,
        [StatEnum.OPENAERIAL_RESISTANCE_PENETRATION] = 85, [StatEnum.PARALYZE_RESISTANCE_PENETRATION] = 71,
        [StatEnum.PERIFICATION_RESISTANCE_PENETRATION] = 82, [StatEnum.POISON_RESISTANCE_PENETRATION] = 69,
        [StatEnum.PULLED_RESISTANCE_PENETRATION] = 91, [StatEnum.ROOT_RESISTANCE_PENETRATION] = 73,
        [StatEnum.SILENCE_RESISTANCE_PENETRATION] = 77, [StatEnum.SLEEP_RESISTANCE_PENETRATION] = 72,
        [StatEnum.SLOW_RESISTANCE_PENETRATION] = 87, [StatEnum.SNARE_RESISTANCE_PENETRATION] = 86,
        [StatEnum.SPIN_RESISTANCE_PENETRATION] = 88, [StatEnum.STAGGER_RESISTANCE_PENETRATION] = 84,
        [StatEnum.STUMBLE_RESISTANCE_PENETRATION] = 83, [StatEnum.STUN_RESISTANCE_PENETRATION] = 81,
        [StatEnum.REGEN_MP] = 21, [StatEnum.REGEN_HP] = 19, [StatEnum.REGEN_FP] = 24,
        [StatEnum.HEAL_BOOST] = 110, [StatEnum.ALLRESIST] = 2,
        [StatEnum.MAGICAL_CRITICAL_RESIST] = 116, [StatEnum.MAGICAL_CRITICAL_DAMAGE_REDUCE] = 118,
        [StatEnum.PHYSICAL_CRITICAL_RESIST] = 115, [StatEnum.PHYSICAL_CRITICAL_DAMAGE_REDUCE] = 117,
        [StatEnum.ABNORMAL_RESISTANCE_ALL] = 1,
        [StatEnum.KNOWIL] = 4, [StatEnum.AGIDEX] = 5, [StatEnum.STRVIT] = 3,
        [StatEnum.MAGICAL_DEFEND] = 125, [StatEnum.MAGIC_SKILL_BOOST_RESIST] = 126,
        [StatEnum.PVP_PHYSICAL_ATTACK] = 111, [StatEnum.PVP_PHYSICAL_DEFEND] = 112,
        [StatEnum.PVP_MAGICAL_ATTACK] = 113, [StatEnum.PVP_MAGICAL_DEFEND] = 114,
        [StatEnum.PVP_ATTACK_RATIO] = 106, [StatEnum.PVP_DEFEND_RATIO] = 107,
    };

    // Java parity: getItemStoneMask()
    public static int GetItemStoneMask(this StatEnum stat) =>
        ItemStoneMasks.TryGetValue(stat, out int mask) ? mask : 0;

    // Java parity: getSign() — default 1; only ATTACK_SPEED is -1.
    public static int GetSign(this StatEnum stat) =>
        stat == StatEnum.ATTACK_SPEED ? -1 : 1;

    // Java parity: static getModifier(int skillId)
    public static StatEnum? GetModifier(int skillId) => skillId switch
    {
        30001 or 30002 => StatEnum.BOOST_ESSENCETAPPING_XP_RATE,
        30003 => StatEnum.BOOST_AETHERTAPPING_XP_RATE,
        40001 => StatEnum.BOOST_COOKING_XP_RATE,
        40002 => StatEnum.BOOST_WEAPONSMITHING_XP_RATE,
        40003 => StatEnum.BOOST_ARMORSMITHING_XP_RATE,
        40004 => StatEnum.BOOST_TAILORING_XP_RATE,
        40007 => StatEnum.BOOST_ALCHEMY_XP_RATE,
        40008 => StatEnum.BOOST_HANDICRAFTING_XP_RATE,
        40010 => StatEnum.BOOST_MENUISIER_XP_RATE,
        _ => null,
    };
}
