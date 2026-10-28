using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Text.Json;
using Aion.GameServer.Dataholders.RetailNpc;
using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.Model.Templates.Stats;

namespace Aion.GameServer.Retail;

public sealed record SkyTempleNpcAttack(int Ordinal, string Type, float Range, int Weight, int Modifier);
public sealed record SkyTempleNpcSkill(int Ordinal, int Id48, int Level, int Count, int Rate, int Delay, string Name,
    bool CountPresent, bool DelayPresent);
public sealed record SkyTempleNpcBounds(float Front, float Side, float Upper, float HorizontalRadius);

/// <summary>Immutable selected retail bases shared by the pilot and statistics cohorts.
/// The historical type name is retained at existing integration seams. Current
/// effects remain owned by each NPC; this is not a captured native actor.</summary>
public sealed partial class SkyTempleNpcProfile
{
    internal SkyTempleNpcProfile(RetailNpcTemplateInputs source, RetailNpcOrdinaryCoreSeeds core,
        RetailNpcOrdinaryScalarInputs scalars, RetailNpcBoundsPlan bounds, string generation,
        RetailNpcCohortBinding? cohort = null, bool statisticsOnly = false)
    {
        Source = source; Core = core; ScalarInputs = scalars; Generation = generation; Cohort = cohort; StatisticsOnly = statisticsOnly;
        if(statisticsOnly)
        {
            PhysicalDamageTrim=LeafInt(source.RawRecord,"physical_damage_trim",0);
            MagicalDamageTrim=LeafInt(source.RawRecord,"magical_damage_trim",0);
            if(PhysicalDamageTrim is not (0 or 1)||MagicalDamageTrim is not (0 or 1))
                throw new InvalidDataException("Unreviewed NPC damage-trim value");
        }
        Values = scalars.Fields.ToFrozenDictionary(x => x.Key, x => checked((int)(x.Value.Specification.Minimum.HasValue
            ? Math.Clamp(x.Value.Value, x.Value.Specification.Minimum.Value, x.Value.Specification.Maximum!.Value) : x.Value.Value)), StringComparer.Ordinal);
        Bounds = new(bounds.FinalAxes.Front, bounds.FinalAxes.Side, bounds.FinalAxes.Upper, bounds.HorizontalRadius);
        // NpcAttackData constructor defaults apply to an authored partial entry;
        // an absent list remains empty. Explicit zero never invokes a default.
        Attacks = source.Attacks.Entries.Select(x => new SkyTempleNpcAttack(x.Ordinal,
            x.Type.Value ?? (statisticsOnly ? "physical" : throw new InvalidDataException("Missing normal type")),
            x.Range.ParsedValue ?? (statisticsOnly ? 1f : throw new InvalidDataException("Missing normal range")),
            x.Weight.ParsedValue ?? (statisticsOnly ? 1000 : throw new InvalidDataException("Missing normal weight")),
            x.Modifier.ParsedValue ?? (statisticsOnly ? 100 : throw new InvalidDataException("Missing normal modifier")))).ToImmutableArray();
        Skills = source.Skills.Entries.Select(x => new SkyTempleNpcSkill(x.Ordinal, x.Supported48Id,
            LeafInt(x.RawNode, "skill_level"), LeafInt(x.RawNode, "skill_count", 0), LeafInt(x.RawNode, "skill_rate"),
            LeafInt(x.RawNode, "skill_delay_time", 0), x.Name.Value!, HasLeaf(x.RawNode, "skill_count"), HasLeaf(x.RawNode, "skill_delay_time"))).ToImmutableArray();
    }
    public RetailNpcTemplateInputs Source { get; }
    internal RetailNpcCohortBinding? Cohort { get; }
    internal bool StatisticsOnly { get; }
    internal RetailNpcOrdinaryCoreSeeds Core { get; }
    internal RetailNpcOrdinaryScalarInputs ScalarInputs { get; }
    public FrozenDictionary<string, int> Values { get; }
    public int NpcId => Source.NpcId;
    public string Devname => Source.Devname;
    public string DatasetId => Source.Receipt.DatasetId;
    public string SourceRecordSha256 => Source.Receipt.RawRecordSha256;
    public string Generation { get; }
    public JsonElement RawProfile => Source.Source.CompleteSourceProfile;
    public int Level => Core.Template.Level.Value;
    public int MaxHp => Values["max_hp"];
    public int HpRegen => Values["hp_regen"];
    public int StatRatio => Values["stat_ratio"];
    public int MinDamage => Core.MainHandMinimumBeforeEffects.Value;
    public int MaxDamage => Core.MainHandMaximumBeforeEffects.Value;
    public int AttackDelay => Math.Clamp(Source.Integers["attack_delay"].ParsedValue!.Value, 500, 10000);
    public int PhysicalDefense => Values["physical_defend"];
    public int MagicalDefense => Values["magical_defend"];
    public int MagicalResist => Values["magical_resist"];
    public int PhysicalAccuracy => Values["hit_accuracy"];
    public int MagicalAccuracy => Values["magical_hit_accuracy"];
    public int Dodge => Values["dodge"];
    public int Parry => Values["parry"];
    public int Block => Values["block"];
    public int PhysicalCritical => Values["critical"];
    public int MagicalCritical => Values["magical_critical"];
    public int MagicalBoost => Values["magical_skill_boost"];
    public int MagicalSuppression => Values["magical_skill_boost_resist"];
    internal int PhysicalDamageTrim { get; }
    internal int MagicalDamageTrim { get; }
    public int HitCount => StatisticsOnly ? Math.Clamp(Source.Integers["hit_count"].ParsedValue ?? 0,1,16)
        : Source.Integers["hit_count"].ParsedValue!.Value;
    public float NormalWalkSpeed => Source.Speeds["move_speed_normal_walk"].ParsedValue!.Value;
    public float NormalRunSpeed => Source.Speeds["move_speed_normal_run"].ParsedValue!.Value;
    public float CombatRunSpeed => Source.Speeds["move_speed_combat_run"].ParsedValue!.Value;
    public int MaxChaseMilliseconds => checked(LeafInt(Source.RawRecord, "max_chase_time") * 1000);
    // NPC ObjectClass::ObjectClass 1401b4630 seeds +16d0=1.5f. The parser
    // replaces it with integer/100 only when the field is actually present.
    public float ReturnSpeedMultiplier => LeafInt(Source.RawRecord, "move_speed_return", 150) / 100f;
    public SkyTempleNpcBounds Bounds { get; }
    public ImmutableArray<SkyTempleNpcAttack> Attacks { get; }
    internal bool HasNormalAttackCorrection => Attacks.Length > 0 && Attacks.Length == Source.Attacks.Entries.Length
        && HitCount == 1
        && Attacks.All(x => x.Weight >= 0 && x.Range >= 0 && x.Modifier >= 0);
    public ImmutableArray<SkyTempleNpcSkill> Skills { get; }
    internal bool HasRunReturnMode => RetailNpcSourceProfile.RawChildren(Source.RawRecord)
        .Where(x => x[0].GetString() == "move_type_return")
        .Select(RetailNpcSourceProfile.RawText).SingleOrDefault() == "run";

