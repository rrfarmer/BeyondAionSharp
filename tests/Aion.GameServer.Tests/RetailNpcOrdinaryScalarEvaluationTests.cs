using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Aion.GameServer.Dataholders.RetailNpc;

namespace Aion.GameServer.Tests;

public class RetailNpcOrdinaryScalarEvaluationTests
{
    [SkippableFact]
    public void All25CompleteSourceFixturesProduceEveryNativeScalarWithVaryingExplicitChannels()
    {
        using var profiles = JsonDocument.Parse(PilotProfilesJson);
        using var provenance = JsonDocument.Parse(PilotProvenanceJson);
        using var manifest = JsonDocument.Parse(PilotSourceManifestJson);
        using var seeds = JsonDocument.Parse(PilotSeedVectorsJson);
        using var vectors = JsonDocument.Parse(PilotScalarVectorsJson);
        using var contracts = JsonDocument.Parse(ScalarContractsJson);
        var cohort = profiles.RootElement.EnumerateArray().Select(x => new RetailNpcSourceProfile(x)).ToDictionary(x => x.NpcId);
        var dataset = new RetailNpcStaticDataset(PilotDatasetId, PilotManifestHash, cohort, 63287, 117, 0,
            ImmutableArray<RetailNpcArtifactReceipt>.Empty, provenance.RootElement, manifest.RootElement);
        Assert.Equal(25, cohort.Count); Assert.Equal(25, vectors.RootElement.GetArrayLength());
        Assert.Equal(85, RetailNpcOrdinaryScalarProjection.Specifications.Length);
        Assert.Equal(161, RetailNpcOrdinaryScalarProjection.ModifierNames.Length);
        foreach (var contract in contracts.RootElement.EnumerateArray())
        {
            var actual = RetailNpcOrdinaryScalarProjection.Specifications.Single(x => x.Field == contract.GetProperty("field").GetString());
            Assert.Equal(contract.GetProperty("actor_offset").GetString(), actual.ActorOffset);
            Assert.Equal(contract.GetProperty("template_offset").GetString(), actual.TemplateOffset);
            Assert.Equal(contract.GetProperty("index").ValueKind == JsonValueKind.Null ? (int?)null : contract.GetProperty("index").GetInt32(), actual.ModifierIndex);
            Assert.Equal(contract.GetProperty("native_name").GetString(), actual.ModifierAuditName);
        }
        foreach (var vector in vectors.RootElement.EnumerateArray())
        {
            int id = vector.GetProperty("id").GetInt32();
            var projection = RetailNpcTemplateProjection.Project(dataset, id, new(PilotDatasetId, PilotManifestHash, "USA"));
            Assert.True(projection.Success, string.Join(";", projection.Issues)); var source = projection.Inputs!;
            Assert.Equal(vector.GetProperty("name").GetString(), source.Devname);
            var sourcePin = seeds.RootElement.EnumerateArray().Single(x => x.GetProperty("id").GetInt32() == id);
            Assert.Equal(sourcePin.GetProperty("raw_sha256").GetString(), source.Receipt.RawRecordSha256);
            Assert.Equal(sourcePin.GetProperty("file_sha256").GetString(), source.Receipt.SourceFileSha256);
            Assert.Equal(sourcePin.GetProperty("raw_tree_sha256").GetString(), Hash(Encoding.UTF8.GetBytes(source.RawRecord.GetRawText())));
            Assert.Equal(sourcePin.GetProperty("byte_start").GetInt64(), source.Receipt.ByteStart);
            Assert.Equal(sourcePin.GetProperty("byte_end").GetInt64(), source.Receipt.ByteEndExclusive);
            var input = Project(source, retailTable: true); var pairs = Varying(id); var aggregate = Aggregate(input, pairs);
            var profile = Evaluate(input, aggregate);
            Assert.Equal((uint?)0u, profile.Core.Domain.MasterObjectId); Assert.Equal((uint?)0u, profile.Core.Domain.SourceRelatedObjectId);
            Assert.Equal((uint?)0u, profile.Context.MasterObjectId); Assert.Equal((uint?)0u, profile.Context.SourceRelatedObjectId);
            Assert.Equal(false, profile.Core.Domain.VirtualProfileRecordPresent); Assert.Equal(false, profile.Context.VirtualProfileRecordPresent);
            Assert.Equal(85, input.Fields.Count); Assert.Equal(85, profile.Fields.Count);
            foreach (var field in vector.GetProperty("fields").EnumerateObject())
            {
                var actual = profile.Fields[field.Name];
                Assert.Equal(field.Value.GetProperty("base").GetInt64(), actual.Source.Value);
                Assert.Equal(field.Value.GetProperty("result").GetInt64(), actual.Value);
                Assert.Equal(field.Value.GetProperty("unclamped").GetInt64(), actual.History.Unclamped);
                Assert.Equal(input.SourceReceipt, actual.Source.SourceReceipt);
                Assert.NotEmpty(actual.Source.Steps);
            }
            Assert.Equal(profile.Parry, profile.CombinedParry);
            Assert.Equal(25, profile.AbnormalResists.Length); Assert.Equal(25, profile.AbnormalPenetrations.Length);
            Assert.Equal(6, profile.ElementalDefenses.Length); Assert.Equal(6, profile.Primaries.Length);
            Assert.Equal(79, profile.ConsumedComponents.Length); //77 percent/flat plus2 flat-only channels.
            Assert.All(profile.ConsumedComponents, x => Assert.True(x.Flat));
            Assert.Equal(2, profile.ConsumedComponents.Count(x => !x.Percent));
            Assert.DoesNotContain(profile.ConsumedComponents, x => x.Index is >= 6 and <= 11);
            Assert.Equal(RetailNpcSourcePresence.Absent, input.Fields["stat_ratio"].SourcePresence);
            Assert.Contains(input.Fields["stat_ratio"].Steps, x => x.Origin == "LevelTableDefault");
            Assert.Equal(3, input.AbnormalMerges.Count(x => x.IgnoredNamelessZero));
            Assert.False(profile.EffectiveActorValueVerified); Assert.False(profile.FixtureVerified); Assert.False(profile.ActivationReady);
            Assert.False(RetailNpcInitializationRules.Assess(source).CanConstructEffectiveInputs);
        }
    }

