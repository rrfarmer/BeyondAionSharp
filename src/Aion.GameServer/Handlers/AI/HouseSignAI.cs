using Aion.GameServer.Ai;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/HouseSignAI (Rolandas).</summary>
[AIName("housesign")]
public class HouseSignAI : GeneralNpcAI
{
    public HouseSignAI(Npc owner)
        : base(owner)
    {
    }

    public override bool OnDialogSelect(Player player, int dialogActionId, int questId, int extendedRewardIndex)
    {
        DialogPage page = DialogPageExtensions.GetByActionId(dialogActionId);
        if (page == DialogPage.NULL)
            return false;

        PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(GetOwner().GetObjectId(), page.Id()));
        return true;
    }
}
