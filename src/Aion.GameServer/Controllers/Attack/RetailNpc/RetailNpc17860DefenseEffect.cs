using System.Runtime.CompilerServices;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Controllers.Effects;
using Aion.GameServer.Model.Stats.Calc.Functions;
using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.SkillEngine.Effects;
using Aion.GameServer.SkillEngine.Model;

namespace Aion.GameServer.Controllers.Attack.RetailNpc;

/// <summary>Prepared 17860 defense changes. The supplied source, target and
/// inputs are fixture-only until their live ownership and mutation are mapped.</summary>
internal static class RetailNpc17860DefenseEffect
{
    internal readonly record struct ElementInput(short Initial,int OtherPercent,int Flat,short Minimum,short Maximum);
    internal sealed record Inputs(uint PhysicalCurrent,uint ArmorBasis,int OtherPhysicalPercent,int PhysicalFlat,
        ElementInput Water,ElementInput Wind,ElementInput Earth,ElementInput Fire,ElementInput Light,ElementInput Dark);
    internal sealed record InputReceipt(Inputs Value,Func<Player,bool> Current);
    private sealed record State(Effect Effect,Player Target,StatdownEffect Template,CreatureGameStats Stats,EffectController Controller,
        long Life,int Percent,Func<Player,InputReceipt?> Capture,Func<Player,bool> Current)
    {
        internal int Started;
    }
    private sealed class DynamicDelta(StatEnum name,State state,int channel):StatAddFunction(name,0,true)
    {
        public override int GetValue()=>Delta(state,channel);
    }
    [ThreadStatic] private static HashSet<State>? activeCaptures;
    private static readonly ConditionalWeakTable<Effect,State> Prepared=new();
    internal static bool IsPreparedForOfflineFixture(Effect effect)=>Prepared.TryGetValue(effect,out _);
    private static readonly StatEnum[] Channels=[StatEnum.PHYSICAL_DEFENSE,StatEnum.WATER_RESISTANCE,
        StatEnum.WIND_RESISTANCE,StatEnum.EARTH_RESISTANCE,StatEnum.FIRE_RESISTANCE,
        StatEnum.LIGHT_RESISTANCE,StatEnum.DARK_RESISTANCE,
        StatEnum.ELEMENTAL_RESISTANCE_LIGHT,StatEnum.ELEMENTAL_RESISTANCE_DARK];

    // The host has separate light/dark names for NPC skill calculations and
    // for its ordinary defense and character-stat packet. Both represent the
    // same two reviewed retail channels for this prepared effect.
    private static int RetailChannel(int hostChannel)=>hostChannel switch {7=>5,8=>6,_=>hostChannel};

