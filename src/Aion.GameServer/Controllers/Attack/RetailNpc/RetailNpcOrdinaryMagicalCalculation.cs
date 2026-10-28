using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Numerics;
using System.Text.Json;
using static Aion.GameServer.Controllers.Attack.RetailNpc.RetailNpcMagicalCalculationInputs;
using Parameters = Aion.GameServer.Controllers.Attack.RetailNpc.RetailNpcSpellInstantParameters;

namespace Aion.GameServer.Controllers.Attack.RetailNpc;

/// <summary>
/// Inactive complete finite NPC→NPC numeric plan. Native Main CalcMagicalSkillAttackDamage
/// 0x14036f850, CheckMagicalAttackResist 0x14036f220 and selected SpellATK_Instant wrapper.
/// Source/defaults, exact widths, complete body bytes and independent full-stage fixtures:
/// phase3/pilot-magical-instant/independent-calculator-review/finite-domain-closure.
/// Original calls are receipts, not an RNG. Begin/application/HP and counter ownership do not run.
/// </summary>
internal static class RetailNpcOrdinaryMagicalCalculation
{
    internal static Outcome PlanOrdinaryCalculation(Parameters.SelectedSpell parameters,
        Invocation current, ImmutableArray<OriginalCall> original, RawWindow storage)
    {
        try
        {
            Validate(parameters, current, original, storage);
            return new(Calculate(parameters, current, original, storage), []);
        }
        catch (ContractError e) { return new(null, [new(e.Code, e.Path, e.Message)]); }
        catch (Exception e) when (e is ArgumentException or InvalidOperationException or OverflowException)
        { return new(null, [new("magical_contract", "/inputs", e.Message)]); }
    }

