using Aion.GameServer.Ai;
using Aion.GameServer.Commons.Utils;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Services.Items;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/PlatinumFountainAI (Luzien).</summary>
[AIName("fountain")]
public class PlatinumFountainAI : ActionItemNpcAI
{
    public PlatinumFountainAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleDialogStart(Player player)
    {
        if (player.GetInventory().GetItemCountByItemId(186000030) > 0)
        {
            base.HandleDialogStart(player);
            PacketSendUtility.SendMessage(player, "Du forderst dein Glück heraus und wirfst eine Goldmedaille in den Brunnen!");
        }
        else
            PacketSendUtility.SendMessage(player, "Du hast leider keine Goldmedaillen bei dir, die du in den Brunnen werfen könntest.");
    }

    protected override void HandleUseItemFinish(Player player)
    {
        if (!player.GetInventory().DecreaseByItemId(186000030, 1))
            return;

        if (Rnd.Chance() < 10)
        {
            ItemService.AddItem(player, 186000096, 1);
            PacketSendUtility.SendMessage(player, "Du hattest Glück! Eine Medaille aus reinem Platin springt dir entgegen!");
        }
        else
        {
            ItemService.AddItem(player, 182005205, 1);
            PacketSendUtility.SendMessage(player, "Du findest leider nur eine alte, verrostete Medaille. Vielleicht hast du beim nächsten Mal mehr Glück!");
        }
    }
}