    [Theory]
    [InlineData("hp_regen", 3, -50, 0, 2L)]
    [InlineData("hp_regen", 3, 50, 0, 4L)]
    [InlineData("hp_regen", 100, 50, 20, 170L)]
    [InlineData("max_hp", 1, -200, 0, 1L)]
    [InlineData("max_hp", 2_000_000_000, 100, 0, 2_000_000_000L)]
    [InlineData("hp_regen", 100, -101, -10, 0L)]
    [InlineData("hp_regen", 0, 100, 37, 37L)]
    [InlineData("physical_defend", -1, 0, 0, 500000L)]
    [InlineData("physical_defend", -1, -100, 12, 12L)]
    [InlineData("magical_defend", -1, int.MaxValue, 0, 500000L)]
    [InlineData("magical_skill_boost", 65535, 0, 0, 0L)]
    [InlineData("magical_skill_boost_resist", 65535, 0, 12, 11L)]
    [InlineData("magical_resist", 65535, 0, 0, 9999L)]
    [InlineData("hit_accuracy", 1000, 10, -7, 1093L)]
    [InlineData("stat_ratio", 0, -1, 0, 1000L)]
    [InlineData("stat_ratio", 65000, 1, 0, 65000L)]
    [InlineData("stat_ratio", null, 10, 2, 1118L)]
    [InlineData("physical_critical_reduce_rate", 1200, 0, 0, 1200L)]
    [InlineData("physical_critical_damage_reduce", 1200, 0, 0, 700L)]
    [InlineData("proc_reduce_rate", 1200, 0, 0, 1000L)]
    [InlineData("elemental_defend_water", -1300, 100, 0, -1300L)]
    [InlineData("elemental_defend_air", -3, -50, 0, -2L)]
    [InlineData("elemental_defend_fire", 1000, 40, 0, 1300L)]
    [InlineData("abnormal_resist[0]", 0, 0, 900, 900L)]
    [InlineData("abnormal_resist[0]", 900, -50, 0, 450L)]
    [InlineData("abnormal_resist[0]", 900, 10000, 0, 9999L)]
    public void FixedNativeWidthTruncationAndClampVectors(string field, int? raw, int percent, int flat, long expected)
    {
        var fixture = new Fixture();
        if (field == "stat_ratio" && raw is null) fixture.Set("level", 66);
        else if (field.StartsWith("elemental_defend_", StringComparison.Ordinal)) fixture.Add(Node("first_level_attr", null, Node(field, raw!.Value.ToString(CultureInfo.InvariantCulture))));
        else if (field.StartsWith("abnormal_", StringComparison.Ordinal)) fixture.Add(AbnormalGroup(false, ("Poison", (ushort)raw!.Value)));
        else fixture.Set(field, raw!.Value);
        var input = Project(fixture.Inputs()); var pairs = ExplicitPairs();
        int index = input.Fields[field].Specification.ModifierIndex!.Value; pairs[index] = new(index, percent, flat);
        var result = Evaluate(input, Aggregate(input, pairs)); var value = result.Fields[field];
        Assert.Equal(expected, value.Value);
        Assert.Equal(percent, value.History.Percent); Assert.Equal(flat, value.History.Flat);
        Assert.Equal(value.Source.Value * (long)percent, value.History.Product);
        Assert.Equal(value.History.Product / 100, value.History.PercentContribution);
    }