    private static Result Calculate(Parameters.SelectedSpell p, Invocation x,
        ImmutableArray<OriginalCall> originals, RawWindow storage)
    {
        ActorRaw a = x.Attacker.Raw, d = x.Defender.Raw;
        var stages = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        var floats = new Dictionary<string, uint>(StringComparer.Ordinal);
        var writes = new List<Write>(); var consumed = new List<OriginalCall>();
        byte[] before = storage.Bytes.ToArray(), window = (byte[])before.Clone(); int next = 0;
        void Stage(string name, object data) => stages.Add(name, JsonSerializer.SerializeToElement(data));
        uint F(string name, uint bits) { floats.Add(name, bits); return bits; }
        void Store(string stage, int offset, ReadOnlySpan<byte> bytes)
        {
            string old = Convert.ToHexStringLower(window.AsSpan(offset, bytes.Length));
            bytes.CopyTo(window.AsSpan(offset));
            writes.Add(new(writes.Count, stage, offset, old, Convert.ToHexStringLower(bytes)));
        }
        void StoreInt(string stage, int offset, int value)
        { byte[] bytes = new byte[4]; BinaryPrimitives.WriteInt32LittleEndian(bytes, value); Store(stage, offset, bytes); }
        int ReadInt(int offset) => BinaryPrimitives.ReadInt32LittleEndian(window.AsSpan(offset, 4));
        int Draw(CallStage stage, int first, int second)
        {
            Need(next < originals.Length, "magical_missing_call", "/original", "A reached original invocation is missing");
            OriginalCall call = originals[next++];
            Need(call.Stage == stage && call.FirstEndpoint == first && call.SecondEndpoint == second
                && call.InclusiveAdapterInvoked == (first != second)
                && call.Returned >= Math.Min(first, second) && call.Returned <= Math.Max(first, second),
                "magical_original_call", "/original/" + (next - 1), "Original stage/order/endpoints/adapter/result differ");
            consumed.Add(call); return call.Returned;
        }
        void End() => Need(next == originals.Length, "magical_extra_call", "/original", "Skipped calls cannot be supplied as consumed invocations");
        int A(int? field) => field!.Value;
        uint ratioA = F("attacker_ratio_div1000", B.Div(B.Int(a.StatRatio1514!.Value), B.Int(1000)));
        uint ratioD = F("defender_ratio_div1000", B.Div(B.Int(d.StatRatio1514!.Value), B.Int(1000)));

        int difference = unchecked(A(d.MagicalResistance149c) - A(a.SelectedSubtypePenetration) - A(a.MagicalAccuracy14c4));
        uint poseProduct = F("resist_pose_product", B.Mul(B.Int(difference), d.RawPoseLowNibble == 5 ? 0x3f000000u : B.One));
        int poseInteger = B.Cvtt(poseProduct);
        int normalized = B.Cvtt(F("resist_normalized", B.Div(B.Int(poseInteger), ratioA)));
        int capped = Math.Clamp(normalized, 0, 500);
        int levelDiff = A(d.EffectiveLevel1492) - A(a.EffectiveLevel1492);
        int levelResist = levelDiff > 4 ? (levelDiff - 4) * 100 : levelDiff < -4 ? (levelDiff + 4) * 100 : 0;
        int threshold = Math.Clamp(unchecked(capped + levelResist), 0, 900);
        int resistDraw = Draw(CallStage.OrdinaryResist, 1, 1000);
        bool success = threshold < resistDraw;
        Stage("resist", new { difference, pose_truncated = poseInteger, normalized, clamped500 = capped,
            level_delta = levelResist, threshold900 = threshold, strict_threshold_less_draw = success });
        // Ordinary36f615 RNG/CMP/SETG returns directly. The earlier immunity/forced
        // branch alone calls0b4640; ordinary resist failure creates no counter intent.
        if (!success)
        {
            End();
            return new(x.Context, 0, ReadInt(4), false, before, window, writes, consumed,
                [], stages, floats);
        }
        Store("resist.success.base-status", 0x34, [10]);
        int draw = Draw(CallStage.NpcBase, A(a.MinimumDamage14dc), A(a.MaximumDamage14e0));
        int cap = x.LoadedCapSelector145a43940 == 0 ? 3400 : 2900;
        int boostDelta = A(a.MagicalBoost1502) - A(d.MagicalSuppression1504);
        int firstClamp = Math.Clamp(boostDelta, 0, cap);
        uint boost = F("normalized_boost", B.Div(B.Int(firstClamp), ratioD));
        int normalizedBoost = Math.Clamp(B.Cvtt(boost), 0, cap), quotient = normalizedBoost / 1000;
        uint coefficient = F("R2_coefficient", B.Add(B.Div(B.Int(p.R2), B.Int(100)), B.One));
        uint scaled = F("sampled_scaled", B.Mul(B.Int(draw), coefficient));
        uint boosted = F("sampled_boosted", B.Mul(scaled, B.Add(B.Int(quotient), B.One)));
        int basis = Math.Max(0, B.Cvtt(F("sampled_with_zero", B.Add(boosted, 0))));
        // The native NPC branch divides the normalized boost as INTEGER before converting.
        // Stage output uses native-independent field spelling "integer" (a C# keyword).
        Stage("base", new Dictionary<string, object>
        { ["cap"] = cap, ["boost_delta"] = boostDelta, ["first_clamp"] = firstClamp,
          ["normalized_boost"] = normalizedBoost, ["integer_quotient"] = quotient, ["integer"] = basis });
        int percent100 = unchecked(A(a.OrdinaryMagicalPercent11c4) + 100);
        uint pct = F("ordinary_percent", B.Div(B.Int(percent100), B.Int(100)));
        uint product = F("ordinary_product", B.Mul(pct, B.Int(basis)));
        int ordinary = B.Cvtt(F("ordinary_sum", B.Add(product, B.Int(A(a.OrdinaryMagicalFlat11c8)))));
        Stage("ordinary_percent_flat", new { percent_plus100_wrapped = percent100, @integer = ordinary });
        Stage("attenuation", new { function = "0x140b60ec0", @integer = ordinary });
        uint movement = F("after_movement", B.Add(B.Mul(B.Div(0, B.Int(100)), B.Int(ordinary)), B.Int(ordinary)));
        int movementInteger = B.Cvtt(movement);
        Stage("movement", new { actual_raw_getter = 0, row_percent = 0, @integer = movementInteger });
        int excess = A(d.EffectiveLevel1492) - A(a.EffectiveLevel1492) - 2;
        uint factor = excess > 0 ? B.Sub(B.One, B.Mul(B.Int(excess), 0x3dcccccdu)) : B.One;
        bool negativeFactorBranch = B.Negative(factor);
        if (negativeFactorBranch) factor = 0;
        F("level_factor", factor);
        uint levelValue = F("after_level", negativeFactorBranch ? 0u : excess > 0 ? B.Mul(B.Int(movementInteger), factor) : B.Int(movementInteger));
        int preliminary = Math.Max(1, B.Cvtt(levelValue));
        Stage("level", new { excess_over_grace2 = excess, preliminary_return = preliminary });
        int mdef = A(d.MagicalDefense1498);
        int mdefFirst = B.Cvtt(F("mdef_movement", B.Add(B.Mul(B.Div(0, B.Int(100)), B.Int(mdef)), B.Int(mdef))));
        int armor = B.Cvtt(F("mdef_tenths", B.Div(B.Int(mdefFirst), B.Int(10))));
        int afterMdef = Math.Max(0, unchecked(preliminary - armor));
        Stage("mdef", new { first_truncated = mdefFirst, armor_truncated = armor, integer_after_subtract_floor0 = afterMdef });
        int erNormalized = B.Cvtt(F("element_normalized", B.Div(B.Int(A(d.SelectedElementalWordSigned)), ratioA)));
        int erClamped = Math.Max(-1300, erNormalized);
        uint erFactor = F("elemental_factor", B.Sub(B.One, B.Div(B.Int(erClamped), B.Int(1300))));
        int damage = B.Cvtt(F("after_element", B.Mul(erFactor, B.Int(afterMdef))));
        Stage("element", new { raw_inline_word_offset = "0x" + (0x764 + 2 * p.Element).ToString("x"), normalized = erNormalized,
            floor = -1300, clamped = erClamped, denominator = 1300, @integer = damage });
        int? criticalThreshold = null;
        bool critical;
        if (ReadInt(0x54) == 0)
        {
            int criticalDraw = Draw(CallStage.OrdinaryCritical, 1, 1000);
            int withPose = unchecked(B.Cvtt(B.Mul(B.Int(A(a.MagicalCritical14c0)), B.One)) + (d.RawPoseLowNibble == 5 ? 500 : 0));
            int criticalDifference = unchecked(withPose - A(d.MagicalCriticalReduce14c8));
            criticalThreshold = Math.Min(500, B.Cvtt(F("critical_normalized", B.Div(B.Int(criticalDifference), ratioD))));
            critical = criticalDraw < criticalThreshold.Value;
            StoreInt("critical.cache-present", 0x54, 1); // 0x140370493 precedes decision store.
            StoreInt("critical.cache-decision", 0x50, critical ? 1 : 0);
        }
        else critical = ReadInt(0x50) != 0;
        uint multiplier = B.One;
        int? reductionInteger = null;
        if (critical)
        {
            reductionInteger = B.Cvtt(B.Div(B.Int(A(d.MagicalCriticalDamageReduce14cc)), ratioA));
            uint reduction = B.Div(B.Int(reductionInteger.Value), B.Int(1000));
            uint candidate = B.Sub(B.Add(0, B.One), reduction);
            multiplier = B.Greater(candidate, B.One) ? candidate : B.One;
            Store("critical.status", 0x35, [12]);
        }
        F("critical_multiplier", multiplier);
        damage = B.Cvtt(F("after_critical", B.Mul(B.Int(damage), multiplier)));
        Stage("critical", new { threshold = criticalThreshold, critical, normalized_damage_reduction = reductionInteger,
            @integer = damage, status_write = critical ? (int?)12 : null,
            cache_after = new { additional = ReadInt(0x4c), decision = ReadInt(0x50), decision_present = ReadInt(0x54) } });
        Stage("sharing", new { source_flag4cc = 0, forced_wrapper_word = 1, division_reached = false, @integer = damage });
        Stage("classification", new { attacker = 0, defender = 0, ratio_branch_reached = false,
            derived_from = "exact NPC vtable getter with each current master188c=0", @integer = damage });
        damage = Math.Max(1, damage);
        Stage("minimum", new { @integer = damage });
        Stage("fixed", new { template_word3f8 = 0, getter_reads = 1, second_read_reached = false });
        Stage("exclusive", new { skill_ec4 = 0, source_bound_attacker_template450 = 0, defender_lookup_reached = false });
        StoreInt("calculator.final-record", 4, damage);
        StoreInt("wrapper.positive-preliminary-header", 0, preliminary);
        End();
        return new(x.Context, preliminary, damage, true, before, window, writes, consumed, [], stages, floats);
    }

