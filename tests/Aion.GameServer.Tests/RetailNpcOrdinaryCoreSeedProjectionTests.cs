using System.Collections.Immutable;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using Aion.GameServer.Dataholders.RetailNpc;

namespace Aion.GameServer.Tests;

public class RetailNpcOrdinaryCoreSeedProjectionTests
{
    [Theory]
    [InlineData(null, 1)]
    [InlineData(int.MinValue, 1)]
    [InlineData(-1, 1)]
    [InlineData(0, 1)]
    [InlineData(1, 1)]
    [InlineData(2_000_000_000, 2_000_000_000)]
    [InlineData(2_000_000_001, 2_000_000_000)]
    [InlineData(int.MaxValue, 2_000_000_000)]
    public void SignedHpCapCopyAndSeparateLowerBounds(int? raw, int expected)
    {
        var fixture = new Fixture();
        if (raw.HasValue) fixture.Set("max_hp", raw.Value);
        var seeds = Success(fixture.Inputs());
        Assert.Equal(expected, seeds.Template.MaxHp.Value);
        Assert.Equal(expected, seeds.Template.InitialHp.Value);
        Assert.Equal(expected, seeds.MaxHpBeforeEffects.Value);
        Assert.Equal(expected, seeds.InitialHpOnSet.Value);
        Assert.Equal(raw.HasValue, seeds.Template.MaxHp.SourceValueUsed);
        Assert.Equal(raw?.ToString(CultureInfo.InvariantCulture), seeds.Template.MaxHp.SourceLexemes.SingleOrDefault());
        if (raw.HasValue && raw.Value < 1)
        {
            Assert.Equal(RetailNpcCoreSeedOrigin.PostparseClamp, seeds.Template.MaxHp.Origin);
            var copy = Assert.Single(seeds.Template.InitialHp.Steps.Where(x => x.Rule.Contains("initial-hp-copy", StringComparison.Ordinal)));
            Assert.Equal((long)raw.Value, copy.Result); // Copy precedes the independent postparse lower bound.
        }
        if (raw is > 2_000_000_000)
            Assert.Contains(seeds.Template.MaxHp.Steps, x => x.Origin == RetailNpcCoreSeedOrigin.ParserClamp);
    }

    [Theory]
    [InlineData(null, 1015)]
    [InlineData(-1, 65000)]
    [InlineData(0, 1000)]
    [InlineData(999, 1000)]
    [InlineData(1000, 1000)]
    [InlineData(65000, 65000)]
    [InlineData(65001, 65000)]
    [InlineData(int.MaxValue, 65000)]
    public void RatioAbsenceUsesLevel66ButExplicitBitsUseUnsignedParserClamp(int? raw, int expected)
    {
        var fixture = new Fixture(); fixture.Set("level", 66);
        if (raw.HasValue) fixture.Set("stat_ratio", raw.Value);
        var seeds = Success(fixture.Inputs());
        Assert.Equal(expected, seeds.Template.StatRatio.Value);
        Assert.Equal(expected, seeds.StatRatioBeforeEffects.Value);
        Assert.Equal(raw is null, seeds.Template.StatRatio.NativeDefaultApplied);
        if (raw is null)
        {
            Assert.Equal(RetailNpcCoreSeedOrigin.LevelTableDefault, seeds.Template.StatRatio.Origin);
            Assert.Equal(SyntheticTableHash, seeds.Template.StatRatio.RatioTableReceipt!.SourceSha256);
        }
        else Assert.Null(seeds.Template.StatRatio.RatioTableReceipt);
        if (raw == -1) Assert.Equal(4_294_967_295L, seeds.Template.StatRatio.Steps[^1].Input);
    }

