using Aion.GameServer.Ai;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.House;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/ButlerAI (Rolandas, Neon, Sykra).</summary>
[AIName("butler")]
public class ButlerAI : GeneralNpcAI
{
    public ButlerAI(Npc owner)
        : base(owner)
    {
    }

    public override bool OnDialogSelect(Player player, int dialogActionId, int questId, int extendedRewardIndex)
    {
        return KickDialog(player, DialogPageExtensions.GetByActionId(dialogActionId));
    }

    private bool KickDialog(Player player, DialogPage page)
    {
        if (page == DialogPage.NULL)
            return false;
        PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(GetOwner().GetObjectId(), page.Id()));
        return true;
    }

    protected override void HandleCreatureSee(Creature creature)
    {
        if (creature is Player player && GetCreator() is House house)
            house.SendScripts(player);
    }
}
