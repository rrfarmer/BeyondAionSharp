using System;
using System.Collections.Generic;
using System.Linq;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Player;
using Aion.GameServer.Model.Templates.Item.Actions;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.Serverpackets;
using Aion.GameServer.Restrictions;
using Aion.GameServer.Utils;
using State = Aion.GameServer.Network.Aion.AionConnection.State;

namespace Aion.GameServer.Network.Aion.Clientpackets;

/// <summary>Java parity: network/aion/clientpackets/CM_MEGAPHONE (Artur, ginho1, Neon). Sends a global faction-chat message via a megaphone item. MegaphoneAction/PlayerRestrictions red-tolerated.</summary>
public class CM_MEGAPHONE : AionClientPacket
{
    private string message;
    private int itemObjId;

    public CM_MEGAPHONE(int opcode, ISet<State> validStates)
        : base(opcode, validStates)
    {
    }

    protected override void ReadImpl()
    {
        message = ReadS();
        itemObjId = ReadD();
    }

    protected override void RunImpl()
    {
        Player player = GetConnection().GetActivePlayer();

        Item item = player.GetInventory().GetItemByObjId(itemObjId);
        if (item == null)
            return;

        if (!PlayerRestrictions.CanUseItem(player, item))
            return;

        MegaphoneAction megaphoneAction = item.GetItemTemplate().GetActions().GetItemActions()
                .OfType<MegaphoneAction>()
                .FirstOrDefault();
        if (megaphoneAction == null)
        {
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_ITEM_IS_NOT_USABLE());
            return;
        }
        if (megaphoneAction.CanAct(player, item, null, message))
        {
            int useDelay = item.GetItemTemplate().GetUseLimits().GetDelayTime();
            if (useDelay > 0)
                player.AddItemCoolDown(item.GetItemTemplate().GetUseLimits().GetDelayId(), DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + useDelay, useDelay / 1000);
            player.GetObserveController().NotifyItemuseObservers(item);
            megaphoneAction.Act(player, item, null, message);
        }
    }
}
