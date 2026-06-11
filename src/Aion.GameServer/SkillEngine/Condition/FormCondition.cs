using System.Xml.Serialization;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.Serverpackets;
using Aion.GameServer.SkillEngine.Model;

namespace Aion.GameServer.SkillEngine.Condition;

/// <summary>Java parity: skillengine/condition/FormCondition (kecimis) : Condition. Defines transform type in which a player may cast. @XmlAttribute value(TransformType); validate: Player→transformModel active && type==value→true else STR_SKILL_CAN_NOT_CAST_IN_THIS_FORM false; non-Player→true. TransformType red-tolerated.</summary>
[XmlType("FormCondition")]
public class FormCondition : Condition
{
    [XmlAttribute]
    protected TransformType value;

    public override bool Validate(Skill env)
    {
        if (env.GetEffector() is Player)
        {
            if (env.GetEffector().GetTransformModel().IsActive() && env.GetEffector().GetTransformModel().GetType_() == value)
                return true;
            else
            {
                PacketSendUtility.SendPacket((Player)env.GetEffector(), SM_SYSTEM_MESSAGE.STR_SKILL_CAN_NOT_CAST_IN_THIS_FORM());
                return false;
            }
        }
        else
            return true;
    }
}
