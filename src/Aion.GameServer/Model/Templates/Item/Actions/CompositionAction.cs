using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Model.Templates.Items.Actions;

/// <summary>
/// Java parity: model/templates/item/actions/CompositionAction. Handles combining two enchantment stones into one via
/// CM_COMPOSITE_STONES. Not part of the regular item-use flow (retail has no server-side marker for this on the
/// combination tool item, the client alone knows to send this packet for it), so unlike other item actions this isn't
/// XML-bound and isn't invoked through AbstractItemAction.
/// </summary>
public class CompositionAction
{
    public bool CanAct(Aion.GameServer.Model.GameObjects.Players.Player player, Item tools, Item first, Item second)
    {
        if (!tools.GetItemTemplate().IsCombinationItem())
            return false;

        if (!first.GetItemTemplate().IsEnchantmentStone())
            return false;

        if (!second.GetItemTemplate().IsEnchantmentStone())
            return false;

        if (first.GetItemCount() < 1 || second.GetItemCount() < 1)
            return false;

        return first.GetItemTemplate().GetLevel() <= 95 && second.GetItemTemplate().GetLevel() <= 95;
    }

    public void Act(Aion.GameServer.Model.GameObjects.Players.Player player, Item tools, Item first, Item second)
    {
        bool result = player.GetInventory().DecreaseByItemId(tools.GetItemId(), 1);
        bool result1 = player.GetInventory().DecreaseByItemId(first.GetItemId(), 1);
        bool result2 = player.GetInventory().DecreaseByItemId(second.GetItemId(), 1);
        if (result && result1 && result2)
        {
            Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, Aion.GameServer.Network.Aion.ServerPackets.SM_SYSTEM_MESSAGE.STR_COMPOUND_SUCCESS(second.GetL10n(), first.GetL10n()));
            Aion.GameServer.Services.Items.ItemService.AddItem(player, GetItemId(CalcLevel(first.GetItemTemplate().GetLevel(), second.GetItemTemplate().GetLevel())), 1);
        }
    }

    private int CalcLevel(int first, int second)
    {
        int value = ((first + second) / 2);
        if (value < 11)
        {
            value = Aion.GameServer.Commons.Utils.Rnd.Get(1, 20);
        }
        else
        {
            int random = Aion.GameServer.Commons.Utils.Rnd.Get(1, 10);
            int bit = Aion.GameServer.Commons.Utils.Rnd.Get(0, 1);
            value = (bit == 0 ? value - random : value + random);
        }
        return value;
    }

    public int GetItemId(int value)
    {
        return 166000000 + value;
    }
}
