using System;
using System.Collections.Generic;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.SkillEngine.Model;
using State = Aion.GameServer.Network.Aion.AionConnection.State;

namespace Aion.GameServer.Network.Aion.Clientpackets;

/// <summary>Java parity: network/aion/clientpackets/CM_USE_CHARGE_SKILL (Cheatkiller). Releases a charge skill with the accumulated charge time. Skill/controller red-tolerated.</summary>
public class CM_USE_CHARGE_SKILL : AionClientPacket
{
    public CM_USE_CHARGE_SKILL(int opcode, ISet<State> validStates)
        : base(opcode, validStates)
    {
    }

    protected override void ReadImpl()
    {
    }

    protected override void RunImpl()
    {
        Player player = GetConnection().GetActivePlayer();
        Skill chargeCastingSkill = player.GetCastingSkill();
        if (chargeCastingSkill == null || !chargeCastingSkill.GetSkillTemplate().IsCharge())
            return;
        long chargeTimeMillis = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - chargeCastingSkill.GetCastStartTime();
        player.GetController().UseChargeSkill(chargeCastingSkill, chargeTimeMillis);
    }
}
