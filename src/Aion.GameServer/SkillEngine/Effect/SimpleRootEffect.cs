using System;
using System.Xml.Serialization;
using Aion.GameServer.GeoEngine.Math;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Player;
using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.Network.Aion.Serverpackets;
using Aion.GameServer.Skillengine.Model;
using Aion.GameServer.Utils;
using Aion.GameServer.World;
using Aion.GameServer.World.Geo;

namespace Aion.GameServer.Skillengine.Effect;

/// <summary>Java parity: skillengine/effect/SimpleRootEffect (VladimirZ, Cheatkiller) : EffectTemplate. applyEffect→addToEffectedController; calculate: CANT_MOVE_STATE guard; STAGGER_RESISTANCE + isSubEffect→SIMPLE_MOVE_BACK, getHeadingTowards, Math.toRadians→*PI/180, cos/sin*0.7f, getClosestCollision, setTargetLoc; startEffect: SpellStatus.NONE, Player onStopMove, isSubEffect→World.updatePosition + non-Player SM_POSITION broadcast, set SIMPLE_MOVE_BACK; endEffect→unset. Vector3f/SpellStatus/SubEffectType red-tolerated.</summary>
[XmlType("SimpleRootEffect")]
public class SimpleRootEffect : EffectTemplate
{
    public override void ApplyEffect(Effect effect)
    {
        effect.AddToEffectedController();
    }

    public override void Calculate(Effect effect)
    {
        if (effect.GetEffected().GetEffectController().IsInAnyAbnormalState(AbnormalState.CANT_MOVE_STATE))
            return;
        if (base.Calculate(effect, StatEnum.STAGGER_RESISTANCE, null) && effect.IsSubEffect())
        {
            effect.SetSubEffectType(SubEffectType.SIMPLE_MOVE_BACK);
            Creature effected = effect.GetEffected();
            byte heading = PositionUtil.GetHeadingTowards(effect.GetEffector(), effect.GetEffected());
            double radian = Math.PI / 180 * PositionUtil.ConvertHeadingToAngle(heading);
            float x1 = (float)(Math.Cos(radian) * 0.7f);
            float y1 = (float)(Math.Sin(radian) * 0.7f);
            Vector3f closestCollision = GeoService.GetInstance().GetClosestCollision(effected, effected.GetX() + x1, effected.GetY() + y1, effected.GetZ());
            effect.SetTargetLoc(closestCollision.GetX(), closestCollision.GetY(), closestCollision.GetZ());
        }
    }

    public override void StartEffect(Effect effect)
    {
        Creature effected = effect.GetEffected();
        effect.SetSpellStatus(SpellStatus.NONE);
        if (effected is Player player)
            player.GetController().OnStopMove();
        if (effect.IsSubEffect())
        {
            World.GetInstance().UpdatePosition(effected, effect.GetTargetX(), effect.GetTargetY(), effect.GetTargetZ(), effected.GetHeading(), false);
            if (!(effected is Player))
                PacketSendUtility.BroadcastPacket(effected, new SM_POSITION(effected));
        }
        effect.GetEffected().GetEffectController().SetAbnormal(AbnormalState.SIMPLE_MOVE_BACK);
        effect.SetAbnormal(AbnormalState.SIMPLE_MOVE_BACK);
    }

    public override void EndEffect(Effect effect)
    {
        effect.GetEffected().GetEffectController().UnsetAbnormal(AbnormalState.SIMPLE_MOVE_BACK);
    }
}
