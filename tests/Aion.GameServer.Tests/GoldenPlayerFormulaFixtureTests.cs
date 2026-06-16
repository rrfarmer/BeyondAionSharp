using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Controllers.Movement;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;
using Aion.GameServer.Model.Account;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Stats.Calc;
using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.Model.Templates.Stats;
using Aion.GameServer.Utils.Stats;

namespace Aion.GameServer.Tests;

/// <summary>
/// Phase A2 / §5-A2 PLAYER-formula golden asserter.
///
/// Reads the SHARED player fixtures produced by the Java harness
/// (game-server GoldenPlayerFormulaFixtureGeneratorTest -> parity-artifacts/golden/formulas/StatFunctions.*.json)
/// and asserts the C# <see cref="StatFunctions"/> methods that take a live <see cref="Player"/> return values
/// identical to real Java. Both sides build a deterministic <see cref="HarnessPlayer"/> (the faithful
/// <see cref="Player"/> with its real base ctor running, but every getter the target formulas touch routed to fixed
/// harness stubs: <see cref="HarnessStats"/> for the StatEnum map, <see cref="HarnessLifeStats"/> for currentHp/maxHp,
/// <see cref="HarnessMoveController"/> for the movement direction). Java is the single source of truth.
///
/// SCOPE: only deterministic-value formulas (no Rnd). Covered: calculateFallDamage,
/// calculateMagicalResistRate Player-vs-Player min(500,..) branch, adjustStatByMovementModifier Player branches.
/// Float results compared bit-exact.
/// </summary>
[Xunit.Collection("GoldenDataManager")]
public sealed class GoldenPlayerFormulaFixtureTests
{
    // Mirror the Java harness: pin the fall-damage config statics to their @Property defaults so the test is
    // independent of any loaded properties file (the C# defaults must equal the Java @Property defaults).
    static GoldenPlayerFormulaFixtureTests()
    {
        FallDamageConfig.FALL_DAMAGE_PERCENTAGE = 1.0f;
        FallDamageConfig.MINIMUM_DISTANCE_DAMAGE = 10;
        FallDamageConfig.MAXIMUM_DISTANCE_DAMAGE = 50;
        EnsureDataManagerBridge();
    }

