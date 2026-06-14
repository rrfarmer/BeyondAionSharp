using System.Collections.Generic;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Items;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.ChatHandlers;

namespace Aion.GameServer.Handlers.ConsoleCommands;

/// <summary>Java parity: data/handlers/consolecommands/Itemcooltime (Neon). Removes cooldowns of all items.</summary>
public class Itemcooltime : ConsoleCommand
{
    public Itemcooltime()
        : base("itemcooltime", "Removes cooldowns of all items.")
    {
    }

    protected override void Execute(Player player, params string[] paramsArr)
    {
        if (player.GetItemCoolDowns() != null)
        {
            Dictionary<int, ItemCooldown> dummyCds = new Dictionary<int, ItemCooldown>(); // 4.8 client ignores reuseTime <= currentTime, but sending old cds + useDelay 0 works
            foreach (KeyValuePair<int, ItemCooldown> en in player.GetItemCoolDowns())
            {
                dummyCds[en.Key] = new ItemCooldown(en.Value.GetReuseTime(), 0);
                player.RemoveItemCoolDown(en.Key);
            }
            PacketSendUtility.SendPacket(player, new SM_ITEM_COOLDOWN(dummyCds));
        }
    }
}