    [Theory]
    [InlineData(int.MinValue, 0)]
    [InlineData(-1, 65535)]
    [InlineData(0, 0)]
    [InlineData(65535, 65535)]
    [InlineData(65536, 0)]
    [InlineData(65537, 1)]
    [InlineData(int.MaxValue, 65535)]
    public void PerHandSeedsUseUnsignedLowWordsWithOffHandZero(int raw, int expected)
    {
        var fixture = new Fixture(); fixture.Set("min_damage", raw); fixture.Set("max_damage", raw);
        var seeds = Success(fixture.Inputs());
        Assert.Equal(raw, seeds.Template.MinDamage.Value); Assert.Equal(raw, seeds.Template.MaxDamage.Value);
        Assert.Equal((ushort)expected, seeds.MainHandMinimumBeforeEffects.Value);
        Assert.Equal((ushort)expected, seeds.MainHandMaximumBeforeEffects.Value);
        Assert.Equal((ushort)0, seeds.OffHandMinimumBeforeEffects.Value);
        Assert.Equal((ushort)0, seeds.OffHandMaximumBeforeEffects.Value);
        Assert.Equal(RetailNpcCoreSeedOrigin.RuntimeNarrowing, seeds.MainHandMinimumBeforeEffects.Origin);
        Assert.False(seeds.OffHandMinimumBeforeEffects.SourceValueUsed);
    }

    [Fact]
    public void ConstructorAbsenceAndExplicitZeroRemainDifferentEvenWhenValuesAgree()
    {
        var absent = Success(new Fixture().Inputs());
        var fixture = new Fixture(); fixture.Set("max_hp", 0); fixture.Set("hp_regen", 0); fixture.Set("max_mp", 0);
        var zero = Success(fixture.Inputs());
        Assert.Equal(1, absent.Template.MaxHp.Value); Assert.Equal(1, zero.Template.MaxHp.Value);
        Assert.Equal(RetailNpcSourcePresence.Absent, absent.Template.MaxHp.SourcePresence);
        Assert.Equal(RetailNpcSourcePresence.ExplicitZero, zero.Template.MaxHp.SourcePresence);
        Assert.True(absent.Template.MaxHp.NativeDefaultApplied); Assert.False(zero.Template.MaxHp.NativeDefaultApplied);
        Assert.Equal(RetailNpcCoreSeedOrigin.ConstructorDefault, absent.Template.InitialHp.Origin);
        Assert.Equal(RetailNpcCoreSeedOrigin.ConstructorDefault, absent.Template.InitialMp.Origin);
        Assert.Equal(RetailNpcSourcePresence.Absent, absent.HpRegenerationBeforeEffects.SourcePresence);
        Assert.Equal(RetailNpcSourcePresence.ExplicitZero, zero.HpRegenerationBeforeEffects.SourcePresence);
        Assert.All(absent.Template.Primaries.Values, x => { Assert.Equal((ushort)0, x.Value); Assert.True(x.NativeDefaultApplied); });
        Assert.All(absent.Template.Elements.Values, x => { Assert.Equal((short)0, x.Value); Assert.True(x.NativeDefaultApplied); });
    }

    [Fact]
    public void FirstLevelWidthsAndOrderArePreservedBeforeElementalEffects()
    {
        var fixture = new Fixture();
        fixture.Add(Node("first_level_attr", null, Node("will", "65535"), Node("str", "0"),
            Node("elemental_defend_fire", "-32768"), Node("elemental_defend_water", "32767"), Node("elemental_defend_air", "-1301")));
        var inputs = fixture.Inputs();
        var seeds = Success(inputs);
        Assert.Equal((ushort)65535, seeds.Template.Primaries["will"].Value);
        Assert.Equal((short)-32768, seeds.FirstLevelBaseElements["elemental_defend_fire"].Value);
        Assert.Equal((short)32767, seeds.FirstLevelCurrentElementsBeforeEffects["elemental_defend_water"].Value);
        Assert.Equal((short)-1301, seeds.FirstLevelCurrentElementsBeforeEffects["elemental_defend_air"].Value); // No effect clamp applied.
        Assert.Equal(inputs.RawRecord.GetRawText(), seeds.Source.RawRecord.GetRawText());
        Assert.Equal(new[] { "will", "str", "elemental_defend_fire", "elemental_defend_water", "elemental_defend_air" },
            RetailNpcSourceProfile.RawChildren(inputs.FirstLevel.RawGroup!.Value).Select(x => x[0].GetString()));
        Assert.Equal(RetailNpcSourcePresence.ExplicitZero, seeds.Template.Primaries["str"].SourcePresence);
    }