    internal static void AttachForOfflineFixture(Effect effect,ReadOnlySpan<byte> source,Inputs inputs,Func<Player,bool> current)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        AttachDynamicForOfflineFixture(effect,source,_=>inputs,current);
    }

    internal static void AttachDynamicForOfflineFixture(Effect effect,ReadOnlySpan<byte> source,Func<Player,Inputs?> capture,Func<Player,bool> current)
    {
        ArgumentNullException.ThrowIfNull(capture);ArgumentNullException.ThrowIfNull(current);
        AttachReceiptForOfflineFixture(effect,source,p=>capture(p) is { } inputs ? new(inputs,current):null,current);
    }

    internal static void AttachReceiptForOfflineFixture(Effect effect,ReadOnlySpan<byte> source,Func<Player,InputReceipt?> capture,Func<Player,bool> current)
    {
        ArgumentNullException.ThrowIfNull(effect);ArgumentNullException.ThrowIfNull(capture);ArgumentNullException.ThrowIfNull(current);
        int percent=RetailNpcPlayerDefenseDrainProjection.Read17860PercentForOfflineFixture(source);
        if(effect.GetSkillId()!=17860 ||effect.RetailHasEnded ||effect.GetEffected() is not Player target
            ||effect.GetEffectTemplates() is not {Count:2} templates ||templates[1] is not StatdownEffect template
            ||!ReferenceEquals(effect.GetSkillTemplate().GetEffectTemplate(2),template)
            ||!effect.IsInSuccessEffects(1) ||!effect.IsInSuccessEffects(2) ||!current(target))
            throw new InvalidOperationException("Unreviewed prepared 17860 defense effect");
        var state=new State(effect,target,template,target.GetGameStats(),target.GetEffectController(),
            target.GetController().RetailLifeEpoch,percent,capture,current);
        for(int i=0;i<Channels.Length;i++)_ = Delta(state,RetailChannel(i));
        Prepared.Add(effect,state);
    }

    private static int Delta(State state,int channel)
    {
        var active=activeCaptures??=new(ReferenceEqualityComparer.Instance);
        if(!active.Add(state))throw new InvalidOperationException("Prepared 17860 input recursively reads its own defense effect");
        try { return CaptureDelta(state,channel); }
        finally { active.Remove(state); }
    }

    private static int CaptureDelta(State state,int channel)
    {
        if(!OwnerCurrent(state) ||!state.Current(state.Target) ||state.Capture(state.Target) is not { } receipt
            ||receipt.Value is not { } inputs ||receipt.Current is null ||!receipt.Current(state.Target))
            throw new InvalidOperationException("Prepared 17860 inputs unavailable");
        int Physical(int p)=>RetailNpcPlayerDefenseDrainProjection.PhysicalDefense(inputs.PhysicalCurrent,
            inputs.ArmorBasis,p,inputs.PhysicalFlat);
        int delta;
        if(channel==0)delta=Physical(unchecked(inputs.OtherPhysicalPercent+state.Percent))-Physical(inputs.OtherPhysicalPercent);
        else
        {
            var e=channel switch {1=>inputs.Water,2=>inputs.Wind,3=>inputs.Earth,4=>inputs.Fire,5=>inputs.Light,6=>inputs.Dark,
                _=>throw new ArgumentOutOfRangeException(nameof(channel))};
            int Before(int p)=>RetailNpcPlayerDefenseDrainProjection.ElementalDefense(e.Initial,p,e.Flat,e.Minimum,e.Maximum);
            delta=Before(unchecked(e.OtherPercent+state.Percent))-Before(e.OtherPercent);
        }
        if(!OwnerCurrent(state) ||!state.Current(state.Target) ||!receipt.Current(state.Target))throw new InvalidOperationException("Prepared 17860 inputs changed");
        return delta;
    }

    private static bool OwnerCurrent(State state)
        =>!state.Effect.RetailHasEnded &&ReferenceEquals(state.Effect.GetEffected(),state.Target)
            &&state.Target.IsSpawned() &&!state.Target.IsDead()
            &&ReferenceEquals(state.Target.GetGameStats(),state.Stats)
            &&ReferenceEquals(state.Target.GetEffectController(),state.Controller)
            &&state.Target.GetController().RetailLifeEpoch==state.Life
            &&(Volatile.Read(ref state.Started)==0 ||ReferenceEquals(state.Controller.GetAbnormalEffect(state.Effect.GetStack()),state.Effect));

    internal static bool TrySelectForOfflineFixture(Effect effect,BufEffect template,out List<IStatFunction> selected)
    {
        selected=[];
        if(!Prepared.TryGetValue(effect,out var state))return false;
        if(!OwnerCurrent(state) ||!ReferenceEquals(effect.GetEffected(),state.Target) ||!ReferenceEquals(template,state.Template)
            ||effect.RetailHasEnded ||!effect.IsInSuccessEffects(1) ||!effect.IsInSuccessEffects(2)
            ||!ReferenceEquals(state.Controller.GetAbnormalEffect(effect.GetStack()),effect)
            ||!state.Current(state.Target) ||Interlocked.Exchange(ref state.Started,1)!=0)
            throw new InvalidOperationException("Prepared 17860 defense effect retired or repeated");
        for(int i=0;i<Channels.Length;i++)selected.Add(new DynamicDelta(Channels[i],state,RetailChannel(i)));
        return true;
    }
}