    [Fact]
    public void EveryPercentFieldReachesBothNativeClampsAndKeepsThePreclampHistory()
    {
        var input = Project(new Fixture().Inputs());
        foreach (var field in input.Fields.Values.Where(x => x.Specification.Operation == RetailNpcScalarOperation.PercentAndFlat))
        {
            int index = field.Specification.ModifierIndex!.Value;
            var low = ExplicitPairs(); low[index] = new(index, -100, int.MinValue);
            var below = Evaluate(input, Aggregate(input, low)).Fields[field.Specification.Field];
            Assert.Equal(field.Specification.Minimum, below.Value); Assert.True(below.History.Unclamped < below.Value);
            var high = ExplicitPairs(); high[index] = new(index, 0, int.MaxValue);
            var above = Evaluate(input, Aggregate(input, high)).Fields[field.Specification.Field];
            Assert.Equal(field.Specification.Maximum, above.Value); Assert.True(above.History.Unclamped > above.Value);
        }
    }

    [Theory]
    [InlineData("Stat_arAll", 0x1f62fbfu)]
    [InlineData("Stat_arStunLike", 0x9d040u)]
    [InlineData("Curse,Stat_arPhysicalAb,Poison", 0x1620b7u)]
    [InlineData("Curse,Stat_arPhysicalAb", 0x101624b7u)]
    [InlineData("Poison,Stat_arMentalAb,Root", 0x800f58u)]
    [InlineData("Poison,Stat_arMentalAb", 0xc800f49u)]
    [InlineData("Poison, Bleed", 3u)]
    [InlineData("Sanctuary", 0x80000000u)]
    public void SourceMasksPreserveNativeGroupsAndCommaReplacementOrder(string name, uint mask)
    {
        var fixture = new Fixture(); fixture.Add(AbnormalGroup(false, (name, 321)));
        var input = Project(fixture.Inputs()); Assert.Equal(mask, Assert.Single(input.AbnormalMerges).Mask);
        for (int i = 0; i < 25; i++) Assert.Equal((mask & (1u << i)) == 0 ? 0L : 321L, input.Fields[$"abnormal_resist[{i}]"].Value);
    }

    [Fact]
    public void AbnormalOverlapUsesMaximumAndRetainsBothEntryOrdersAndTheInertPlaceholder()
    {
        foreach (bool reverse in new[] { false, true })
        {
            var fixture = new Fixture(); var entries = new[] { ("Poison", (ushort)900), ("Poison,Bleed", (ushort)200) };
            if (reverse) Array.Reverse(entries); fixture.Add(AbnormalGroup(false, entries));
            fixture.Add(Node("abnormal_penetrations", null, Node("data", null, Node("abnormal_penetration_value", "0"))));
            var input = Project(fixture.Inputs());
            Assert.Equal(900, input.Fields["abnormal_resist[0]"].Value); Assert.Equal(200, input.Fields["abnormal_resist[1]"].Value);
            Assert.Equal(2, input.Fields["abnormal_resist[0]"].SourceLexemes.Length);
            Assert.Equal(entries[0].Item1, input.AbnormalMerges[0].Name);
            Assert.True(input.AbnormalMerges[^1].IgnoredNamelessZero);
            Assert.Equal(0u, input.AbnormalMerges[^1].Mask);
            Assert.Equal(RetailNpcSourcePresence.Absent, input.Fields["abnormal_penetration[0]"].SourcePresence);
            Assert.All(input.Fields.Values, x => Assert.Equal(input.SourceReceipt, x.SourceReceipt));
        }
    }

    [Fact]
    public void PenetrationNoFlyAndSimpleRootConsumeTheNativeMismatchedNamedIndices()
    {
        var fixture = new Fixture(); fixture.Add(AbnormalGroup(true, ("NoFly", 100), ("SimpleRoot", 200)));
        var input = Project(fixture.Inputs()); var pairs = ExplicitPairs(); pairs[92] = new(92, 20, 1000); pairs[93] = new(93, -50, 2000);
        var profile = Evaluate(input, Aggregate(input, pairs));
        Assert.Equal((ushort)1120, profile.AbnormalPenetrations[23]); Assert.Equal((ushort)2100, profile.AbnormalPenetrations[24]);
        Assert.Equal("SimpleRoot_Arp", profile.Fields["abnormal_penetration[23]"].Source.Specification.ModifierAuditName);
        Assert.Equal("NoFly_Arp", profile.Fields["abnormal_penetration[24]"].Source.Specification.ModifierAuditName);
    }

