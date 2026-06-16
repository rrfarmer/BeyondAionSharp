using System;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Aion.GameServer.Controllers.Attack;
using Aion.GameServer.Controllers.Observer;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.GameObjects.State;
using Aion.GameServer.Model.Templates.Items.Actions;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Utils;
using static Aion.GameServer.SkillEngine.Model.Skill;

namespace Aion.GameServer.SkillEngine.Effects;

/// <summary>Java parity: skillengine/effect/HideEffect (Sweetkr, Cura) : BufEffect. @XmlAttribute state(CreatureVisualState)/bufcount/type; startEffect: set HIDE + visualState, AttackUtil.cancelCastOn, SM_PLAYER_STATE broadcast, schedule(500ms) removeTargetFrom, onHide; Player→4 anonymous observers (STARTSKILLCAST stateful buffNumber, ATTACK, ITEMUSE)→nested; type==0→setCancelOnDmg(true); else Npc type==0→cancelOnDmg + ATTACK/STARTSKILLCAST observers; endEffect: unset visual/HIDE, onHideEnd, SM_PLAYER_STATE. ItemActions/Skill.SkillMethod red-tolerated.</summary>
[XmlType("HideEffect")]
public class HideEffect : BufEffect
{
    [XmlAttribute]
    public CreatureVisualState state;
    [XmlAttribute("bufcount")]
    public int buffCount;
    [XmlAttribute]
    public int type = 0;

    public override void ApplyEffect(Effect effect)
    {
        effect.AddToEffectedController();
    }

    public override void EndEffect(Effect effect)
    {
        base.EndEffect(effect);

        Creature effected = effect.GetEffected();
        effected.UnsetVisualState(state);
        effected.GetEffectController().UnsetAbnormal(AbnormalState.HIDE);
        effected.GetController().OnHideEnd();
        PacketSendUtility.BroadcastPacketAndReceive(effected, new SM_PLAYER_STATE(effected)); // update visibility
    }

    public override void StartEffect(Effect effect)
    {
        base.StartEffect(effect);

        Creature effected = effect.GetEffected();
        effected.GetEffectController().SetAbnormal(AbnormalState.HIDE);
        effect.SetAbnormal(AbnormalState.HIDE);

        effected.SetVisualState(state);

        // Cancel targeted enemy cast
        AttackUtil.CancelCastOn(effected);

        // send all to set new 'effected' visual state (remove all visual targetting from 'effected')
        PacketSendUtility.BroadcastPacketAndReceive(effected, new SM_PLAYER_STATE(effected));

        ThreadPoolManager.GetInstance().Schedule(ct =>
        {
            // do on all who targetting on 'effected' (set target null, cancel attack skill, cancel npc pursuit)
            AttackUtil.RemoveTargetFrom(effected, true);
            return ValueTask.CompletedTask;
        }, TimeSpan.FromMilliseconds(500));

        effected.GetController().OnHide();
        // for player adding: Remove Hide when using any item action . when requesting dialog to any npc . when being attacked . when attacking
        if (effected is Player)
        {
            // Remove Hide when use skill / item skill
            effect.AddObserver(effected, new HideSkillCastObserver(this, effect));
            effect.AddObserver(effected, new HideAttackObserver(effect));
            effect.AddObserver(effected, new HideItemUseObserver(this, effect));

            // type >= 1, hide is maintained even after damage
            if (type == 0)
                effect.SetCancelOnDmg(true);
        }
        else
        { // effected is npc
            if (type == 0)
            { // type >= 1, hide is maintained even after damage
                effect.SetCancelOnDmg(true);

                // Remove Hide when attacking
                effect.AddObserver(effected, new HideAttackObserver(effect));

                // Remove Hide when use skill
                effect.AddObserver(effected, new HideNpcSkillCastObserver(effect));
            }
        }
    }

    private sealed class HideSkillCastObserver : ActionObserver
    {
        private readonly HideEffect outer;
        private readonly Effect effect;
        private int buffNumber = 0;

        public HideSkillCastObserver(HideEffect outer, Effect effect)
            : base(ObserverType.STARTSKILLCAST)
        {
            this.outer = outer;
            this.effect = effect;
        }

        public override void StartSkillCast(Skill skill)
        {
            // TODO find better way
            if (skill.GetSkillMethod() == SkillMethod.ITEM)
            {
                if (skill.GetItemTemplate().IsPotion() || skill.GetSkillTemplate().GetDuration() > 0)
                    effect.EndEffect();
                return;
            }
            bool isShapeChange = skill.GetSkillTemplate().GetEffects().HasAnyEffectType(EffectType.SHAPECHANGE);
            if (isShapeChange || !skill.IsSelfBuff() || ++buffNumber >= outer.buffCount)
                effect.EndEffect();
        }
    }

    private sealed class HideAttackObserver : ActionObserver
    {
        private readonly Effect effect;

        public HideAttackObserver(Effect effect)
            : base(ObserverType.ATTACK)
        {
            this.effect = effect;
        }

        public override void Attack(Creature creature, int skillId)
        {
            effect.EndEffect();
        }
    }

    private sealed class HideItemUseObserver : ActionObserver
    {
        private readonly HideEffect outer;
        private readonly Effect effect;

        public HideItemUseObserver(HideEffect outer, Effect effect)
            : base(ObserverType.ITEMUSE)
        {
            this.outer = outer;
            this.effect = effect;
        }

        public override void Itemused(Item item)
        {
            // [4.5] Buff items do not affect Hide II. Hide I is cancelled.
            ItemActions actions = item.GetItemTemplate().GetActions();
            if (actions != null)
            {
                if (outer.buffCount == 0 || actions.GetSkillUseAction() == null)
                    effect.EndEffect();
            }
        }
    }

    private sealed class HideNpcSkillCastObserver : ActionObserver
    {
        private readonly Effect effect;

        public HideNpcSkillCastObserver(Effect effect)
            : base(ObserverType.STARTSKILLCAST)
        {
            this.effect = effect;
        }

        public override void StartSkillCast(Skill skill)
        {
            effect.EndEffect();
        }
    }
}
