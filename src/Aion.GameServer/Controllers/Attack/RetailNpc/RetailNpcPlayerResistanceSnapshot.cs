using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Stats.Container;

namespace Aion.GameServer.Controllers.Attack.RetailNpc;

/// <summary>Bounded point-in-time input assembly for offline vampire fixtures.
/// The supplied base and post-capture lifecycle are not production evidence.</summary>
internal static class RetailNpcPlayerResistanceSnapshot
{
    // An optimistic fixture witness: it detects tracked mutations and rechecks
    // the source-backed inputs. It does not make the subsequent commit atomic.
    internal sealed class Receipt(Player target, CreatureGameStats stats, long stamp, long life,
        ushort fixtureBase, RetailNpcPlayerCurseAbsoluteCapture.Catalog absolutes,
        RetailNpcPlayerCurseItemCapture.Catalog items, RetailNpcPlayerCurseItemCapture.DirectCatalog direct,
        RetailNpcPlayerResistanceCapture.Catalog buffs, RetailNpcStatSpellDrain.CurseResistanceInput input,
        Func<Player,bool>? constructedBaseCurrent=null)
    {
        internal RetailNpcStatSpellDrain.CurseResistanceInput Input => input;
        internal bool IsCurrent(Player current) => ReferenceEquals(current,target)
            && ReferenceEquals(current.GetGameStats(),stats) && current.IsSpawned() && !current.IsDead()
            && current.GetController().RetailLifeEpoch==life && (constructedBaseCurrent?.Invoke(current)??true)
            && stats.RetailStatMutationUnchanged(stamp)
            && TryCaptureForOfflineFixture(current,fixtureBase,absolutes,items,direct,buffs,out var now)
            && now==input && stats.RetailStatMutationUnchanged(stamp)
            && current.GetController().RetailLifeEpoch==life && (constructedBaseCurrent?.Invoke(current)??true);

        // A prepared curse legitimately changes the effect set and stat stamp.
        // Re-read the reviewed resistance inputs rather than treating that
        // expected change as the original snapshot. This compares values, not
        // source identities over time, and does not reserve later mutations.
        internal bool HasEquivalentInputForOfflineFixture(Player current) => ReferenceEquals(current,target)
            && ReferenceEquals(current.GetGameStats(),stats) && current.IsSpawned() && !current.IsDead()
            && current.GetController().RetailLifeEpoch==life && (constructedBaseCurrent?.Invoke(current)??true)
            && TryCaptureForOfflineFixture(current,fixtureBase,absolutes,items,direct,buffs,out var now)
            && now==input && current.GetController().RetailLifeEpoch==life
            && (constructedBaseCurrent?.Invoke(current)??true);
    }

    // Creature::Init clears the 25-word base array, including curse index 10;
    // User construction calls that initializer and its race/class setup writes
    // a separate primary-stat region. A freshly constructed native User has a
    // zero curse seed. Later changes and host/native lifetime equivalence are
    // not inferred: the explicit fixture witness must remain current.
    internal static bool TryCaptureReceiptFromConstructedRetailBaseForOfflineFixture(Player target,
        Func<Player,bool> constructedBaseCurrent,
        RetailNpcPlayerCurseAbsoluteCapture.Catalog absolutes,
        RetailNpcPlayerCurseItemCapture.Catalog items, RetailNpcPlayerCurseItemCapture.DirectCatalog direct,
        RetailNpcPlayerResistanceCapture.Catalog buffs,out Receipt? receipt)
    {
        ArgumentNullException.ThrowIfNull(constructedBaseCurrent);
        receipt=null;
        if(!target.IsSpawned()||target.IsDead()||target.GetRace() is not (Race.ELYOS or Race.ASMODIANS)
            ||!Enum.IsDefined(target.GetPlayerClass())||target.GetLevel() is <1 or >65
            ||!constructedBaseCurrent(target))return false;
        var race=target.GetRace();var playerClass=target.GetPlayerClass();int level=target.GetLevel();
        bool BaseCurrent(Player p)=>ReferenceEquals(p,target)&&p.GetRace()==race
            &&p.GetPlayerClass()==playerClass&&p.GetLevel()==level&&constructedBaseCurrent(p);
        var stats=target.GetGameStats();long life=target.GetController().RetailLifeEpoch;
        if(!stats.TryCaptureRetailStatMutation(out long stamp)
            ||!TryCaptureForOfflineFixture(target,0,absolutes,items,direct,buffs,out var input)
            ||!stats.RetailStatMutationUnchanged(stamp)||target.GetController().RetailLifeEpoch!=life
            ||!BaseCurrent(target))return false;
        var result=new Receipt(target,stats,stamp,life,0,absolutes,items,direct,buffs,input,BaseCurrent);
        if(!result.IsCurrent(target))return false;
        receipt=result;return true;
    }

