using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using N = Aion.GameServer.Controllers.Attack.RetailNpc.RetailNpcImmediateDamageOwner;

namespace Aion.GameServer.Controllers.Attack.RetailNpc;

/// <summary>Inactive source/current contracts for NpcNativeGearRefreshWithJava48OwnedLifeV1.
/// These describe an original successful User gear seam; they do not create one or consume a lease.
/// No desired fresh maximum, callback HP answer, serializer result or database-success field exists.</summary>
internal static class RetailNpcUserGearStatLifeInputs
{
    internal const string Policy = "NpcNativeGearRefreshWithJava48OwnedLifeV1";
    internal const string Proof = "d2360db9915737617c7f00e313bea472a4b2f4e45f934343dfb85542b38b2a1a";
    internal const string AbsAmendment = "3a0fcbfd4e17497a98805714cbbac8d8c17a0099606582a254045c052bd5a782";
    internal const string TaskAmendment = "cd5b68287c2f2560c5b135e51699414a5ef958e67f9ef65be6b3dd326e77ca3e";
    internal const string ClassSource = "a4d50d031e75596460c045ec56c24e1fe6fad5beea5b1863b040b769a885891c";
    internal const string AbsSource = "6b1ccdcfd4180135721275351c9eea844b2fca70c0049ff23b365275bef67cba";
    internal enum Known { Unknown, Null, Present }
    internal enum Persistent { Unknown, New, Updated, UpdateRequired, Deleted, NoAction }
    internal enum FunctionKind { Add, Sub, Rate, Set, Abs, Intrinsic }
    internal enum ConditionKind { Charge, OnFly }
    internal enum AbsRecipe { Unknown, DefaultFalseUnchanged, ArgumentConstructorUnchanged, KnownCurrentPrivateField }
    internal sealed record Binding(string Owner, string Generation, string Operation, string Epoch, N.Evidence Evidence)
    {
        internal void Validate(string operation, string epoch)
        {
            Require(!string.IsNullOrWhiteSpace(Owner) && !string.IsNullOrWhiteSpace(Generation)
                && Operation == operation && Epoch == epoch, "current owner/generation/operation/epoch");
            Require(Evidence is { Kind: N.FactKind.SourceBoundDeclaration or N.FactKind.ObservedOriginal }
                && !string.IsNullOrWhiteSpace(Evidence.Receipt), "current receipt");
        }
    }
    internal sealed record AbsField(AbsRecipe Recipe, bool? Debuff, string SourceSha256,
        string Field, string Owner, string Operation, string Epoch, bool CompleteRawAbsence,
        bool ConstructionUnchanged, bool CurrentFieldKnown, bool? ConstructorDebuffArgument = null);
    internal sealed record Condition(ConditionKind Kind, int Value);
    internal sealed record Function(FunctionKind Kind, string Stat, int Value, bool Bonus,
        ImmutableArray<Condition> Conditions, string Owner, long Ordinal, Binding Current, AbsField? Abs = null,
        string SourceProgramSha256 = "")
    {
        internal int Priority(string operation, string epoch)
        {
            Current.Validate(operation, epoch);
            Require(SourceProgramSha256 == FunctionProgram(Kind), "exact function source class");
            Require(Current.Owner == Owner && Ordinal >= 0 && !Conditions.IsDefault, "function owner/order/conditions");
            if (Kind != FunctionKind.Abs)
                return Kind switch { FunctionKind.Add or FunctionKind.Sub or FunctionKind.Intrinsic => Bonus ? 60 : 30,
                    FunctionKind.Rate => Bonus ? 50 : 20, FunctionKind.Set => Bonus ? 70 : 40,
                    _ => throw new Domain("unknown function subclass") };
            Require(Abs is not null && Abs.Debuff.HasValue && Abs.SourceSha256 == AbsSource
                && Abs.Field == "StatAbsFunction.debuff" && Abs.Owner == Owner
                && Abs.Operation == operation && Abs.Epoch == epoch, "ABS independent current debuff binding");
            bool admitted = Abs.Recipe switch
            {
                AbsRecipe.DefaultFalseUnchanged => Abs.Debuff == false && Abs.CompleteRawAbsence && Abs.ConstructionUnchanged,
                AbsRecipe.ArgumentConstructorUnchanged => !Bonus && Abs.ConstructionUnchanged
                    && Abs.ConstructorDebuffArgument.HasValue && Abs.ConstructorDebuffArgument == Abs.Debuff,
                AbsRecipe.KnownCurrentPrivateField => Abs.CurrentFieldKnown,
                _ => false
            };
            Require(admitted, "ABS source/current recipe");
            return (Abs.Debuff.Value ? 90 : 80) + (Bonus ? 20 : 0);
        }
    }
    internal static string FunctionProgram(FunctionKind kind) => kind switch
    {
        FunctionKind.Add => "872497b4e43242f102e453ffc702b94f045d9c47781cd74760b7a469a1abc36f",
        FunctionKind.Sub => "c15f90e13df60e3cfdde0ff4b735c5f16ef0744fe595d258c47e8aaa92a11c0f",
        FunctionKind.Rate => "9c953119b60cd7ce5d3a5c472ca955a6e4c45e48d602fdc955a31afe9cc4719d",
        FunctionKind.Set => "ca613676fd96b7668fffcfdeac7b2475e2b1d5da2873611eb56a1f9f7f64c06f",
        FunctionKind.Abs => AbsSource,
        FunctionKind.Intrinsic => "900d889928c5c8410a4de6fbe6cd39e121e0f283ac45a1f81711f00082fc2a76",
        _ => throw new Domain("unknown function source program")
    };

