using System.Globalization;
using System.Text.Json;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Stats.Calc;
using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.Model.Templates.Npc;
using Aion.GameServer.Model.Templates.Stats;
using Aion.GameServer.Utils.Stats;

namespace Aion.GameServer.Tests;

/// <summary>
/// Phase A2 / §5-A2 combat-formula golden asserter (COMBAT half).
///
/// Reads the SHARED combat fixtures produced by the Java harness
/// (game-server GoldenCombatFormulaFixtureGeneratorTest -> parity-artifacts/golden/formulas/StatFunctions.*.json)
/// and asserts the C# <see cref="StatFunctions"/> methods that take live <see cref="Creature"/>s return values
/// identical to real Java. Each fixture case carries a fully-specified creature spec (level, race, pvpTarget,
/// and an explicit StatEnum->value map). Both sides build a deterministic <see cref="HarnessCreature"/> whose
/// <see cref="HarnessStats.GetStat(StatEnum, float, CalculationType[])"/> resolves each StatEnum to the fixed
/// fixture value (defaulting to the base the formula passes in), so the entire combat math is bit-reproducible.
/// Java is the single source of truth.
///
/// SCOPE: only deterministic-value formulas (no Rnd). Float results are compared bit-exact.
/// </summary>
public sealed class GoldenCombatFormulaFixtureTests
{
    [Theory]
    [InlineData("StatFunctions.calculateHate.json")]
    [InlineData("StatFunctions.calculateMagicalResistRate.json")]
    [InlineData("StatFunctions.adjustDamageByPvpOrPveModifiers.json")]
    [InlineData("StatFunctions.adjustStatByMovementModifier.json")]
    public void CsharpCombatFormulaMatchesJavaGoldenFixture(string fixtureFile)
    {
        using var fixture = LoadFixture(fixtureFile);
        var formula = fixture.RootElement.GetProperty("formula").GetString()!;

        foreach (var caseElement in fixture.RootElement.GetProperty("cases").EnumerateArray())
        {
            var inputs = caseElement.GetProperty("inputs");

            if (caseElement.TryGetProperty("resultFloat", out var rf))
            {
                float expected = ParseFloat(rf.GetString()!);
                float actual = EvaluateFloat(formula, inputs);
                Assert.True(expected.Equals(actual),
                    $"{formula}({DescribeInputs(inputs)}): C# diverged from Java golden. " +
                    $"Java={expected:R}, C#={actual:R}");
            }
            else
            {
                long expected = caseElement.GetProperty("result").GetInt64();
                long actual = EvaluateLong(formula, inputs);
                Assert.True(expected == actual,
                    $"{formula}({DescribeInputs(inputs)}): C# diverged from Java golden. " +
                    $"Java={expected}, C#={actual}");
            }
        }
    }

    private static long EvaluateLong(string formula, JsonElement inputs) => formula switch
    {
        "StatFunctions.calculateHate" => StatFunctions.CalculateHate(
            BuildCreature(inputs.GetProperty("creature")),
            inputs.GetProperty("value").GetInt32()),
        "StatFunctions.calculateMagicalResistRate" => StatFunctions.CalculateMagicalResistRate(
            BuildCreature(inputs.GetProperty("attacker")),
            BuildCreature(inputs.GetProperty("attacked")),
            inputs.GetProperty("accMod").GetInt32(),
            ParseElement(inputs.GetProperty("element").GetString()!)),
        _ => throw new NotSupportedException($"No C# numeric dispatch registered for combat formula {formula}"),
    };

    private static float EvaluateFloat(string formula, JsonElement inputs) => formula switch
    {
        "StatFunctions.adjustDamageByPvpOrPveModifiers" => StatFunctions.AdjustDamageByPvpOrPveModifiers(
            BuildCreature(inputs.GetProperty("attacker")),
            BuildCreature(inputs.GetProperty("target")),
            ParseFloat(inputs.GetProperty("baseDamage").GetRawText()),
            inputs.GetProperty("pvpDamage").GetInt32(),
            inputs.GetProperty("useTemplateDmg").GetBoolean(),
            ParseElement(inputs.GetProperty("element").GetString()!)),
        "StatFunctions.adjustStatByMovementModifier" => StatFunctions.AdjustStatByMovementModifier(
            BuildCreature(inputs.GetProperty("creature")),
            Enum.Parse<StatEnum>(inputs.GetProperty("stat").GetString()!),
            ParseFloat(inputs.GetProperty("value").GetRawText())),
        _ => throw new NotSupportedException($"No C# float dispatch registered for combat formula {formula}"),
    };

