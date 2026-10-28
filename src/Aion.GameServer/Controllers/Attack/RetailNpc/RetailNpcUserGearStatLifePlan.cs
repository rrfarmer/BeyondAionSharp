using System.Collections.Immutable;
using GearItem = Aion.GameServer.Controllers.Attack.RetailNpc.RetailNpcUserGearStatLifeInputs.Item;
using System.Xml.Linq;
using N = Aion.GameServer.Controllers.Attack.RetailNpc.RetailNpcImmediateDamageOwner;
using static Aion.GameServer.Controllers.Attack.RetailNpc.RetailNpcUserGearStatLifeInputs;
using G = Aion.GameServer.Controllers.Attack.RetailNpc.RetailNpcUserOwnedStatGraph;

namespace Aion.GameServer.Controllers.Attack.RetailNpc;

/// <summary>Complete inactive finite gear→owned graph→cached life child under the logged
/// NPC-only policy. Requests carry source-bound normal-return contracts, never success answers.
/// No caller callbacks execute. An unknown late child discards the entire local candidate.</summary>
internal static class RetailNpcUserGearStatLifePlan
{
    internal const string NativeProgramProof = Proof;
    internal enum Completion { RejectedBeforeEvaluation, IncompleteReachedProgram, CompleteFiniteGearStatLifeChild }
    internal enum RequestKind { InventoryUpdate, ChargeDescription, PolishDescription, AttackStatusHp,
        AttackStatusMp, SelfHp, SelfMp, TeamStatEnqueue, RestoreTask, StatsInfo, ChangeSpeed,
        StoreIdianStones }
    internal sealed record Event(int Ordinal, string Stage, string Kind, string? Owner = null,
        long? Before = null, long? After = null, int? Slot = null);
    internal sealed record Request(int Ordinal, RequestKind Kind, string Owner, int? Current = null,
        int? Maximum = null, int? DelayMs = null, int? PeriodMs = null, int? Category = null,
        bool? DeleteRow = null, bool RetainsOwnerReferences = true,
        uint? ItemObjectId = null, ulong? ItemSerial = null, int? VisualBucket = null)
    {
        internal bool SuccessObserved => false;
    }
    internal sealed record Mutation(string Owner, int Slot, bool Charge, int Before, int Burn,
        int Actual, int After, uint CounterBefore, uint CounterAfter, bool ChangedStatOwner);
    internal sealed record RemovedOption(string Owner, string Generation, Binding Listener,
        Persistent InitialState, Persistent DeleteRuleState, Persistent StateAfterStoreNormalReturn);
    internal sealed record Refresh(string Phase, Life Before, Life After, G.Derived Graph,
        ImmutableArray<Event> Events);
    internal sealed record Candidate(Equipment Equipment, Life Life, ImmutableArray<Function> Registry,
        ImmutableArray<G.RetainedProgram> RetainedRegistry,
        Lease ConsumptionLease,
        Callbacks Callbacks, Motion Motion, ImmutableArray<Mutation> Mutations,
        ImmutableArray<RemovedOption> RemovedOptions, ImmutableArray<Refresh> Refreshes,
        ImmutableArray<Event> Events, ImmutableArray<Request> Requests);
    internal sealed record Result(Completion Status, Candidate? Calculated, string? Failure)
    {
        internal bool ActivationReady => false;
        internal int? ActualRuntimeHp => null;
        internal bool LeaseConsumed => false;
    }
    private static readonly ImmutableArray<ulong> ItemPrograms =
        [0x141256bc0, 0x141172990, 0x1412169b8, 0x14120d6c0, 0x1411b8548];
    internal static Result Evaluate(Input input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var run = new Evaluation(input);
        try { return new(Completion.CompleteFiniteGearStatLifeChild, run.Run(), null); }
        catch (Domain ex) { return new(run.Reached || ex.Incomplete ? Completion.IncompleteReachedProgram
            : Completion.RejectedBeforeEvaluation, null, ex.Message); }
    }
    private sealed class Evaluation(Input input)
    {
        internal bool Reached { get; private set; }
        private Equipment gear = input.Equipment;
        private Life life = input.Life;
        private Callbacks callbacks = input.Callbacks;
        private Motion motion = input.Motion;
        private ImmutableArray<Function> registry;
        private ImmutableArray<G.RetainedProgram> retained;
        private readonly List<Event> events = [];
        private readonly List<Request> requests = [];
        private readonly List<Mutation> mutations = [];
        private readonly List<RemovedOption> removedOptions = [];
        private readonly List<Refresh> refreshes = [];
        private int order;
        internal Candidate Run()
        {
            Validate(); registry = G.Project(input.Graph, input.Operation, input.Epoch);
            retained = G.RetainedPrograms(input.Graph, input.Operation, input.Epoch);
            ValidateOwnedSources(); Reached = true;
            bool changed = false;
            for (int slot = 0; slot < 17; slot++)
            {
                Emit("charge", "lock", slot: slot, before: 0, after: 1);
                Slot entry = gear.ChargeSlots[slot];
                if (entry.Presence == Known.Present)
                {
                    GearItem item = Resolve(entry);
                    Require(item.ChargeFeature158.HasValue, "reached current charge feature158");
                    Emit("charge", "feature158-read", item.Current.Owner, after: item.ChargeFeature158.Value, slot: slot);
                    if (item.ChargeFeature158.Value != 0) changed |= BurnCharge(item, slot);
                }
                Emit("charge", "unlock", slot: slot, before: 1, after: 0);
            }
            if (changed) RefreshLife("after-all17-charge-unlocks");
            Emit("polish", "lock", slot: 0, before: 0, after: 1);
            bool depleted = false;
            if (gear.PolishSlots[0].Presence == Known.Present)
            {
                GearItem item = Resolve(gear.PolishSlots[0]);
                Require(item.TemplateFlags40.HasValue, "reached current polish template flags40");
                uint feature = (item.TemplateFlags40.Value >> 18) & 1;
                Emit("polish", "feature-bit18-read", item.Current.Owner, after: feature, slot: 0);
                if (feature != 0) depleted = BurnPolish(item);
            }
            Emit("polish", "unlock", slot: 0, before: 1, after: 0);
            if (depleted) RefreshLife("after-polish0-unlock");
            return new(gear, life, registry, retained, input.Lease, callbacks, motion,
                [..mutations], [..removedOptions], [..refreshes], [..events], [..requests]);
        }
        private void Validate()
        {
            Require(input.Scope is not null && input.Scope.Attacker == RetailNpcNormalActorKind.OrdinaryOwnerlessNpc
                && input.Scope.Defender == RetailNpcNormalActorKind.Player, "distinct NPC→User scope");
            input.Scope.RequireNpcAttacker();
            Require(input.MainSha256 == RetailNpcNormalScope.MainBinarySha256 && input.Selected48Recipe == "country99-selected-Java48"
                && input.ClassSourceSha256 == ClassSource && !string.IsNullOrWhiteSpace(input.Operation)
                && !string.IsNullOrWhiteSpace(input.Epoch), "pinned binary/selected4.8/class recipe");
            Actor a = input.Attacker, u = input.Receiver;
            a.Current.Validate(input.Operation, input.Epoch); u.Current.Validate(input.Operation, input.Epoch);
            Require(a.Type == 2 && a.Vtable == 0x1412b8470 && u.Type == 1 && u.Vtable == 0x141161250
                && a.ObjectId != 0 && u.ObjectId != 0 && a.ObjectId != u.ObjectId && u.IsStaff == false,
                "actual NPC/nonstaff User class identity");
            Require(a.Master188c == 0 && a.Related1894 == 0 && a.Virtual1850 == Known.Null
                && a.Difficulty == false && a.Kind1940 == 1 && a.Flags434.HasValue
                && (a.Flags434.Value & 0x200000) == 0 && a.Current914 == 0, "independent ordinary source domain");
            EntrySeam seam = input.Entry; seam.OriginalLife.Validate(input.Operation, input.Epoch);
            Require(seam.UserEntry == 0x140541d20 && seam.CoreEntry == 0x140372920 && seam.ChildEntry == 0x1405e29d0
                && seam.ClosureSha256 == "de34659016525047f61d3fe0a7c0f335cf4e988eb894b1ebda334e3277015be9"
                && seam.UserObjectId == u.ObjectId && seam.NpcObjectId == a.ObjectId
                && seam.ActorGeneration == u.Current.Generation && seam.OriginalLife.Owner == u.Current.Owner
                && seam.ReachedOriginalFrame is { Kind: N.FactKind.SourceBoundDeclaration or N.FactKind.ObservedOriginal }
                && !string.IsNullOrWhiteSpace(seam.ReachedOriginalFrame.Receipt)
                && !string.IsNullOrWhiteSpace(seam.OriginalApplicationCell) && !seam.ActorStorage.IsDefault
                && seam.ActorStorage.Length >= 2, "original successful User core→gear/life owner seam");
            Require(input.ClassId is >= 0 and < 17 && input.Level is >= 1 and <= 65, "class/finite level");
            Floating f = input.Floating;
            Require(f.RoundNearestEven == true && f.MaskedExceptions == true && f.FlushToZero == false
                && f.DenormalsAreZero == false && f.NormalReturn == true, "explicit binary32 environment");
            Infrastructure infrastructure = input.Infrastructure;
            Require(infrastructure.SerializedEpoch && infrastructure.NoOtherWriter && infrastructure.NoInlineActorWriter
                && infrastructure.NoReentrancy && infrastructure.DeferredSerialization && infrastructure.FutureTasksNotInline
                && infrastructure.NormalReturn && infrastructure.StableLifetime && infrastructure.DaoCaughtFailureIsNormalReturn,
                "request/lifetime/serialized-owner contract");
            Lease lease = input.Lease; lease.Current.Validate(input.Operation, input.Epoch);
            Require(lease.Policy == Policy && !string.IsNullOrWhiteSpace(lease.Identity) && lease.ExclusiveOnce
                && lease.Current.Owner == lease.Identity
                && lease.LegacyChargeAndIdianAttackedSuppressed && lease.DirectExactOwnerRemoval
                && lease.NoLegacyNestedRefresh, "exclusive NPC consumption/direct removal policy");
            Require(lease.UserObjectId == u.ObjectId && lease.NpcObjectId == a.ObjectId
                && !lease.SuppressedObserverIdentities.IsDefault
                && lease.SuppressedObserverIdentities.All(x => !string.IsNullOrWhiteSpace(x))
                && lease.SuppressedObserverIdentities.Distinct(StringComparer.Ordinal).Count() == lease.SuppressedObserverIdentities.Length,
                "lease actual source/receiver/observer identities");
            gear.Current.Validate(input.Operation, input.Epoch);
            Require(gear.CompleteActiveAlternateAndPlume && gear.CompleteCurrentRestrictions
                && gear.UserObjectId == u.ObjectId && input.Graph.UserObjectId == u.ObjectId
                && !gear.Items.IsDefault && gear.Items.Length <= 20 && !gear.ChargeSlots.IsDefault
                && !gear.PolishSlots.IsDefault && gear.ChargeSlots.Length == 17 && gear.PolishSlots.Length == 17
                && !gear.ChargeLocks.IsDefault && gear.ChargeLocks.Length == 17
                && gear.ChargeLocks.All(x => x == 0) && gear.PolishLock == 0, "complete uncontended current registries/equipment");
            Require(gear.Persistence is Persistent.New or Persistent.Updated or Persistent.UpdateRequired, "equipment persistence");
            Require(gear.Items.Select(x => x.Current.Owner).Distinct(StringComparer.Ordinal).Count() == gear.Items.Length
                && gear.Items.Select(x => x.ObjectId70).Distinct().Count() == gear.Items.Length
                && gear.Items.Select(x => x.EquipmentIndex).Distinct().Count() == gear.Items.Length, "equipment owner/slot aliases");
            Binding[] observers = gear.Items.SelectMany(x => new[] { x.ChargeObserver, x.IdianListener })
                .Where(x => x is not null).Select(x => x!).ToArray();
            Require(observers.Select(x => x.Owner).Distinct(StringComparer.Ordinal).Count() == observers.Length
                && observers.All(x => !gear.Items.Any(item => item.Current.Owner == x.Owner))
                && lease.SuppressedObserverIdentities.ToHashSet(StringComparer.Ordinal)
                    .SetEquals(observers.Select(x => x.Owner)), "exact disjoint legacy observer lease set");
            foreach (Binding observer in observers) observer.Validate(input.Operation, input.Epoch);
            var storage = new HashSet<string>(StringComparer.Ordinal);
            Require(seam.ActorStorage.All(x => !string.IsNullOrWhiteSpace(x) && storage.Add(x))
                && storage.Add(seam.OriginalApplicationCell), "disjoint actor/application-cell ownership");
            foreach (GearItem item in gear.Items)
            {
                item.Current.Validate(input.Operation, input.Epoch);
                Require(item.ObjectId70 is > 0 and <= int.MaxValue && item.ObjectId70 != a.ObjectId
                    && item.ObjectId70 != u.ObjectId, "original disjoint item object ID");
                Require(!item.StorageCells.IsDefault && item.StorageCells.Length >= 2
                    && item.StorageCells.All(x => !string.IsNullOrWhiteSpace(x) && storage.Add(x)), "disjoint current item storage");
                Require(item.Charge is >= 0 and <= 1000000 && item.Polish is >= 0 and <= 1000000
                    && item.TemperingLevel is >= 0 and <= 255
                    && item.Persistence is Persistent.New or Persistent.Updated or Persistent.UpdateRequired,
                    "current points/persistence domain");
                XElement root = item.Source.Read();
                int expectedSlot = item.Source.Key switch { "item_template-100001286.xml" => 0,
                    "item_template-110601149.xml" => 3, "item_template-187100015.xml" => 19,
                    _ => throw new Domain("unreviewed current equipment source") };
                Require(item.EquipmentIndex == expectedSlot || expectedSlot == 0 && item.EquipmentIndex is 17 or 18,
                    "source group legal active/alternate/PLUME placement");
                Require(item.Mapped48Definition == "4.8:" + Integer(root, "id")
                    && item.ProgramReceipt == NativeProgramProof && ItemPrograms.Contains(item.NativeVtable), "current native/4.8 program mapping");
                Require(expectedSlot != 0 || item.NativeVtable == 0x141256bc0, "sword actual ordinary Weapon program");
                Require(expectedSlot != 3 || item.NativeVtable == 0x14120d6c0, "torso actual Armor program");
                if (root.Attribute("restrict") is { } restrict)
                {
                    string[] values = restrict.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    Require(values.Length == 17 && int.TryParse(values[input.ClassId], out int required)
                        && required >= 0 && input.Level >= required, "class/required-level source");
                }
                if (expectedSlot == 0) Require(gear.SwordMastery, "current SWORD mastery source37 binding");
                if (expectedSlot == 3) Require(gear.PlateMastery, "current PLATE mastery source54 binding");
                if (expectedSlot == 19) Require(gear.Race == "ELYOS", "source PLUME race");
                if (item.EquipmentIndex < 17)
                {
                    Require(item.ChargeFeature158.HasValue && item.TemplateFlags40.HasValue,
                        "current feature-registry producer fields");
                    if (item.ChargeFeature158.Value != 0)
                        Require(gear.ChargeSlots[item.EquipmentIndex].ItemOwner == item.Current.Owner
                            && root.Element("improve") is not null, "missing charge producer edge");
                    if (((item.TemplateFlags40.Value >> 18) & 1) != 0)
                        Require(gear.PolishSlots[item.EquipmentIndex].ItemOwner == item.Current.Owner
                            && root.Element("idian") is not null, "missing polish producer edge");
                    if (root.Element("improve") is not null)
                    {
                        Require(item.ChargeObserver is not null, "current ChargeInfo observer identity");
                        item.ChargeObserver.Validate(input.Operation, input.Epoch);
                        Require(lease.SuppressedObserverIdentities.Contains(item.ChargeObserver.Owner),
                            "lease does not cover exact legacy charge observer");
                    }
                }
            }
            for (int slot = 0; slot < 17; slot++)
            {
                ValidateSlot(gear.ChargeSlots[slot], slot, true);
                ValidateSlot(gear.PolishSlots[slot], slot, false);
            }
            callbacks.Current.Validate(input.Operation, input.Epoch); motion.Current.Validate(input.Operation, input.Epoch);
            Require(callbacks.UserObjectId == u.ObjectId && motion.UserObjectId == u.ObjectId,
                "callback/motion current receiver ownership");
            Require(!callbacks.HpObservers.IsDefault && !callbacks.AggroEntries.IsDefault, "complete current observer/aggro containers");
            Require(!motion.Riding && !motion.Walking && !motion.Sprinting, "unclosed ride/walk/sprint child");
            Require(motion.StateFlying.HasValue && motion.Resting.HasValue
                && (motion.Flying || motion.StateFlying == false), "unclosed gliding/pose movement branch");
            Require(life.Hp >= 0 && life.Mp >= 0 && life.CachedHp > 0 && life.CachedMp > 0 && life.KillingBlow >= 0,
                "initial primary/cached-life state");
        }
        private void ValidateSlot(Slot slot, int index, bool charge)
        {
            Require(slot.Presence is Known.Null or Known.Present, "unknown feature slot");
            if (slot.Presence == Known.Null) { Require(slot.ItemOwner is null && slot.Generation is null, "null feature identity"); return; }
            GearItem item = Resolve(slot);
            Require(item.EquipmentIndex == index, "alternate/PLUME/high-bit registry index");
            XElement root = item.Source.Read();
            Require(charge ? root.Element("improve") is not null : root.Element("idian") is not null, "source feature capability");
        }
        private void ValidateOwnedSources()
        {
            foreach (GearItem item in gear.Items.Where(x => x.EquipmentIndex < 17 || x.EquipmentIndex == 19))
            {
                Require(input.Graph.Registrations.Count(r => r.Current.Owner == item.Current.Owner
                    && r.Current.Generation == item.Current.Generation && r.Source.RawSha256 == item.Source.RawSha256
                    && r.ChildPath == "modifiers") == 1, "complete active/outside17 item owner graph");
                if (item.IdianOwner is not null)
                {
                    Require(item.Polish > 0 && item.IdianVariant == 2 && item.IdianActionSource?.Key == "item_template-166050005.xml"
                        && Integer(item.IdianActionSource.Read().Element("actions")!.Element("polish")!, "set_id") == 8,
                        "source action/set/variant current Idian binding");
                    Require(input.Graph.Registrations.Count(r => r.Current.Owner == item.IdianOwner
                        && r.Current.Generation == item.IdianGeneration
                        && r.Source.Key == "random_bonus-POLISH-8.xml" && r.ChildPath == "POLISH:8:2") == 1,
                        "exact composite Idian owner graph");
                    Require(item.IdianListener is not null, "current Idian action-listener identity");
                    item.IdianListener.Validate(input.Operation, input.Epoch);
                    Require(input.Lease.SuppressedObserverIdentities.Contains(item.IdianListener.Owner),
                        "lease does not cover exact legacy Idian action listener");
                }
                else Require(item.Polish == 0, "positive polish without current option owner");
                if (item.EquipmentIndex == 19 && item.TemperingLevel > 0)
                    Require(input.Graph.Registrations.Count(r => r.ChildPath == "PLUME_HP"
                        && r.Source.RawSha256 == item.Source.RawSha256 && r.ItemOwner == item.Current.Owner
                        && r.TemperingLevel == item.TemperingLevel) == 1, "outside17 current PLUME tempering owner");
                if (item.EquipmentIndex == 0)
                    Require(input.Graph.Registrations.Any(r => r.ChildPath == "retained/e=1" && r.Source.Key == "skill_template-37.xml"),
                        "current learned/registered SWORD mastery owner");
                if (item.EquipmentIndex == 3)
                    Require(input.Graph.Registrations.Any(r => r.ChildPath == "retained/e=1" && r.Source.Key == "skill_template-54.xml"),
                        "current learned/registered PLATE mastery owner");
            }
            var itemOwners = gear.Items.Where(x => x.EquipmentIndex < 17 || x.EquipmentIndex == 19)
                .Select(x => x.Current.Owner).ToHashSet(StringComparer.Ordinal);
            Require(input.Graph.Registrations.Where(x => x.ChildPath == "modifiers").All(x => itemOwners.Contains(x.Current.Owner)),
                "orphan item proxy owner");
            Require(input.Graph.Registrations.Where(x => x.ChildPath == "PLUME_HP").All(r =>
                gear.Items.Any(x => x.EquipmentIndex == 19 && x.Current.Owner == r.ItemOwner
                    && x.TemperingLevel == r.TemperingLevel && x.Source.RawSha256 == r.Source.RawSha256)),
                "orphan PLUME constructor owner");
            Require(input.Graph.Registrations.Where(x => x.ChildPath == "POLISH:8:2").All(r =>
                gear.Items.Any(x => x.IdianOwner == r.Current.Owner && x.Polish > 0)), "orphan Idian registration owner");
        }
        private GearItem Resolve(Slot slot)
        {
            GearItem[] matches = gear.Items.Where(x => x.Current.Owner == slot.ItemOwner).ToArray();
            Require(matches.Length == 1 && matches[0].Current.Generation == slot.Generation, "registry item generation/identity");
            return matches[0];
        }
        private void Save(GearItem item) => gear = gear with
        { Items = gear.Items.Select(x => x.Current.Owner == item.Current.Owner ? item : x).ToImmutableArray() };
        private bool BurnCharge(GearItem item, int slot)
        {
            int burn = Integer(item.Source.Read().Element("improve")!, "burn_defend");
            Require(burn is >= 0 and <= byte.MaxValue, "ordinary defend burn byte source");
            Emit("charge", "source-burn-read", item.Current.Owner, item.Charge, burn, slot);
            if (burn == 0 || item.Charge == 0) return false;
            int after = item.Charge - Math.Min(item.Charge, burn);
            bool changed = G.Tier(item.Charge) != G.Tier(after);
            uint counter = unchecked(item.ChargeCounter + 1);
            Save(item with { Charge = after, ChargeCounter = counter,
                Persistence = item.Persistence == Persistent.New ? Persistent.New : Persistent.UpdateRequired });
            gear = gear with { Persistence = Persistent.UpdateRequired };
            mutations.Add(new(item.Current.Owner, slot, true, item.Charge, burn, Math.Min(item.Charge, burn),
                after, item.ChargeCounter, counter, changed));
            Emit("charge", "points-write", item.Current.Owner, item.Charge, after, slot);
            Emit("charge", "counter-write", item.Current.Owner, item.ChargeCounter, counter, slot);
            Require(!item.ClassPermits.IsDefault && item.ClassPermits.Length == 17, "complete actual class-permit fields");
            int permit = item.ClassPermits[input.ClassId];
            if (permit != 0)
            {
                Require(item.PermitReduction.HasValue, "reached signed permit reduction");
                if (item.PermitReduction.Value != 0)
                {
                    Require(item.PermitFloor.HasValue, "reached global permit floor");
                    if (permit > item.PermitFloor.Value) permit -= unchecked((sbyte)item.PermitReduction.Value);
                    permit = Math.Max(permit, item.PermitFloor.Value);
                }
            }
            Emit("charge", "class-permit-read", item.Current.Owner, input.ClassId, permit, slot);
            if (G.Bucket(item.Charge) != G.Bucket(after)) Request(RequestKind.ChargeDescription, item.Current.Owner,
                after, visualBucket: G.Bucket(after));
            return changed;
        }
        private bool BurnPolish(GearItem item)
        {
            int burn = Integer(item.Source.Read().Element("idian")!, "burn_defend");
            Emit("polish", "source-burn-read", item.Current.Owner, item.Polish, burn, 0);
            if (burn <= 0) return false;
            int actual = Math.Min(item.Polish, burn), after = item.Polish - actual;
            bool depleted = item.Polish > 0 && after == 0;
            uint counter = unchecked(item.PolishCounter + (uint)actual);
            if (item.Polish > 0)
            {
                Save(item with { Polish = after, PolishCounter = counter });
                Emit("polish", "points-write", item.Current.Owner, item.Polish, after, 0);
                Emit("polish", "counter-write", item.Current.Owner, item.PolishCounter, counter, 0);
            }
            mutations.Add(new(item.Current.Owner, 0, false, item.Polish, burn, actual, after,
                item.PolishCounter, counter, depleted));
            if (depleted)
            {
                Require(item.IdianOwner is not null, "reached exact Idian owner");
                int removed = registry.Count(x => x.Owner == item.IdianOwner);
                Require(removed > 0, "reached option owner has no registered functions");
                registry = registry.Where(x => x.Owner != item.IdianOwner).ToImmutableArray();
                retained = retained.Where(x => x.Current.Owner != item.IdianOwner).ToImmutableArray();
                Emit("polish", "remove-all-owned-stat-functions", item.IdianOwner, removed, 0);
                Require(item.RemovedListener == Known.Present && item.ListenerProgram == "IdianStone.action-listener/base-OnRemoved-noop",
                    "reached unknown observer OnRemoved");
                Emit("polish", "remove-exact-Idian-listener", item.IdianListener!.Owner);
                Request(RequestKind.InventoryUpdate, item.Current.Owner);
                Require(item.IdianPersistence is Persistent.New or Persistent.Updated or Persistent.UpdateRequired, "reached Idian persistence");
                Persistent deletion = item.IdianPersistence == Persistent.New ? Persistent.NoAction : Persistent.Deleted;
                Emit("polish", "Idian-delete-rule", item.IdianOwner, (int)item.IdianPersistence, (int)deletion);
                Save(gear.Items.Single(x => x.Current.Owner == item.Current.Owner) with
                { IdianOwner = null, IdianGeneration = null, IdianListener = null, IdianActionSource = null,
                  IdianVariant = 0, IdianPersistence = Persistent.Unknown, RemovedListener = Known.Null, ListenerProgram = null });
                requests.Add(new(order++, RequestKind.StoreIdianStones, item.Current.Owner, Category: 3,
                    DeleteRow: deletion == Persistent.Deleted, ItemObjectId: item.ObjectId70, ItemSerial: item.Serial110));
                Emit("polish", "Store-normalreturn-state-write", item.IdianOwner, (int)deletion, (int)Persistent.Updated);
                removedOptions.Add(new(item.IdianOwner, item.IdianGeneration!, item.IdianListener!,
                    item.IdianPersistence, deletion, Persistent.Updated));
            }
            if (unchecked((int)(item.PolishCounter + (uint)burn)) > 50000)
                Request(RequestKind.PolishDescription, item.Current.Owner, after);
            return depleted;
        }
        private G.Derived Derive()
        {
            var points = gear.Items.ToImmutableDictionary(x => x.Current.Owner, x => x.Charge, StringComparer.Ordinal);
            GearItem? main = gear.Items.SingleOrDefault(x => x.EquipmentIndex == 0);
            int delay = main is null ? 1500 : Integer(main.Source.Read().Element("weapon_stats")!, "attack_speed");
            return G.Evaluate(input.ClassId, input.Level, input.ClassSourceSha256, registry, points,
                motion.Flying, delay, main?.Current.Owner, input.Operation, input.Epoch);
        }
        private void RefreshLife(string phase)
        {
            int start = events.Count; Life before = life;
            Emit(phase, "life-dead-guard", before: life.Hp == 0 ? 1 : 0);
            if (life.Hp != 0) { RefreshResource(true, phase); RefreshResource(false, phase); }
            Request(RequestKind.StatsInfo, input.Receiver.Current.Owner);
            G.Derived g = Derive();
            Emit(phase, "speed-read", before: motion.CachedSpeed, after: g.Speed);
            bool speedChanged = g.Speed != motion.CachedSpeed;
            if (speedChanged) { Request(RequestKind.ChangeSpeed, input.Receiver.Current.Owner); motion = motion with { CachedSpeed = g.Speed }; }
            Emit(phase, "attack-speed-read", before: motion.CachedDelay, after: g.Delay);
            if (g.Delay != motion.CachedDelay)
            {
                if (!speedChanged) Request(RequestKind.ChangeSpeed, input.Receiver.Current.Owner);
                motion = motion with { CachedDelay = g.Delay };
            }
            refreshes.Add(new(phase, before, life, g, events.Skip(start).ToImmutableArray()));
        }
        private void RefreshResource(bool hp, string phase)
        {
            G.Derived graph = Derive(); int maximum = hp ? graph.MaxHp : graph.MaxMp;
            int previous = hp ? life.Hp : life.Mp, cache = hp ? life.CachedHp : life.CachedMp, next = previous;
            string name = hp ? "hp" : "mp";
            Emit(phase, name + "-max-read", after: maximum);
            if (cache != maximum)
            {
                next = G.Ratio(previous, maximum, cache);
                life = hp ? life with { CachedHp = maximum } : life with { CachedMp = maximum };
                Emit(phase, name + "-cache-ratio", before: cache, after: maximum);
            }
            if (next == previous) return;
            graph = Derive(); maximum = hp ? graph.MaxHp : graph.MaxMp;
            next = Math.Clamp(next, 0, maximum);
            Emit(phase, name + "-repeat-graph-read-and-clamp", before: previous, after: next);
            if (hp)
            {
                life = life with { Hp = next };
                if (previous == 0 || next == 0) life = life with { CachedHp = graph.MaxHp, CachedMp = graph.MaxMp };
                if (life.KillingBlow != 0 && (next == 0 || next > life.KillingBlow))
                { Emit(phase, "clear-killing-blow", before: life.KillingBlow, after: 0); life = life with { KillingBlow = 0 }; }
                if (next == previous) return;
                Request(RequestKind.AttackStatusHp, input.Receiver.Current.Owner);
                if (next == graph.MaxHp)
                {
                    Require(callbacks.HateTask == Known.Null, "reached unknown hate-task cancellation");
                    Emit(phase, "aggro-clear", before: callbacks.AggroEntries.Length, after: 0);
                    callbacks = callbacks with { AggroEntries = [] };
                }
                if (callbacks.Spawned)
                {
                    Request(RequestKind.SelfHp, input.Receiver.Current.Owner, next, maximum);
                    if (callbacks.InTeam) Request(RequestKind.TeamStatEnqueue, input.Receiver.Current.Owner);
                    if (previous == 0 || next < previous) Restore(phase);
                    Require(previous > 0, "unclosed FP restore/death transition");
                }
                Require(next > 0, "unclosed zero/death transition");
                Require(callbacks.MatchingHpObservers == Known.Present && callbacks.HpObservers.IsEmpty,
                    "reached unknown/nonempty HP_CHANGED callback program");
                Emit(phase, "HP_CHANGED-complete-empty-snapshot");
            }
            else
            {
                life = life with { Mp = next };
                if (next == previous) return;
                Request(RequestKind.AttackStatusMp, input.Receiver.Current.Owner);
                if (callbacks.Spawned)
                {
                    Request(RequestKind.SelfMp, input.Receiver.Current.Owner, next, maximum);
                    if (callbacks.InTeam) Request(RequestKind.TeamStatEnqueue, input.Receiver.Current.Owner);
                    if (next < previous) Restore(phase);
                }
            }
        }
        private void Restore(string phase)
        {
            Emit(phase, "restore-task-guard", before: callbacks.RestoreTaskPresent ? 1 : 0);
            if (!callbacks.RestoreTaskPresent)
            {
                requests.Add(new(order++, RequestKind.RestoreTask, input.Receiver.Current.Owner, DelayMs: 1700, PeriodMs: 6000));
                callbacks = callbacks with { RestoreTaskPresent = true };
            }
        }
        private void Emit(string stage, string kind, string? owner = null, long? before = null, long? after = null, int? slot = null)
            => events.Add(new(order++, stage, kind, owner, before, after, slot));
        private void Request(RequestKind kind, string owner, int? current = null, int? maximum = null, int? visualBucket = null)
        {
            GearItem? item = kind is RequestKind.InventoryUpdate or RequestKind.ChargeDescription or RequestKind.PolishDescription
                ? gear.Items.Single(x => x.Current.Owner == owner) : null;
            requests.Add(new(order++, kind, owner, current, maximum,
                ItemObjectId: item?.ObjectId70, ItemSerial: item?.Serial110, VisualBucket: visualBucket));
        }
    }
}
