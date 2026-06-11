using System.Collections.Generic;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Utils.Audit;
using State = Aion.GameServer.Network.Aion.AionConnection.State;

namespace Aion.GameServer.Network.Aion.Clientpackets;

/// <summary>Java parity: network/aion/clientpackets/CM_TOGGLE_SKILL_DEACTIVATE (ATracer). Removes a toggle/stance skill effect (audits non-toggle attempts). DataManager.SKILL_DATA/SkillTemplate red-tolerated.</summary>
public class CM_TOGGLE_SKILL_DEACTIVATE : AionClientPacket
{
    private int skillId;

    public CM_TOGGLE_SKILL_DEACTIVATE(int opcode, ISet<State> validStates)
        : base(opcode, validStates)
    {
    }

    protected override void ReadImpl()
    {
        skillId = ReadUH();
        ReadH();
        ReadH();
    }

    protected override void RunImpl()
    {
        Player player = GetConnection().GetActivePlayer();
        SkillTemplate skillTemplate = DataManager.SKILL_DATA.GetSkillTemplate(skillId);
        if (skillTemplate == null || (!skillTemplate.IsToggle() && !skillTemplate.IsStance()))
        {
            AuditLogger.Log(player, "tried to remove non-toggle skill effect (" + skillId + ") through CM_TOGGLE_SKILL_DEACTIVATE");
            return;
        }
        player.GetEffectController().RemoveEffect(skillId);

        if (player.GetController().GetStanceSkillId() == skillId)
            player.GetController().StopStance();
    }
}
