using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.GameObjects.State;
using Aion.GameServer.Model.Items;
using Aion.GameServer.Model.Templates.Items;
using Aion.GameServer.Model.Templates.Items.Actions;
using Aion.GameServer.Model.Templates.Items.Enums;
using Aion.GameServer.Model.Templates.Itemset;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.ChatHandlers;

namespace Aion.GameServer.Handlers.PlayerCommands;

/// <summary>Java parity: data/handlers/playercommands/Preview (Neon). Previews equipment and emotion cards.</summary>
public class Preview : PlayerCommand
{
    private static readonly ConcurrentDictionary<int, ScheduledTask> PREVIEW_RESETS = new();
    private const int PREVIEW_TIME_SECONDS = 10;

    public Preview()
        : base("preview", "Previews equipment and emotion cards.")
    {
        SetSyntaxInfo(
            "<emotion card item> - Previews the emotion.",
            "<color> - Previews your equipped items in the specified color (dye item, color name or color HEX code).",
            "<item(s)> [color] - Previews the specified equipment on your character (default: standard item color, optional: dye item, color name or color HEX code).",
            "Multiple items can be separated by commas or spaces.",
            "If a single item is given and it's a part of an item set, you will get a preview of the whole item set."
        );
    }

    public override void Execute(Player player, params string[] paramsArr)
    {
        if (paramsArr.Length == 0)
        {
            SendInfo(player);
            return;
        }

        List<ItemParam> itemParams = Parse(paramsArr);
        if (!PreviewEmotion(player, itemParams[0]))
            PreviewEquipment(player, itemParams);
    }

