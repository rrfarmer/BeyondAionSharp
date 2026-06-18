using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Templates.Items.Enums;
using Aion.GameServer.Model.Templates.Spawns;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;
using Aion.GameServer.Services.Items;
using Aion.GameServer.SpawnEngine;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.ChatHandlers;
using Aion.GameServer.Utils.Xml;

namespace Aion.GameServer.Handlers.ConsoleCommands;

/// <summary>
/// Sent in the following cases:
/// - Spawning npcs from the npc tab in the GM Panel (Shift + F1)
/// - Adding items from the item tab in the GM Panel (Shift + F1)
/// - Pressing Ctrl + Shift + Alt while clicking on an item if the console has been activated.
/// Java parity: data/handlers/consolecommands/Wish (ginho1, Neon).
/// </summary>
public class Wish : ConsoleCommand
{
    public Wish()
        : base("wish", "Spawns npcs and adds items.")
    {
        SetSyntaxInfo(
            "<npc name> - Spawns the specified npc on your targets position.",
            "<count> <item name> - Adds the specified item to your target.",
            "<item name> <enchant> - Adds the specified item with the enchant level to your target.");
    }

    public override void Execute(Player admin, params string[] paramsArr)
    {
        if (paramsArr.Length == 0)
        {
            SendInfo(admin);
            return;
        }

        if (paramsArr.Length == 1)
        { // spawn npc
            string npcName = paramsArr[0];
            FileInfo xml = new FileInfo("./data/handlers/consolecommands/data/npcs.xml");
            NpcData data = JAXBUtil.Deserialize<NpcData>(xml);
            NpcTemplate npcTemplate = data.GetNpcTemplate(npcName);

            if (npcTemplate == null)
            {
                SendInfo(admin, "There is no template with this name");
                return;
            }
            int npcId = npcTemplate.GetTemplateId();
            SpawnTemplate spawn = global::Aion.GameServer.SpawnEngine.SpawnEngine.NewSpawn(admin.GetWorldId(), npcId, admin.GetX(), admin.GetY(), admin.GetZ(),
                admin.GetHeading(), 0);
            VisibleObject visibleObject = global::Aion.GameServer.SpawnEngine.SpawnEngine.SpawnObject(spawn, admin.GetInstanceId());
            if (visibleObject == null)
            {
                SendInfo(admin, "Spawn id " + npcId + " was not found!");
                return;
            }

            string objectName = visibleObject.GetObjectTemplate().GetName();
            SendInfo(admin, objectName + " spawned");
        }
        else
        { // add item
            if (!(admin.GetTarget() is Player player))
            {
                PacketSendUtility.SendPacket(admin, SM_SYSTEM_MESSAGE.STR_INVALID_TARGET());
                return;
            }

            string itemName = paramsArr[0];
            long addCount = 1;
            int itemId;
            int enchant = 0;
            if (long.TryParse(paramsArr[0], out addCount))
            {
                itemName = paramsArr[1];
            }
            else
            {
                int.TryParse(paramsArr[1], out enchant);
            }

            FileInfo xml = new FileInfo("./data/handlers/consolecommands/data/items.xml");
            ItemData data = JAXBUtil.Deserialize<ItemData>(xml);
            ItemTemplate itemTemplate = data.GetItemTemplate(itemName);

            if (itemTemplate != null)
            {
                itemId = itemTemplate.GetTemplateId();
                if (!AdminService.GetInstance().CanOperate(admin, player, itemId, "command ///wish"))
                    return;

                long addedCount;
                if (enchant > 0)
                {
                    global::Aion.GameServer.Model.GameObjects.Item newItem = ItemFactory.NewItem(itemId);

                    if (newItem == null)
                        return;
                    enchant = Math.Min(enchant, 255);
                    if (newItem.GetItemTemplate().GetEquipmentType() != EquipType.PLUME)
                    {
                        if (newItem.GetItemTemplate().CanTune() && newItem.GetItemTemplate().GetMaxEnchantBonus() > 0)
                            enchant = Math.Min(enchant, newItem.GetItemTemplate().GetMaxEnchantLevel());
                        newItem.SetEnchantLevel(enchant);
                        if (enchant > newItem.GetItemTemplate().GetMaxEnchantLevel())
                        {
                            newItem.SetAmplified(true);
                            if (enchant >= 20)
                                newItem.SetBuffSkill(EnchantService.GetEquipBuff(newItem));
                        }
                    }
                    else
                    {
                        newItem.SetTempering(enchant);
                    }
                    addedCount = addCount - ItemService.AddItem(player, newItem);
                }
                else
                {
                    addedCount = addCount - ItemService.AddItem(player, itemId, addCount, true);
                }

                if (addedCount <= 0)
                {
                    SendInfo(admin, "Item couldn't be added");
                }
                else
                {
                    if (!admin.Equals(player))
                    {
                        SendInfo(admin, "You gave " + addedCount + " " + ChatUtil.Item(itemId) + " to " + player.GetName() + ".");
                        SendInfo(player, "You received " + addedCount + " " + ChatUtil.Item(itemId) + " from " + admin.GetName() + ".");
                    }
                }
            }
        }
    }

    [XmlRoot("item")]
    public class ItemTemplate
    {
        [XmlAttribute("id")]
        public string id;

        [XmlAttribute("name")]
        public string name;

        public string GetName() => name;

        // Java parity: afterUnmarshal parsed the @XmlID String id into an int.
        public int GetTemplateId() => int.Parse(id);
    }

    [XmlRoot("items")]
    public class ItemData
    {
        [XmlElement("item")]
        public List<ItemTemplate> its;

        public ItemTemplate GetItemTemplate(string item)
        {
            foreach (ItemTemplate it in GetData())
            {
                if (it.GetName().Equals(item))
                    return it;
            }
            return null;
        }

        protected List<ItemTemplate> GetData() => its;
    }

    [XmlRoot("npc")]
    public class NpcTemplate
    {
        [XmlAttribute("id")]
        public string id;

        [XmlAttribute("name")]
        public string name;

        public string GetName() => name;

        // Java parity: afterUnmarshal parsed the @XmlID String id into an int.
        public int GetTemplateId() => int.Parse(id);
    }

    [XmlRoot("npcs")]
    public class NpcData
    {
        [XmlElement("npc")]
        public List<NpcTemplate> its;

        public NpcTemplate GetNpcTemplate(string npcName)
        {
            foreach (NpcTemplate it in GetData())
            {
                if (it.GetName().ToLower().Equals(npcName.ToLower()))
                    return it;
            }
            return null;
        }

        protected List<NpcTemplate> GetData() => its;
    }
}