    [Fact]
    public void MpTemplateCopiesAndNativeZeroStagesNeverBecomeOneEffectiveMpValue()
    {
        var fixture = new Fixture(); fixture.Set("max_mp", 375); fixture.Set("mp_regen", 25); fixture.Set("hp_regen", -1);
        var seeds = Success(fixture.Inputs());
        Assert.Equal(375, seeds.Template.MaxMp.Value); Assert.Equal(375, seeds.Template.InitialMp.Value);
        Assert.Equal(25, seeds.Template.MpRegeneration.Value);
        Assert.Equal(375, seeds.MpDiagnostics.CurrentMpOnSet.Value);
        Assert.Equal(0, seeds.MpDiagnostics.MaxMpAtCommonSeedPoint.Value);
        Assert.Equal(0, seeds.MpDiagnostics.MpRegenAtFreshCreatureInitialization.Value);
        Assert.Equal("375", Assert.Single(seeds.MpDiagnostics.MaxMpAtCommonSeedPoint.SourceLexemes));
        Assert.False(seeds.MpDiagnostics.MaxMpAtCommonSeedPoint.SourceValueUsed);
        Assert.False(seeds.MpDiagnostics.MpLifecycleResolved);
        Assert.Equal(-1, seeds.HpRegenerationBeforeEffects.Value); // Later HP-regen effects/clamp are a different stage.
    }

