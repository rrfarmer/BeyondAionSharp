using System.Collections.Immutable;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Xml.Linq;
using N = Aion.GameServer.Controllers.Attack.RetailNpc.RetailNpcImmediateDamageOwner;
using static Aion.GameServer.Controllers.Attack.RetailNpc.RetailNpcUserGearStatLifeInputs;

namespace Aion.GameServer.Controllers.Attack.RetailNpc;

/// <summary>Pure Java4.8 owned graph for the admitted life/speed getters. Unread nonlife
/// functions remain in the registry so exact-owner removal is never a MAXHP-only shortcut.</summary>
internal static class RetailNpcUserOwnedStatGraph
{
    internal sealed record ClassRow(string Name, int Health, int Will, int HealthMultiplier, int WillMultiplier);
    private static readonly ImmutableArray<ClassRow> Classes =
    [
        new("WARRIOR", 110, 90, 400, 400),
        new("GLADIATOR", 115, 90, 440, 400),
        new("TEMPLAR", 100, 105, 460, 400),
        new("SCOUT", 100, 90, 360, 400),
        new("ASSASSIN", 100, 90, 360, 400),
        new("RANGER", 100, 90, 280, 400),
        new("MAGE", 90, 115, 260, 600),
        new("SORCERER", 90, 110, 260, 600),
        new("SPIRIT_MASTER", 90, 115, 280, 600),
        new("PRIEST", 95, 100, 360, 600),
        new("CLERIC", 110, 110, 320, 600),
        new("CHANTER", 105, 110, 360, 600),
        new("ENGINEER", 100, 90, 360, 400),
        new("RIDER", 100, 105, 420, 480),
        new("GUNNER", 105, 100, 360, 400),
        new("ARTIST", 95, 105, 320, 600),
        new("BARD", 100, 110, 320, 520),
    ];
    internal sealed record Fields(uint Base, uint Bonus, uint BaseRate, uint BonusRate, uint FixedBonusRate, uint FinalRate);
    internal sealed record Step(long Ordinal, string Owner, FunctionKind Kind, bool Condition, Fields? State, uint? Exact);
    internal sealed record StatValue(int Current, uint Exact, Fields Fields, ImmutableArray<Step> History);
    internal sealed record Derived(int MaxHp, int MaxMp, int Health, int Will, int HealthAdditional,
        int WillAdditional, int BaseHp, int BaseMp, int Delay, int Speed,
        ImmutableDictionary<string, StatValue> Stats, ImmutableArray<Function> Registry);
    internal sealed record RetainedProgram(Binding Current, string ProgramSourceSha256, string Program,
        SourceRecord? Record, string ChildPath, ImmutableArray<string> Stats, int RegistrationIndex);
    internal static ImmutableArray<RetainedProgram> RetainedPrograms(Graph graph, string operation, string epoch)
    {
        graph.Current.Validate(operation, epoch);
        Require(graph.CompleteAllOwnersAndStatLists && graph.RegistrationOrderKnown
            && !graph.Registrations.IsDefault, "complete retained owner registry/order");
        var result = ImmutableArray.CreateBuilder<RetainedProgram>();
        // Known predefined functions with no inline life/speed getter in this program. Their
        // actual owner references survive deferred SM_STATS_INFO; no zero scalar is invented.
        (string Program, string Stat, int Index)[] names = [("PhysicalAttackFunction", "PHYSICAL_ATTACK", 0),
            ("MagicalAttackFunction", "MAGICAL_ATTACK", 1), ("BoostCastingTimeFunction", "BOOST_CASTING_TIME", 3),
            ("PvPAttackRatioFunction", "PVP_ATTACK_RATIO", 4), ("PDefFunction", "PHYSICAL_DEFENSE", 5),
            ("BlockFunction", "BLOCK", 8), ("ParryFunction", "PARRY", 9), ("EvasionFunction", "EVASION", 10),
            ("PhysicalCriticalFunction", "PHYSICAL_CRITICAL", 11), ("PhysicalAccuracyFunction", "PHYSICAL_ACCURACY", 12),
            ("PvEAttackRatioFunction", "PVE_ATTACK_RATIO", 13), ("PvEDefendRatioFunction", "PVE_DEFEND_RATIO", 14)];
        foreach (var name in names) result.Add(new(Predefined(graph.Current),
            FunctionProgram(FunctionKind.Intrinsic), name.Program, null, "source-predefined-registration", [name.Stat], name.Index));
        int ordinal = 0;
        foreach (Registration r in graph.Registrations)
        {
            if (!r.ChildPath.StartsWith("retained/e=", StringComparison.Ordinal)) { ordinal++; continue; }
            r.Current.Validate(operation, epoch);
            Require(r.SourceRecipeUnchanged == true, "current retained-source construction/materialization recipe");
            Require(r.Source.Key is "skill_template-37.xml" or "skill_template-54.xml"
                && r.ChildPath == "retained/e=1" && r.MaterializedSkillLevel == 1, "known noninline mastery source child");
            XElement root = r.Source.Read();
            XElement[] children = root.Element("effects")?.Elements().Where(x => Integer(x, "e") == 1).ToArray() ?? [];
            Require(children.Length == 1 && children[0].Name.LocalName is "wpnmastery" or "armormastery", "known mastery program");
            ImmutableArray<string> stats = children[0].Elements("change").Select(x => x.Attribute("stat")?.Value ?? "").ToImmutableArray();
            Require(!stats.IsEmpty && stats.All(x => x is not ("HEALTH" or "WILL" or "MAXHP" or "MAXMP" or "SPEED" or "FLY_SPEED" or "ATTACK_SPEED")
                && !string.IsNullOrWhiteSpace(x)), "retained program reaches no evaluated getter");
            if (children[0].Name.LocalName == "wpnmastery")
            {
                Require(children[0].Attribute("weapon")?.Value == "SWORD"
                    && stats.All(x => x is "PHYSICAL_ATTACK" or "MAGICAL_ATTACK"), "finite one-hand mastery constructor");
                // WeaponMasteryEffect rewrites each one-hand attack change into both power lists.
                stats = stats.SelectMany(_ => new[] { "MAIN_HAND_POWER", "OFF_HAND_POWER" }).ToImmutableArray();
            }
            string factory = r.Source.Key == "skill_template-37.xml"
                ? "e9e3046bcbbfb89fd41fb24ccebf0770ede9c0f9a0c375a34071aa92f797fc62"
                : "ec599f60416849f9031950160d5096496f834ed9188d78f7bf4a6e75a10173ba";
            result.Add(new(r.Current, factory, children[0].Name.LocalName, r.Source, r.ChildPath, stats, ordinal));
            ordinal++;
        }
        return result.ToImmutable();
    }
    internal sealed record Bases(int Hp, int Mp, int HealthAdditional, int WillAdditional,
        uint HpMod1, uint HpMod2, uint MpMod2);
    internal static Bases ClassBases(int classId, int level, string classSourceSha256)
    {
        Require(classSourceSha256 == ClassSource && classId is >= 0 and < 17 && level is >= 1 and <= 65,
            "class source/finite level domain");
        ClassRow c = Classes[classId];
        float h1 = Mul(0.1075f, c.HealthMultiplier), h2 = Mul(0.002875f, c.HealthMultiplier);
        int square = unchecked(level * level);
        int hp = JavaInt(Add(Add(c.HealthMultiplier / 2, Mul(level, h1)), Mul(square, h2)));
        float mb = Mul(c.WillMultiplier, 0.35f);
        float m2 = Div(Mul(unchecked(square * c.WillMultiplier), 0.125f), 10000f);
        int mp = JavaInt(Add(Add(mb, Div(Mul(level, mb), 2f)), m2));
        return new(hp, mp, JavaInt(Mul(Div(unchecked(c.Health - 100), 100f), c.HealthMultiplier)),
            JavaInt(Mul(Div(unchecked(c.Will - 100), 100f), c.WillMultiplier)), Word(h1), Word(h2), Word(m2));
    }
    internal static ImmutableArray<Function> Project(Graph graph, string operation, string epoch)
    {
        graph.Current.Validate(operation, epoch);
        Require(graph.CompleteAllOwnersAndStatLists && graph.RegistrationOrderKnown
            && !graph.Registrations.IsDefault && !graph.CurrentConstructorFunctions.IsDefault, "complete owned registry/order");
        Require(graph.Registrations.Length <= 1024 && graph.CurrentConstructorFunctions.Length <= 1024, "bounded graph");
        var functions = ImmutableArray.CreateBuilder<Function>();
        // These two PlayerStatFunctions are registered before added item/effect owners.
        Binding intrinsic = Predefined(graph.Current);
        functions.Add(new(FunctionKind.Intrinsic, "MAXHP", 0, false, [], "predefined", 0, intrinsic,
            SourceProgramSha256: FunctionProgram(FunctionKind.Intrinsic)));
        functions.Add(new(FunctionKind.Intrinsic, "MAXMP", 0, false, [], "predefined", 1, intrinsic,
            SourceProgramSha256: FunctionProgram(FunctionKind.Intrinsic)));
        var owners = new HashSet<string>(StringComparer.Ordinal);
        var generations = new Dictionary<string, string>(StringComparer.Ordinal) { ["predefined"] = intrinsic.Generation };
        foreach (Registration r in graph.Registrations)
        {
            r.Current.Validate(operation, epoch);
            Require(r.SourceRecipeUnchanged == true, "current source construction/materialization recipe");
            Require(owners.Add(r.Current.Owner) && r.MaterializedSkillLevel >= 0 && r.TemperingLevel >= 0, "duplicate current source owner/negative level");
            Require(generations.TryAdd(r.Current.Owner, r.Current.Generation), "reserved/duplicate current function owner");
            XElement root = r.Source.Read();
            IEnumerable<XElement> nodes;
            bool skill = false;
            if (r.ChildPath.StartsWith("retained/e=", StringComparison.Ordinal))
            { _ = RetainedPrograms(graph, operation, epoch); continue; }
            if (r.ChildPath == "modifiers")
            {
                Require(root.Name == "item_template", "item modifier owner source");
                XElement[] children = root.Elements("modifiers").ToArray();
                Require(children.Length <= 1, "duplicate modifiers");
                nodes = children.SelectMany(x => x.Elements());
            }
            else if (r.ChildPath == "POLISH:8:2")
            {
                Require(root.Name == "random_bonus" && root.Attribute("type")?.Value == "POLISH"
                    && Integer(root, "id") == 8, "composite random-bonus identity");
                XElement[] variants = root.Elements("modifiers").ToArray();
                Require(variants.Length >= 2, "one-based bonus index");
                nodes = variants[1].Elements();
            }
            else if (r.ChildPath == "PLUME_HP")
            {
                Require(r.Source.Key == "item_template-187100015.xml" && r.TemperingLevel <= 255, "source PLUM_HP tempering");
                functions.Add(new(FunctionKind.Add, "MAXHP", unchecked(150 * r.TemperingLevel), true,
                    [], r.Current.Owner, functions.Count, r.Current, SourceProgramSha256: FunctionProgram(FunctionKind.Add)));
                continue;
            }
            else if (r.ChildPath.StartsWith("effects/e=", StringComparison.Ordinal))
            {
                Require(root.Name == "skill_template", "materialized skill source");
                Require(int.TryParse(r.ChildPath.AsSpan(10), NumberStyles.None, CultureInfo.InvariantCulture, out int selected)
                    && selected > 0 && r.ChildPath == "effects/e=" + selected.ToString(CultureInfo.InvariantCulture)
                    && Integer(root, "lvl") == r.MaterializedSkillLevel, "materialized skill/effect identity");
                XElement[] matches = root.Element("effects")?.Elements().Where(x => Integer(x, "e") == selected).ToArray() ?? [];
                Require(matches.Length == 1 && matches[0].Name.LocalName is "statup" or "statboost" or "statdown" or "curse", "selected effect child program");
                Require(matches[0].Elements().All(x => x.Name == "change"), "unknown registered effect child");
                nodes = matches[0].Elements("change"); skill = true;
            }
            else throw new Domain("unknown current source child path");
            foreach (XElement node in nodes)
            {
                FunctionKind kind = skill ? node.Attribute("func")?.Value switch
                { "ADD" => FunctionKind.Add, "PERCENT" => FunctionKind.Rate, "REPLACE" => FunctionKind.Set,
                    _ => throw new Domain("unknown registered effect function") }
                    : node.Name.LocalName switch { "add" => FunctionKind.Add, "sub" => FunctionKind.Sub,
                    "rate" => FunctionKind.Rate, "set" => FunctionKind.Set, "abs" => FunctionKind.Abs,
                    _ => throw new Domain("unknown registered function subclass") };
                string? stat = node.Attribute(skill ? "stat" : "name")?.Value;
                Require(!string.IsNullOrWhiteSpace(stat), "stat identity");
                int operand = skill ? unchecked(Integer(node, "value", 0) + unchecked(Integer(node, "delta", 0) * r.MaterializedSkillLevel))
                    : Integer(node, "value");
                bool bonus = skill ? kind != FunctionKind.Set : Boolean(node, "bonus");
                Require(node.Elements().All(x => x.Name == "conditions") && node.Elements("conditions").Count() <= 1, "function child/condition list");
                var conditions = ImmutableArray.CreateBuilder<Condition>();
                foreach (XElement condition in node.Elements("conditions").SelectMany(x => x.Elements()))
                {
                    Require(!condition.HasElements, "condition nested program");
                    conditions.Add(condition.Name.LocalName switch
                    { "charge" => new(ConditionKind.Charge, Integer(condition, "value")),
                      "onfly" => new(ConditionKind.OnFly, 0), _ => throw new Domain("unknown condition subclass") });
                }
                functions.Add(new(kind, stat, operand, bonus, conditions.ToImmutable(), r.Current.Owner,
                    functions.Count, r.Current, r.Abs, FunctionProgram(kind)));
            }
        }
        // Independently bound constructor/current-field functions, not caller-supplied results.
        foreach (Function function in graph.CurrentConstructorFunctions)
        {
            Require(function.Kind != FunctionKind.Intrinsic && function.Ordinal == functions.Count, "constructor function registration ordinal");
            if (generations.TryGetValue(function.Owner, out string? generation))
                Require(function.Current.Generation == generation, "current constructor/registration owner generation");
            else generations.Add(function.Owner, function.Current.Generation);
            functions.Add(function);
        }
        Require(functions.Count <= 8192, "bounded materialized graph");
        foreach (Function function in functions) _ = function.Priority(operation, epoch);
        return functions.ToImmutable();
    }
    private static Binding Predefined(Binding graph) => graph with
    {
        Owner = "predefined",
        Evidence = new N.Evidence(N.FactKind.SourceBoundDeclaration,
            "source-predefined projection from current graph receipt: " + graph.Evidence.Receipt)
    };