    [Fact]
    public void PrimaryAndFlatOnlyChannelsAndCurrentPoolStagesRemainDistinct()
    {
        var fixture = new Fixture(); fixture.Set("max_hp", 200); fixture.Set("physical_attack", 999999); fixture.Set("magical_attack", 888888);
        fixture.Set("min_damage", 50); fixture.Set("max_mp", 300); fixture.Set("mp_regen", 9);
        fixture.Add(Node("first_level_attr", null, Node("str", "7")));
        var input = Project(fixture.Inputs()); var pairs = Varying(3);
        pairs[6] = new(6, 1000, 9999); pairs[18] = new(18, 50, 0); pairs[110] = new(110, int.MaxValue, -1100); pairs[152] = new(152, int.MinValue, 1500);
        var profile = Evaluate(input, Aggregate(input, pairs));
        Assert.Equal((ushort)7, profile.Primaries[0]); Assert.Equal(300, profile.MaximumHp);
        Assert.Equal(200, profile.Core.InitialHpOnSet.Value); Assert.Equal(300, profile.Core.MpDiagnostics.CurrentMpOnSet.Value);
        Assert.Equal(0, profile.Core.MpDiagnostics.MaxMpAtCommonSeedPoint.Value); Assert.Equal(0, profile.Core.MpDiagnostics.MpRegenAtFreshCreatureInitialization.Value);
        Assert.Equal((short)-1000, profile.HealSkillBoost); Assert.Equal((short)1000, profile.MpHealSkillBoost);
        Assert.Null(profile.Fields["heal_skill_boost"].History.Percent); Assert.Null(profile.Fields["str"].History.Flat);
        Assert.Equal((ushort)50, profile.Core.MainHandMinimumBeforeEffects.Value);
        Assert.Equal("999999", Assert.Single(profile.Core.Source.RawPhysicalAttack.RawValues));
        Assert.False(profile.Core.MpDiagnostics.MpLifecycleResolved); Assert.False(profile.ActivationReady);
        Assert.Throws<InvalidOperationException>(() => RetailNpcInitializationRules.RequireEffectiveInputs(profile.Core.Source));
    }

    [Theory]
    [InlineData("unknown-name")]
    [InlineData("comma-prefix")]
    [InlineData("trailing-space")]
    [InlineData("empty-token")]
    [InlineData("comma-outside25")]
    [InlineData("missing-value")]
    [InlineData("duplicate-value")]
    [InlineData("nameless-nonzero")]
    [InlineData("unknown-child")]
    [InlineData("case-root")]
    public void UnresolvedStatusSyntaxCannotPublishAScalarInput(string failure)
    {
        var fixture = new Fixture();
        string name = failure switch { "unknown-name" => "Mystery", "comma-prefix" => "Poi,Bleed", "trailing-space" => "Poison ",
            "empty-token" => "Poison,,Bleed", "comma-outside25" => "BuffStun,Poison", _ => "Poison" };
        var entry = Node("data", null, Node("abnormal_resist_name", name), Node("abnormal_resist_value", "100"));
        if (failure == "missing-value") entry[2]!.AsArray().RemoveAt(1);
        if (failure == "duplicate-value") entry[2]!.AsArray().Add(Node("abnormal_resist_value", "200"));
        if (failure == "nameless-nonzero") entry[2]!.AsArray().RemoveAt(0);
        if (failure == "unknown-child") entry[2]!.AsArray().Add(Node("mystery", "1"));
        fixture.Add(Node(failure == "case-root" ? "Abnormal_Resists" : "abnormal_resists", null, entry));
        var source = fixture.Project();
        if (!source.Success) { Assert.Null(source.Inputs); Assert.NotEmpty(source.Issues); return; }
        var core = Core(source.Inputs!); var result = RetailNpcOrdinaryScalarProjection.Project(source.Inputs!, core, new(core.Domain));
        Assert.False(result.Success); Assert.Null(result.Inputs); Assert.NotEmpty(result.Issues);
    }

