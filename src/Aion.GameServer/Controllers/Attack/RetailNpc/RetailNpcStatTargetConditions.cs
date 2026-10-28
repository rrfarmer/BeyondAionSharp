using Aion.GameServer.SkillEngine.Effects;

namespace Aion.GameServer.Controllers.Attack.RetailNpc;

/// <summary>NPC numeric consumers' read side of Player Poison/Bleed/Paralyze/Aerial events.
/// Main140362df0 increments counters125c/1258 and sets bits0/1;140363280
/// clears a bit only when its decremented counter is zero. Public4.8 state,
/// effect admission, packets and timers remain with their existing owners.</summary>
internal sealed class RetailNpcStatTargetConditions
{
    internal const int PoisonMask=1;
    internal const int BleedMask=2;
    internal const int ParalyzeMask=1<<2;
    internal const int OpenAerialMask=1<<16;
    private readonly object gate=new();
    private int poisonReferences,bleedReferences,mask;
    internal int Mask=>Volatile.Read(ref mask);

    internal void Set(AbnormalState state)
    {
        int selected=state.GetId()&(PoisonMask|BleedMask|ParalyzeMask|OpenAerialMask);
        if(selected==0)return;
        lock(gate)
        {
            if((selected&PoisonMask)!=0)poisonReferences=unchecked(poisonReferences+1);
            if((selected&BleedMask)!=0)bleedReferences=unchecked(bleedReferences+1);
            Volatile.Write(ref mask,mask|selected);
        }
    }

    internal void Clear(AbnormalState state)
    {
        int selected=state.GetId()&(PoisonMask|BleedMask|ParalyzeMask|OpenAerialMask);
        if(selected==0)return;
        lock(gate)
        {
            int next=mask;
            if((selected&ParalyzeMask)!=0)next&=~ParalyzeMask; // Native index2 has no reference counter.
            if((selected&OpenAerialMask)!=0)next&=~OpenAerialMask; //Native index16 has no reference counter.
            if((selected&PoisonMask)!=0&&unchecked(--poisonReferences)==0)next&=~PoisonMask;
            if((selected&BleedMask)!=0&&unchecked(--bleedReferences)==0)next&=~BleedMask;
            // Native signed32 counters are not clamped; the bit is separate
            // storage, not a derived count>0 predicate. No new lifecycle owner.
            Volatile.Write(ref mask,next);
        }
    }
}
