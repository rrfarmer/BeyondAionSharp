using Aion.GameServer.Retail;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Controllers.Attack.RetailNpc;

/// <summary>Offline ordinary 17860 physical-defense stage for a single
/// equipment snapshot and explicitly supplied actor/modifier inputs. It is
/// not effect admission, actual player state, or a complete defense result.</summary>
internal static class RetailNpcPlayerDefenseDrainEquipmentProjection
{
    internal readonly record struct ItemContext(sbyte? LevelAdjustment,ushort? HighLevelFloor,bool? ArmorRevision,
        RetailNpcArmorDefenseSource.Restriction? Restriction,RetailNpcCurseItemBonusSource.RevisionContext Normal,
        RetailNpcCurseItemBonusSource.RevisionContext Charge,
        RetailNpcCurseEquipmentObservation.FusionProjectionContext? Fusion=null);
    internal sealed record Result(int? PhysicalDefense,int RetailPercent,
        RetailNpcPlayerArmorDefenseObservation.Report Equipment,
        RetailNpcPlayerItemDefenseBonusObservation.Report ItemBonuses)
    {
        internal bool GameplayReady=>false;
    }

    // Bind the already checked equipment projection to the prepared effect's
    // physical basis. Element inputs and actor/modifier state remain explicit.
    // No caller may treat this point-in-time result as live admission.
    internal static bool TryBuildEffectInputsForOfflineFixture(Result result,ushort baseDefense,int otherPercent,int otherFlat,
        IReadOnlyList<RetailNpc17860DefenseEffect.ElementInput> elements,out RetailNpc17860DefenseEffect.Inputs? inputs)
    {
        ArgumentNullException.ThrowIfNull(result);ArgumentNullException.ThrowIfNull(elements);inputs=null;
        if(result.PhysicalDefense is null ||result.RetailPercent!=-10 ||elements.Count!=6
            ||!result.Equipment.Issues.IsEmpty ||!result.ItemBonuses.Issues.IsEmpty ||result.Equipment.RevisedArmorSubtotal<0)
            return false;
        long current=(long)baseDefense+result.Equipment.RevisedArmorSubtotal;
        if(current is <0 or >uint.MaxValue)return false;
        int flat=unchecked(otherFlat+result.ItemBonuses.Points);
        if(RetailNpcPlayerDefenseDrainProjection.PhysicalDefense((uint)current,(uint)result.Equipment.RevisedArmorSubtotal,
            unchecked(otherPercent+result.RetailPercent),flat)!=result.PhysicalDefense)return false;
        inputs=new((uint)current,(uint)result.Equipment.RevisedArmorSubtotal,otherPercent,flat,
            elements[0],elements[1],elements[2],elements[3],elements[4],elements[5]);
        return true;
    }