    // The standard 4.8 player resistance getter constructs the curse stat
    // with base zero; it has no persistent counterpart of the native seed.
    // Map that ordinary host state to the freshly initialized retail seed.
    // This is a compatibility inference, not an observation of every live
    // 5.8 player. The source-backed readers still reject unsupported item,
    // effect and absolute-status inputs, and production admission is separate.
    internal static bool TryCaptureReceiptFromMappedHostSeedForOfflineFixture(Player target,
        RetailNpcPlayerCurseAbsoluteCapture.Catalog absolutes,
        RetailNpcPlayerCurseItemCapture.Catalog items, RetailNpcPlayerCurseItemCapture.DirectCatalog direct,
        RetailNpcPlayerResistanceCapture.Catalog buffs,out Receipt? receipt)
    {
        var stats=target.GetGameStats();
        bool HostSeedCurrent(Player p)=>ReferenceEquals(p,target)&&ReferenceEquals(p.GetGameStats(),stats)
            &&stats.GetType()==typeof(PlayerGameStats);
        return TryCaptureReceiptFromConstructedRetailBaseForOfflineFixture(target,HostSeedCurrent,
            absolutes,items,direct,buffs,out receipt);
    }

    internal static bool TryCaptureReceiptForOfflineFixture(Player target,ushort fixtureBase,
        RetailNpcPlayerCurseAbsoluteCapture.Catalog absolutes,
        RetailNpcPlayerCurseItemCapture.Catalog items,
        RetailNpcPlayerCurseItemCapture.DirectCatalog direct,
        RetailNpcPlayerResistanceCapture.Catalog buffs, out Receipt? receipt)
    {
        receipt=null;var stats=target.GetGameStats();long life=target.GetController().RetailLifeEpoch;
        if(!stats.TryCaptureRetailStatMutation(out long stamp)
            ||!TryCaptureForOfflineFixture(target,fixtureBase,absolutes,items,direct,buffs,out var input)
            ||!stats.RetailStatMutationUnchanged(stamp)||target.GetController().RetailLifeEpoch!=life)return false;
        receipt=new(target,stats,stamp,life,fixtureBase,absolutes,items,direct,buffs,input);return true;
    }
    internal static bool TryCaptureForOfflineFixture(Player target,ushort fixtureBase,
        RetailNpcPlayerCurseAbsoluteCapture.Catalog absolutes,
        RetailNpcPlayerCurseItemCapture.Catalog items,
        RetailNpcPlayerCurseItemCapture.DirectCatalog direct,
        RetailNpcPlayerResistanceCapture.Catalog buffs,
        out RetailNpcStatSpellDrain.CurseResistanceInput input)
    {
        input=default;var stats=target.GetGameStats();
        if(!stats.TryCaptureRetailStatMutation(out long stamp)
            ||!RetailNpcPlayerCurseAbsoluteCapture.TryCaptureReceiptForOfflineFixture(target,absolutes,out var receipt)
            ||receipt is null
            ||!RetailNpcPlayerCurseItemCapture.TryCaptureForOfflineFixture(target,items,out var specific,direct,receipt)
            ||!RetailNpcPlayerResistanceCapture.TryCaptureGeneralForOfflineFixture(target,buffs,out var general)
            ||!stats.RetailStatMutationUnchanged(stamp)||!receipt.IsCurrent(target))return false;
        input=new(fixtureBase,specific,general,receipt.Buff,receipt.Debuff);return true;
    }
}
