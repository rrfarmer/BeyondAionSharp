using System;
using System.IO;
using System.Xml;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Templates.Items.Enums;
using Aion.GameServer.Model.Templates.Spawns;
using Aion.GameServer.Services;
using Aion.GameServer.Services.Items;
using Aion.GameServer.Utils.ChatHandlers;

namespace Aion.GameServer.Handlers.ConsoleCommands;

/// <summary>
/// Sent in the following cases:
/// - Spawning npcs from the npc tab in the GM Panel (Shift + F1)
/// - Adding items from the item tab in the GM Panel (Shift + F1)
/// - Pressing Ctrl + Shift + Alt while clicking on an item if the console has been activated.
/// Java parity: data/handlers/consolecommands/Wish (ginho1, Neon). Item names are read directly from
/// item_templates.xml (cName attribute) via a streaming reader instead of a JAXB-bound items.xml.
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
            int npcId = FindNpcId(npcName);
            if (npcId == 0)
            {
                SendInfo(admin, "There is no npc with that name.");
                return;
            }
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
            Player target = admin.GetTarget() is Player targetPlayer ? targetPlayer : admin;
            string itemName = paramsArr[0];
            long addCount = 1;
            int enchant = 0;
            if (TryParseInt(paramsArr[0], out int parsedAddCount))
            {
                addCount = parsedAddCount;
                itemName = paramsArr[1];
            }
            else
            {
                TryParseInt(paramsArr[1], out enchant);
            }
            int itemId = FindItemId(itemName);
            if (itemId == 0)
            {
                SendInfo(admin, "There is no item named " + itemName + ".");
                return;
            }
            if (!AdminService.GetInstance().CanOperate(admin, target, itemId, "command ///wish"))
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
                addedCount = addCount - ItemService.AddItem(target, newItem);
            }
            else
            {
                addedCount = addCount - ItemService.AddItem(target, itemId, addCount, true);
            }

            if (addedCount <= 0)
            {
                SendInfo(admin, "Item couldn't be added");
            }
            else if (!admin.Equals(target))
            {
                SendInfo(admin, "You gave " + addedCount + " " + Aion.GameServer.Utils.ChatUtil.Item(itemId) + " to " + target.GetName() + ".");
                SendInfo(target, "You received " + addedCount + " " + Aion.GameServer.Utils.ChatUtil.Item(itemId) + " from " + admin.GetName() + ".");
            }
        }
    }

    private static int FindNpcId(string npcName)
    {
        return FindIdInXml("./data/handlers/consolecommands/data/npcs.xml", "npc", "name", npcName);
    }

    private static int FindItemId(string itemName)
    {
        return FindIdInXml("./data/static_data/items/item_templates.xml", "item_template", "cName", itemName);
    }

    private static int FindIdInXml(string xml, string elementName, string attributeName, string attributeValue)
    {
        try
        {
            using var stream = new StreamReader(xml);
            using var reader = XmlReader.Create(stream);
            while (reader.Read())
            {
                if (reader.NodeType == XmlNodeType.Element && elementName.Equals(reader.LocalName)
                    && attributeValue.Equals(reader.GetAttribute(attributeName), StringComparison.OrdinalIgnoreCase))
                {
                    return JavaNumberParser.ParseInt(reader.GetAttribute("id")!);
                }
            }
        }
        catch (Exception e)
        {
            throw new InvalidOperationException("Failed to search " + xml, e);
        }
        return 0;
    }
}
