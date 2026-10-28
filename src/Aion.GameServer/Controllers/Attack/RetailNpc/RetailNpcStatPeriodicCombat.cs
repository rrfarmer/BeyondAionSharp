using System.Collections.Immutable;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using Aion.GameServer.Controllers.Observer;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Retail;
using Aion.GameServer.SkillEngine.Effects;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Utils;
using Microsoft.Extensions.Logging;
using F = Aion.GameServer.Controllers.Attack.RetailNpc.RetailNpcUserMagicalInputs;
using Family = Aion.GameServer.Controllers.Attack.RetailNpc.RetailNpcPeriodicSpellParameters.Family;
using LOG = Aion.GameServer.Network.Aion.ServerPackets.SmAttackStatus.LOG;

namespace Aion.GameServer.Controllers.Attack.RetailNpc;

/// <summary>Source-bound periodic damage numerics on the existing Effect timer.
/// Check caches the linear base and magical critical; each existing periodic
/// callback reads current fortitude/PvE and makes one controller/HP dispatch.
/// Native evidence: external periodic-stat-review-01 (Main bbe370/bbed40 and
/// actual Poison/Bleed identity virtuals). No AI, target selector or timer owner.</summary>
internal static class RetailNpcStatPeriodicCombat
{
    // Keep authored zero distinct from an absent operand. This is source data,
    // not permission to run an incomplete instant/periodic drain program.
    internal sealed record DrainOperands(int? Radius, int? HpRate, int? HpPercent,
        int? MpRate, int? MpPercent, int? Remain1, int? Remain2, int? BasicLevel,
        int? NoResist, string? TargetType, string? PreEffect, int? PreEffectProbability1,
        int? PreEffectProbability2, int? EffectId = null,
        ImmutableArray<KeyValuePair<string,string>> OrderedFields = default);
    internal sealed record Part(int Position, Family Family, int Delta, int Value, int Interval,
        SkillElement Element, int CriticalPercent, string? UnsupportedReason,
        bool ApplyBoost = false, bool ApplyMagicalDefense = false, int DurationJitter = 0,
        DrainOperands? Drain = null)
    { internal bool Supported => UnsupportedReason is null; }
    internal sealed record InstantDrainOperands(int? Value, int? LevelRate, int? WeaponMode,
        int? HpRate, int? HpPercent, int? MpRate, int? MpPercent, int? NoResist,
        int? CriticalPercent, string? Element, string? TargetType, string? FirstTarget,
        string? TargetRange, string? TargetRelation, int? TargetMaxCount, int? ApplyBoost,
        int? ApplyCritical, ImmutableArray<KeyValuePair<string,string>> OrderedPrimaryFields,
        int? FirstTargetValidDistance = null, string? TargetSpecies = null);
    internal sealed record Source(int Id, string Name, string Sha256, ImmutableArray<Part> Parts,
        InstantDrainOperands? InitialDrain = null);
    private static readonly ILogger Log = AionLog.For(nameof(RetailNpcStatPeriodicCombat));
    private sealed class PreparedDrainTickRetiredException()
        : InvalidOperationException("Prepared NPC drain periodic work retired");
    private static readonly ConditionalWeakTable<Npc, HashSet<(int, int, string)>> Warnings = new();
    private static readonly ConditionalWeakTable<Effect, Dictionary<int, State>> States = new();