    internal static Derived Evaluate(int classId, int level, string classSourceSha256,
        ImmutableArray<Function> registry, ImmutableDictionary<string, int> charge,
        bool flying, int weaponDelay, string? mainWeaponOwner, string operation, string epoch)
    {
        Bases b = ClassBases(classId, level, classSourceSha256); ClassRow c = Classes[classId];
        var stats = ImmutableDictionary.CreateBuilder<string, StatValue>(StringComparer.Ordinal);
        StatValue h = EvaluateStat("HEALTH", c.Health, registry, charge, flying, 0, operation, epoch);
        StatValue w = EvaluateStat("WILL", c.Will, registry, charge, flying, 0, operation, epoch);
        int ah = JavaInt(Mul(Div(unchecked(h.Current - 100), 100f), c.HealthMultiplier));
        int am = JavaInt(Mul(Div(unchecked(w.Current - 100), 100f), c.WillMultiplier));
        stats.Add("HEALTH", h); stats.Add("WILL", w);
        StatValue hp = EvaluateStat("MAXHP", b.Hp, registry, charge, flying, ah, operation, epoch);
        StatValue mp = EvaluateStat("MAXMP", b.Mp, registry, charge, flying, am, operation, epoch);
        stats.Add("MAXHP", hp); stats.Add("MAXMP", mp);
        string speedStat = flying ? "FLY_SPEED" : "SPEED";
        StatValue speed = EvaluateStat(speedStat, flying ? 9000 : 6000, registry, charge, flying, 0, operation, epoch);
        stats.Add(speedStat, speed);
        // ItemEquipmentListener excludes weapon ATTACK_SPEED from ordinary item registration.
        // One actual unfused main weapon in this finite source domain; no offhand/fusion guess.
        Function[] weapon = registry.Where(x => x.Stat == "ATTACK_SPEED" && x.Owner == mainWeaponOwner
            && Valid(x, charge, flying)).ToArray();
        Function? selected = weapon.OrderByDescending(x => x.Value).FirstOrDefault();
        var delayFunctions = registry.Where(x => x.Stat != "ATTACK_SPEED" || x.Owner != mainWeaponOwner).ToImmutableArray();
        if (selected is not null)
        {
            // DuplicateStatFunction overrides inherited priority to60; apply the selected raw function there.
            long order = delayFunctions.IsEmpty ? 2 : delayFunctions.Max(x => x.Ordinal) + 1;
            delayFunctions = delayFunctions.Add(selected with { Ordinal = order });
        }
        StatValue delay = EvaluateStat("ATTACK_SPEED", weaponDelay, delayFunctions, charge, flying, 0, operation, epoch,
            selected is null ? null : selected.Owner);
        stats.Add("ATTACK_SPEED", delay);
        return new(hp.Current, mp.Current, h.Current, w.Current, ah, am, b.Hp, b.Mp,
            delay.Current, speed.Current, stats.ToImmutable(), registry);
    }

