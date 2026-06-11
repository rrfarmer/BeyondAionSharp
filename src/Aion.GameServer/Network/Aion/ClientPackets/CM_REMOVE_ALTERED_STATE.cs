using System.Collections.Generic;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Utils.Audit;
using State = Aion.GameServer.Network.Aion.AionConnection.State;

namespace Aion.GameServer.Network.Aion.Clientpackets;

/// <summary>Java parity: network/aion/clientpackets/CM_REMOVE_ALTERED_STATE (dragoon112, Neon). Ends a removable altered-state effect (audits debuff-removal attempts). Effect/SkillSubType red-tolerated.</summary>
public class CM_REMOVE_ALTERED_STATE : AionClientPacket
{
    private int skillId;

    public CM_REMOVE_ALTERED_STATE(int opcode, ISet<State> validStates)
        : base(opcode, validStates)
    {
    }

    protected override void ReadImpl()
    {
        skillId = ReadUH();
        ReadC();
        ReadC(); // seen 1 with skillId 3573
    }

    protected override void RunImpl()
    {
        Player player = GetConnection().GetActivePlayer();
        Effect effect = player.GetEffectController().FindBySkillId(skillId);
        if (effect != null)
        {
            if (effect.GetSkillSubType() == SkillSubType.DEBUFF)
            {
                AuditLogger.Log(player, "tried to remove a debuff: " + skillId + " " + effect.GetSkillName() + " (effector: "
                    + effect.GetEffector() + ")");
            }
            else
            {
                effect.EndEffect();
            }
        }
    }
}