    [Fact]
    public void CompleteAggregateOwnsItsSortedPairsBytesAndDisposedSourceDocuments()
    {
        var fixture = new Fixture(); fixture.Add(AbnormalGroup(false, ("Poison", 901)));
        var input = Project(fixture.Inputs()); var pairs = Varying(17); pairs[18] = new(18, int.MinValue, int.MaxValue);
        var original = pairs[18]; var receipt = Receipt(input, pairs);
        pairs.Reverse(); var admitted = RetailNpcOrdinaryScalarEvaluation.AdmitModifierAggregate(pairs, receipt);
        Assert.True(admitted.Success, string.Join(";", admitted.Issues)); var aggregate = admitted.Aggregate!;
        pairs.Clear(); fixture.Set("max_hp", 123456);
        Assert.Equal(original, aggregate.Pairs[18]); Assert.Equal(161, aggregate.Pairs.Length);
        Assert.Equal(int.MinValue, BinaryPrimitives.ReadInt32LittleEndian(aggregate.CanonicalBytes.AsSpan(18 * 8, 4)));
        Assert.Equal(receipt.ContentSha256, Hash(aggregate.CanonicalBytes.AsSpan()));
        Assert.Equal("data", input.AbnormalMerges[0].RawNode[0].GetString());
        Assert.Equal("abnormal_resists", input.AbnormalMerges[0].Group);
        Assert.Equal(901, input.Fields["abnormal_resist[0]"].Value);
        Assert.False(aggregate.RuntimeObservationVerified);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("extra")]
    [InlineData("duplicate")]
    [InlineData("negative")]
    [InlineData("outofrange")]
    [InlineData("null")]
    [InlineData("hash")]
    [InlineData("bytes")]
    [InlineData("epoch")]
    [InlineData("origin")]
    [InlineData("actor")]
    [InlineData("build")]
    [InlineData("region")]
    public void IncompleteOrUnpinnedAggregateFailsAtomically(string failure)
    {
        var input = Project(new Fixture().Inputs()); var pairs = Varying(9); var receipt = Receipt(input, pairs);
        switch (failure)
        {
            case "missing": pairs.RemoveAt(0); break;
            case "extra": pairs.Add(new(161, 0, 0)); break;
            case "duplicate": pairs[0] = new(1, 2, 3); break;
            case "negative": pairs[0] = new(-1, 2, 3); break;
            case "outofrange": pairs[0] = new(161, 2, 3); break;
            case "null": pairs[0] = null!; break;
            case "hash": receipt = receipt with { ContentSha256 = new('f', 64) }; break;
            case "bytes": receipt = receipt with { ContentBytes = 1289 }; break;
            case "epoch": receipt = receipt with { Epoch = "" }; break;
            case "origin": receipt = receipt with { Origin = (RetailNpcModifierInputOrigin)99 }; break;
            case "actor": receipt = receipt with { Origin = RetailNpcModifierInputOrigin.ExternalActorSnapshot, ActorObjectIdentity = null }; break;
            case "build": receipt = receipt with { Source = receipt.Source with { MainSha256 = new('f', 64) } }; break;
            case "region": receipt = receipt with { Source = receipt.Source with { Region = "" } }; break;
        }
        var result = RetailNpcOrdinaryScalarEvaluation.AdmitModifierAggregate(pairs, receipt);
        Assert.False(result.Success); Assert.Null(result.Aggregate); Assert.NotEmpty(result.Issues);
    }

    [Theory]
    [InlineData("source")]
    [InlineData("region")]
    [InlineData("table")]
    [InlineData("package")]
    [InlineData("hash")]
    [InlineData("epoch")]
    [InlineData("origin")]
    [InlineData("actor")]
    [InlineData("kind")]
    [InlineData("virtual-profile")]
    [InlineData("difficulty")]
    [InlineData("dual")]
    [InlineData("unresolved-virtual-profile")]
    public void SourceContextEpochAndCurrentEligibilityPinsCannotBeMixed(string failure)
    {
        var input = Project(new Fixture().Inputs()); var aggregate = Aggregate(input, Varying(2)); var context = Context(input, aggregate);
        context = failure switch
        {
            "source" => context with { ExpectedSource = input.SourceReceipt with { RawRecordSha256 = new('f', 64) } },
            "region" => context with { ExpectedSource = input.SourceReceipt with { Region = "China" } },
            "table" => context with { ExpectedRatioTableSha256 = new('f', 64) },
            "package" => context with { ExpectedPackageContextSha256 = new('f', 64) },
            "hash" => context with { ExpectedModifierContentSha256 = new('f', 64) },
            "epoch" => context with { ExpectedEpoch = "another-epoch" },
            "origin" => context with { ExpectedOrigin = RetailNpcModifierInputOrigin.ExternalActorSnapshot },
            "actor" => context with { ExpectedActorObjectIdentity = "actor42" },
            "kind" => context with { NativeKind = 2 },
            "virtual-profile" => context with { VirtualProfileRecordPresent = true },
            "difficulty" => context with { DifficultyRecordPresent = true },
            "dual" => context with { DualWeaponFlagPresent = true },
            _ => context with { VirtualProfileRecordPresent = null }
        };
        var result = RetailNpcOrdinaryScalarEvaluation.Evaluate(input, aggregate, context);
        Assert.False(result.Success); Assert.Null(result.Profile); Assert.NotEmpty(result.Issues);
    }