    // A point-in-time receipt binds the equipment observation and the host
    // stat-collection version around the source projection. It does not
    // reserve either owner, prove item eligibility or detect equipment ABA.
    internal static RetailNpc17860DefenseEffect.InputReceipt? CaptureEffectInputsForOfflineFixture(Player player,
        Func<Player,RetailNpcCurseEquipmentObservation.Snapshot> observe,Func<RetailNpcCurseEquipmentObservation.Snapshot,Result> project,
        ushort baseDefense,int otherPercent,int otherFlat,IReadOnlyList<RetailNpc17860DefenseEffect.ElementInput> elements)
    {
        ArgumentNullException.ThrowIfNull(player);ArgumentNullException.ThrowIfNull(observe);ArgumentNullException.ThrowIfNull(project);
        var stats=player.GetGameStats();
        if(!stats.TryCaptureRetailStatMutation(out long stamp))return null;
        var snapshot=observe(player);
        var equipmentCurrent=RetailNpcCurseEquipmentObservation.EquipmentWitness(player,snapshot,observe);
        var projected=project(snapshot);
        if(!TryBuildEffectInputsForOfflineFixture(projected,baseDefense,otherPercent,otherFlat,elements,out var inputs)
            ||inputs is null ||!equipmentCurrent(player) ||!stats.RetailStatMutationUnchanged(stamp))return null;
        bool Current(Player p)=>ReferenceEquals(p,player) &&ReferenceEquals(p.GetGameStats(),stats)
            &&stats.RetailStatMutationUnchanged(stamp) &&equipmentCurrent(p) &&stats.RetailStatMutationUnchanged(stamp);
        return new(inputs,Current);
    }
    internal static Result ProjectForOfflineFixture(RetailNpcCurseEquipmentObservation.Snapshot snapshot,
        ReadOnlySpan<byte> source17860,ushort baseDefense,int otherPercent,int otherFlat,
        RetailNpcArmorDefenseSource armor,RetailNpcShieldDefenseSource shields,
        RetailNpcOtherEquipmentDefenseSource otherEquipment,RetailNpcWeaponDefenseSource weapons,
        RetailNpcItemDefenseBonusSource itemBonuses,RetailNpcCurseClassPermitSource permits,
        Func<RetailNpcCurseEquipmentObservation.ItemObservation,ItemContext?> context)
    {
        ArgumentNullException.ThrowIfNull(snapshot);ArgumentNullException.ThrowIfNull(context);
        if(snapshot.NativeClass is <0 or >=17||snapshot.PlayerLevel is <0 or >ushort.MaxValue||snapshot.Items.IsDefault)
            throw new ArgumentException("Invalid equipment observation",nameof(snapshot));
        int percent=RetailNpcPlayerDefenseDrainProjection.Read17860PercentForOfflineFixture(source17860);
        // Capture each explicitly supplied item context once. Both projections
        // consume the same immutable equipment snapshot and the same levels.
        var inputs=new Dictionary<RetailNpcCurseEquipmentObservation.ItemObservation,ItemContext?>();
        foreach(var row in snapshot.Items)if(!inputs.ContainsKey(row))inputs.Add(row,context(row));
        RetailNpcPlayerArmorDefenseObservation.Context? Raw(RetailNpcCurseEquipmentObservation.ItemObservation row)
        {
            if(inputs[row] is not { } value)return null;
            return new(value.LevelAdjustment,value.HighLevelFloor,value.ArmorRevision,value.Restriction);
        }
        RetailNpcPlayerItemDefenseBonusObservation.Context? Bonus(RetailNpcCurseEquipmentObservation.ItemObservation row)
        {
            if(inputs[row] is not { } value)return null;
            return new(value.LevelAdjustment,value.HighLevelFloor,value.Normal,value.Charge);
        }
        RetailNpcCurseEquipmentObservation.FusionProjectionContext? Fusion(RetailNpcCurseEquipmentObservation.ItemObservation row)
            =>inputs[row]?.Fusion;
        var raw=RetailNpcPlayerArmorDefenseObservation.ProjectForOfflineFixture(snapshot,armor,Raw,shields,otherEquipment,weapons,Fusion);
        var bonus=RetailNpcPlayerItemDefenseBonusObservation.ProjectForOfflineFixture(snapshot,itemBonuses,permits,Bonus,Fusion);
        if(!raw.Issues.IsEmpty||!bonus.Issues.IsEmpty||raw.RevisedArmorSubtotal<0)
            return new(null,percent,raw,bonus);
        // Native ordinary stage starts from the actor's supplied initial
        // defense plus revised raw equipment. Bonus points are in the flat
        // vector; the percentage applies to the separate raw equipment basis.
        uint current=checked((uint)((long)baseDefense+raw.RevisedArmorSubtotal));
        int value=RetailNpcPlayerDefenseDrainProjection.PhysicalDefense(current,(uint)raw.RevisedArmorSubtotal,
            unchecked(otherPercent+percent),unchecked(otherFlat+bonus.Points));
        return new(value,percent,raw,bonus);
    }
}