    private static HarnessCreature BuildCreature(JsonElement spec)
    {
        sbyte level = (sbyte)spec.GetProperty("level").GetInt32();
        Race race = Enum.Parse<Race>(spec.GetProperty("race").GetString()!);
        bool pvpTarget = spec.GetProperty("pvpTarget").GetBoolean();
        var statMap = new Dictionary<StatEnum, int>();
        foreach (var prop in spec.GetProperty("stats").EnumerateObject())
            statMap[Enum.Parse<StatEnum>(prop.Name)] = prop.Value.GetInt32();
        return new HarnessCreature(level, race, statMap, pvpTarget);
    }

    private static SkillElement ParseElement(string name) => Enum.Parse<SkillElement>(name);

    // Java Float.toString text -> bit-exact float (invariant culture).
    private static float ParseFloat(string s) => float.Parse(s, NumberStyles.Float, CultureInfo.InvariantCulture);

    private static string DescribeInputs(JsonElement inputs) =>
        string.Join(", ", inputs.EnumerateObject().Select(p => $"{p.Name}={p.Value.GetRawText()}"));

    private static JsonDocument LoadFixture(string fileName)
    {
        var path = Path.Combine(FixtureRoot(), fileName);
        Assert.True(File.Exists(path), $"Missing Java golden fixture: {path}. " +
            "Regenerate with: mvn -pl game-server -am test -Dtest=GoldenCombatFormulaFixtureGeneratorTest " +
            "-Dmaven.test.skip=false -Dsurefire.failIfNoSpecifiedTests=false");
        return JsonDocument.Parse(File.ReadAllText(path));
    }

    private static string FixtureRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "parity-artifacts", "golden", "formulas");
            if (Directory.Exists(candidate))
                return candidate;
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException(
            "Could not locate parity-artifacts/golden/formulas above " + AppContext.BaseDirectory);
    }

    /// <summary>
    /// Minimal deterministic Creature mirroring the Java harness HarnessCreature: no spawn/world.
    /// All stat reads route through <see cref="HarnessStats"/> which resolves a fixed value per StatEnum.
    /// </summary>
    internal sealed class HarnessCreature : Creature
    {
        private readonly sbyte _level;
        private readonly Race _race;
        private readonly bool _pvpTarget;
        private readonly CreatureGameStats _gs;

        public HarnessCreature(sbyte level, Race race, Dictionary<StatEnum, int> statMap, bool pvpTarget)
            : base(0, null!, null!, new NpcTemplate(), null!, false)
        {
            _level = level;
            _race = race;
            _pvpTarget = pvpTarget;
            _gs = new HarnessStats(this, statMap);
        }

        public override sbyte GetLevel() => _level;

        public override Race GetRace() => _race;

        public override CreatureGameStats GetGameStats() => _gs;

        public override bool IsPvpTarget(Creature creature) => _pvpTarget;

        // NOTE: IsInInstance() is non-virtual in the C# port and would dereference a null position.
        // It is only reached on the PvP cross-race branch; all fixtures keep attacker.race == target.race
        // (NPC vs NPC), so the `GetRace() != GetRace()` guard short-circuits before IsInInstance() is called.
    }

    internal sealed class HarnessStats : CreatureGameStats<Creature>
    {
        private readonly Dictionary<StatEnum, int> _statMap;

        public HarnessStats(Creature owner, Dictionary<StatEnum, int> statMap) : base(owner)
        {
            _statMap = statMap;
        }

        // The single seam: resolve a fixed value per StatEnum (else use the base the formula passed in).
        public override Stat2 GetStat(StatEnum statEnum, float baseValue, params CalculationType[] calculationTypes)
        {
            float resolved = _statMap.TryGetValue(statEnum, out int v) ? v : baseValue;
            return new AdditionStat(statEnum, resolved, owner);
        }

        public override StatsTemplate GetStatsTemplate() => new StatsTemplate();
        public override Stat2 GetAttackSpeed() => new AdditionStat(StatEnum.ATTACK_SPEED, 1000, owner);
        public override Stat2 GetMovementSpeed() => new AdditionStat(StatEnum.SPEED, 6000, owner);
        public override Stat2 GetAttackRange() => new AdditionStat(StatEnum.ATTACK_RANGE, 1500, owner);
        public override Stat2 GetHpRegenRate() => new AdditionStat(StatEnum.REGEN_HP, 1, owner);
        public override Stat2 GetMpRegenRate() => new AdditionStat(StatEnum.REGEN_MP, 1, owner);
    }
}