    [Theory]
    [InlineData("StatFunctions.calculateFallDamage.json")]
    [InlineData("StatFunctions.calculateMagicalResistRate.pvp.json")]
    [InlineData("StatFunctions.adjustStatByMovementModifier.player.json")]
    public void CsharpPlayerFormulaMatchesJavaGoldenFixture(string fixtureFile)
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
        "StatFunctions.calculateFallDamage" => StatFunctions.CalculateFallDamage(
            BuildPlayer(inputs.GetProperty("player")),
            ParseFloat(inputs.GetProperty("distance").GetRawText())),
        "StatFunctions.calculateMagicalResistRate" => StatFunctions.CalculateMagicalResistRate(
            BuildPlayer(inputs.GetProperty("attacker")),
            BuildPlayer(inputs.GetProperty("attacked")),
            inputs.GetProperty("accMod").GetInt32(),
            Enum.Parse<SkillElement>(inputs.GetProperty("element").GetString()!)),
        _ => throw new NotSupportedException($"No C# numeric dispatch registered for player formula {formula}"),
    };

    private static float EvaluateFloat(string formula, JsonElement inputs) => formula switch
    {
        "StatFunctions.adjustStatByMovementModifier" => StatFunctions.AdjustStatByMovementModifier(
            BuildPlayer(inputs.GetProperty("player")),
            Enum.Parse<StatEnum>(inputs.GetProperty("stat").GetString()!),
            ParseFloat(inputs.GetProperty("value").GetRawText())),
        _ => throw new NotSupportedException($"No C# float dispatch registered for player formula {formula}"),
    };

    private static HarnessPlayer BuildPlayer(JsonElement spec)
    {
        sbyte level = (sbyte)spec.GetProperty("level").GetInt32();
        Race race = Enum.Parse<Race>(spec.GetProperty("race").GetString()!);
        MovementModifierDirection direction = Enum.Parse<MovementModifierDirection>(spec.GetProperty("direction").GetString()!);
        int currentHp = spec.GetProperty("currentHp").GetInt32();
        var statMap = new Dictionary<StatEnum, int>();
        foreach (var prop in spec.GetProperty("stats").EnumerateObject())
            statMap[Enum.Parse<StatEnum>(prop.Name)] = prop.Value.GetInt32();
        return new HarnessPlayer(level, race, direction, statMap, currentHp);
    }

    // Java Float.toString text -> bit-exact float (invariant culture).
    private static float ParseFloat(string s) => float.Parse(s, NumberStyles.Float, CultureInfo.InvariantCulture);

    private static string DescribeInputs(JsonElement inputs) =>
        string.Join(", ", inputs.EnumerateObject().Select(p => $"{p.Name}={p.Value.GetRawText()}"));

    private static JsonDocument LoadFixture(string fileName)
    {
        var path = Path.Combine(FixtureRoot(), fileName);
        Assert.True(File.Exists(path), $"Missing Java golden fixture: {path}. " +
            "Regenerate with: mvn -pl game-server -am test -Dtest=GoldenPlayerFormulaFixtureGeneratorTest " +
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

    // ---- DataManager bridge (test-only) -------------------------------------------------------------------------
    // The faithful Player ctor builds an AbsoluteStatOwner that reads DataManager.ABSOLUTE_STATS_DATA, which throws
    // unless the DI singleton bridge is registered. Production registers a fully-loaded StaticData (huge, disk-backed).
    // For this pure unit test we register a minimal DataManager whose StaticData exposes an EMPTY AbsoluteStatsData
    // (so GetTemplate(0) returns null, handled benignly). StaticData's ctor is private and 70-arg, so we materialize an
    // uninitialized instance and set only the AbsoluteStatsDataDh backing property — the single field the ctor reads.
    private static void EnsureDataManagerBridge()
    {
        // Already registered (by a prior test/bootstrap) -> leave it; it can only be more complete than ours.
        try { _ = DataManager.ABSOLUTE_STATS_DATA; return; }
        catch (InvalidOperationException) { /* not registered yet */ }

        var staticData = (StaticData)RuntimeHelpers.GetUninitializedObject(typeof(StaticData));
        SetAutoProperty(staticData, nameof(StaticData.AbsoluteStatsDataDh), new AbsoluteStatsData());

        var dmCtor = typeof(DataManager).GetConstructor(
            BindingFlags.Instance | BindingFlags.NonPublic, binder: null, new[] { typeof(StaticData) }, modifiers: null)!;
        var dm = (DataManager)dmCtor.Invoke(new object[] { staticData });
        DataManager.RegisterInstance(dm);
    }

    private static void SetAutoProperty(object target, string propertyName, object value)
    {
        // C# auto-property backing field is "<PropName>k__BackingField".
        var field = target.GetType().GetField($"<{propertyName}>k__BackingField",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingFieldException(target.GetType().FullName, propertyName);
        field.SetValue(target, value);
    }

    // ---- harness types (mirror GoldenPlayerFormulaFixtureGeneratorTest) -----------------------------------------

    /// <summary>
    /// Minimal deterministic Player: the faithful base ctor runs (so <c>is Player</c> and all player plumbing exist),
    /// then every container/controller the target formulas touch is replaced with a fixed harness stub.
    /// </summary>
    internal sealed class HarnessPlayer : Player
    {
        private readonly sbyte _level;
        private readonly Race _race;

        public HarnessPlayer(sbyte level, Race race, MovementModifierDirection direction,
            Dictionary<StatEnum, int> statMap, int currentHp)
            : base(MinimalAccountData(), new Account(1))
        {
            _level = level;
            _race = race;
            // Replace the base ctor's real containers/controller with deterministic harness ones. (The getters are
            // non-overridable / called from the base ctor before our fields exist, so we install the stubs here.)
            SetGameStats(new HarnessStats(this, statMap));
            SetLifeStats(new HarnessLifeStats(this, currentHp));
            moveController = new HarnessMoveController(this, direction);
        }

        public override sbyte GetLevel() => _level;

        public override Race GetRace() => _race;

        private static PlayerAccountData MinimalAccountData()
        {
            var common = new PlayerCommonData(1);
            // PlayerGameStats(owner) runs UpdateStatsTemplate() -> GetPlayerClass().CreateStatsTemplate(level)
            // (pure math, no game data) during the base Player ctor, so a non-null PlayerClass is required.
            common.SetPlayerClass(PlayerClass.WARRIOR);
            return new PlayerAccountData(common, new PlayerAppearance());
        }
    }

    // Same seam as the Creature harness: resolve a fixed value per StatEnum (else use the passed base).
    // Typed as PlayerGameStats so Player.GetGameStats()'s cast succeeds.
    internal sealed class HarnessStats : PlayerGameStats
    {
        private readonly Dictionary<StatEnum, int> _statMap;

        public HarnessStats(Player owner, Dictionary<StatEnum, int> statMap) : base(owner)
        {
            _statMap = statMap;
        }

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

    // Fixed currentHp; maxHp resolves through HarnessStats (StatEnum.MAXHP). CreatureLifeStats.GetCurrentHp() is
    // non-virtual and reads a private field that PlayerLifeStats sets to maxHp; we override it to the fixture's
    // currentHp by setting that field (test-only), mirroring the Java getCurrentHp() override.
    internal sealed class HarnessLifeStats : Aion.GameServer.Model.Stats.Container.PlayerLifeStats
    {
        private static readonly FieldInfo CurrentHpField =
            typeof(CreatureLifeStats).GetField("currentHp", BindingFlags.Instance | BindingFlags.NonPublic)!;

        public HarnessLifeStats(Player owner, int currentHp) : base(owner)
        {
            CurrentHpField.SetValue(this, currentHp);
        }
    }

    // Fixed movement direction, no world/timing. GetMovementDirection() is non-virtual in the C# port; it returns the
    // private movementModifierDirection field provided the "not moving and stale" guard doesn't short-circuit to NONE.
    // We set that field directly and force IsInMove()=true so the guard always falls through to the fixed direction
    // (for NONE the field IS NONE, so it returns NONE too) — deterministic regardless of timing.
    internal sealed class HarnessMoveController : PlayerMoveController
    {
        private static readonly FieldInfo DirField =
            typeof(PlayableMoveController<Player>).GetField("movementModifierDirection",
                BindingFlags.Instance | BindingFlags.NonPublic)!;

        public HarnessMoveController(Player owner, MovementModifierDirection dir) : base(owner)
        {
            DirField.SetValue(this, dir);
            SetInMove(true);
        }
    }
}