    /// <summary>Exact reviewed source fragment. Full-file identity and byte span are independently
    /// pinned; hashing an arbitrary fragment or supplying an alias dictionary cannot admit it.</summary>
    internal sealed class SourceRecord
    {
        internal string Key { get; }
        internal string SourceSha256 { get; }
        internal long Start { get; }
        internal long End { get; }
        internal string RawSha256 { get; }
        internal ImmutableArray<byte> Raw { get; }
        private SourceRecord(string key, string source, long start, long end, string sha, ReadOnlySpan<byte> raw)
        { Key = key; SourceSha256 = source; Start = start; End = end; RawSha256 = sha; Raw = [..raw]; }
        internal XElement Read() => Parse(Raw.AsSpan());
        internal static SourceRecord Admit(string key, string sourceSha256, long start, long end, ReadOnlySpan<byte> raw)
        {
            Require(raw.Length is > 0 and <= 65536 && end - start == raw.Length, "source fragment bounds");
            string hash = Convert.ToHexStringLower(SHA256.HashData(raw));
            Require(SourcePins.TryGetValue(key, out var pin) && pin.Source == sourceSha256
                && pin.Start == start && pin.End == end && pin.Raw == hash, "source hash/span/identity");
            XElement root = Parse(raw);
            string actual = root.Name.LocalName switch
            {
                "item_template" => "item_template-" + root.Attribute("id")?.Value + ".xml",
                "skill_template" => "skill_template-" + root.Attribute("skill_id")?.Value + ".xml",
                "random_bonus" => "random_bonus-" + root.Attribute("type")?.Value + "-" + root.Attribute("id")?.Value + ".xml",
                _ => ""
            };
            Require(actual == key, "source record identity");
            return new(key, sourceSha256, start, end, hash, raw);
        }
        private static XElement Parse(ReadOnlySpan<byte> raw)
        {
            try
            {
                string text = new UTF8Encoding(false, true).GetString(raw);
                using var reader = XmlReader.Create(new StringReader(text), new XmlReaderSettings
                { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 65536 });
                return XElement.Load(reader, LoadOptions.PreserveWhitespace);
            }
            catch (Exception ex) when (ex is XmlException or DecoderFallbackException)
            { throw new Domain("malformed source XML/UTF8"); }
        }
    }
    private sealed record SourcePin(string Source, long Start, long End, string Raw);
    // Generated from the reviewed selected-input-spans.json; no live file reads or fallback names.
    private static readonly ImmutableDictionary<string, SourcePin> SourcePins = CreateSourcePins();
    private static ImmutableDictionary<string, SourcePin> CreateSourcePins()
    {
        var pins = ImmutableDictionary.CreateBuilder<string, SourcePin>(StringComparer.Ordinal);
        pins.Add("item_template-100001286.xml", new("405cb8acd9e3ac3633cc6d7947d66a5bcfcb7c718ac88f7169a6ed76a2cff3ce", 824627, 826990, "b11b94ef2dbfb09f699242e8215703768ce690089286dc054bd9b39d64370d18"));
        pins.Add("item_template-110601149.xml", new("405cb8acd9e3ac3633cc6d7947d66a5bcfcb7c718ac88f7169a6ed76a2cff3ce", 18136090, 18138293, "341e6648a92c57b6250aa4e7a947c236ba1f7d4b1f0b1ec3a8fec5bae34c53f2"));
        pins.Add("item_template-166050005.xml", new("405cb8acd9e3ac3633cc6d7947d66a5bcfcb7c718ac88f7169a6ed76a2cff3ce", 51192130, 51192406, "c38731c234bbbbe3b4cc627cc5f3732ba29727a7cace616b79ac40b9e746caec"));
        pins.Add("item_template-187100015.xml", new("405cb8acd9e3ac3633cc6d7947d66a5bcfcb7c718ac88f7169a6ed76a2cff3ce", 55907221, 55907636, "f7d7807867dd602b86c4e268a6c589f66d9512f11395076faac588589b1593f6"));
        pins.Add("skill_template-37.xml", new("11de07111bb1b84daadeb5292153a026ed2da7b4a3418fe475c3129cea2cc9d7", 44384, 45004, "f32697e892ad7a98a2382575bf662098fd6162cdce8d61943a1d17d8dc8df98d"));
        pins.Add("skill_template-54.xml", new("11de07111bb1b84daadeb5292153a026ed2da7b4a3418fe475c3129cea2cc9d7", 55112, 55755, "c29b1f9d0e7cd878116b2212958e9659616ac4074a0aab4b438a0326ffe57c9d"));
        pins.Add("skill_template-273.xml", new("11de07111bb1b84daadeb5292153a026ed2da7b4a3418fe475c3129cea2cc9d7", 198271, 199038, "8c8170667a6bf26a8d2b974d25338650900d8420ddadbb06f9a874f73705c60b"));
        pins.Add("skill_template-347.xml", new("11de07111bb1b84daadeb5292153a026ed2da7b4a3418fe475c3129cea2cc9d7", 260417, 261486, "0d1721f250c75d655bf7a2a914662d2812fcedb4f8936f605ee6ce835032c57b"));
        pins.Add("skill_template-9838.xml", new("11de07111bb1b84daadeb5292153a026ed2da7b4a3418fe475c3129cea2cc9d7", 5595631, 5596636, "60ae49414e5e76ef391db78270d663ab058707fddf70ff57a6f4d3cf5421da72"));
        pins.Add("skill_template-9840.xml", new("11de07111bb1b84daadeb5292153a026ed2da7b4a3418fe475c3129cea2cc9d7", 5597329, 5598207, "848f67183c1afb60eb6533177bd4a668242bac29b12456f9699ad04419c31ee7"));
        pins.Add("random_bonus-INVENTORY-8.xml", new("4f0182acd030741fbc438a89903672443dfd31d37f881c4915bf0fc223f23cc3", 3971, 4535, "109c6d7d5d3201fcf25c0795c6ff2fd31f874891f139995ebeb964d9fdeea566"));
        pins.Add("random_bonus-POLISH-8.xml", new("4f0182acd030741fbc438a89903672443dfd31d37f881c4915bf0fc223f23cc3", 936796, 937627, "a74e1a4941e95cc4b6de107ed630229533fb1ac3481c4c66dd574338fb9750b0"));
        return pins.ToImmutable();
    }
    internal sealed record Registration(Binding Current, SourceRecord Source, string ChildPath,
        int MaterializedSkillLevel, int TemperingLevel, AbsField? Abs = null, string? ItemOwner = null,
        bool? SourceRecipeUnchanged = null);
    internal sealed record Slot(Known Presence, string? ItemOwner, string? Generation);
    internal sealed record Item(Binding Current, SourceRecord Source, int EquipmentIndex, ulong NativeVtable,
        string Mapped48Definition, string ProgramReceipt, int Charge, uint ChargeCounter, int Polish, uint PolishCounter,
        Persistent Persistence, string? IdianOwner, Persistent IdianPersistence, Known RemovedListener,
        string? ListenerProgram, ImmutableArray<string> StorageCells,
        ImmutableArray<byte> ClassPermits, byte? PermitReduction, ushort? PermitFloor,
        SourceRecord? IdianActionSource = null, int IdianVariant = 0,
        byte? ChargeFeature158 = null, uint? TemplateFlags40 = null, int TemperingLevel = 0,
        string? IdianGeneration = null, Binding? IdianListener = null, Binding? ChargeObserver = null,
        uint ObjectId70 = 0, ulong Serial110 = 0);
    internal sealed record Equipment(ImmutableArray<Slot> ChargeSlots, ImmutableArray<Slot> PolishSlots,
        ImmutableArray<int?> ChargeLocks, int? PolishLock, ImmutableArray<Item> Items,
        Persistent Persistence, Binding Current, bool CompleteActiveAlternateAndPlume,
        bool CompleteCurrentRestrictions, bool SwordMastery, bool PlateMastery, string Race, uint UserObjectId);
    internal sealed record Graph(ImmutableArray<Registration> Registrations, Binding Current,
        bool CompleteAllOwnersAndStatLists, bool RegistrationOrderKnown,
        ImmutableArray<Function> CurrentConstructorFunctions, uint UserObjectId = 0);
    internal sealed record Life(int Hp, int Mp, int CachedHp, int CachedMp, int KillingBlow);
    internal sealed record Callbacks(Known MatchingHpObservers, ImmutableArray<string> HpObservers,
        Known HateTask, ImmutableArray<uint> AggroEntries, bool Spawned, bool InTeam,
        bool RestoreTaskPresent, Binding Current, uint UserObjectId);
    internal sealed record Motion(bool Flying, bool Riding, bool Walking, bool Sprinting,
        int CachedSpeed, int CachedDelay, Binding Current, uint UserObjectId,
        bool? StateFlying, bool? Resting);
    internal sealed record Floating(bool? RoundNearestEven, bool? MaskedExceptions, bool? FlushToZero,
        bool? DenormalsAreZero, bool? NormalReturn);
    internal sealed record Infrastructure(bool SerializedEpoch, bool NoOtherWriter, bool NoInlineActorWriter,
        bool NoReentrancy, bool DeferredSerialization, bool FutureTasksNotInline, bool NormalReturn,
        bool StableLifetime, bool DaoCaughtFailureIsNormalReturn);
    internal sealed record Lease(string Policy, string Identity, Binding Current, bool ExclusiveOnce,
        bool LegacyChargeAndIdianAttackedSuppressed, bool DirectExactOwnerRemoval,
        bool NoLegacyNestedRefresh, uint UserObjectId, uint NpcObjectId, ImmutableArray<string> SuppressedObserverIdentities);
    internal sealed record Actor(uint ObjectId, byte Type, ulong Vtable, uint? Master188c,
        uint? Related1894, Known Virtual1850, bool? Difficulty, uint? Kind1940,
        uint? Flags434, uint? Current914, Binding Current, bool? IsStaff = null);
    internal sealed record EntrySeam(ulong UserEntry, ulong CoreEntry, ulong ChildEntry,
        string ClosureSha256, uint UserObjectId, uint NpcObjectId, string ActorGeneration,
        Binding OriginalLife, string OriginalApplicationCell, ImmutableArray<string> ActorStorage,
        N.Evidence ReachedOriginalFrame);
    internal sealed record Input(RetailNpcNormalScope Scope, string MainSha256, Actor Attacker,
        Actor Receiver, string Operation, string Epoch, string Selected48Recipe, string ClassSourceSha256,
        int ClassId, int Level, Equipment Equipment, Graph Graph, Life Life, Callbacks Callbacks,
        Motion Motion, Floating Floating, Infrastructure Infrastructure, Lease Lease, EntrySeam Entry);
    /// <summary>Separate pure general-source read frame. It does not widen Input eligibility,
    /// admit an actor or carry desired post-refresh HP/maxima. Native class/permit/family/lease
    /// activation checks remain outside this inactive stat projection.</summary>
    internal sealed record GeneralCharge(Binding Current, int Points);
    internal sealed record GeneralStatFrame(string Operation, string Epoch, Binding Current,
        int ClassId, int Level, string ClassSourceSha256, bool? Flying, bool? StateFlying,
        bool? Resting, bool? Walking, bool? Riding, bool? Sprinting, bool? ShieldEquipped,
        ImmutableArray<GeneralCharge> Charge, Floating Floating, bool? IsStaff = null);
    internal sealed class Domain(string message, bool incomplete = false) : Exception(message)
    { internal bool Incomplete { get; } = incomplete; }
    internal static void Require([DoesNotReturnIf(false)] bool condition, string reason)
    { if (!condition) throw new Domain(reason); }
    internal static int Integer(XElement node, string name, int? absent = null)
    {
        string? text = node.Attribute(name)?.Value;
        if (text is null && absent.HasValue) return absent.Value;
        Require(text is not null && int.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out _), "integer source field " + name);
        return int.Parse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);
    }
    internal static bool Boolean(XElement node, string name, bool absent = false)
    {
        string? text = node.Attribute(name)?.Value;
        Require(text is null or "true" or "false", "boolean source field " + name);
        return text is null ? absent : text == "true";
    }
}
