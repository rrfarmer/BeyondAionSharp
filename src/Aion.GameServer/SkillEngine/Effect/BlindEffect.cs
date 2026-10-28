using Aion.GameServer.Commons.Utils;
using System.Xml.Serialization;
using Aion.GameServer.Controllers.Attack;
using Aion.GameServer.Controllers.Attack.RetailNpc;
using Aion.GameServer.Controllers.Observer;
using Aion.GameServer.Model.GameObjects.State;
using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.SkillEngine.Model;

namespace Aion.GameServer.SkillEngine.Effects;

/// <summary>Java parity: skillengine/effect/BlindEffect (ATracer) : EffectTemplate. applyEffect: visualState &amp; ~BLINKING &lt; HIDE10→removeHideEffects, addToEffectedController; calculate→BLIND_RESISTANCE; startEffect: set BLIND, anonymous AttackStatusObserver(value, DODGE) overriding checkAttackerStatus→nested BlindObserver (Rnd.Chance&lt;Value); endEffect→unset. CreatureVisualState/AttackStatusObserver red-tolerated.</summary>
[XmlType("BlindEffect")]
public class BlindEffect : EffectTemplate
{
    public override void ApplyEffect(Effect effect)
    {
        int visualStateExcludingBlinking = effect.GetEffected().GetVisualState() & ~CreatureVisualState.BLINKING.GetId();
        if (visualStateExcludingBlinking < CreatureVisualState.HIDE10.GetId())
            effect.GetEffected().GetEffectController().RemoveHideEffects();
        effect.AddToEffectedController();
    }

    public override void Calculate(Effect effect)
    {
        base.Calculate(effect, StatEnum.BLIND_RESISTANCE, null);
    }

    public override void StartEffect(Effect effect)
    {
        bool nativeRecorded=RetailNpcStatPhysicalStatuses.BeginBlind(effect,this);
        bool nativeNpcDrain=RetailNpcStatSpellDrain.IsSelectedBlind(effect,this);
        effect.SetAbnormal(AbnormalState.BLIND);
        effect.GetEffected().GetEffectController().SetAbnormal(AbnormalState.BLIND);
        effect.AddObserver(effect.GetEffected(), new BlindObserver(Value,nativeRecorded,nativeNpcDrain));
    }

    public override void EndEffect(Effect effect)
    {
        RetailNpcStatPhysicalStatuses.EndBlind(effect);
        effect.GetEffected().GetEffectController().UnsetAbnormal(AbnormalState.BLIND);
    }

    private sealed class BlindObserver : AttackStatusObserver
    {
        internal readonly bool NativeRecorded;
        private readonly bool nativeNpcDrain;
        public BlindObserver(int value,bool nativeRecorded,bool nativeNpcDrain)
            : base(value, AttackStatus.DODGE)
        {
            NativeRecorded=nativeRecorded;
            this.nativeNpcDrain=nativeNpcDrain;
        }

        public override bool CheckAttackerStatus(AttackStatus status)
        {
            return nativeNpcDrain ? RetailNpcStatSpellDrain.BlindMiss(Value,SkyTempleNpcCombat.Draw) : Rnd.Chance() < Value;
        }
    }
    internal static bool IsNativeRecordedObserver(AttackCalcObserver observer)
        =>observer is BlindObserver {NativeRecorded:true};
}
