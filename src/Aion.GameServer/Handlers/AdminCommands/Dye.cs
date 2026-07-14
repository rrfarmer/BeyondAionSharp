using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Templates.Items;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services.Items;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.ChatHandlers;

namespace Aion.GameServer.Handlers.AdminCommands;

/// <summary>Java parity: data/handlers/admincommands/Dye (loleron, Neon). Java reflection java.awt.Color.getField -> System.Drawing.Color named property (uppercase), getRGB() -> ToArgb().</summary>
public class Dye : AdminCommand
{
    public Dye()
        : base("dye", "Dyes a players visible equipment.")
    {
        SetSyntaxInfo("<color> - Dyes the selected player in the specified color (can be dye item link/ID, color name or color HEX code). 0 removes all dyeing.");
    }

    public override void Execute(Player player, params string[] paramsArr)
    {
        if (paramsArr.Length == 0)
        {
            SendInfo(player);
            return;
        }

        Player target = player.GetTarget() is Player p ? p : player;
        int? itemColor = null; // null = default item color
        string colorText = "default";
        string colorParam = paramsArr[0];

        if (!"0".Equals(colorParam, StringComparison.OrdinalIgnoreCase))
        {
            // try to get itemId of a dyeing item
            itemColor = ChatUtil.GetItemId(colorParam);
            ItemTemplate dyeItemTemplate = DataManager.ITEM_DATA.GetItemTemplate(itemColor.Value);

            if (itemColor != 0 && dyeItemTemplate != null && dyeItemTemplate.GetActions() != null && dyeItemTemplate.GetActions().GetDyeAction() != null)
            {
                itemColor = dyeItemTemplate.GetActions().GetDyeAction().GetColor();
                colorText = ChatUtil.Item(dyeItemTemplate.GetTemplateId());
            }
            else
            {
                try
                {
                    try
                    {
                        // try to get color by name
                        itemColor = GetAwtColorByName(colorParam.ToUpper());
                    }
                    catch (Exception)
                    {
                        // try to get color by hex code
                        if (colorParam.Length <= 8)
                        {
                            if (colorParam.StartsWith("#"))
                                colorParam = colorParam.Substring(1);
                            else if (colorParam.StartsWith("0x") || colorParam.StartsWith("0X"))
                                colorParam = colorParam.Substring(2);
                        }
                        itemColor = ParseInt(colorParam, 16);
                    }
                    colorText = ChatUtil.Color("#" + (itemColor.Value & 0xFFFFFF).ToString("X6"), itemColor.Value);
                }
                catch (FormatException)
                {
                    SendInfo(player, "Invalid color.");
                    return;
                }
            }
        }

        List<Item> appearanceItems = target.GetEquipment().GetEquippedForAppearance();
        if (appearanceItems.Count == 0)
        {
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_CHANGE_ITEM_SKIN_NO_TARGET_ITEM());
            return;
        }
        if (!appearanceItems.Any(item => item.GetItemTemplate().IsItemDyePermitted()))
        {
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_ITEM_COLOR_CHANGE_ERROR_CANNOTDYE(appearanceItems[0].GetL10n()));
            return;
        }
        foreach (Item item in appearanceItems)
        {
            if (item.GetItemSkinTemplate().IsItemDyePermitted())
                item.SetItemColor(itemColor);
            ItemPacketService.UpdateItemAfterInfoChange(target, item);
        }
        PacketSendUtility.BroadcastPacket(target, new SM_UPDATE_PLAYER_APPEARANCE(target.GetObjectId(), appearanceItems), true);
        target.GetEquipment().SetPersistentState(Aion.GameServer.Model.GameObjects.IPersistable.PersistentState.UPDATE_REQUIRED);

        if (itemColor == null)
            SendInfo(player, "Removed dyeing from " + target.GetName() + "'s visible equipment.");
        else
            SendInfo(player, "Dyed " + target.GetName() + " (color: " + colorText + ")");

        if (!target.Equals(player))
            SendInfo(target, player.GetName() + " has changed the color of your visible equipment to: " + colorText);
    }

    // Java parity: ((Color) Class.forName("java.awt.Color").getField(NAME).get(null)).getRGB()
    private static int GetAwtColorByName(string name)
    {
        PropertyInfo property = typeof(Color).GetProperty(name, BindingFlags.Public | BindingFlags.Static | BindingFlags.IgnoreCase);
        if (property == null || property.PropertyType != typeof(Color))
            throw new MissingFieldException("java.awt.Color", name);
        if (property.GetValue(null) is not Color color)
            throw new MissingFieldException("java.awt.Color", name);
        return color.ToArgb();
    }
}