    internal StatsTemplate CreateStatsTemplate(StatsTemplate? compatibility = null)
    {
        if (StatisticsOnly && compatibility is null)
            throw new InvalidOperationException("Statistics-only projection requires the original 4.8 template for retained MP, skill attack and movement values.");
        return new SourceStats(this)
        {
            MaxHp = MaxHp, MaxMp = compatibility?.GetMaxMp() ?? 0, Pdef = PhysicalDefense, Mdef = MagicalDefense,
            Evasion = Dodge, Parry = Parry, Block = Block, Mresist = MagicalResist,
            Accuracy = PhysicalAccuracy, Macc = MagicalAccuracy, Pcrit = PhysicalCritical, Mcrit = MagicalCritical,
            MagicBoost = MagicalBoost, Msup = MagicalSuppression,
            StrikeResist = Values["physical_critical_reduce_rate"], SpellResist = Values["magical_critical_reduce_rate"],
            // Main ignores raw physical_attack/magical_attack. Autos use MinDamage/MaxDamage through the scoped combat adapter.
            Attack = compatibility?.GetAttack() ?? 0, Matk = compatibility?.GetMagicalAttack() ?? 0,
            AbnormalResistance = 0, StunLikeResistanceField = 0,
            // Statistics-only admission deliberately retains the host's movement
            // programs. It does not select source navigation, speed or regeneration.
            Speeds = compatibility is not null ? compatibility.Speeds : new() { WalkSpeed = NormalWalkSpeed, GroupWalkSpeed = NormalWalkSpeed,
                RunSpeed = NormalRunSpeed, RunSpeedFight = CombatRunSpeed, GroupRunSpeedFight = CombatRunSpeed }
        };
    }

