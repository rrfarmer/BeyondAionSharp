using System;
using System.Collections.Generic;
using System.Linq;
using Aion.GameServer.Handlers.ConsoleCommands;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Items;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.ChatHandlers;

namespace Aion.GameServer.Handlers.AdminCommands;

/// <summary>Java parity: data/handlers/admincommands/RemoveCd (kecimis). Clears cooldowns for skills, items and instances.</summary>
public class RemoveCd : AdminCommand
{
    public RemoveCd()
        : base("removecd", "Clears cooldowns for skills, items and instances.")
    {
        SetSyntaxInfo(
            " - Removes all item and skill cds of your target.",
            "<instance> <all|worldId> - Removes specified instance cd(s) of your target.");
    }

    protected override void Execute(Player admin, params string[] paramsArr)
    {
        VisibleObject target = admin.GetTarget();
        if (target == null)
            target = admin;

        if (target is Player player)
        {
            if (paramsArr.Length == 0)
            {
                if (player.GetSkillCoolDowns() != null)
                {
                    long nowMillis = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    List<int> cooldownIds = player.GetSkillCoolDowns().Where(e => e.Value > nowMillis).Select(e => e.Key).ToList();
                    PacketSendUtility.SendPacket(player, new SM_SKILL_COOLDOWN(player, cooldownIds));
                    player.GetSkillCoolDowns().Clear();
                }

                Dictionary<int, ItemCooldown> dummyCds = new Dictionary<int, ItemCooldown>(); // 4.8 client ignores reuseTime <= currentTime, but sending old cds + useDelay 0 works
                foreach (KeyValuePair<int, ItemCooldown> en in player.GetItemCoolDowns())
                {
                    dummyCds[en.Key] = new ItemCooldown(en.Value.GetReuseTime(), 0);
                    player.RemoveItemCoolDown(en.Key);
                }
                PacketSendUtility.SendPacket(player, new SM_ITEM_COOLDOWN(dummyCds));

                player.GetHouseObjectCooldowns().Clear();

                if (player.Equals(admin))
                {
                    SendInfo(admin, "Your item and skill cooldowns were removed.");
                }
                else
                {
                    SendInfo(admin, "You have removed item and skill cooldowns of " + player.GetName() + '.');
                    SendInfo(player, admin.GetName(true) + " removed your item and skill cooldowns.");
                }
            }
            else if (paramsArr[0].Equals("instance", StringComparison.OrdinalIgnoreCase) && paramsArr.Length >= 2)
            {
                if (paramsArr[1].Equals("all", StringComparison.OrdinalIgnoreCase))
                {
                    Clearusercoolt.ClearAllInstanceCooldowns(admin, player);
                }
                else
                {
                    int worldId = int.Parse(paramsArr[1]);
                    if (player.GetPortalCooldownList().IsPortalUseDisabled(worldId))
                    {
                        player.GetPortalCooldownList().RemovePortalCooldown(worldId);
                        player.GetPortalCooldownList().SendEntryInfo(worldId);

                        string worldName = World.World.GetInstance().GetWorldMap(worldId).GetName().Replace('_', ' ');
                        if (player.Equals(admin))
                        {
                            SendInfo(admin, "Your instance cooldown for " + worldName + " was removed.");
                        }
                        else
                        {
                            SendInfo(admin, "You have removed the instance cooldown for " + worldName + " of " + player.GetName() + '.');
                            SendInfo(player, admin.GetName(true) + " removed your instance cooldown for " + worldName);
                        }
                    }
                    else
                        SendInfo(admin, (player.Equals(admin) ? "You have" : player.GetName() + " has") + " no cooldown on given instance.");
                }
            }
            else
            {
                SendInfo(admin);
            }
        }
        else
            PacketSendUtility.SendPacket(admin, SM_SYSTEM_MESSAGE.STR_INVALID_TARGET());
    }
}