    [Theory]
    [InlineData(0, 1000)]
    [InlineData(100, 1525)]
    public void TableIndexUsesTemplateLevelIncludingEntryZero(int level, int expected)
    {
        var fixture = new Fixture(); fixture.Set("level", level);
        Assert.Equal(expected, Success(fixture.Inputs()).Template.StatRatio.Value);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    [InlineData(int.MaxValue)]
    public void AbsentRatioCannotInventSpawnLevelOrReadOutsideTheTable(int level)
    {
        var fixture = new Fixture(); fixture.Set("level", level);
        Failure(fixture.Inputs(), "ratio_template_level");
        fixture.Set("stat_ratio", 0);
        Assert.Equal(1000, Success(fixture.Inputs()).Template.StatRatio.Value);
    }

    [Fact]
    public void UnknownFirstLevelTokenOrAliasCannotBeIgnoredPastNativeEarlyTermination()
    {
        var unknown = new Fixture(); unknown.Add(Node("first_level_attr", null, Node("unknown", "1"), Node("str", "42")));
        Failure(unknown.Inputs(), "seed_unknown_first_level");
        var aliases = new Fixture(); aliases.Add(Node("first_level_attr", null,
            Node("elemental_resist_air", "250"), Node("elemental_defend_air", "130")));
        Failure(aliases.Inputs(), "seed_unknown_first_level");
        var rootAlias = new Fixture(); rootAlias.Add(Node("MAX_HP", "200"));
        Failure(rootAlias.Inputs(), "seed_root_alias");
    }

    [Fact]
    public void RepeatedOrMalformedKnownFieldsFailTheSourcePipelineWithoutASeed()
    {
        var root = new Fixture(); root.Add(Node("max_hp", "10")); root.Add(Node("max_hp", "20"));
        Assert.False(root.Project().Success);
        var group = new Fixture(); group.Add(Node("first_level_attr", null, Node("str", "1"), Node("str", "2")));
        Assert.False(group.Project().Success);
        var malformed = new Fixture(); malformed.Add(Node("max_hp", "10trailing"));
        Assert.False(malformed.Project().Success);
        var width = new Fixture(); width.Add(Node("first_level_attr", null, Node("str", "65536")));
        Assert.False(width.Project().Success);
    }

    [Theory]
    [InlineData("template", false)] [InlineData("template", null)]
    [InlineData("actor", false)] [InlineData("actor", null)]
    [InlineData("virtual-profile", true)] [InlineData("virtual-profile", null)]
    [InlineData("difficulty", true)] [InlineData("difficulty", null)]
    [InlineData("dual", true)] [InlineData("dual", null)]
    public void UnsupportedAndUnresolvedContextsAreVisibleFailures(string field, bool? value)
    {
        var source = new Fixture().Inputs(); var table = Table(); var domain = Domain(source, table);
        domain = field switch
        {
            "template" => domain with { FreshUniqueTemplate = value }, "actor" => domain with { FreshActor = value },
            "virtual-profile" => domain with { VirtualProfileRecordPresent = value }, "difficulty" => domain with { DifficultyRecordPresent = value },
            _ => domain with { DualWeaponFlagPresent = value }
        };
        var result = RetailNpcOrdinaryCoreSeedProjection.ProjectOrdinaryCoreSeeds(source, table, domain);
        Assert.False(result.Success); Assert.Null(result.Seeds); Assert.Single(result.Issues);
    }

    [Theory]
    [InlineData("master", null)] [InlineData("master", 1u)] [InlineData("master", uint.MaxValue)]
    [InlineData("related", null)] [InlineData("related", 1u)] [InlineData("related", uint.MaxValue)]
    public void AbsentVirtualProfileDoesNotProveIndependentObjectIdsAreZero(string field, uint? value)
    {
        var source = new Fixture().Inputs(); var table = Table(); var domain = Domain(source, table);
        Assert.Equal(false, domain.VirtualProfileRecordPresent);
        domain = field == "master" ? domain with { MasterObjectId = value } : domain with { SourceRelatedObjectId = value };
        var result = RetailNpcOrdinaryCoreSeedProjection.ProjectOrdinaryCoreSeeds(source, table, domain);
        Assert.False(result.Success); Assert.Null(result.Seeds);
        var issue = Assert.Single(result.Issues);
        Assert.Equal(field == "master" ? "seed_master_context" : "seed_related_context", issue.Code);
        Assert.Equal(field == "master" ? "/context/master_object_id" : "/context/source_related_object_id", issue.RecordPath);
    }

    [Theory]
    [InlineData(null)] [InlineData(2)] [InlineData(4)]
    public void OtherOrUnknownNativeKindsDoNotUseTheOrdinaryBranch(int? kind)
    {
        var source = new Fixture().Inputs(); var table = Table();
        var result = RetailNpcOrdinaryCoreSeedProjection.ProjectOrdinaryCoreSeeds(source, table, Domain(source, table) with { NativeKind = kind });
        Assert.Equal("seed_kind_context", Assert.Single(result.Issues).Code); Assert.Null(result.Seeds);
    }

    [Theory]
    [InlineData(1)] [InlineData(-1)]
    public void SourceDualFlagCannotDisagreeWithTheNonDualContextAssertion(int dual)
    {
        var fixture = new Fixture(); fixture.Set("dual", dual); Failure(fixture.Inputs(), "seed_dual_source");
    }

    [Fact]
    public void PinnedSourceReceiptMutationsAndTablePinAreRejected()
    {
        var source = new Fixture().Inputs(); var table = Table(); var domain = Domain(source, table);
        var receipts = new[] { source.Receipt with { DatasetId = new string('a', 64) },
            source.Receipt with { Region = "China" }, source.Receipt with { RawRecordSha256 = new string('f', 64) },
            source.Receipt with { ByteStart = 1 }, source.Receipt with { SourcePath = "another.xml" },
            source.Receipt with { OutputManifestSha256 = new string('a', 64) } };
        foreach (var receipt in receipts)
        {
            var result = RetailNpcOrdinaryCoreSeedProjection.ProjectOrdinaryCoreSeeds(source, table, domain with { ExpectedSource = receipt });
            Assert.Equal("seed_source_identity", Assert.Single(result.Issues).Code); Assert.Null(result.Seeds);
        }
        var badPin = RetailNpcOrdinaryCoreSeedProjection.ProjectOrdinaryCoreSeeds(source, table,
            domain with { ExpectedRatioTableSha256 = new string('a', 64) });
        Assert.Equal("seed_table_context", Assert.Single(badPin.Issues).Code);
    }

    [Theory]
    [InlineData("dataset")] [InlineData("manifest")] [InlineData("region")]
    public void ACompleteTableFromAnIncompatibleStaticContextCannotBeSubstituted(string field)
    {
        var policy = TablePolicy();
        policy = field switch { "dataset" => policy with { ExpectedDatasetId = new string('a', 64) },
            "manifest" => policy with { ExpectedOutputManifestSha256 = new string('a', 64) }, _ => policy with { ExpectedRegion = "China" } };
        var table = Table(policy); var source = new Fixture().Inputs();
        var result = RetailNpcOrdinaryCoreSeedProjection.ProjectOrdinaryCoreSeeds(source, table, Domain(source, table));
        Assert.Equal("seed_table_context", Assert.Single(result.Issues).Code); Assert.Null(result.Seeds);
    }

    [Fact]
    public void TableOwnsItsBytesAndPreservesAllOrderedLexemesWithoutClaimingRuntimeLoad()
    {
        byte[] raw = SyntheticNpcRatioTable.Bytes();
        var result = RetailNpcLevelRatioTableInputs.Admit(raw, TablePolicy());
        Assert.True(result.Success, string.Join("; ", result.Issues)); var table = result.Table!;
        raw[0] ^= 0xff;
        Assert.Equal(SyntheticTableHash, Hash(table.SourceBytes.AsSpan()));
        Assert.Equal(100, table.Rows.Length); Assert.Equal((ushort)1000, table.EntryZero);
        Assert.Equal(66, table.Rows[65].Level); Assert.Equal((ushort)1015, table.Rows[65].Ratio);
        Assert.Equal("66", table.Rows[65].LevelLexeme); Assert.Equal("1015", table.Rows[65].RatioLexeme.Trim());
        Assert.True(table.Rows[65].SourceLine > 0); Assert.False(table.LoadedRuntimeTableVerified); Assert.False(table.ActivationReady);
        Assert.Throws<ArgumentOutOfRangeException>(() => table.ForTemplateLevel(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => table.ForTemplateLevel(101));
    }

    [Theory]
    [InlineData("late-invalid")] [InlineData("missing")] [InlineData("duplicate")]
    [InlineData("reordered")] [InlineData("extra")] [InlineData("wrong-child")]
    [InlineData("duplicate-ratio")] [InlineData("unexpected-text")]
    public void ATableIsPublishedOnlyAfterEveryRowAndTheWholeDocumentAreValid(string damage)
    {
        var root = TableDocument();
        var levels = root.Elements().ToArray();
        switch (damage)
        {
            case "late-invalid": levels[99].Element("stat_ratio")!.Value = "65001"; break;
            case "missing": levels[99].Remove(); break;
            case "duplicate": levels[99].SetAttributeValue("level", "99"); break;
            case "reordered": levels[0].SetAttributeValue("level", "2"); levels[1].SetAttributeValue("level", "1"); break;
            case "extra": root.Add(new XElement("level", new XAttribute("level", 101), new XElement("stat_ratio", 1000))); break;
            case "wrong-child": levels[99].Element("stat_ratio")!.Name = "unknown"; break;
            case "duplicate-ratio": levels[99].Add(new XElement("stat_ratio", 1000)); break;
            default: root.Add(new XText("not whitespace")); break;
        }
        var result = AdmitXml(root.ToString(SaveOptions.DisableFormatting));
        Assert.False(result.Success); Assert.Null(result.Table); Assert.NotEmpty(result.Issues);
    }

    [Fact]
    public void TableHashPathDepthSizeAndTrailingXmlAreBoundedBeforePublication()
    {
        Assert.Equal("table_hash", Assert.Single(RetailNpcLevelRatioTableInputs.Admit(
            SyntheticNpcRatioTable.Bytes(), TablePolicy() with { ExpectedSourceSha256 = new string('a', 64) }).Issues).Code);
        foreach (string path in new[] { "../pc_stat_ratio.xml", "USA/../pc_stat_ratio.xml", "C:/pc_stat_ratio.xml", "China/pc_stat_ratio.xml" })
            Assert.Equal("table_selection", Assert.Single(RetailNpcLevelRatioTableInputs.Admit(
                SyntheticNpcRatioTable.Bytes(), TablePolicy() with { SelectedRelativePath = path }).Issues).Code);
        Assert.False(AdmitXml("<pc_stat_ratios>" + string.Concat(Enumerable.Repeat("<nested>", 10))
            + string.Concat(Enumerable.Repeat("</nested>", 10)) + "</pc_stat_ratios>").Success);
        Assert.False(AdmitXml(TableDocument().ToString(SaveOptions.DisableFormatting) + "<extra/>").Success);
        Assert.False(AdmitXml("<!DOCTYPE pc_stat_ratios [<!ENTITY custom '1000'>]>"
            + TableDocument().ToString(SaveOptions.DisableFormatting).Replace("<stat_ratio>1000</stat_ratio>",
                "<stat_ratio>&custom;</stat_ratio>", StringComparison.Ordinal)).Success);
        byte[] oversized = new byte[RetailNpcLevelRatioTableInputs.MaximumSourceBytes + 1];
        Assert.Equal("table_size", Assert.Single(RetailNpcLevelRatioTableInputs.Admit(oversized,
            TablePolicy() with { ExpectedSourceSha256 = Hash(oversized) }).Issues).Code);
    }

    [Fact]
    public void RawIgnoredAttacksAndNewSeedsCannotBypassTheFrozenEffectiveGate()
    {
        var fixture = new Fixture(); fixture.Set("physical_attack", 200000); fixture.Set("magical_attack", 300000);
        fixture.Set("min_damage", 226); fixture.Set("max_damage", 339);
        var inputs = fixture.Inputs(); var seeds = Success(inputs);
        Assert.Equal((ushort)226, seeds.MainHandMinimumBeforeEffects.Value);
        Assert.Equal((ushort)339, seeds.MainHandMaximumBeforeEffects.Value);
        Assert.Equal("200000", Assert.Single(seeds.Source.RawPhysicalAttack.RawValues));
        Assert.False(seeds.EffectiveInputsVerified); Assert.False(seeds.FixtureVerified); Assert.False(seeds.ActivationReady);
        Assert.False(RetailNpcInitializationRules.Assess(inputs).CanConstructEffectiveInputs);
        Assert.Throws<InvalidOperationException>(() => RetailNpcInitializationRules.RequireEffectiveInputs(inputs));
    }

    [SkippableFact]
    public void All25CompleteFrozenSourceRecordsMatchIndependentConditionalSeedVectors()
    {
        using var profiles = JsonDocument.Parse(PilotProfilesJson);
        using var provenance = JsonDocument.Parse(PilotProvenanceJson);
        using var manifest = JsonDocument.Parse(PilotSourceManifestJson);
        using var vectors = JsonDocument.Parse(PilotSeedVectorsJson);
        var cohort = profiles.RootElement.EnumerateArray().Select(x => new RetailNpcSourceProfile(x)).ToDictionary(x => x.NpcId);
        Assert.Equal(25, cohort.Count); Assert.Equal(25, vectors.RootElement.GetArrayLength());
        // Private source fixtures stay external and hash-pinned. Full dataset
        // hash/coverage admission is independently tested by RetailNpcDatasetReaderTests.
        var dataset = new RetailNpcStaticDataset(PilotDatasetId, PilotManifestHash, cohort, 63287, 117, 0,
            ImmutableArray<RetailNpcArtifactReceipt>.Empty, provenance.RootElement, manifest.RootElement);
        var policy = new RetailNpcTemplateProjectionPolicy(PilotDatasetId, PilotManifestHash, "USA");
        var tableResult = RetailNpcLevelRatioTableInputs.Admit(Convert.FromBase64String(RealTableBase64),
            TablePolicy() with { ExpectedDatasetId = PilotDatasetId, ExpectedOutputManifestSha256 = PilotManifestHash,
                ExpectedSourceSha256 = "b7947b4725c0d273ab62722adfe576b508e6e4179ffb18e2e9b5c1767a4b779f" });
        Assert.True(tableResult.Success); var table = tableResult.Table!;
        foreach (var vector in vectors.RootElement.EnumerateArray())
        {
            int id = vector.GetProperty("id").GetInt32();
            var projected = RetailNpcTemplateProjection.Project(dataset, id, policy);
            Assert.True(projected.Success, string.Join("; ", projected.Issues)); var inputs = projected.Inputs!;
            Assert.Equal(vector.GetProperty("name").GetString(), inputs.Devname);
            Assert.Equal(vector.GetProperty("raw_sha256").GetString(), inputs.Receipt.RawRecordSha256);
            Assert.Equal(vector.GetProperty("file_sha256").GetString(), inputs.Receipt.SourceFileSha256);
            Assert.Equal(vector.GetProperty("byte_start").GetInt64(), inputs.Receipt.ByteStart);
            Assert.Equal(vector.GetProperty("byte_end").GetInt64(), inputs.Receipt.ByteEndExclusive);
            Assert.Equal(vector.GetProperty("raw_tree_sha256").GetString(), Hash(Encoding.UTF8.GetBytes(inputs.RawRecord.GetRawText())));
            var result = RetailNpcOrdinaryCoreSeedProjection.ProjectOrdinaryCoreSeeds(inputs, table, Domain(inputs, table));
            Assert.True(result.Success, string.Join("; ", result.Issues)); var seeds = result.Seeds!;
            Assert.Equal((uint?)0u, seeds.Domain.MasterObjectId); Assert.Equal((uint?)0u, seeds.Domain.SourceRelatedObjectId);
            Assert.Equal(false, seeds.Domain.VirtualProfileRecordPresent); // Explicit conditional fixture, not observed ownerlessness.
            Assert.Equal(vector.GetProperty("hp").GetInt32(), seeds.Template.MaxHp.Value);
            Assert.Equal(vector.GetProperty("hp").GetInt32(), seeds.InitialHpOnSet.Value);
            Assert.Equal(vector.GetProperty("regen").GetInt32(), seeds.HpRegenerationBeforeEffects.Value);
            Assert.Equal(vector.GetProperty("min").GetUInt16(), seeds.MainHandMinimumBeforeEffects.Value);
            Assert.Equal(vector.GetProperty("max").GetUInt16(), seeds.MainHandMaximumBeforeEffects.Value);
            Assert.Equal(1000, seeds.StatRatioBeforeEffects.Value);
            Assert.Equal(RetailNpcSourcePresence.Absent, seeds.Template.StatRatio.SourcePresence);
            Assert.Equal(RetailNpcCoreSeedOrigin.LevelTableDefault, seeds.Template.StatRatio.Origin);
            Assert.Equal(RetailNpcSourcePresence.Absent, seeds.Template.MaxMp.SourcePresence);
            Assert.Equal(RetailNpcSourcePresence.Absent, seeds.Template.MpRegeneration.SourcePresence);
            Assert.Equal(0, seeds.MpDiagnostics.CurrentMpOnSet.Value);
            Assert.Equal(0, seeds.MpDiagnostics.MaxMpAtCommonSeedPoint.Value);
            Assert.Equal(0, seeds.MpDiagnostics.MpRegenAtFreshCreatureInitialization.Value);
            foreach (var element in vector.GetProperty("elements").EnumerateObject())
                Assert.Equal(element.Value.GetInt16(), seeds.FirstLevelBaseElements[element.Name].Value);
            Assert.All(seeds.Template.Primaries.Values, x => { Assert.Equal((ushort)0, x.Value); Assert.Equal(RetailNpcSourcePresence.Absent, x.SourcePresence); });
            Assert.Same(inputs.Receipt, seeds.SourceReceipt); Assert.Equal(table.Receipt, seeds.RatioTableReceipt);
            Assert.False(seeds.EffectiveInputsVerified); Assert.False(seeds.ActivationReady);
        }
    }

    private static RetailNpcOrdinarySeedDomain Domain(RetailNpcTemplateInputs source, RetailNpcLevelRatioTableInputs table) =>
        new(source.Receipt, table.Receipt.SourceSha256, FreshUniqueTemplate: true, FreshActor: true, NativeKind: 1,
            MasterObjectId: 0u, SourceRelatedObjectId: 0u,
            VirtualProfileRecordPresent: false, DifficultyRecordPresent: false, DualWeaponFlagPresent: false);

    private static RetailNpcOrdinaryCoreSeeds Success(RetailNpcTemplateInputs source)
    {
        var table = Table();
        var result = RetailNpcOrdinaryCoreSeedProjection.ProjectOrdinaryCoreSeeds(source, table, Domain(source, table));
        Assert.True(result.Success, string.Join("; ", result.Issues)); return result.Seeds!;
    }

    private static void Failure(RetailNpcTemplateInputs source, string code)
    {
        var table = Table();
        var result = RetailNpcOrdinaryCoreSeedProjection.ProjectOrdinaryCoreSeeds(source, table, Domain(source, table));
        Assert.False(result.Success); Assert.Null(result.Seeds); Assert.Equal(code, Assert.Single(result.Issues).Code);
    }

    private static RetailNpcLevelRatioTablePolicy TablePolicy() => new(new string('d', 64), new string('e', 64), "USA",
        SyntheticTableHash, @"Z:\must-not-be-opened\pc_stat_ratio.xml", "pc_stat_ratio.xml", "regional_missing_base_fallback");
    private static RetailNpcLevelRatioTableInputs Table(RetailNpcLevelRatioTablePolicy? policy = null)
    {
        var result = RetailNpcLevelRatioTableInputs.Admit(SyntheticNpcRatioTable.Bytes(), policy ?? TablePolicy());
        Assert.True(result.Success, string.Join("; ", result.Issues)); return result.Table!;
    }
    private static string Hash(ReadOnlySpan<byte> bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    private static RetailNpcLevelRatioTableAdmissionResult AdmitXml(string xml)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(xml);
        return RetailNpcLevelRatioTableInputs.Admit(bytes, TablePolicy() with { ExpectedSourceSha256 = Hash(bytes) });
    }
    private static XElement TableDocument() => new("pc_stat_ratios", Enumerable.Range(1, 100)
        .Select(i => new XElement("level", new XAttribute("level", i), new XElement("stat_ratio", i == 66 ? 1015 : i == 100 ? 1525 : 1000))));
    private static JsonArray Node(string tag, string? value, params JsonNode[] children)
    {
        var text = new JsonArray(); if (value is not null) text.Add(value);
        foreach (var child in children) text.Add(child);
        return new(JsonValue.Create(tag), new JsonArray(), text);
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

    private static string SyntheticTableHash => SyntheticNpcRatioTable.Sha256;
    private const string PilotDatasetId = "33dfd001439620a102ccfca9b30765578efa88acb6f561f8c116ce8d98873335";
    private const string PilotManifestHash = "036c316db96dcb1d50ccd2b55ef7b472afff9be557ea10c385cc22369d49b0cd";
    // Complete source records, receipts and instruction-derived vectors are external private fixtures.
    // External fixture accessors
    private static string RealTableBase64 => RetailNpcPrivateStatFixtures.ReadText("RealTableBase64");

    private static string PilotProfilesJson => RetailNpcPrivateStatFixtures.ReadText("PilotProfilesJson");

    private static string PilotProvenanceJson => RetailNpcPrivateStatFixtures.ReadText("PilotProvenanceJson");

    private static string PilotSourceManifestJson => RetailNpcPrivateStatFixtures.ReadText("PilotSourceManifestJson");

    private static string PilotSeedVectorsJson => RetailNpcPrivateStatFixtures.ReadText("PilotSeedVectorsJson");
}