    internal bool TrySpecialBase(StatEnum stat, out int value)
    {
        string? field = stat switch
        {
            StatEnum.WATER_RESISTANCE => "elemental_defend_water", StatEnum.WIND_RESISTANCE => "elemental_defend_air",
            StatEnum.EARTH_RESISTANCE => "elemental_defend_earth", StatEnum.FIRE_RESISTANCE => "elemental_defend_fire",
            StatEnum.LIGHT_RESISTANCE or StatEnum.ELEMENTAL_RESISTANCE_LIGHT => "elemental_defend_light",
            StatEnum.DARK_RESISTANCE or StatEnum.ELEMENTAL_RESISTANCE_DARK => "elemental_defend_dark",
            StatEnum.PHYSICAL_CRITICAL_DAMAGE_REDUCE => "physical_critical_damage_reduce",
            StatEnum.MAGICAL_CRITICAL_DAMAGE_REDUCE => "magical_critical_damage_reduce",
            _ => null
        };
        if (field is not null) { value = Values[field]; return true; }
        field = AbnormalField(stat);
        if (field is not null) { value = Values[field]; return true; }
        value = 0; return false;
    }

    // The same 25-slot names select both the template base and the native
    // first-level modifier pair. Keep non-status elemental resistances out.
    internal static string? AbnormalField(StatEnum stat)
    {
        string token = stat.ToString(); bool penetration = token.EndsWith("_RESISTANCE_PENETRATION", StringComparison.Ordinal);
        string suffix = penetration ? "_RESISTANCE_PENETRATION" : "_RESISTANCE";
        if (token.EndsWith(suffix, StringComparison.Ordinal))
        {
            string name = token[..^suffix.Length];
            if (name == "PERIFICATION") name = "PETRIFICATION";
            for (int i = 0; i < 25; i++)
                if (RetailNpcOrdinaryScalarProjection.StatusNames[i].ToUpperInvariant() == name)
                    return $"abnormal_{(penetration ? "penetration" : "resist")}[{i}]";
        }
        return null;
    }
    private static bool HasLeaf(JsonElement node, string tag) => RetailNpcSourceProfile.RawChildren(node).Any(x => x[0].GetString() == tag);
    internal static int LeafInt(JsonElement node, string tag, int? constructorDefault = null)
    {
        var leaf = RetailNpcSourceProfile.RawChildren(node).Where(n => n[0].GetString() == tag).ToArray();
        if (leaf.Length == 0 && constructorDefault.HasValue) return constructorDefault.Value;
        if (leaf.Length != 1 || !int.TryParse(RetailNpcSourceProfile.RawText(leaf[0]), System.Globalization.NumberStyles.AllowLeadingSign,
            System.Globalization.CultureInfo.InvariantCulture, out int value)) throw new InvalidDataException("Missing/ambiguous NPC skill field " + tag);
        return value;
    }
    private sealed class SourceStats(SkyTempleNpcProfile p) : StatsTemplate
    {
        public override int GetPower() => p.Values["str"];
        public override int GetHealth() => p.Values["vit"];
        public override int GetAgility() => p.Values["agi"];
        public override int GetBaseAccuracy() => p.Values["dex"];
        public override int GetKnowledge() => p.Values["kno"];
        public override int GetWill() => p.Values["will"];
    }
}