    [Theory]
    [InlineData("master", null)] [InlineData("master", 1u)] [InlineData("master", uint.MaxValue)]
    [InlineData("related", null)] [InlineData("related", 1u)] [InlineData("related", uint.MaxValue)]
    public void CurrentIndependentObjectIdsCannotBeInferredFromTheZeroSeedOrAbsentProfile(string field, uint? value)
    {
        var input = Project(new Fixture().Inputs()); var aggregate = Aggregate(input, Varying(2)); var context = Context(input, aggregate);
        Assert.Equal((uint?)0u, input.Core.Domain.MasterObjectId); Assert.Equal((uint?)0u, input.Core.Domain.SourceRelatedObjectId);
        Assert.Equal(false, context.VirtualProfileRecordPresent);
        context = field == "master" ? context with { MasterObjectId = value } : context with { SourceRelatedObjectId = value };
        var result = RetailNpcOrdinaryScalarEvaluation.Evaluate(input, aggregate, context);
        Assert.False(result.Success); Assert.Null(result.Profile);
        Assert.Equal(field == "master" ? "scalar_effect_master" : "scalar_effect_related", Assert.Single(result.Issues).Code);
    }

    [Theory]
    [InlineData("master", null)] [InlineData("master", 1u)] [InlineData("master", uint.MaxValue)]
    [InlineData("related", null)] [InlineData("related", 1u)] [InlineData("related", uint.MaxValue)]
    public void ProjectionCannotReplaceThePinnedSeedObjectIdsWithDifferentAssertions(string field, uint? value)
    {
        var source = new Fixture().Inputs(); var core = Core(source);
        var wrong = field == "master" ? core.Domain with { MasterObjectId = value } : core.Domain with { SourceRelatedObjectId = value };
        var result = RetailNpcOrdinaryScalarProjection.Project(source, core, new(wrong));
        Assert.False(result.Success); Assert.Null(result.Inputs);
        Assert.Equal("scalar_source_identity", Assert.Single(result.Issues).Code);
    }

    [Fact]
    public void ExternalSnapshotAdmissionAndSeedReceiptMismatchNeverCertifyAnObservedActor()
    {
        var source = new Fixture().Inputs(); var core = Core(source);
        var wrong = RetailNpcOrdinaryScalarProjection.Project(source, core, new(core.Domain with { ExpectedSource = source.Receipt with { ByteStart = 1 } }));
        Assert.False(wrong.Success); Assert.Null(wrong.Inputs);
        var input = Project(source); var pairs = Varying(31);
        var receipt = Receipt(input, pairs) with { Origin = RetailNpcModifierInputOrigin.ExternalActorSnapshot, ActorObjectIdentity = "actor42", Epoch = "observed-epoch" };
        var admitted = RetailNpcOrdinaryScalarEvaluation.AdmitModifierAggregate(pairs, receipt); Assert.True(admitted.Success);
        var profile = Evaluate(input, admitted.Aggregate!);
        Assert.False(profile.Modifiers.RuntimeObservationVerified); Assert.False(profile.EffectiveActorValueVerified); Assert.False(profile.ActivationReady);
    }

