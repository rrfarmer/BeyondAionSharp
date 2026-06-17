using System;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Aion.GameServer.Controllers.Observer;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Templates.Items.Enums;

namespace Aion.GameServer.Model.Templates.Items.Actions;

/// <summary>Java parity: model/templates/item/actions/EnchantItemAction.</summary>
[XmlType("EnchantItemAction")]
public class EnchantItemAction : AbstractItemAction
{
    [XmlAttribute("count")] public int count;
    // Java parity: nullable Integer @XmlAttribute fields. XmlSerializer cannot bind Nullable<T> as an attribute,
    // so each round-trips via a string proxy (null when the attribute is absent).
    [XmlIgnore] public int? min_level;
    [XmlIgnore] public int? max_level;
    [XmlAttribute("min_level")] public string MinLevelRaw { get => min_level?.ToString(); set => min_level = value == null ? (int?)null : int.Parse(value); }
    [XmlAttribute("max_level")] public string MaxLevelRaw { get => max_level?.ToString(); set => max_level = value == null ? (int?)null : int.Parse(value); }
    [XmlAttribute("manastone_only")] public bool manastone_only;
    [XmlAttribute("chance")] public float chance;

    public override bool CanAct(Aion.GameServer.Model.GameObjects.Players.Player player, Item parentItem, Item targetItem, params object[] @params)
    {
        if (IsSupplementAction())
            return false;
        if (parentItem == null)
            return false;
        if (targetItem == null) // no item selected.
        {
            bool isEnchantmentStone2 = parentItem.GetItemTemplate().GetItemGroup() == ItemGroup.ENCHANTMENT;
            Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, isEnchantmentStone2 ? Aion.GameServer.Network.Aion.ServerPackets.SM_SYSTEM_MESSAGE.STR_ENCHANT_ITEM_NO_TARGET_ITEM() : Aion.GameServer.Network.Aion.ServerPackets.SM_SYSTEM_MESSAGE.STR_GIVE_ITEM_OPTION_NO_TARGET_ITEM());
            return false;
        }
        if (parentItem.GetItemTemplate().GetItemGroup() == ItemGroup.ENCHANTMENT)
        {
            if (targetItem.GetItemTemplate().IsNoEnchant())
                return false;
            if (targetItem.GetItemTemplate().GetMaxEnchantLevel() == 0 && !targetItem.GetItemTemplate().CanExceedEnchant())
            {
                Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, Aion.GameServer.Network.Aion.ServerPackets.SM_SYSTEM_MESSAGE.STR_GIVE_ITEM_OPTION_IT_CAN_NOT_BE_GIVEN_OPTION(targetItem.GetItemTemplate().GetL10n(), parentItem.GetItemTemplate().GetL10n()));
                return false;
            }
            else if (!targetItem.IsAmplified() && targetItem.GetEnchantLevel() >= targetItem.GetMaxEnchantLevel())
            {
                Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, Aion.GameServer.Network.Aion.ServerPackets.SM_SYSTEM_MESSAGE.STR_GIVE_ITEM_OPTION_IT_CAN_NOT_BE_GIVEN_OPTION_MORE_TIME(targetItem.GetItemTemplate().GetL10n(), parentItem.GetItemTemplate().GetL10n()));
                return false;
            }
            else if (targetItem.GetEnchantLevel() >= 255)
            {
                Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, Aion.GameServer.Network.Aion.ServerPackets.SM_SYSTEM_MESSAGE.STR_GIVE_ITEM_OPTION_IT_CAN_NOT_BE_GIVEN_OPTION_MORE_TIME(targetItem.GetItemTemplate().GetL10n(), parentItem.GetItemTemplate().GetL10n()));
                return false;
            }
            else if (targetItem.IsAmplified() && Aion.GameServer.Model.Enchants.EnchantmentStoneExtensions.GetByItemId(parentItem.GetItemId()) != Aion.GameServer.Model.Enchants.EnchantmentStone.OMEGA)
            {
                Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, Aion.GameServer.Network.Aion.ServerPackets.SM_SYSTEM_MESSAGE.STR_MSG_EXCEED_CANNOT_02(parentItem.GetItemTemplate().GetL10n()));
                return false;
            }
        }
        int msID = parentItem.GetItemTemplate().GetTemplateId() / 1000000;
        int tID = targetItem.GetItemTemplate().GetTemplateId() / 1000000;
        return (msID == 167 || msID == 166) && tID < 120;
    }

    public override void Act(Aion.GameServer.Model.GameObjects.Players.Player player, Item parentItem, Item targetItem, params object[] @params)
    {
        Act(player, parentItem, targetItem, null, 1);
    }

    // necessary overloading to not change AbstractItemAction
    public void Act(Aion.GameServer.Model.GameObjects.Players.Player player, Item parentItem, Item targetItem, Item supplementItem, int targetWeapon)
    {
        if (supplementItem != null && !CheckSupplementLevel(player, supplementItem.GetItemTemplate(), targetItem.GetItemTemplate()))
            return;

        bool isEnchantmentStone = parentItem.GetItemTemplate().GetItemGroup() == ItemGroup.ENCHANTMENT;
        int enchantDurationMillis = isEnchantmentStone ? 4000 : 2000;

        StartMovingListener move = new EnchantStartMovingListener(player, targetItem, isEnchantmentStone);
        player.GetObserveController().Attach(move);

        // Current enchant level
        int currentEnchant = targetItem.GetEnchantLevel();
        bool isSuccess = IsSuccess(player, parentItem, targetItem, supplementItem, targetWeapon);
        // Item template
        Aion.GameServer.Utils.PacketSendUtility.BroadcastPacketAndReceive(player, new Aion.GameServer.Network.Aion.ServerPackets.SM_ITEM_USAGE_ANIMATION(player.GetObjectId(), targetItem.GetObjectId(), parentItem.GetObjectId(), parentItem.GetItemTemplate().GetTemplateId(), enchantDurationMillis, 0, 0, 1, 0, 0));

        player.GetController().AddTask(Aion.GameServer.Model.TaskId.ITEM_USE, Aion.GameServer.Utils.ThreadPoolManager.GetInstance().Schedule(ct =>
        {
            player.GetObserveController().RemoveObserver(move);

            if (player.GetInventory().GetItemByObjId(targetItem.GetObjectId()) == null && !targetItem.IsEquipped())
            {
                Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, Aion.GameServer.Network.Aion.ServerPackets.SM_SYSTEM_MESSAGE.STR_ENCHANT_ITEM_NO_TARGET_ITEM());
                Aion.GameServer.Utils.PacketSendUtility.BroadcastPacketAndReceive(player, new Aion.GameServer.Network.Aion.ServerPackets.SM_ITEM_USAGE_ANIMATION(player.GetObjectId(), parentItem.GetObjectId(), parentItem.GetItemTemplate().GetTemplateId(), 0, 2, 0));
                return ValueTask.CompletedTask;
            }

            if (isEnchantmentStone)
                Aion.GameServer.Services.EnchantService.EnchantItemAct(player, parentItem, targetItem, supplementItem, currentEnchant, isSuccess);
            else // Manastone
                Aion.GameServer.Services.EnchantService.SocketManastoneAct(player, parentItem, targetItem, supplementItem, targetWeapon, isSuccess);

            Aion.GameServer.Utils.PacketSendUtility.BroadcastPacketAndReceive(player, new Aion.GameServer.Network.Aion.ServerPackets.SM_ITEM_USAGE_ANIMATION(player.GetObjectId(), parentItem.GetObjectId(), parentItem.GetItemTemplate().GetTemplateId(), 0, isSuccess ? 1 : 2, 0));
            if (Aion.GameServer.Configs.Main.CustomConfig.ENABLE_ENCHANT_ANNOUNCE)
            {
                if (isEnchantmentStone && isSuccess && (targetItem.GetEnchantLevel() == 15 || targetItem.GetEnchantLevel() == 20))
                {
                    Aion.GameServer.Network.Aion.ServerPackets.SM_SYSTEM_MESSAGE packet;
                    if (targetItem.GetEnchantLevel() == 15)
                        packet = Aion.GameServer.Network.Aion.ServerPackets.SM_SYSTEM_MESSAGE.STR_MSG_ENCHANT_ITEM_SUCCEEDED_15(player.GetName(), targetItem.GetItemTemplate().GetL10n());
                    else
                        packet = Aion.GameServer.Network.Aion.ServerPackets.SM_SYSTEM_MESSAGE.STR_MSG_ENCHANT_ITEM_SUCCEEDED_20(player.GetName(), targetItem.GetItemTemplate().GetL10n());
                    Aion.GameServer.Utils.PacketSendUtility.BroadcastToWorld(packet, Aion.GameServer.Utils.Collections.Predicates.Players.SameRace(player));
                }
            }
            return ValueTask.CompletedTask;
        }, TimeSpan.FromMilliseconds(enchantDurationMillis)));
    }

    /// <summary>Check if the item enchant will be successful.</summary>
    private bool IsSuccess(Aion.GameServer.Model.GameObjects.Players.Player player, Item parentItem, Item targetItem, Item supplementItem, int targetWeapon)
    {
        if (parentItem.GetItemTemplate() != null)
        {
            // Item template
            Aion.GameServer.Model.Templates.Items.ItemTemplate itemTemplate = parentItem.GetItemTemplate();
            // Enchantment stone
            if (itemTemplate.GetItemGroup() == ItemGroup.ENCHANTMENT)
            {
                return Aion.GameServer.Services.EnchantService.EnchantItem(player, parentItem, targetItem, supplementItem);
            }
            // Manastone
            return Aion.GameServer.Services.EnchantService.SocketManastone(player, parentItem, targetItem, supplementItem, targetWeapon);
        }
        return false;
    }

    public int GetCount()
    {
        return count;
    }

    public int GetMaxLevel()
    {
        return max_level != null ? max_level.Value : 0;
    }

    public int GetMinLevel()
    {
        return min_level != null ? min_level.Value : 0;
    }

    public bool IsManastoneOnly()
    {
        return manastone_only;
    }

    public float GetChance()
    {
        return chance;
    }

    public bool IsSupplementAction()
    {
        return GetMinLevel() > 0 || GetMaxLevel() > 0 || GetChance() > 0 || IsManastoneOnly();
    }

    private bool CheckSupplementLevel(Aion.GameServer.Model.GameObjects.Players.Player player, Aion.GameServer.Model.Templates.Items.ItemTemplate supplementTemplate, Aion.GameServer.Model.Templates.Items.ItemTemplate targetItemTemplate)
    {
        // Is item manastone? True - check if player can use supplement
        if (supplementTemplate.GetItemGroup() != ItemGroup.ENCHANTMENT)
        {
            // Check if max item level is ok for the enchant
            int minEnchantLevel = targetItemTemplate.GetLevel();
            int maxEnchantLevel = targetItemTemplate.GetLevel();

            EnchantItemAction action = supplementTemplate.GetActions().GetEnchantAction();
            if (action != null)
            {
                if (action.GetMinLevel() != 0)
                    minEnchantLevel = action.GetMinLevel();
                if (action.GetMaxLevel() != 0)
                    maxEnchantLevel = action.GetMaxLevel();
            }

            if (minEnchantLevel <= targetItemTemplate.GetLevel() && maxEnchantLevel >= targetItemTemplate.GetLevel())
                return true;

            Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, Aion.GameServer.Network.Aion.ServerPackets.SM_SYSTEM_MESSAGE.STR_ITEM_ENCHANT_ASSISTANT_NO_RIGHT_ITEM());
            return false;
        }
        return true;
    }

    // Java parity: anonymous StartMovingListener in act().
    private sealed class EnchantStartMovingListener : StartMovingListener
    {
        private readonly Aion.GameServer.Model.GameObjects.Players.Player player;
        private readonly Item targetItem;
        private readonly bool isEnchantmentStone;

        public EnchantStartMovingListener(Aion.GameServer.Model.GameObjects.Players.Player player, Item targetItem, bool isEnchantmentStone)
        {
            this.player = player;
            this.targetItem = targetItem;
            this.isEnchantmentStone = isEnchantmentStone;
        }

        public override void Moved()
        {
            base.Moved();
            player.GetObserveController().RemoveObserver(this);
            player.GetController().CancelUseItem();
            Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, isEnchantmentStone ? Aion.GameServer.Network.Aion.ServerPackets.SM_SYSTEM_MESSAGE.STR_ENCHANT_ITEM_CANCELED(targetItem.GetL10n()) : Aion.GameServer.Network.Aion.ServerPackets.SM_SYSTEM_MESSAGE.STR_GIVE_ITEM_OPTION_CANCELED(targetItem.GetL10n()));
        }
    }
}