    internal static Source Parse(ReadOnlySpan<byte> bytes, string sha)
    {
        // Reuse the complete-record identity/XML validation, even if its primary
        // recipe is outside the immediate-damage family.
        var identity = RetailNpcStatSkillCombat.ParseSource(bytes, sha);
        var root = XDocument.Parse(new UnicodeEncoding(false, false, true).GetString(bytes)).Root!;
        var parts = ImmutableArray.CreateBuilder<Part>();
        for (int position = 1; position <= 8; position++)
        {
            string prefix = "effect" + position + "_";
            string? Leaf(string name)
            {
                var rows = root.Elements(name).ToArray();
                if (rows.Length == 0) return null;
                if (rows.Length != 1 || rows[0].HasElements || rows[0].HasAttributes)
                    throw new InvalidDataException("Ambiguous periodic field: " + name);
                return rows[0].Value;
            }
            string? type = Leaf(prefix + "type");
            if (!string.Equals(type, "Poison", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(type, "Bleed", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(type, "SpellATK", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(type, "SpellATKDrain", StringComparison.OrdinalIgnoreCase)) continue;
            int Number(string name, int fallback = 0)
            {
                string? value = Leaf(name);
                if (value is null) return fallback;
                if (!int.TryParse(value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int n))
                    throw new InvalidDataException("Noninteger periodic operand: " + name);
                return n;
            }
            int? OptionalNumber(string name) => Leaf(name) is null ? null : Number(name);
            var family = string.Equals(type, "Poison", StringComparison.OrdinalIgnoreCase) ? Family.Poison
                : string.Equals(type, "Bleed", StringComparison.OrdinalIgnoreCase) ? Family.Bleed
                : string.Equals(type, "SpellATKDrain", StringComparison.OrdinalIgnoreCase) ? Family.SpellDrain
                : Family.SpellAttack;
            int delta = Number(prefix + "reserved8"), value = Number(prefix + "reserved9"), interval = Number(prefix + "checktime");
            int critical = Number(prefix + "critical_prob_mod2", 100);
            int boostFlag = Number("apply_magical_skill_boost_bonus"), jitter = Number(prefix + "randomtime");
            SkillElement element = (Leaf(prefix + "reserved10") ?? "").ToLowerInvariant() switch
            {
                "fire" => SkillElement.FIRE, "air" => SkillElement.WIND, "water" => SkillElement.WATER,
                "earth" => SkillElement.EARTH, "light" => SkillElement.LIGHT, "dark" => SkillElement.DARK,
                _ => SkillElement.NONE
            };
            string? reason = interval <= 0 ? "nonpositive-periodic-interval"
                : Number(prefix + "critical_prob_mod1") != 0 || critical is < 0 or > 100000 ? "unreviewed-periodic-critical-probability"
                : Number(prefix + "critical_add_dmg_mod1") != 0 || Number(prefix + "critical_add_dmg_mod2") != 0 ? "unreviewed-periodic-critical-add"
                : Number(prefix + "reserved19") != 0 ? "unreviewed-periodic-sharing"
                : Leaf("exclusive_attribute") is not (null or "0") ? "unreviewed-periodic-source-exclusive"
                : family is Family.SpellAttack or Family.SpellDrain && boostFlag is not (0 or 1) ? "unreviewed-periodic-boost-flag"
                : jitter < 0 ? "unreviewed-periodic-duration-jitter"
                : delta < 0 || value < 0 || element == SkillElement.NONE ? "unreviewed-periodic-base-or-element"
                : family == Family.SpellDrain ? "unreviewed-coherent-instant-periodic-drain"
                : null;
            var drain = family == Family.SpellDrain ? new DrainOperands(
                OptionalNumber(prefix + "reserved7"), OptionalNumber(prefix + "reserved14"),
                OptionalNumber(prefix + "reserved15"), OptionalNumber(prefix + "reserved16"),
                OptionalNumber(prefix + "reserved17"), OptionalNumber(prefix + "remain1"),
                OptionalNumber(prefix + "remain2"), OptionalNumber(prefix + "basiclv"),
                OptionalNumber(prefix + "noresist"), Leaf(prefix + "target_type"),
                Leaf(prefix + "cond_preeffect"), OptionalNumber(prefix + "cond_preeffect_prob1"),
                OptionalNumber(prefix + "cond_preeffect_prob2"), OptionalNumber(prefix + "effectid"),
                root.Elements().Where(x=>x.Name.LocalName.StartsWith(prefix,StringComparison.Ordinal))
                    .Select(x=>new KeyValuePair<string,string>(x.Name.LocalName,Leaf(x.Name.LocalName)!)).ToImmutableArray()) : null;
            bool magical = family is Family.SpellAttack or Family.SpellDrain;
            parts.Add(new(position, family, delta, value, interval, element, critical, reason,
                magical && boostFlag != 0, magical && Number(prefix + "reserved12") != 0, jitter, drain));
        }
        var selected=parts.ToImmutable();
        InstantDrainOperands? initial=null;
        if(selected.Any(p=>p.Family==Family.SpellDrain))
        {
            string? Leaf(string name)
            {
                var rows=root.Elements(name).ToArray();
                if(rows.Length==0)return null;
                if(rows.Length!=1 || rows[0].HasElements || rows[0].HasAttributes)
                    throw new InvalidDataException("Ambiguous initial drain field: "+name);
                return rows[0].Value;
            }
            int? Number(string name)
            {
                string? value=Leaf(name);
                if(value is null)return null;
                if(!int.TryParse(value,NumberStyles.AllowLeadingSign,CultureInfo.InvariantCulture,out int number))
                    throw new InvalidDataException("Noninteger initial drain operand: "+name);
                return number;
            }
            if(Leaf("effect1_type")=="SpellATKDrain_Instant")
            {
                var fields=root.Elements().Where(x=>x.Name.LocalName.StartsWith("effect1_",StringComparison.Ordinal))
                    .Select(x=>new KeyValuePair<string,string>(x.Name.LocalName,Leaf(x.Name.LocalName)!)).ToImmutableArray();
                initial=new(Number("effect1_reserved2"),Number("effect1_reserved1"),Number("effect1_reserved6"),
                    Number("effect1_reserved14"),Number("effect1_reserved15"),Number("effect1_reserved16"),Number("effect1_reserved17"),
                    Number("effect1_noresist"),Number("effect1_critical_prob_mod2"),Leaf("effect1_reserved10"),
                    Leaf("effect1_target_type"),Leaf("first_target"),Leaf("target_range"),Leaf("target_relation_restriction"),
                    Number("target_maxcount"),Number("apply_magical_skill_boost_bonus"),Number("apply_magical_critical"),fields,
                    Number("first_target_valid_distance"),Leaf("target_species_restriction"));
            }
        }
        return new(identity.Id, identity.Name, sha, selected, initial);
    }

    internal sealed record Application(int LinearBase,int Boost,int Suppression,int? Cap,int CachedBase);

    private sealed class State(Effect effect, AbstractOverTimeEffect template, Source source, Part part,
        SkyTempleNpcProfile profile, SkyTempleNpcProfile? targetProfile, int sourceLevel, int packedBase, bool critical, Application application)
    {
        internal readonly object Gate = new();
        internal readonly Effect Effect = effect;
        internal readonly AbstractOverTimeEffect Template = template;
        internal readonly Source Source = source;
        internal readonly Application Application = application;
        internal readonly Part Part = part;
        internal readonly SkyTempleNpcProfile Profile = profile;
        internal readonly SkyTempleNpcProfile? TargetProfile = targetProfile;
        internal readonly int SourceLevel = sourceLevel;
        internal readonly int Base = packedBase, CasterId = effect.GetEffector().GetObjectId();
        internal readonly bool Critical = critical;
        internal readonly int Map = effect.GetEffected().GetWorldId(), Instance = effect.GetEffected().GetInstanceId();
        internal readonly long Life = effect.GetEffected().GetController().RetailLifeEpoch;
        internal bool Started;
        internal int Tick;
    }

    internal static bool TryResolveCritical(Effect effect, AbstractOverTimeEffect template)
    {
        if (effect.GetEffector() is not Npc npc || !RetailNpcStatCatalog.TryGet(npc, out var profile)) return false;
        bool Compatibility(string reason)
        {
            var warnings = Warnings.GetValue(npc, _ => new());
            lock (warnings) if (warnings.Add((effect.GetSkillId(), template.Position, reason)))
                Log.LogWarning("RETAIL_STAT_PERIODIC_COMPAT npc={Npc} skill={Skill} effect={Position} reason={Reason}; host periodic program retained",
                    profile.NpcId, effect.GetSkillId(), template.Position, reason);
            return false;
        }
        Creature target=effect.GetEffected();
        SkyTempleNpcProfile? targetProfile=null;
        bool npcDrain=target is Npc && template is SpellAtkDrainEffect
            && RetailNpcStatSpellDrain.TryGetPreparedNpcPeriodic(effect,out _);
        int sourceLevel=effect.GetSkillLevel();
        if(npcDrain)
        {
            _=RetailNpcStatSpellDrain.TryGetPreparedNpcPeriodic(effect,out sourceLevel);
            var targetNpc=(Npc)target;
            if(!RetailNpcStatCatalog.TryGet(targetNpc,out targetProfile))return Compatibility("unselected-or-owned-periodic-npc-target");
            var targetStats=targetNpc.GetGameStats().StatisticsProfile;
            if(targetStats is null || targetStats.Generation!=targetProfile.Generation
                || targetStats.SourceRecordSha256!=targetProfile.SourceRecordSha256)
                return Compatibility("periodic-npc-target-stats-not-materialized");
            if(targetProfile.StatRatio is <1000 or >65000)return Compatibility("unreviewed-periodic-npc-target-ratio");
            if(targetProfile.MagicalDamageTrim<0)return Compatibility("unreviewed-negative-npc-fixed-damage");
            if(target.GetObserveController().HasDrainProtectionObserver)return Compatibility("unreviewed-periodic-npc-target-protection");
        }
        else if(target is not Player)return Compatibility("non-player-target");
        if (RetailNpcStatEquipmentResponses.Required(profile)) return Compatibility("unreviewed-periodic-npc-exclusive");
        if (!RetailNpcStatCatalog.TryGetPeriodic(profile, effect.GetSkillId(), sourceLevel, out var source))
            return Compatibility("missing-exact-source-binding");
        var part = source.Parts.SingleOrDefault(x => x.Position == template.Position);
        if (part is null || (!part.Supported && !(npcDrain && part.Family==Family.SpellDrain
            && part.UnsupportedReason=="unreviewed-coherent-instant-periodic-drain")))
            return Compatibility(part?.UnsupportedReason ?? "missing-periodic-part");
        bool familyMatches = part.Family switch
        { Family.Poison => template.GetType() == typeof(PoisonEffect), Family.Bleed => template.GetType() == typeof(BleedEffect),
            Family.SpellAttack => template.GetType() == typeof(SpellAttackEffect),
            Family.SpellDrain => npcDrain && template.GetType()==typeof(SpellAtkDrainEffect), _ => false };
        if (!familyMatches || template.percent || template.shared || template.checktime != part.Interval || template.Element != part.Element
            || (!npcDrain && template.Value != part.Value) || template.Delta != part.Delta || template.RandomTime != part.DurationJitter)
            return Compatibility("host-periodic-numeric-shape-differs");
        if(npcDrain && (part.Drain is not { } drain || template is not SpellAtkDrainEffect host
            || drain.Radius is null || drain.HpRate is null || drain.HpPercent is null
            || drain.MpRate.GetValueOrDefault()!=0 || drain.MpPercent.GetValueOrDefault()!=0
            || drain.NoResist!=1 || !host.NoResist || host.hpPercent!=drain.HpPercent || host.mpPercent!=0
            || host.Duration1!=drain.Remain1 || host.Duration2!=drain.Remain2 || host.BasicLvl!=drain.BasicLevel
            || drain.EffectId is null || host.Effectid!=drain.EffectId
            || drain.TargetType!="Target_Only" || drain.PreEffect!="e1" || host.PreEffects is not [1]
            || drain.PreEffectProbability1!=0 || drain.PreEffectProbability2!=100 || host.PreEffectProb!=100))
            return Compatibility("host-npc-drain-shape-differs");
        if (sourceLevel is < 0 or > short.MaxValue) return Compatibility("unreviewed-periodic-level");
        int amount = RetailNpcPeriodicDamageStages.BaseDamage((short)sourceLevel, part.Delta, part.Value);
        if (amount is < 0 or > 0xffffff) return Compatibility("periodic-base-overlaps-native-packed-count");
        int linearBase = amount, boost = 0, suppression = 0;int? cap = null;
        if (part.ApplyBoost)
        {
            boost = SkyTempleCombatInputs.SignedWord(npc.GetGameStats().GetMBoost().GetCurrent(), "periodic boost");
            suppression = SkyTempleCombatInputs.SignedWord(target.GetGameStats().GetMBResist().GetCurrent(), "periodic suppression");
            cap = RetailNpcStatCatalog.MagicBoostCap;
            if (cap is null && unchecked(boost-suppression) >= 2900)
                return Compatibility("missing-periodic-boost-cap-context");
            amount = RetailNpcPeriodicDamageStages.SkillBoost(Family.SpellAttack, boost, suppression,
                targetProfile is null ? SkyTempleCombatInputs.PlayerStatRatio : checked((uint)targetProfile.StatRatio),
                (cap ?? 2900) == 3400 ? 0 : 1, amount).Damage;
            if (amount is < 0 or > 0xffffff) return Compatibility("boosted-periodic-base-overlaps-native-packed-count");
        }
        var application = new Application(linearBase, boost, suppression, cap, amount);
        if (npc.GetObserveController().HasSkillCriticalObserver) return Compatibility("untranslated-periodic-critical-owner");
        bool reused = effect.TryGetCalculatedMagicalCritical(out bool critical);
        if (!reused)
        {
            int raw = npc.GetGameStats().GetMCritical().GetCurrent(), resistance = target.GetGameStats().GetMCR().GetCurrent();
            float factor = F.Div(F.FromInt(part.CriticalPercent), 100f);
            int scaled = F.Cvtt(F.Mul(F.FromInt(raw), factor));
            byte pose = SkyTempleCombatInputs.Pose(target);
            int threshold = RetailNpcStatSkillCombat.CriticalThreshold(scaled, resistance, pose,
                targetProfile is null ? SkyTempleCombatInputs.PlayerStatRatio : checked((uint)targetProfile.StatRatio));
            int draw = SkyTempleNpcCombat.Draw(1, 1000); critical = draw < threshold;
            effect.SetCalculatedMagicalCritical(part.Position, critical);
            Log.LogInformation("RETAIL_STAT_PERIODIC_CRITICAL_INPUT {Inputs}", JsonSerializer.Serialize(new
            {
                npc = profile.NpcId, source = npc.GetObjectId(), target = target.GetObjectId(), skill = source.Id,
                position = part.Position, sourcePercent = part.CriticalPercent, rawCritical = raw,
                scaledCritical = scaled, resistance, pose, threshold, draw, critical, skillHash = source.Sha256
            }));
        }
        else effect.ReuseMagicalCritical(part.Position);
        int count = effect.GetSkill()?.GetEffectedList().Count ?? 1;
        var packed = RetailNpcPeriodicDamageStages.Pack(amount, critical, checked((ushort)Math.Min(count, ushort.MaxValue)));
        var states = States.GetValue(effect, _ => new());
        lock (states) states[part.Position] = new(effect, template, source, part, profile, targetProfile, sourceLevel, packed.BaseDamage, packed.Critical, application);
        return true;
    }

    private static bool Find(Effect effect, AbstractOverTimeEffect template, out State state)
    {
        state = null!;
        if (!States.TryGetValue(effect, out var states)) return false;
        lock (states) return states.TryGetValue(template.Position, out state!);
    }
    internal static bool MatchesPreparedNpcDrainSource(Effect effect,AbstractOverTimeEffect template,DrainOperands source)
        => Find(effect,template,out var state) && ReferenceEquals(state.Template,template)
            && state.Part.Family==Family.SpellDrain && ReferenceEquals(state.Part.Drain,source);
    internal static bool TryStart(Effect effect, AbstractOverTimeEffect template)
    {
        if (!Find(effect, template, out var state)) return false;
        lock (state.Gate)
        {
            if (!ReferenceEquals(template, state.Template) || !effect.IsInSuccessEffects(template.Position))
                throw new InvalidOperationException("Periodic prepared source/template differs");
            if (state.Started) throw new InvalidOperationException("Periodic numeric program started twice");
            state.Started = true;
            effect.SetReserveds(new EffectReserved(template.Position, state.Base, EffectReserved.ResourceType.HP, true, false), true);
            return true; // The caller still invokes AbstractOverTimeEffect.StartEffect once.
        }
    }
    private static bool Current(State s) => s.Started && !s.Effect.RetailHasEnded && s.Effect.GetEffected().IsSpawned()
        && s.Effect.GetEffected().GetController().RetailLifeEpoch == s.Life;

    internal static bool TryTick(Effect effect, AbstractOverTimeEffect template)
        => TryTick(effect,template,out _);

    // An HP receipt is captured under the life-stat lock, before callbacks can
    // change HP again. It is not the native mutable damage result in unreviewed
    // survival/protection contexts and is not yet used to enable drain skills.
    internal static bool TryTick(Effect effect, AbstractOverTimeEffect template, out int? committedHpLoss)
    {
        committedHpLoss=null;
        if (!Find(effect, template, out var state)) return false;
        lock (state.Gate)
        {
            if (!Current(state) || effect.GetEffected().IsDead()) return true;
            Creature target=effect.GetEffected();
            RetailNpcStatSpellDrain.NpcPeriodicProgram preparedProgram=null!;
            bool preparedDrain=target is Npc && state.TargetProfile is not null && state.Part.Family==Family.SpellDrain
                &&RetailNpcStatSpellDrain.TryGetPreparedNpcPeriodicProgram(effect,out preparedProgram);
            bool SkipDrainTick(string reason)
            {
                Log.LogWarning("RETAIL_NPC_DRAIN_TICK_SKIP npc={Npc} skill={Skill} reason={Reason}",
                    state.Profile.NpcId,state.Source.Id,reason);
                return true;
            }
            if(preparedDrain && (!ReferenceEquals(target.GetLifeStats(),preparedProgram.TargetLifeStats)
                ||target.GetController().RetailLifeEpoch!=preparedProgram.TargetLife))
                return SkipDrainTick("target-life-owner-retired");
            if(target is Npc targetNpc && (!RetailNpcStatCatalog.TryGet(targetNpc,out var currentTarget)
                || state.TargetProfile is null || currentTarget.Generation!=state.TargetProfile.Generation
                || currentTarget.SourceRecordSha256!=state.TargetProfile.SourceRecordSha256
                || targetNpc.GetGameStats().StatisticsProfile is not { } materialized
                || materialized.Generation!=currentTarget.Generation
                || materialized.SourceRecordSha256!=currentTarget.SourceRecordSha256))
            {
                if(preparedDrain)return SkipDrainTick("target-context-retired");
                throw new InvalidOperationException("Selected periodic NPC target context retired or changed");
            }
            if(target is not Player && target is not Npc)throw new InvalidOperationException("Selected periodic target kind changed");
            if(state.TargetProfile is not null && target.GetObserveController().HasDrainProtectionObserver)
            {
                if(preparedDrain)return SkipDrainTick("unreviewed-protection");
                throw new InvalidOperationException("Unreviewed NPC protection appeared after periodic preflight");
            }
            int amount = state.Base;int? mdef = null;short? elemental = null;
            if(state.Part.ApplyMagicalDefense)
            {
                // Interval reads actor1498 directly; unlike instant damage, it
                // does not add a separate movement-defense table adjustment.
                mdef = target.GetGameStats().GetMDef().GetCurrent();
                amount = RetailNpcPeriodicDamageStages.MagicalDefense(Family.SpellAttack, mdef.Value, amount);
            }
            int afterMdef = amount;
            Creature? caster = Aion.GameServer.World.World.GetInstance().FindVisibleObject(state.CasterId) as Creature;
            // Native Interval resolves the current global creature registry. The
            // numeric bridge must not introduce a new map/instance cancellation
            // rule; the existing Effect still owns transfer/end behavior.
            if (caster is not null && !caster.IsSpawned()) caster = null;
            if (caster is not null && (caster is not Npc current || !RetailNpcStatCatalog.TryGet(current, out _)))
            {
                if(preparedDrain)return SkipDrainTick("caster-context-retired");
                throw new InvalidOperationException("Current periodic caster changed to an unreviewed owner/scaling domain");
            }
            int sourceLevel = caster?.GetLevel() ?? target.GetLevel(), targetLevel = target.GetLevel();
            if(state.Part.Family is Family.SpellAttack or Family.SpellDrain)
            {
                var elementStat = state.Part.Element.GetStatForElement()
                    ?? throw new InvalidOperationException("Periodic spell has no elemental stat");
                // User slot518 -> 1400de340 selects the current signed word;
                // the periodic caller has no additional movement/floor term.
                elemental = SkyTempleCombatInputs.SignedWord(target.GetGameStats().GetResistance(elementStat).GetCurrent(), "periodic element");
                var scope=state.TargetProfile is null ? SkyTempleCombatInputs.Scope(state.Profile.NpcId)
                    : RetailNpcNormalScope.Create(RetailNpcNormalScope.MainBinarySha256,
                        RetailNpcNormalActorKind.OrdinaryOwnerlessNpc,RetailNpcNormalActorKind.OrdinaryOwnerlessNpc,
                        state.Profile.NpcId,state.TargetProfile.NpcId);
                amount = RetailNpcPeriodicDamageStages.ElementalDefense(Family.SpellAttack,
                    scope, elemental.Value, amount, sourceLevel, targetLevel).Damage;
            }
            int afterElement = amount;
            int fortitude = SkyTempleCombatInputs.CriticalReduction(target, false);
            ushort nativeFortitude = SkyTempleCombatInputs.Word(fortitude, "periodic fortitude");
            amount = state.Critical ? RetailNpcPeriodicDamageStages.NpcCritical(amount, (short)state.SourceLevel, 0, 0, nativeFortitude) : amount;
            int afterCritical = amount;
            var pve = caster is null || state.TargetProfile is not null ? (Attack: 0, Defense: 0) : SkyTempleCombatInputs.Pve(caster, target, false);
            if (caster is not null && state.TargetProfile is null) amount = F.Cvtt(F.Mul(F.FromInt(amount), RetailNpcNormalDamageStages.CalculatePveRatio(
                SkyTempleCombatInputs.Scope(((Npc)caster).GetNpcId()), pve.Attack, pve.Defense)));
            amount = Math.Max(1, amount);
            if(state.TargetProfile is { MagicalDamageTrim: > 0 } trimmed)amount=trimmed.MagicalDamageTrim;
            int requested = amount;
            var attack = new AttackResult(amount, state.Critical ? AttackStatus.CRITICAL : AttackStatus.NORMALHIT, HitType.MAHIT);
            Aion.GameServer.Controllers.ObserveController.NpcDrainTickProtectionReceipt? protection=null;
            if(preparedDrain)
            {
                if(!target.GetObserveController().TryCheckUnprotectedNpcDrainTickForOfflineFixture([attack],effect,caster,out protection))
                    return SkipDrainTick("protection-before-calculation");
            }
            else target.GetObserveController().CheckStatisticsPeriodicProtection([attack], effect, caster);
            amount = Math.Max(0, attack.GetDamage());
            var dispatch = TickDispatch.Produce(state, caster, amount,protection);
            int before = target.GetLifeStats().GetCurrentHp();
            try { target.GetController().OnStatisticsPeriodicAttack(dispatch); }
            catch(Exception ex) when(preparedDrain && dispatch.CommittedHpLoss is null
                && (ex is PreparedDrainTickRetiredException
                    or Aion.GameServer.Controllers.ObserveController.NpcDrainProtectionChangedException))
            { return SkipDrainTick(ex.Message); }
            committedHpLoss=dispatch.CommittedHpLoss;
            if(preparedDrain && committedHpLoss is null)return SkipDrainTick("hp-not-committed");
            Log.LogInformation("RETAIL_STAT_PERIODIC_INPUT {Inputs}", JsonSerializer.Serialize(new
            {
                npc = state.Profile.NpcId, source = caster?.GetObjectId() ?? 0, originalCaster = state.CasterId,
                target = target.GetObjectId(), world = target.GetWorldId(), sourceWorld = caster?.GetWorldId(), applicationWorld = state.Map,
                skill = state.Source.Id, skillLevel = effect.GetSkillLevel(), sourceSkillLevel = state.SourceLevel,
                position = state.Part.Position, family = state.Part.Family.ToString(), profileHash = state.Profile.SourceRecordSha256,
                skillHash = state.Source.Sha256, generation = state.Profile.Generation, tick = ++state.Tick,
                delta = state.Part.Delta, value = state.Part.Value, cachedBase = state.Base, critical = state.Critical,
                linearBase = state.Application.LinearBase, applyBoost = state.Part.ApplyBoost,
                applicationBoost = state.Application.Boost, applicationSuppression = state.Application.Suppression, boostCap = state.Application.Cap,
                applyMagicalDefense = state.Part.ApplyMagicalDefense, mdef, afterMdef, elemental, sourceLevel, targetLevel, afterElement,
                fortitude, afterCritical, pveAttack = pve.Attack, pveDefense = pve.Defense, requestedDamage = requested,
                protectedDamage = amount, before, after = target.GetLifeStats().GetCurrentHp(), hpEntered = dispatch.HpEntered,
                committedHpLoss
            }));
            target.GetObserveController().NotifyDotAttackedObservers(caster!, effect);
            return true;
        }
    }

    internal sealed class TickDispatch
    {
        private readonly State state;
        private readonly Aion.GameServer.Controllers.ObserveController.NpcDrainTickProtectionReceipt? protection;
        private int claimed, entered, committedHpLoss=-1;
        internal Creature? Caster { get; }
        internal int Damage { get; }
        internal Effect Effect => state.Effect;
        internal bool Critical => state.Critical;
        internal HopType? Hop => state.Template.Hoptype;
        internal LOG Log => state.Part.Family switch { Family.Poison => LOG.POISON, Family.Bleed => LOG.BLEED,
            Family.SpellDrain => LOG.SPELLATKDRAIN, _ => LOG.SPELLATK };
        internal bool HpEntered => Volatile.Read(ref entered) != 0;
        internal int? CommittedHpLoss { get { int loss=Volatile.Read(ref committedHpLoss);return loss<0?null:loss; } }
        private TickDispatch(State owner, Creature? caster, int damage,
            Aion.GameServer.Controllers.ObserveController.NpcDrainTickProtectionReceipt? receipt)
        { state = owner; Caster = caster; Damage = damage;protection=receipt; }
        internal static TickDispatch Produce(object producer,Creature? caster,int damage,
            Aion.GameServer.Controllers.ObserveController.NpcDrainTickProtectionReceipt? receipt=null)
        {
            if(producer is not State s||!Monitor.IsEntered(s.Gate)||!Current(s)||damage<0)
                throw new InvalidOperationException("Only the active periodic numeric owner can produce HP work");
            return new(s,caster,damage,receipt);
        }
        private void Validate(Creature target)
        {
            if (!ReferenceEquals(target, Effect.GetEffected())) throw new InvalidOperationException("Retired/mismatched periodic work");
            if (!Current(state))
            {
                if(protection is not null)throw new PreparedDrainTickRetiredException();
                throw new InvalidOperationException("Retired/mismatched periodic work");
            }
            if(protection is not null && (!RetailNpcStatSpellDrain.TryGetPreparedNpcPeriodicProgram(Effect,out var program)
                ||!ReferenceEquals(target.GetLifeStats(),program.TargetLifeStats)
                ||target.GetController().RetailLifeEpoch!=program.TargetLife))
                throw new PreparedDrainTickRetiredException();
        }
        internal void ClaimFor(Creature target)
        { Validate(target); if (Interlocked.Exchange(ref claimed, 1) != 0) throw new InvalidOperationException("Periodic dispatch replay"); }
        internal void EnterHp(Creature target)
        { Validate(target); if (Volatile.Read(ref claimed) != 1 || Interlocked.Exchange(ref entered, 1) != 0) throw new InvalidOperationException("Periodic HP replay"); }
        internal IDisposable? EnterNpcDrainProtection(Creature target)
        {
            if(protection is null)return null;
            if(target is not Npc npc ||!ReferenceEquals(target,Effect.GetEffected())||Volatile.Read(ref entered)!=1)
                throw new InvalidOperationException("Invalid periodic drain protection owner");
            return target.GetObserveController().EnterNpcDrainTickHpForOfflineFixture(protection,npc);
        }
        internal void RecordHpLoss(Creature target,int loss)
        {
            if(!ReferenceEquals(target,Effect.GetEffected()) || !Monitor.IsEntered(target.GetLifeStats())
                || Volatile.Read(ref entered)!=1 || loss<0 || loss>Damage
                || Interlocked.CompareExchange(ref committedHpLoss,loss,-1)!=-1)
                throw new InvalidOperationException("Invalid periodic HP receipt");
        }
    }
}