    private static void Validate(Parameters.SelectedSpell p, Invocation x, ImmutableArray<OriginalCall> calls, RawWindow storage)
    {
        Need(p is not null && x is not null && storage is not null, "magical_required", "/inputs", "All complete typed inputs required");
        // Re-admit even internally assembled objects; never trust a caller's claimed constructed map.
        var admitted = Parameters.ProjectSelectedSpell(p.RawBytes.AsSpan(), p.Binding, Parameters.RequiredPins);
        Need(p.Id == admitted.Id && p.Name == admitted.Name && p.RawSha256 == admitted.RawSha256
            && p.R2 == admitted.R2 && p.Subtype == admitted.Subtype && p.Element == admitted.Element
            && p.Constructed.Count == admitted.Constructed.Count && p.Constructed.All(t => admitted.Constructed.TryGetValue(t.Key, out long v) && v == t.Value),
            "magical_recipe", "/skill/source", "Exact admitted source construction required");
        Need(x.Context is not null && Text(x.Context.Operation, 256) && Text(x.Context.Epoch, 256),
            "magical_context", "/context", "Explicit operation and epoch required");
        EvidenceCheck(x.Context, x.Context);
        Need(x.FloatingPoint is not null && x.FloatingPoint.Rounding == Rounding.NearestEven
            && x.FloatingPoint.MaskedExceptions == true && x.FloatingPoint.NormalReturn == true,
            "magical_fp", "/float", "Explicit RNE/masked exceptions/normal return required");
        Need(x.Attacker is not null && x.Defender is not null && x.Skill is not null && x.AttackerOptions is not null,
            "magical_actor", "/context", "Actual actor/Skill/options bindings required");
        ActorCheck(x.Attacker, x.Context, true); ActorCheck(x.Defender, x.Context, false);
        Need(x.Attacker.Current.Address != x.Defender.Current.Address && x.Attacker.ObjectId != x.Defender.ObjectId
            && x.Attacker.LifeIdentity != x.Defender.LifeIdentity,
            "magical_distinct_actor", "/actors", "Distinct ordinary actors and lives required");
        Need(x.Attacker.TemplateRecipe.Id == p.Binding.NpcId && x.Attacker.TemplateRecipe.Devname == p.Binding.NpcDevname,
            "magical_attacker_source", "/attacker", "Actual attacker template must bind the selected source NPC list");
        BindingCheck(x.Skill.Current, x.Context);
        Need(x.Skill.RecipeRawSha256 == p.RawSha256 && x.Skill.SkillLevel == p.Binding.SkillLevel
            && x.Skill.SelectedFields is not null && x.Skill.SelectedFields.Count == p.Constructed.Count,
            "magical_current_skill", "/skill", "Actual loaded identity, source recipe, level and complete selected fields required");
        foreach (var t in p.Constructed)
            Need(x.Skill.SelectedFields.TryGetValue(t.Key, out long? value) && value.HasValue && value.Value == t.Value,
                "magical_current_skill_field", "/skill/" + t.Key, "Unknown or changed current selected field is unsupported");
        Need(x.LoadedCapSelector145a43940.HasValue && x.ForceCompareInput == 0,
            "magical_optional_context", "/context", "Known loaded cap selector and exact zero forced input required");
        var a = x.Attacker.Raw; var d = x.Defender.Raw;
        Need(a.SelectedSubtypePenetrationOffset == 0xfc4 + 4 * p.Subtype && d.SelectedElementalWordOffset == 0x764 + 2 * p.Element,
            "magical_selected_read_offset", "/actor/raw", "Exact current selected subtype/element read offsets required");
        Need(x.AttackerOptions.OneTimeBoostPointer200.HasValue && x.AttackerOptions.OneTimeCriticalPointer208.HasValue,
            "magical_one_time_pointer", "/attacker", "Known raw one-time pointers required; unknown is not null");
        if (x.AttackerOptions.OneTimeBoostPointer200 != 0)
            Need((Flags(x.Attacker.Manager, true) & 0x00000002u) == 0, "magical_one_time_boost", "/manager/4444", "Mask2 selects an unsupported owner");
        if (x.AttackerOptions.OneTimeCriticalPointer208 != 0)
            Need((Flags(x.Attacker.Manager, false) & 0x00000002u) == 0, "magical_one_time_critical", "/manager/4454", "Mask2 selects an unsupported owner");
        int count = x.Defender.Manager.Present == false ? 0 : Known(x.Defender.Manager.Counter4400, "/defender/manager/4400");
        Need(count == 0 && d.ImmunityMask27c.HasValue && (d.ImmunityMask27c.Value & (1 << p.Element)) == 0
            && d.TemplateFixedMagical3f8 == 0 && a.TemplateExclusive450 == 0,
            "magical_optional_branch", "/context", "Forced/immunity/fixed/exclusive branches are outside the finite domain");
        Need(!calls.IsDefault && calls.Length <= 3, "magical_original_count", "/original", "Exact finite invocation sequence required");
        foreach (var call in calls)
        { Need(call is not null, "magical_original", "/original", "Non-null original receipt required"); EvidenceCheck(call.Evidence, x.Context); }
        StorageCheck(storage, x);
    }