    // Synthetic helpers are separate from the hash-pinned external source integration fixture.
    private static RetailNpcOrdinarySeedDomain Domain(RetailNpcTemplateInputs source, RetailNpcLevelRatioTableInputs table) =>
        new(source.Receipt, table.Receipt.SourceSha256, FreshUniqueTemplate: true, FreshActor: true, NativeKind: 1,
            MasterObjectId: 0u, SourceRelatedObjectId: 0u, VirtualProfileRecordPresent: false, DifficultyRecordPresent: false, DualWeaponFlagPresent: false);
    private static RetailNpcOrdinaryCoreSeeds Core(RetailNpcTemplateInputs source, bool retailTable = false)
    {
        var table = Table(source, retailTable); var result = RetailNpcOrdinaryCoreSeedProjection.ProjectOrdinaryCoreSeeds(source, table, Domain(source, table));
        Assert.True(result.Success, string.Join(";", result.Issues)); return result.Seeds!;
    }
    private static RetailNpcOrdinaryScalarInputs Project(RetailNpcTemplateInputs source, bool retailTable = false)
    {
        var core = Core(source, retailTable); var result = RetailNpcOrdinaryScalarProjection.Project(source, core, new(core.Domain));
        Assert.True(result.Success, string.Join(";", result.Issues)); return result.Inputs!;
    }
    private static RetailNpcLevelRatioTableInputs Table(RetailNpcTemplateInputs source, bool retailTable)
    {
        byte[] bytes = retailTable ? Convert.FromBase64String(RealTableBase64) : SyntheticNpcRatioTable.Bytes();
        var result = RetailNpcLevelRatioTableInputs.Admit(bytes,
            new(source.Receipt.DatasetId, source.Receipt.OutputManifestSha256, source.Receipt.Region,
                (retailTable ? RealTableHash : SyntheticNpcRatioTable.Sha256), @"Z:\must-not-be-opened\pc_stat_ratio.xml", "pc_stat_ratio.xml", "regional_missing_base_fallback"));
        Assert.True(result.Success, string.Join(";", result.Issues)); return result.Table!;
    }
    private static string Hash(ReadOnlySpan<byte> bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    private static List<RetailNpcModifierPairInput> ExplicitPairs() => Enumerable.Range(0,161).Select(i => new RetailNpcModifierPairInput(i, 0, 0)).ToList();
    private static List<RetailNpcModifierPairInput> Varying(int seed) => Enumerable.Range(0,161)
        .Select(i => new RetailNpcModifierPairInput(i, (i * 11 + seed) % 41 - 20, (i * 17 + seed) % 101 - 50)).ToList();
    private static RetailNpcModifierAggregateReceipt Receipt(RetailNpcOrdinaryScalarInputs input, IReadOnlyList<RetailNpcModifierPairInput> pairs)
    {
        byte[] bytes = new byte[1288];
        foreach (var pair in pairs)
        {
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(pair.Index*8,4), pair.Percent);
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(pair.Index*8+4,4), pair.Flat);
        }
        return new(RetailNpcModifierInputOrigin.FixedMathematicalFixture, input.SourceReceipt, input.RatioTableReceipt.SourceSha256,
            new('7',64), Hash(bytes), bytes.Length, "fixed-scalar-epoch", null, "embedded-fixed-input");
    }
    private static RetailNpcModifierAggregate Aggregate(RetailNpcOrdinaryScalarInputs input, IReadOnlyList<RetailNpcModifierPairInput> pairs)
    {
        var result = RetailNpcOrdinaryScalarEvaluation.AdmitModifierAggregate(pairs, Receipt(input,pairs));
        Assert.True(result.Success, string.Join(";", result.Issues)); return result.Aggregate!;
    }
    private static RetailNpcOrdinaryScalarEvaluationContext Context(RetailNpcOrdinaryScalarInputs input, RetailNpcModifierAggregate aggregate) =>
        new(input.SourceReceipt,input.RatioTableReceipt.SourceSha256,aggregate.Receipt.PackageContextSha256,aggregate.Receipt.ContentSha256,
            aggregate.Receipt.Epoch,aggregate.Receipt.Origin,aggregate.Receipt.ActorObjectIdentity,
            NativeKind: 1, MasterObjectId: 0u, SourceRelatedObjectId: 0u,
            VirtualProfileRecordPresent: false, DifficultyRecordPresent: false, DualWeaponFlagPresent: false);
    private static RetailNpcOrdinaryScalarProfile Evaluate(RetailNpcOrdinaryScalarInputs input, RetailNpcModifierAggregate aggregate)
    {
        var result = RetailNpcOrdinaryScalarEvaluation.Evaluate(input,aggregate,Context(input,aggregate));
        Assert.True(result.Success, string.Join(";", result.Issues)); return result.Profile!;
    }
    private static JsonArray Node(string tag, string? value, params JsonNode[] children)
    {
        var text = new JsonArray(); if (value is not null) text.Add(value);
        foreach (var child in children) text.Add(child);
        return new(JsonValue.Create(tag),new JsonArray(),text);
    }
    private static JsonArray AbnormalGroup(bool penetration, params (string Name, ushort Value)[] entries)
    {
        string prefix = penetration ? "abnormal_penetration" : "abnormal_resist";
        return Node(penetration ? "abnormal_penetrations" : "abnormal_resists",null,entries.Select(x => (JsonNode)Node("data",null,
            Node(prefix+"_name",x.Name),Node(prefix+"_value",x.Value.ToString(CultureInfo.InvariantCulture)))).ToArray());
    }
    private sealed class Fixture
    {
        private JsonArray Root { get; } = Node("npc", null, Node("id", "1"), Node("name", "One"), Node("level", "32"));
        internal void Add(JsonNode node) => Root[2]!.AsArray().Add(node);
        internal void Set(string tag, int value)
        {
            var content = Root[2]!.AsArray();
            foreach (var node in content.Where(x => x is JsonArray a && a[0]!.GetValue<string>() == tag).ToArray()) content.Remove(node);
            Add(Node(tag, value.ToString(CultureInfo.InvariantCulture)));
        }
        internal RetailNpcTemplateInputs Inputs()
        {
            var result = Project(); Assert.True(result.Success, string.Join("; ", result.Issues)); return result.Inputs!;
        }
        internal RetailNpcTemplateProjectionResult Project()
        {
            const string path = @"Z:\must-not-be-opened\npcs_monsters.xml";
            var policy = new RetailNpcTemplateProjectionPolicy(new string('d', 64), new string('e', 64), "USA");
            var profile = new JsonObject { ["schema_version"] = 1, ["dataset_id"] = policy.ExpectedDatasetId, ["npc_id"] = 1, ["devname"] = "One",
                ["scalar_fields"] = new JsonObject(), ["raw_record"] = Root.DeepClone(), ["skill_bindings"] = new JsonArray(),
                ["source"] = new JsonObject { ["path"] = path, ["byte_start"] = 0, ["byte_end_exclusive"] = 64, ["line"] = 1, ["record_index"] = 0,
                    ["raw_sha256"] = new string('a', 64), ["semantic_sha256"] = new string('b', 64) },
                ["ordered_lists"] = new JsonObject(), ["binding48"] = new JsonObject(), ["aggregate_crosscheck"] = new JsonObject(),
                ["blockers"] = new JsonArray("effective_runtime_profile_not_verified"),
                ["verification"] = new JsonObject { ["stage"] = "static_source_extracted", ["fixture_verified"] = false, ["activation_ready"] = false } };
            var provenance = new JsonObject { ["consumer_registry"] = new JsonObject { ["builds"] = new JsonObject {
                ["main"] = new JsonObject { ["sha256"] = RetailNpcTemplateProjection.MainBinarySha256 },
                ["npc"] = new JsonObject { ["sha256"] = RetailNpcTemplateProjection.NpcBinarySha256 } } },
                ["selection"] = new JsonArray(new JsonObject { ["filename"] = "npcs_monsters.xml", ["selected"] = path,
                    ["region"] = "USA", ["reason"] = "regional_missing_base_fallback" }) };
            var manifest = new JsonArray(new JsonObject { ["path"] = path, ["bytes"] = 1000, ["sha256"] = new string('c', 64),
                ["record_count"] = 1, ["record_tag"] = "npc", ["strict_xml_complete"] = true });
            using var profileJson = JsonDocument.Parse(profile.ToJsonString());
            using var provenanceJson = JsonDocument.Parse(provenance.ToJsonString());
            using var manifestJson = JsonDocument.Parse(manifest.ToJsonString());
            var source = new RetailNpcSourceProfile(profileJson.RootElement);
            var dataset = new RetailNpcStaticDataset(policy.ExpectedDatasetId, policy.ExpectedOutputManifestSha256,
                new Dictionary<int, RetailNpcSourceProfile> { [1] = source }, 1, 0, 0,
                ImmutableArray<RetailNpcArtifactReceipt>.Empty, provenanceJson.RootElement, manifestJson.RootElement);
            return RetailNpcTemplateProjection.Project(dataset, 1, policy);
        }
    }

    private const string RealTableHash = "b7947b4725c0d273ab62722adfe576b508e6e4179ffb18e2e9b5c1767a4b779f";
    private const string PilotDatasetId = "33dfd001439620a102ccfca9b30765578efa88acb6f561f8c116ce8d98873335";
    private const string PilotManifestHash = "036c316db96dcb1d50ccd2b55ef7b472afff9be557ea10c385cc22369d49b0cd";

    private static string RealTableBase64 => RetailNpcPrivateStatFixtures.ReadText("RealTableBase64");

    private static string PilotProfilesJson => RetailNpcPrivateStatFixtures.ReadText("PilotProfilesJson");

    private static string PilotProvenanceJson => RetailNpcPrivateStatFixtures.ReadText("PilotProvenanceJson");

    private static string PilotSourceManifestJson => RetailNpcPrivateStatFixtures.ReadText("PilotSourceManifestJson");

    private static string PilotSeedVectorsJson => RetailNpcPrivateStatFixtures.ReadText("PilotSeedVectorsJson");

    private static string PilotScalarVectorsJson => RetailNpcPrivateStatFixtures.ReadText("PilotScalarVectorsJson");

    private static string ScalarContractsJson => RetailNpcPrivateStatFixtures.ReadText("ScalarContractsJson");
}
