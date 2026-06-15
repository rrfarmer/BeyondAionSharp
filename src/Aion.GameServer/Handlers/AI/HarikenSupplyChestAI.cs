using Aion.GameServer.Ai;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services.Items;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/aturamSkyFortress/HarikenSupplyChestAI (@author cheatkiller).</summary>
[AIName("hariken_supply_chest")]
public class HarikenSupplyChestAI : NpcAI
{
    public HarikenSupplyChestAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleDialogStart(Player player)
    {
        PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(GetObjectId(), 1011));
    }

    public override bool OnDialogSelect(Player player, int dialogActionId, int questId, int extendedRewardIndex)
    {
        if (dialogActionId == DialogAction.SETPRO1)
        {
            AddItems(player);
        }
        PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(GetObjectId(), 0));
        return true;
    }

    private void AddItems(Player player)
    {
        Item BottomlessBucket = player.GetInventory().GetFirstItemByItemId(164000202);
        Item TalonSummoningDevice = player.GetInventory().GetFirstItemByItemId(164000163);
        if (TalonSummoningDevice == null && BottomlessBucket == null)
        {
            ItemService.AddItem(player, 164000163, 1);
            ItemService.AddItem(player, 164000202, 1);
        }
    }
}