    internal static StatValue EvaluateStat(string name, int initial, ImmutableArray<Function> functions,
        ImmutableDictionary<string, int> charge, bool flying, int intrinsic, string operation, string epoch,
        string? duplicateWeaponOwner = null)
    {
        Require(!functions.IsDefault && functions.Length <= 8192, "stat registry bound");
        Require(functions.Select(x => x.Ordinal).Distinct().Count() == functions.Length, "distinct stable function ordinals");
        State s = new() { Base = initial };
        var history = ImmutableArray.CreateBuilder<Step>();
        foreach (Function fn in functions.Select(x => (Fn: x, Priority: x.Priority(operation, epoch)))
            .OrderBy(x => x.Fn.Stat == "ATTACK_SPEED" && x.Fn.Owner == duplicateWeaponOwner ? 60 : x.Priority)
            .ThenBy(x => x.Fn.Ordinal).Select(x => x.Fn))
        {
            if (fn.Stat != name) continue;
            bool valid = Valid(fn, charge, flying);
            if (!valid) { history.Add(new(fn.Ordinal, fn.Owner, fn.Kind, false, null, null)); continue; }
            switch (fn.Kind)
            {
                case FunctionKind.Intrinsic:
                    Require(fn.Stat is "MAXHP" or "MAXMP" && !fn.Bonus && fn.Owner == "predefined", "intrinsic identity");
                    s.Base = Add(s.Base, intrinsic); break;
                case FunctionKind.Add: if (fn.Bonus) s.Bonus = Add(s.Bonus, fn.Value); else s.Base = Add(s.Base, fn.Value); break;
                case FunctionKind.Sub: if (fn.Bonus) s.Bonus = Add(s.Bonus, unchecked(-fn.Value)); else s.Base = Add(s.Base, unchecked(-fn.Value)); break;
                case FunctionKind.Set: if (fn.Bonus) s.Bonus = fn.Value; else s.Base = fn.Value; break;
                case FunctionKind.Rate:
                    if (fn.Bonus)
                    {
                        int value = name == "SPEED" && fn.Value < 0 && JavaInt(s.Bonus) < 0 ? JavaInt(s.Current()) : JavaInt(s.Base);
                        s.Bonus = Add(s.Bonus, Div(unchecked(value * fn.Value), 100f));
                    }
                    else s.Base = Mul(JavaInt(s.Base), Div(unchecked(100 + fn.Value), 100f));
                    break;
                case FunctionKind.Abs: if (!fn.Bonus) { s.Base = fn.Value; s.Bonus = 0; s.BaseRate = 1; } break;
                default: throw new Domain("unknown stat function");
            }
            history.Add(new(fn.Ordinal, fn.Owner, fn.Kind, true, s.Fields(), Word(s.Current())));
        }
        (int lower, int upper) = name switch { "HEALTH" or "WILL" => (80, 999), "MAXHP" => (100, int.MaxValue),
            "MAXMP" => (1, int.MaxValue), "SPEED" => (0, 12000), "FLY_SPEED" => (0, 16000),
            "ATTACK_SPEED" => (500, 10000), _ => throw new Domain("unadmitted evaluated stat") };
        s.Cap(lower, upper);
        if (name == "ATTACK_SPEED") s.Cap(JavaInt(Mul(JavaInt(Mul(s.Base, s.BaseRate)), .5f)), unchecked(JavaInt(Mul(s.Base, s.BaseRate)) * 2));
        return new(JavaInt(s.Current()), Word(s.Current()), s.Fields(), history.ToImmutable());
    }
    private static bool Valid(Function fn, ImmutableDictionary<string, int> charge, bool flying)
    {
        foreach (Condition condition in fn.Conditions)
        {
            if (condition.Kind == ConditionKind.OnFly) { if (!flying) return false; }
            else if (condition.Kind == ConditionKind.Charge)
            {
                Require(charge.TryGetValue(fn.Owner, out int points), "charge condition actual proxy item owner");
                if (Tier(points) < condition.Value) return false;
            }
            else throw new Domain("unknown condition program");
        }
        return true;
    }
    // Separate general entry points. No fixed-source API, guard, operation or arithmetic above changes.
    internal static RetailNpcUserStatSourceMaterialization.ProjectionResult ProjectGeneral(
        RetailNpcUserStatSourceMaterialization.RegistryInput input) => RetailNpcUserStatSourceMaterialization.Project(input);
    internal static RetailNpcUserStatSourceMaterialization.RefreshResult EvaluateGeneral(
        RetailNpcUserStatSourceMaterialization.Projection projection, GeneralStatFrame frame) =>
        RetailNpcUserStatSourceMaterialization.Refresh(projection, frame);
    internal sealed record GeneralOperation(RetailNpcUserStatSourceRecipes.Function Function,
        long Ordinal, bool Condition, int Intrinsic);
    /// <summary>Same State channels/caps and Java operators as the fixed entry; the caller supplies
    /// source-validated current list order and computed predicates, including the duplicate invocation.
    /// No source selection or live effect application is performed here.</summary>
    internal static StatValue EvaluateGeneralOrdered(string name, int initial, ImmutableArray<GeneralOperation> operations)
    {
        Require(!operations.IsDefault && operations.Length<=8192 && operations.Select(x=>x.Ordinal).Distinct().Count()==operations.Length,
            "bounded distinct general read order");
        State s=new(){Base=initial};var history=ImmutableArray.CreateBuilder<Step>();
        foreach(GeneralOperation op in operations)
        {
            var fn=op.Function;Require(fn.Stat==name,"general current stat list target");
            if(!op.Condition){history.Add(new(op.Ordinal,fn.Owner.Owner,fn.Kind,false,null,null));continue;}
            if(fn.Program==RetailNpcUserStatSourceRecipes.Program.WeaponMastery)
            {
                if(fn.Bonus)s.FixedBonusRate=Div(fn.Value,100f);
                else s.Base=Mul(s.Base,Div(unchecked(100+fn.Value),100f));
            }
            else if(fn.Program==RetailNpcUserStatSourceRecipes.Program.DuplicateDelay)
            { /* Source-bound empty duplicate invocation; selected raw functions are expanded at this position. */ }
            else
            {
                int operand=fn.Program==RetailNpcUserStatSourceRecipes.Program.ArmorMastery
                    ? RetailNpcUserStatSourceRecipes.JavaDivide(unchecked(fn.Value*fn.ArmorFactor),100):fn.Value;
                switch(fn.Kind)
                {
                    case FunctionKind.Intrinsic:
                        Require(fn.Program is RetailNpcUserStatSourceRecipes.Program.MaxHp or RetailNpcUserStatSourceRecipes.Program.MaxMp,
                            "source intrinsic program");s.Base=Add(s.Base,op.Intrinsic);break;
                    case FunctionKind.Add:if(fn.Bonus)s.Bonus=Add(s.Bonus,operand);else s.Base=Add(s.Base,operand);break;
                    case FunctionKind.Sub:if(fn.Bonus)s.Bonus=Add(s.Bonus,unchecked(-operand));else s.Base=Add(s.Base,unchecked(-operand));break;
                    case FunctionKind.Set:if(fn.Bonus)s.Bonus=operand;else s.Base=operand;break;
                    case FunctionKind.Rate:
                        if(fn.Bonus)
                        {
                            int value=name=="SPEED" && operand<0 && JavaInt(s.Bonus)<0?JavaInt(s.Current()):JavaInt(s.Base);
                            s.Bonus=Add(s.Bonus,Div(unchecked(value*operand),100f));
                        }
                        else s.Base=Mul(JavaInt(s.Base),Div(unchecked(100+operand),100f));
                        break;
                    case FunctionKind.Abs:if(!fn.Bonus){s.Base=operand;s.Bonus=0;s.BaseRate=1;}break;
                    default:throw new Domain("unknown reached general arithmetic");
                }
                if(fn.Program==RetailNpcUserStatSourceRecipes.Program.ArmorMastery && fn.ArmorFixedBonus!=0 && fn.ArmorFactor!=0)
                    s.Bonus=Add(s.Bonus,Div(unchecked(fn.ArmorFixedBonus*fn.ArmorFactor),100f));
            }
            history.Add(new(op.Ordinal,fn.Owner.Owner,fn.Kind,true,s.Fields(),Word(s.Current())));
        }
        (int lower,int upper)=name switch{"HEALTH" or "WILL"=>(80,999),"MAXHP"=>(100,int.MaxValue),
            "MAXMP"=>(1,int.MaxValue),"SPEED"=>(0,12000),"FLY_SPEED"=>(0,16000),"ATTACK_SPEED"=>(500,10000),
            _=>throw new Domain("unadmitted general evaluated stat")};
        s.Cap(lower,upper);
        if(name=="ATTACK_SPEED")s.Cap(JavaInt(Mul(JavaInt(Mul(s.Base,s.BaseRate)),.5f)),unchecked(JavaInt(Mul(s.Base,s.BaseRate))*2));
        return new(JavaInt(s.Current()),Word(s.Current()),s.Fields(),history.ToImmutable());
    }
    private sealed class State
    {
        internal float Base, Bonus, BaseRate = 1, BonusRate = 1, FixedBonusRate = 0, FinalRate = 1;
        internal float Current() => Mul(Add(Add(Mul(Base, BaseRate), Mul(Bonus, BonusRate)), Mul(Base, FixedBonusRate)), FinalRate);
        internal Fields Fields() => new(Word(Base), Word(Bonus), Word(BaseRate), Word(BonusRate), Word(FixedBonusRate), Word(FinalRate));
        internal void Cap(int lower, int upper)
        {
            float current = Current();
            if (current > (float)upper || current < (float)lower)
            {
                FinalRate = BonusRate = 1;
                // Native Java cap clears bonusRate/finalRate; computes without bonus, retaining base channels.
                float withoutBonus = Mul(Add(Mul(Base, BaseRate), Mul(Base, FixedBonusRate)), FinalRate);
                Bonus = Sub(current > (float)upper ? upper : lower, withoutBonus);
            }
        }
    }
    internal static int Tier(int points) => points <= 0 ? 0 : (points + 499999) / 500000;
    internal static int Bucket(int points) => points <= 0 ? 0 : (points + 49999) / 50000;
    internal static uint Word(float f) => BitConverter.SingleToUInt32Bits(f);
    internal static int JavaInt(float f)
    {
        Require(float.IsFinite(f), "finite stat arithmetic");
        if (f >= 2147483648f) return int.MaxValue;
        if (f <= -2147483648f) return int.MinValue;
        return (int)f;
    }
    internal static int JavaRound(float f)
    {
        Require(float.IsFinite(f), "finite life ratio");
        double rounded = Math.Floor((double)f + .5d);
        return rounded >= int.MaxValue ? int.MaxValue : rounded <= int.MinValue ? int.MinValue : (int)rounded;
    }
    internal static int Ratio(int current, int freshMaximum, int cachedMaximum)
    {
        Require(current >= 0 && freshMaximum > 0 && cachedMaximum > 0, "positive cached-life ratio inputs");
        long product = (long)current * freshMaximum;
        return Math.Max(1, JavaRound(Div((float)product, cachedMaximum)));
    }
    [MethodImpl(MethodImplOptions.NoInlining)] internal static float Add(float a, float b) => Finite(a + b);
    [MethodImpl(MethodImplOptions.NoInlining)] internal static float Sub(float a, float b) => Finite(a - b);
    [MethodImpl(MethodImplOptions.NoInlining)] internal static float Mul(float a, float b) => Finite(a * b);
    [MethodImpl(MethodImplOptions.NoInlining)] internal static float Div(float a, float b) { Require(b != 0, "division denominator"); return Finite(a / b); }
    private static float Finite(float f) { Require(float.IsFinite(f), "finite binary32 intermediate"); return f; }
}
