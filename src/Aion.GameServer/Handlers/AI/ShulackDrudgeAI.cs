using Aion.GameServer.Ai;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services.Items;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/dredgion/ShulackDrudgeAI (@author cheatkiller).</summary>
[AIName("shulackdrudge")]
public class ShulackDrudgeAI : GeneralNpcAI
{
    public ShulackDrudgeAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleDialogFinish(Player player)
    {
        AddItems(player);
        base.HandleDialogFinish(player);
    }

    protected override void HandleDialogStart(Player player)
    {
        PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(GetObjectId(), 1011));
    }

    private void AddItems(Player player)
    {
        int itemId = player.GetRace() == Race.ELYOS ? 182212606 : 182212607;
        Item dredgionSupplies = player.GetInventory().GetFirstItemByItemId(itemId);
        if (dredgionSupplies == null)
        {
            ItemService.AddItem(player, itemId, 1);
            GetOwner().OverrideNpcType(CreatureType.PEACE);
        }
    }
}