    private bool PreviewEmotion(Player player, ItemParam itemParam)
    {
        if (itemParam.ItemTemplate == null || itemParam.ItemTemplate.GetActions() == null)
            return false;
        foreach (AbstractItemAction itemAction in itemParam.ItemTemplate.GetActions().GetItemActions())
        {
            if (itemAction is EmotionLearnAction emotionLearnAction)
            {
                if ((player.GetState() & ~(int)CreatureState.POWERSHARD) > (int)CreatureState.ACTIVE) // prevent bugged animations
                {
                    PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_SKILL_CAN_NOT_CAST_IN_CURRENT_STANCE());
                }
                else
                {
                    int targetObjectId = player.GetTarget() != null ? player.GetTarget().GetObjectId() : 0;
                    PacketSendUtility.SendPacket(player, new SM_EMOTION(player, EmotionType.EMOTE_END));
                    PacketSendUtility.SendPacket(player, new SM_EMOTION(player, EmotionType.EMOTE, emotionLearnAction.GetEmotionId(), targetObjectId));
                }
                return true;
            }
        }
        return false;
    }

    private void PreviewEquipment(Player player, List<ItemParam> itemParams)
    {
        int? itemColor = null; // null = default item color
        string colorText = "default";
        foreach (ItemParam itemParam in itemParams)
        {
            itemColor = itemParam.DyeColor();
            if (itemColor != null)
            {
                if (itemParam.ItemTemplate == null)
                    colorText = ChatUtil.Color("#" + (itemColor.Value & 0xFFFFFF).ToString("X6", CultureInfo.InvariantCulture), itemColor.Value);
                else
                    colorText = ChatUtil.Item(itemParam.ItemTemplate.GetTemplateId());
                itemParams.Remove(itemParam);
                break;
            }
        }
        PreviewEquipment(player, itemParams.Select(ip => ip.ItemTemplate).ToList(), itemColor, colorText);
    }

    private void PreviewEquipment(Player player, List<ItemTemplate> equipment, int? itemColor, string colorText)
    {
        if (!equipment.All(itemTemplate => ValidateForPreview(player, itemTemplate)))
            return;
        if (itemColor != null && equipment.Count != 0 && !equipment.Any(t => t.IsItemDyePermitted()))
        {
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_ITEM_COLOR_CHANGE_ERROR_CANNOTDYE(equipment[0].GetL10n()));
            return;
        }
        string itemNames = "";
        long previewItemsSlotMask = 0;
        List<Item> previewItems = new List<Item>();
        if (equipment.Count == 1 && equipment[0].IsItemSet()) // preview whole set
        {
            ItemSetTemplate itemSet = equipment[0].GetItemSet();
            foreach (ItemPart part in itemSet.GetItempart())
                previewItemsSlotMask |= AddFakeItem(previewItems, previewItemsSlotMask, part.GetItemId(), itemColor);
        }
        else
        {
            foreach (ItemTemplate template in equipment)
                previewItemsSlotMask |= AddFakeItem(previewItems, previewItemsSlotMask, template.GetTemplateId(), itemColor);
        }
        int previewRobotId = 0;
        foreach (Item previewItem in previewItems)
        {
            itemNames += "\n\t" + ChatUtil.Item(previewItem.GetItemId());
            if (player.IsInRobotMode() && previewItem.GetItemTemplate().GetItemGroup() == ItemGroup.KEYBLADE)
                previewRobotId = previewItem.GetItemTemplate().GetRobotId();
        }

        AddOwnEquipment(player, previewItems, previewItemsSlotMask, itemColor);
        previewItems.Sort((a, b) => a.GetEquipmentSlot().CompareTo(b.GetEquipmentSlot())); // order by equipment slot ids (ascending) to avoid display bugs
        int display = player.GetPlayerSettings().GetDisplay() | SM_CUSTOM_SETTINGS.HIDE_LEGION_CLOAK;
        if (previewItems.Any(item => item.GetEquipmentSlot() == ItemSlot.HELMET.GetSlotIdMask()))
        {
            display &= ~SM_CUSTOM_SETTINGS.HIDE_HELMET;
        }
        if (previewItems.Any(item => item.GetEquipmentSlot() == ItemSlot.PLUME.GetSlotIdMask()))
        {
            display &= ~SM_CUSTOM_SETTINGS.HIDE_PLUME;
        }
        PacketSendUtility.SendPacket(player, new SM_CUSTOM_SETTINGS(player.GetObjectId(), 1, display, player.GetPlayerSettings().GetDeny()));
        PacketSendUtility.SendPacket(player, new SM_UPDATE_PLAYER_APPEARANCE(player.GetObjectId(), previewItems));
        int switchRobotAnimationSeconds = 0;
        if (previewRobotId != 0)
        {
            switchRobotAnimationSeconds = 1;
            UpdateRobotAppearance(player, previewRobotId);
        }
        SchedulePreviewReset(player, PREVIEW_TIME_SECONDS + switchRobotAnimationSeconds, previewRobotId != 0);
        if (equipment.Count == 0)
            SendInfo(player, "Previewing your equipment for " + PREVIEW_TIME_SECONDS + " seconds in color " + colorText);
        else
            SendInfo(player, "Previewing the following items for " + PREVIEW_TIME_SECONDS + " seconds (color: " + colorText + "):" + itemNames);
    }

    private bool ValidateForPreview(Player player, ItemTemplate itemTemplate)
    {
        if (itemTemplate == null)
        {
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_CHANGE_ITEM_SKIN_NO_TARGET_ITEM());
            return false;
        }
        else if (itemTemplate.GetEquipmentType() == EquipType.NONE)
        {
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_CHANGE_ITEM_SKIN_PREVIEW_INVALID_COSMETIC());
            return false;
        }
        else if (itemTemplate.GetRace() == player.GetOppositeRace())
        {
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_PREVIEW_INVALID_RACE());
            return false;
        }
        else if (itemTemplate.GetUseLimits().GetGenderPermitted() != null && itemTemplate.GetUseLimits().GetGenderPermitted() != player.GetGender())
        {
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_PREVIEW_INVALID_GENDER());
            return false;
        }
        else if (!ItemSlotExtensions.IsVisible(itemTemplate.GetItemSlot()))
        {
            SendInfo(player, itemTemplate.GetL10n() + " is no visible equipment.");
            return false;
        }
        return true;
    }

    private List<ItemParam> Parse(string[] paramsArr)
    {
        List<ItemParam> itemParams = new List<ItemParam>();
        foreach (string param in paramsArr)
        {
            string[] ids = System.Text.RegularExpressions.Regex.Split(param, @",|(?<=[^,])(?=\[)|(?<=[\]])(?=[^\[])"); // split on comma and between item tags (square brackets)
            foreach (string id in ids)
            {
                itemParams.Add(new ItemParam(id, DataManager.ITEM_DATA.GetItemTemplate(ChatUtil.GetItemId(id))));
            }
        }
        return itemParams;
    }

    /// <summary>Equipment slot mask of the preview item, 0 if it was not added.</summary>
    private static long AddFakeItem(List<Item> items, long previewItemsSlotMask, int itemId, int? itemColor)
    {
        ItemTemplate itemTemplate = DataManager.ITEM_DATA.GetItemTemplate(itemId);
        long itemSlotMask = itemTemplate.GetItemSlot();
        if (!ItemSlotExtensions.IsVisible(itemSlotMask)) // don't add invisible items (like rings or belts)
            return 0;
        long occupiedSlots = previewItemsSlotMask & itemSlotMask;
        if (occupiedSlots == itemSlotMask) // an item of that kind is already present in the list
            return 0;
        if (itemTemplate.IsTwoHandWeapon() && occupiedSlots != 0) // only allow two-handed if both hands are free
            return 0;
        if (!itemTemplate.IsTwoHandWeapon())
            itemSlotMask = GetFirstFreeSlot(itemSlotMask, occupiedSlots); // select the correct slot for CL_MULTISLOT, weapons, earrings and power shards
        Item previewItem = new Item(0, itemTemplate, 1, true, itemSlotMask); // ObjId 0 to avoid allocating new IDFactory IDs (it'll not be used anywhere)
        if (itemTemplate.IsItemDyePermitted())
            previewItem.SetItemColor(itemColor);
        items.Add(previewItem);
        return itemSlotMask;
    }

    private static long GetFirstFreeSlot(long targetSlots, long occupiedSlots)
    {
        long freeSlots = targetSlots & ~occupiedSlots;
        return freeSlots & (-freeSlots); // Long.lowestOneBit
    }

    private static void AddOwnEquipment(Player player, List<Item> previewItems, long previewItemsSlotMask, int? itemColor)
    {
        bool previewContainsMainHandWeapon = (previewItemsSlotMask & ItemSlot.MAIN_HAND.GetSlotIdMask()) != 0;
        foreach (Item visibleEquipment in player.GetEquipment().GetEquippedForAppearance())
        {
            if (previewContainsMainHandWeapon && visibleEquipment.GetItemTemplate().IsOneHandWeapon())
                continue; // don't show own weapon in off-hand if player wants to preview a specific weapon
            previewItemsSlotMask |= AddFakeItem(previewItems, previewItemsSlotMask, visibleEquipment.GetItemId(), itemColor);
        }
    }

    private static void SchedulePreviewReset(Player player, int duration, bool previewRobot)
    {
        PREVIEW_RESETS.AddOrUpdate(player.GetObjectId(),
            _ => CreateResetTask(player, duration, previewRobot),
            (_, resetTask) =>
            {
                if (resetTask != null) // cancel previous scheduled preview reset thread
                {
                    if (!previewRobot && player.IsInRobotMode()) // restore robot appearance in case it was previewed just a few seconds ago
                        UpdateRobotAppearance(player, player.GetRobotId());
                    resetTask.Cancel(true);
                }
                return CreateResetTask(player, duration, previewRobot);
            });
    }

    private static ScheduledTask CreateResetTask(Player player, int duration, bool previewRobot)
    {
        return Aion.GameServer.Utils.ThreadPoolManager.GetInstance().Schedule(_ =>
        {
            PacketSendUtility.SendPacket(player, new SM_CUSTOM_SETTINGS(player));
            PacketSendUtility.SendPacket(player, new SM_UPDATE_PLAYER_APPEARANCE(player.GetObjectId(), player.GetEquipment().GetEquippedForAppearance()));
            if (previewRobot && player.IsInRobotMode())
                UpdateRobotAppearance(player, player.GetRobotId());
            PacketSendUtility.SendMessage(player, "Preview time ended.");
            PREVIEW_RESETS.TryRemove(player.GetObjectId(), out ScheduledTask _);
            return System.Threading.Tasks.ValueTask.CompletedTask;
        }, duration * 1000L);
    }

    private static void UpdateRobotAppearance(Player player, int robotId)
    {
        PacketSendUtility.SendPacket(player, new SM_RIDE_ROBOT(player, 0));
        PacketSendUtility.SendPacket(player, new SM_RIDE_ROBOT(player, robotId));
    }

    private sealed class ItemParam
    {
        public string Input { get; }
        public ItemTemplate ItemTemplate { get; }

        public ItemParam(string input, ItemTemplate itemTemplate)
        {
            Input = input;
            ItemTemplate = itemTemplate;
        }

        public int? DyeColor()
        {
            if (ItemTemplate != null)
            {
                if (ItemTemplate.GetActions() == null || ItemTemplate.GetActions().GetDyeAction() == null)
                    return null;
                return ItemTemplate.GetActions().GetDyeAction().GetColor();
            }
            string colorParam = Input;
            try
            {
                // try to get color by name
                PropertyInfo field = typeof(Color).GetProperty(ToTitleCaseColorName(colorParam), BindingFlags.Public | BindingFlags.Static);
                if (field != null && field.PropertyType == typeof(Color))
                    return ((Color)field.GetValue(null)).ToArgb();
                throw new MissingFieldException();
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
                try
                {
                    return Convert.ToInt32(colorParam, 16);
                }
                catch (Exception)
                {
                    return null;
                }
            }
        }

        // Java parity: Color.class.getField(name.toUpperCase()) — java.awt.Color exposes static fields like RED/BLUE.
        // System.Drawing.Color exposes named colors as PascalCase static properties, so map UPPERCASE input to the property name.
        private static string ToTitleCaseColorName(string name)
        {
            if (string.IsNullOrEmpty(name))
                return name;
            return char.ToUpper(name[0]) + name.Substring(1).ToLowerInvariant();
        }
    }
}