    private static void ActorCheck(Actor actor, Evidence context, bool attacker)
    {
        Need(actor.Current is not null && actor.LoadedTemplate is not null && actor.TemplateRecipe is not null
            && actor.Raw is not null && actor.Manager is not null, "magical_actor_binding", "/actor", "Complete actual actor/template/raw/manager required");
        BindingCheck(actor.Current, context); BindingCheck(actor.LoadedTemplate, context);
        Need(actor.ObjectId > 0 && Text(actor.LifeIdentity, 256), "magical_actor_life", "/actor", "Explicit object/life identity required");
        var recipe = Parameters.ProjectNpcRecipe(actor.TemplateRecipe.RawBytes.AsSpan(), Parameters.RequiredPins);
        Need(actor.TemplateRecipe.Id == recipe.Id && actor.TemplateRecipe.Devname == recipe.Devname
            && actor.LoadedTemplateRecipeRawSha256 == recipe.RawSha256,
            "magical_loaded_template", "/actor/template", "Actual loaded template must independently bind admitted source identity");
        ActorRaw r = actor.Raw;
        Need(r.VtableAddress == 0x1412b8470ul && r.Type144 == 2 && r.Kind1940 == 1 && r.Master188c == 0u && r.Related1894 == 0u
            && r.VirtualProfile1850 == 0ul && r.Difficulty1bf8 == 0ul && r.TemplateFlags434.HasValue
            && (r.TemplateFlags434.Value & 0x00200000u) == 0 && r.CurrentDual914 == 0,
            "magical_ordinary_domain", "/actor/raw", "Independent current type/vtable/kind/owner/profile/difficulty/dual facts required");
        Need(r.EffectiveLevel1492 is >= 1 and <= 100 && r.StatRatio1514 is >= 1000 and <= 65000 && r.MovementGetterByte == 0,
            "magical_level_ratio_movement", "/actor/raw", "Actual ordinary getters and positive construction ratio range required");
        Need(actor.Manager.Present.HasValue && (actor.Manager.Present.Value ? actor.Manager.LoadedManager is not null : actor.Manager.LoadedManager is null),
            "magical_manager_presence", "/actor/manager", "Known presence and matching actual pointer required");
        if (actor.Manager.LoadedManager is not null) BindingCheck(actor.Manager.LoadedManager, context);
        if (attacker)
        {
            foreach (int? value in new[] { r.MinimumDamage14dc, r.MaximumDamage14e0, r.MagicalAccuracy14c4, r.MagicalCritical14c0 }) Word(value);
            SignedWord(r.MagicalBoost1502); Known(r.OrdinaryMagicalPercent11c4, "/attacker/11c4");
            Known(r.OrdinaryMagicalFlat11c8, "/attacker/11c8"); Known(r.SelectedSubtypePenetration, "/attacker/subtype-penetration");
            Need(r.TemplateExclusive450 == 0, "magical_exclusive", "/attacker/template450", "Exact knownzero template fallback required");
        }
        else
        {
            foreach (int? value in new[] { r.MagicalResistance149c, r.MagicalCriticalReduce14c8, r.MagicalCriticalDamageReduce14cc, r.ImmunityMask27c }) Word(value);
            SignedWord(r.MagicalSuppression1504); SignedWord(r.SelectedElementalWordSigned);
            Need(r.MagicalDefense1498 is >= 0 and <= 500000 && r.RawPoseLowNibble is >= 0 and <= 15,
                "magical_defender_scalar", "/defender", "Known ordinary construction MDef and raw pose range required");
            Need(r.TemplateFixedMagical3f8 == 0, "magical_fixed", "/defender/template3f8", "Actual zero fixed getter required");
        }
    }
    private static int Known(int? value, string path)
    { Need(value.HasValue, "magical_unknown_scalar", path, "Explicit current scalar required"); return value.Value; }
    private static void Word(int? value) => Need(value is >= 0 and <= 65535, "magical_word", "/actor/raw", "Native uint16 carrier must fit before narrowing");
    private static void SignedWord(int? value) => Need(value is >= -32768 and <= 32767, "magical_signed_word", "/actor/raw", "Native signed16 carrier must fit before narrowing");
    private static uint Flags(Manager manager, bool boost)
    {
        if (manager.Present == false) return 0;
        uint? flags = boost ? manager.BoostFlags4444 : manager.CriticalFlags4454;
        Need(flags.HasValue, "magical_manager_flags", "/manager", "Reached current flags must be known"); return flags.Value;
    }
    private static void EvidenceCheck(Evidence evidence, Evidence context)
    {
        Need(evidence is not null && evidence.MainSha256 == Parameters.MainSha256 && evidence.PackageContext == Parameters.PackageContext
            && evidence.Operation == context.Operation && evidence.Epoch == context.Epoch
            && evidence.Origin is Origin.MathematicalFixture or Origin.ExternalSourceAssertion or Origin.ObservedOriginal
            && evidence.Origin == context.Origin && Text(evidence.Reference),
            "magical_evidence", "/evidence", "Pinned context, coherent operation/epoch and explicit provenance required");
    }
    private static void BindingCheck(Binding binding, Evidence context)
    {
        Need(binding is not null && binding.Address != 0 && binding.Address % 8 == 0 && Text(binding.Identity, 256),
            "magical_binding", "/binding", "Actual aligned identity binding required");
        EvidenceCheck(binding.Evidence, context);
    }
    private static void StorageCheck(RawWindow w, Invocation x)
    {
        Need(w.LayoutIdentity == RawWindow.Layout && w.Bytes.Length == 0x58 && w.SerializedLifetimeAndCoverageKnown == true
            && Text(w.AllocationIdentity, 256) && w.OriginalHeader is not null
            && w.OriginalHeader.Address % 4 == 0 && w.OriginalHeader.Address != 0,
            "magical_window", "/storage", "Exact original layout/88 bytes/alignment/serialized lifetime coverage required");
        EvidenceCheck(w.OriginalHeader.Evidence, x.Context);
        Need(Text(w.OriginalHeader.Identity, 256), "magical_window_identity", "/storage", "Original invocation window identity required");
        ulong allocationEnd = End(w.AllocationStart, w.AllocationBytes), windowEnd = End(w.OriginalHeader.Address, 0x58);
        Need(w.AllocationStart <= w.OriginalHeader.Address && windowEnd <= allocationEnd,
            "magical_window_bounds", "/storage", "Full consumed interval must lie in the original allocation");
        Need(!w.DisjointStorage.IsDefault && w.DisjointStorage.Length is >= 9 and <= 64,
            "magical_storage_coverage", "/storage", "Complete bounded typed other-storage coverage required");
        foreach (var span in w.DisjointStorage)
        {
            Need(span is not null && span.Role != SpanRole.Unknown && Enum.IsDefined(span.Role) && Text(span.AllocationIdentity, 256),
                "magical_storage_role", "/storage", "Known typed storage required");
            EvidenceCheck(span.Evidence, x.Context); ulong end = End(span.Address, span.Bytes);
            Need(end <= w.OriginalHeader.Address || windowEnd <= span.Address,
                "magical_storage_alias", "/storage", "Intentional header/record/cache alias cannot overlap actor/effect/ref/timer/table storage");
            Need(span.AllocationIdentity != w.AllocationIdentity, "magical_storage_allocation", "/storage", "Other storage cannot share the original window allocation");
        }
        void Covers(SpanRole role, ulong address, int size)
        {
            Need(w.DisjointStorage.Any(s => s.Role == role && s.Address <= address && End(address, size) <= End(s.Address, s.Bytes)),
                "magical_storage_binding", "/storage/" + role, "Actual consumed identity must lie in its declared typed storage");
        }
        Covers(SpanRole.Attacker, x.Attacker.Current.Address, 0x1944);
        Covers(SpanRole.Defender, x.Defender.Current.Address, 0x1944);
        Covers(SpanRole.AttackerTemplate, x.Attacker.LoadedTemplate.Address, 0x454);
        Covers(SpanRole.DefenderTemplate, x.Defender.LoadedTemplate.Address, 0x454);
        Covers(SpanRole.Skill, x.Skill.Current.Address, 0xec8);
        Covers(SpanRole.LoadedTables, 0x141069ed0ul, 28);
        Covers(SpanRole.LoadedTables, 0x145a43940ul, 4);
        if (x.Attacker.Manager.LoadedManager is { } am) Covers(SpanRole.AttackerManager, am.Address, 0x4458);
        if (x.Defender.Manager.LoadedManager is { } dm) Covers(SpanRole.DefenderManager, dm.Address, 0x4458);
        foreach (SpanRole role in new[] { SpanRole.EffectStorage, SpanRole.ReferenceStorage, SpanRole.TimerStorage, SpanRole.LoadedTables })
            Need(w.DisjointStorage.Any(s => s.Role == role), "magical_storage_coverage", "/storage/" + role, "Explicit complete role coverage required");
        foreach (ulong pointer in new[] { x.AttackerOptions.OneTimeBoostPointer200!.Value, x.AttackerOptions.OneTimeCriticalPointer208!.Value })
            if (pointer != 0) Covers(SpanRole.EffectStorage, pointer, 1);
        for (int i = 0; i < w.DisjointStorage.Length; i++)
            for (int j = i + 1; j < w.DisjointStorage.Length; j++)
            {
                Span a = w.DisjointStorage[i], b = w.DisjointStorage[j];
                bool templateAlias = a.Role is SpanRole.AttackerTemplate or SpanRole.DefenderTemplate
                    && b.Role is SpanRole.AttackerTemplate or SpanRole.DefenderTemplate
                    && a.Address == b.Address && a.Bytes == b.Bytes && a.AllocationIdentity == b.AllocationIdentity
                    && x.Attacker.LoadedTemplateRecipeRawSha256 == x.Defender.LoadedTemplateRecipeRawSha256;
                Need(templateAlias || End(a.Address, a.Bytes) <= b.Address || End(b.Address, b.Bytes) <= a.Address,
                    "magical_storage_role_alias", "/storage", "Other role overlaps require a separately proved adapter domain");
            }
    }
    private static ulong End(ulong address, int length)
    {
        Need(address != 0 && length is > 0 and <= 16 * 1024 * 1024 && address <= ulong.MaxValue - (ulong)length,
            "magical_address_bound", "/storage", "Known finite interval without address overflow required");
        return address + (ulong)length;
    }

