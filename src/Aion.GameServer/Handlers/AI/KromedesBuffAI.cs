using Aion.GameServer.Ai;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.SkillEngine;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/kromedesTrial/KromedesBuffAI (@author Tiger0319, Gigi, xTz).</summary>
[AIName("krbuff")]
public class KromedesBuffAI : ActionItemNpcAI
{
    public KromedesBuffAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleUseItemFinish(Player player)
    {
        switch (GetNpcId())
        {
            case 730336:
                SkillEngine.SkillEngine.GetInstance().ApplyEffectDirectly(19216, player, player);
                PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_IDCROMEDE_BUFF_01());
                break;
            case 730337:
                SkillEngine.SkillEngine.GetInstance().ApplyEffectDirectly(19217, player, player);
                PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_IDCROMEDE_BUFF_02());
                AIActions.DeleteOwner(this);
                break;
            case 730338:
                SkillEngine.SkillEngine.GetInstance().ApplyEffectDirectly(19218, player, player);
                PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_IDCROMEDE_BUFF_03());
                AIActions.DeleteOwner(this);
                break;
            case 730339:
                SkillEngine.SkillEngine.GetInstance().ApplyEffectDirectly(19219, player, player);
                PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_IDCROMEDE_BUFF_04());
                break;
        }
    }
}