    // Private bounded instruction arithmetic only. Exact rational operations reproduce binary32
    // RNE after each selected instruction, preserve signed zero, reject subnormal/nonfinite/CVTT
    // indefinites. This never changes MXCSR or calls a target/general floating-point service.
    private static class B
    {
        internal const uint One = 0x3f800000u;
        internal static uint Int(long value) => Round(value, BigInteger.One, false);
        internal static bool Negative(uint value) { var (n, _) = Decode(value); return n.Sign < 0; }
        internal static bool Greater(uint a, uint b) { var (an, ad) = Decode(a); var (bn, bd) = Decode(b); return an * bd > bn * ad; }
        internal static uint Add(uint a, uint b)
        { var (an, ad) = Decode(a); var (bn, bd) = Decode(b); return Round(an * bd + bn * ad, ad * bd, (a & b & 0x80000000u) != 0); }
        internal static uint Sub(uint a, uint b) => Add(a, b ^ 0x80000000u);
        internal static uint Mul(uint a, uint b)
        { var (an, ad) = Decode(a); var (bn, bd) = Decode(b); return Round(an * bn, ad * bd, ((a ^ b) & 0x80000000u) != 0); }
        internal static uint Div(uint a, uint b)
        {
            var (an, ad) = Decode(a); var (bn, bd) = Decode(b);
            Need(!bn.IsZero, "magical_float_division", "/float", "Zero denominator is outside the finite program");
            BigInteger n = an * bd, d = ad * bn;
            if (d.Sign < 0) { n = -n; d = -d; }
            return Round(n, d, ((a ^ b) & 0x80000000u) != 0);
        }
        internal static int Cvtt(uint bits)
        {
            var (n, d) = Decode(bits);
            Need(n >= (BigInteger)int.MinValue * d && n < ((BigInteger)int.MaxValue + 1) * d,
                "magical_cvtt_range", "/float", "Representable signed32 conversion required before truncation");
            return (int)(n / d);
        }
        private static (BigInteger N, BigInteger D) Decode(uint bits)
        {
            int exponent = (int)((bits >> 23) & 255); uint fraction = bits & 0x7fffffu;
            Need(exponent < 255 && (exponent != 0 || fraction == 0), "magical_float_domain", "/float", "Only finite normal or signed zero admitted");
            BigInteger n = exponent == 0 ? 0 : fraction | 0x800000u;
            if ((bits & 0x80000000u) != 0) n = -n;
            int power = exponent == 0 ? -149 : exponent - 150;
            return power >= 0 ? (n << power, BigInteger.One) : (n, BigInteger.One << -power);
        }
        private static uint Round(BigInteger n, BigInteger d, bool negativeZero)
        {
            if (n.IsZero) return negativeZero ? 0x80000000u : 0u;
            uint sign = n.Sign < 0 ? 0x80000000u : 0;
            n = BigInteger.Abs(n);
            int e = checked((int)(n.GetBitLength() - d.GetBitLength()));
            if (e >= 0 ? n < (d << e) : (n << -e) < d) e--;
            Need(e is >= -126 and <= 127, "magical_float_domain", "/float", "Outside admitted normal-or-zero result");
            int shift = 23 - e;
            BigInteger scaledN = shift >= 0 ? n << shift : n, scaledD = shift >= 0 ? d : d << -shift;
            BigInteger q = BigInteger.DivRem(scaledN, scaledD, out BigInteger remainder);
            int tie = (remainder * 2).CompareTo(scaledD);
            if (tie > 0 || tie == 0 && !q.IsEven) q++;
            if (q == BigInteger.One << 24) { q >>= 1; e++; }
            Need(e <= 127, "magical_float_domain", "/float", "Overflow is outside admitted finite result");
            return sign | ((uint)(e + 127) << 23) | ((uint)q & 0x7fffffu);
        }
    }
}
