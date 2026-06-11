using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Aion.GameServer.Controllers.Observer;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Templates.Items;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aion.GameServer.Model.Templates.Items.Actions;

/// <summary>Java parity: model/templates/item/actions/DecomposeAction (oslo(a00441234)).</summary>
[XmlType("DecomposeAction")]
public class DecomposeAction : AbstractItemAction
{
    private static readonly ILogger log = NullLogger.Instance;
    public const int USAGE_DELAY = 3000;
    private static readonly Dictionary<Race, int[]> chunkEarth = new();
    private static readonly Dictionary<Race, int[]> chunkSand = new();
    private static readonly Dictionary<Race, int[]> premiumOphidanRecipe = new();

    private static readonly int[] chunkRock = { 152000104, 152000107, 152000113, 152000204, 152000207, 152000214, 152000307, 152000309, 152000311, 152000313,
        152000315, 152000317, 152000320, 152000322, 152000324 };

    private static readonly int[] chunkGemstone = { 152000112, 152000116, 152000212, 152000213, 152000217, 152000326, 152000327, 152000328 };

    private static readonly int[] scrolls = { 164000073, 164000134, 164000076, 164000079, 164000122, 164000131, 164000118 };

    private static readonly int[] potion = { 162000045, 162000079, 162000016, 162000021, 162000027, 162000023 };

    private static readonly int[] lesser_potions = { 162000003, 162000008, 162000042, 162000022, 162000013, 162000018, 162000047 };

    private static readonly int[] potion_50 = { 162000075, 162000076, 162000077, 162000078, 162000079, 162000080, 162000081 };

    private static readonly int[] illusion_godstones = { 168000161, 168000162, 168000163, 168000164, 168000165, 168000166, 168000167, 168000168, 168000169,
        168000170, 168000171, 168000172, 168000173, 168000174, 168000175, 168000176, 168000177 };

    static DecomposeAction()
    {
        chunkEarth[Race.ASMODIANS] =
            new int[] { 152000051, 152000052, 152000053, 152000054, 152000055, 152000056, 152000057, 152000058, 152000059, 152000061, 152000062, 152000063,
                152000101, 152000102, 152000104, 152000107, 152000113, 152000201, 152000202, 152000204, 152000207, 152000214, 152000451, 152000453, 152000455,
                152000457, 152000459, 152000461, 152000463, 152000465, 152000468, 152000470, 152000551, 152000552, 152000553, 152000554, 152000556, 152000651,
                152000652, 152000653, 152000654, 152000656, 152000751, 152000752, 152000753, 152000754, 152000755, 152000756, 152000757, 152000758, 152000759,
                152000760, 152000762, 152000763, 152000851, 152000852, 152000853, 152000854, 152000855, 152000856, 152000857, 152000858, 152000860, 152000861,
                152001051, 152001052, 152001053, 152001055, 152001056 };
        chunkEarth[Race.ELYOS] =
            new int[] { 152000001, 152000002, 152000003, 152000004, 152000005, 152000006, 152000007, 152000008, 152000009, 152000010, 152000011, 152000012,
                152000101, 152000102, 152000104, 152000107, 152000113, 152000201, 152000202, 152000204, 152000207, 152000214, 152000401, 152000403, 152000405,
                152000407, 152000409, 152000411, 152000413, 152000415, 152000417, 152000419, 152000501, 152000502, 152000503, 152000504, 152000505, 152000601,
                152000602, 152000603, 152000604, 152000605, 152000701, 152000702, 152000703, 152000704, 152000705, 152000706, 152000707, 152000708, 152000709,
                152000710, 152000711, 152000712, 152000801, 152000802, 152000803, 152000804, 152000805, 152000806, 152000807, 152000808, 152000809, 152000810,
                152001001, 152001002, 152001003, 152001004, 152001005 };

        chunkSand[Race.ASMODIANS] =
            new int[] { 152000452, 152000454, 152000301, 152000302, 152000303, 152000456, 152000458, 152000103, 152000203, 152000304, 152000305, 152000306,
                152000460, 152000462, 152000105, 152000205, 152000307, 152000309, 152000311, 152000464, 152000466, 152000108, 152000208, 152000313, 152000315,
                152000317, 152000469, 152000471, 152000114, 152000215, 152000320, 152000322, 152000324 };
        chunkSand[Race.ELYOS] =
            new int[] { 152000402, 152000404, 152000301, 152000302, 152000303, 152000406, 152000408, 152000103, 152000203, 152000304, 152000305, 152000306,
                152000410, 152000412, 152000105, 152000205, 152000307, 152000309, 152000311, 152000414, 152000416, 152000108, 152000208, 152000313, 152000315,
                152000317, 152000418, 152000420, 152000114, 152000215, 152000320, 152000322, 152000324 };

        premiumOphidanRecipe[Race.ASMODIANS] =
            new int[] { 152230698, 152230699, 152230700, 152230701, 152230702, 152230703, 152230704, 152230759, 152230760, 152230761, 152230762, 152230763,
                152230764, 152230839, 152230840, 152230841, 152230842, 152230843, 152230844, 152230845, 152231021, 152231022, 152231023, 152231107, 152231108,
                152231253, 152231254, 152231255, 152231256, 152231257, 152231258, 152231313, 152231314, 152231315, 152231316, 152231317, 152231318, 152231385,
                152231386, 152231387, 152231388, 152231389, 152231390, 152231403, 152231404, 152231405, 152231406, 152231407, 152231408, 152231421, 152231422,
                152231423, 152231424, 152231425, 152231426, 152231439, 152231440, 152231441, 152231442, 152231443, 152231444, 152231566 };
        premiumOphidanRecipe[Race.ELYOS] =
            new int[] { 152220709, 152220710, 152220711, 152220712, 152220713, 152220714, 152220715, 152220770, 152220771, 152220772, 152220773, 152220774,
                152220775, 152220850, 152220851, 152220852, 152220853, 152220854, 152220855, 152220856, 152221032, 152221033, 152221034, 152221118, 152221119,
                152221264, 152221265, 152221266, 152221267, 152221268, 152221269, 152221324, 152221325, 152221326, 152221327, 152221328, 152221329, 152221396,
                152221397, 152221398, 152221399, 152221400, 152221401, 152221414, 152221415, 152221416, 152221417, 152221418, 152221419, 152221432, 152221433,
                152221434, 152221435, 152221436, 152221437, 152221450, 152221451, 152221452, 152221453, 152221454, 152221455, 152221576 };
    }

    public override bool CanAct(Aion.GameServer.Model.GameObjects.Players.Player player, Item parentItem, Item targetItem, params object[] @params)
    {
        if (player.IsDead() || !player.IsSpawned())
            return false;
        List<ExtractedItemsCollection> itemsCollections = null;
        itemsCollections = DataManager.DECOMPOSABLE_ITEMS_DATA.GetInfoByItemId(parentItem.GetItemId());
        if (itemsCollections == null || itemsCollections.Count == 0)
        {
            if (DataManager.DECOMPOSABLE_ITEMS_DATA.GetSelectableItems(parentItem.GetItemId()) != null) // selectable decomposable
                return true;
            Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, Aion.GameServer.Network.Aion.ServerPackets.SmSystemMessage.STR_DECOMPOSE_ITEM_IT_CAN_NOT_BE_DECOMPOSED(parentItem.GetL10n()));
            return false;
        }
        if (player.GetInventory().IsFull() || player.GetInventory().IsFullSpecialCube() && ContainsSpecialCubeItems(itemsCollections, player))
        {
            Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, Aion.GameServer.Network.Aion.ServerPackets.SmSystemMessage.STR_DECOMPOSE_ITEM_INVENTORY_IS_FULL());
            return false;
        }
        return true;
    }

    public override void Act(Aion.GameServer.Model.GameObjects.Players.Player player, Item parentItem, Item targetItem, params object[] @params)
    {
        player.GetController().CancelUseItem();
        ICollection<ResultedItem> selectable = DataManager.DECOMPOSABLE_ITEMS_DATA.GetSelectableItems(parentItem.GetItemId());
        if (selectable != null)
        {
            // Java parity: selectable.removeIf(item -> !item.isObtainableFor(player));
            foreach (ResultedItem item in selectable.Where(item => !item.IsObtainableFor(player)).ToList())
                selectable.Remove(item);
            Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, new Aion.GameServer.Network.Aion.ServerPackets.SmFirstShowDecomposable(parentItem.GetObjectId(), selectable));
            return;
        }
        List<ExtractedItemsCollection> itemsCollections = DataManager.DECOMPOSABLE_ITEMS_DATA.GetInfoByItemId(parentItem.GetItemId());
        ICollection<ExtractedItemsCollection> levelSuitableItems = FilterItemsByLevel(player, itemsCollections);
        ExtractedItemsCollection selectedCollection = Chance.SelectElement(levelSuitableItems);
        if (selectedCollection.GetRandomItems().Count == 0 && !selectedCollection.GetItems().Any(i => i.IsObtainableFor(player)))
        {
            log.LogWarning(
                "Empty decomposable " + parentItem.GetItemId() + " for " + player + ", class: " + player.GetPlayerClass() + ", level: " + player.GetLevel());
            Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, Aion.GameServer.Network.Aion.ServerPackets.SmSystemMessage.STR_DECOMPOSE_ITEM_FAILED(parentItem.GetL10n()));
            return;
        }

        Aion.GameServer.Utils.PacketSendUtility.BroadcastPacket(player,
            new Aion.GameServer.Network.Aion.ServerPackets.SmItemUsageAnimation(player.GetObjectId(), parentItem.GetObjectId(), parentItem.GetItemId(), USAGE_DELAY, 0, 0), true);

        ItemUseObserver observer = new DecomposeUseObserver(player, parentItem);

        player.GetObserveController().Attach(observer);
        player.GetController().AddTask(TaskId.ITEM_USE, Aion.GameServer.Utils.ThreadPoolManager.GetInstance().Schedule(ct =>
        {
            player.GetObserveController().RemoveObserver(observer);
            bool validAction = PostValidate(player, parentItem, targetItem);
            if (validAction)
            {
                Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, Aion.GameServer.Network.Aion.ServerPackets.SmSystemMessage.STR_DECOMPOSE_ITEM_SUCCEED(parentItem.GetL10n()));
                foreach (ResultedItem resultItem in selectedCollection.GetItems())
                {
                    if (resultItem.IsObtainableFor(player))
                    {
                        int count = Aion.GameServer.Commons.Utils.Rnd.Get(resultItem.GetMinCount(), resultItem.GetMaxCount());
                        Aion.GameServer.Services.Items.ItemService.AddItem(player, resultItem.GetItemId(), count, true,
                            new Aion.GameServer.Services.Items.ItemService.ItemUpdatePredicate(Aion.GameServer.Services.Items.ItemPacketService.ItemAddType.DECOMPOSABLE, Aion.GameServer.Services.Items.ItemPacketService.ItemUpdateType.INC_ITEM_COLLECT));
                    }
                }
                foreach (RandomItem randomItem in selectedCollection.GetRandomItems())
                {
                    RandomType randomType = randomItem.Type;
                    {
                        int randomId = 0;
                        int i = 0;
                        int itemLvl = parentItem.GetItemTemplate().GetLevel();
                        switch (randomItem.Type)
                        {
                            case RandomType.ENCHANTMENT:
                                do
                                {
                                    randomId = 166000191 + (int)Math.Floor(itemLvl / 100f + 0.5f) + Aion.GameServer.Commons.Utils.Rnd.NextInt(4);
                                    i++;
                                    if (i > 50)
                                    {
                                        randomId = 0;
                                        break;
                                    }
                                } while (!IsValidItemId(randomId));
                                break;
                            case RandomType.MANASTONE:
                            case RandomType.MANASTONE_COMMON_GRADE_10:
                            case RandomType.MANASTONE_COMMON_GRADE_20:
                            case RandomType.MANASTONE_COMMON_GRADE_30:
                            case RandomType.MANASTONE_COMMON_GRADE_40:
                            case RandomType.MANASTONE_COMMON_GRADE_50:
                            case RandomType.MANASTONE_COMMON_GRADE_60:
                            case RandomType.MANASTONE_COMMON_GRADE_70:
                            case RandomType.MANASTONE_RARE_GRADE_10:
                            case RandomType.MANASTONE_RARE_GRADE_20:
                            case RandomType.MANASTONE_RARE_GRADE_30:
                            case RandomType.MANASTONE_RARE_GRADE_40:
                            case RandomType.MANASTONE_RARE_GRADE_50:
                            case RandomType.MANASTONE_RARE_GRADE_60:
                            case RandomType.MANASTONE_RARE_GRADE_70:
                            case RandomType.MANASTONE_LEGEND_GRADE_10:
                            case RandomType.MANASTONE_LEGEND_GRADE_20:
                            case RandomType.MANASTONE_LEGEND_GRADE_30:
                            case RandomType.MANASTONE_LEGEND_GRADE_40:
                            case RandomType.MANASTONE_LEGEND_GRADE_50:
                            case RandomType.MANASTONE_LEGEND_GRADE_60:
                            case RandomType.MANASTONE_LEGEND_GRADE_70:
                            {
                                if (randomType == RandomType.MANASTONE) // stone level near or equal to item level (if 1, near player level)
                                    itemLvl = itemLvl % 10 == 0 ? itemLvl : ((int)Math.Ceiling((itemLvl == 1 ? player.GetLevel() : itemLvl) / 10f) * 10);
                                else
                                    itemLvl = randomType.GetLevel();
                                List<ItemTemplate> stones = DataManager.ITEM_DATA.GetManastones(itemLvl);
                                if (stones == null)
                                {
                                    log.LogWarning("No lv" + itemLvl + " manastones found for decomposable random type " + randomItem.Type);
                                    break;
                                }
                                if (randomType != RandomType.MANASTONE)
                                {
                                    ItemQuality itemQuality;
                                    if (randomType.ToString().Contains("RARE"))
                                        itemQuality = ItemQuality.RARE;
                                    else if (randomType.ToString().Contains("LEGEND"))
                                        itemQuality = ItemQuality.LEGEND;
                                    else
                                        itemQuality = ItemQuality.COMMON;
                                    List<ItemTemplate> selectedStones = stones
                                        .Where(t => t.GetItemQuality() == itemQuality && !t.GetName().Contains(" MP ")).ToList();
                                    randomId = Aion.GameServer.Commons.Utils.Rnd.Get(selectedStones).GetTemplateId();
                                }
                                else
                                {
                                    List<ItemTemplate> selectedStones = stones
                                        .Where(t => t.GetItemQuality() != ItemQuality.LEGEND && !t.GetName().Contains(" MP ")).ToList();
                                    randomId = Aion.GameServer.Commons.Utils.Rnd.Get(selectedStones).GetTemplateId();
                                }
                                break;
                            }
                            case RandomType.SPECIAL_MANASTONE_RARE_GRADE:
                            case RandomType.SPECIAL_MANASTONE_LEGEND_GRADE:
                            case RandomType.SPECIAL_MANASTONE_UNIQUE_GRADE:
                            case RandomType.SPECIAL_MANASTONE_EPIC_GRADE:
                            {
                                List<ItemTemplate> ancientStones = DataManager.ITEM_DATA.GetAncientManastones(randomType.GetLevel());
                                if (ancientStones == null)
                                {
                                    log.LogWarning("No ancient manastones found for decomposable random type " + randomItem.Type);
                                    break;
                                }
                                ItemQuality itemQuality;
                                if (randomType.ToString().Contains("RARE"))
                                    itemQuality = ItemQuality.RARE;
                                else if (randomType.ToString().Contains("LEGEND"))
                                    itemQuality = ItemQuality.LEGEND;
                                else if (randomType.ToString().Contains("UNIQUE"))
                                    itemQuality = ItemQuality.UNIQUE;
                                else if (randomType.ToString().Contains("EPIC"))
                                    itemQuality = ItemQuality.EPIC;
                                else
                                    itemQuality = ItemQuality.COMMON;
                                List<ItemTemplate> selectedStones = ancientStones
                                    .Where(t => t.GetItemQuality() == itemQuality && !t.GetName().Contains(" MP ")).ToList();
                                randomId = Aion.GameServer.Commons.Utils.Rnd.Get(selectedStones).GetTemplateId();
                                break;
                            }
                            case RandomType.CHUNK_EARTH:
                            {
                                int[] earth = chunkEarth[player.GetRace()];
                                randomId = Aion.GameServer.Commons.Utils.Rnd.Get(earth);
                                break;
                            }
                            case RandomType.CHUNK_SAND:
                            {
                                int[] sand = chunkSand[player.GetRace()];
                                randomId = Aion.GameServer.Commons.Utils.Rnd.Get(sand);
                                break;
                            }
                            case RandomType.PREMIUM_OPHIDAN_RECIPE:
                            {
                                int[] recipe = premiumOphidanRecipe[player.GetRace()];
                                randomId = Aion.GameServer.Commons.Utils.Rnd.Get(recipe);
                                break;
                            }
                            case RandomType.CHUNK_ROCK:
                                randomId = Aion.GameServer.Commons.Utils.Rnd.Get(chunkRock);
                                break;
                            case RandomType.CHUNK_GEMSTONE:
                                randomId = Aion.GameServer.Commons.Utils.Rnd.Get(chunkGemstone);
                                break;
                            case RandomType.SCROLLS:
                                randomId = Aion.GameServer.Commons.Utils.Rnd.Get(scrolls);
                                break;
                            case RandomType.POTION:
                                randomId = Aion.GameServer.Commons.Utils.Rnd.Get(potion);
                                break;
                            case RandomType.LESSER_POTIONS:
                                randomId = Aion.GameServer.Commons.Utils.Rnd.Get(lesser_potions);
                                break;
                            case RandomType.POTION_50:
                                randomId = Aion.GameServer.Commons.Utils.Rnd.Get(potion_50);
                                break;
                            case RandomType.ILLUSION_GODSTONE:
                                randomId = Aion.GameServer.Commons.Utils.Rnd.Get(illusion_godstones);
                                break;
                            case RandomType.ANCIENTITEMS:
                                do
                                {
                                    randomId = Aion.GameServer.Commons.Utils.Rnd.Get(186000051, 186000066);
                                    i++;
                                    if (i > 50)
                                    {
                                        randomId = 0;
                                        break;
                                    }
                                } while (!IsValidItemId(randomId));
                                break;
                            case RandomType.ANCIENT_CROWN:
                                do
                                {
                                    randomId = Aion.GameServer.Commons.Utils.Rnd.Get(186000051, 186000054);
                                    i++;
                                    if (i > 50)
                                    {
                                        randomId = 0;
                                        break;
                                    }
                                } while (!IsValidItemId(randomId));
                                break;
                            case RandomType.ANCIENT_GOBLET:
                                do
                                {
                                    randomId = Aion.GameServer.Commons.Utils.Rnd.Get(186000055, 186000058);
                                    i++;
                                    if (i > 50)
                                    {
                                        randomId = 0;
                                        break;
                                    }
                                } while (!IsValidItemId(randomId));
                                break;
                            case RandomType.ANCIENT_SEAL:
                                do
                                {
                                    randomId = Aion.GameServer.Commons.Utils.Rnd.Get(186000059, 186000062);
                                    i++;
                                    if (i > 50)
                                    {
                                        randomId = 0;
                                        break;
                                    }
                                } while (!IsValidItemId(randomId));
                                break;
                            case RandomType.ANCIENT_ICON:
                                do
                                {
                                    randomId = Aion.GameServer.Commons.Utils.Rnd.Get(186000063, 186000066);
                                    i++;
                                    if (i > 50)
                                    {
                                        randomId = 0;
                                        break;
                                    }
                                } while (!IsValidItemId(randomId));
                                break;
                        }
                        if (randomId != 0)
                        {
                            int count = Aion.GameServer.Commons.Utils.Rnd.Get(randomItem.GetMinCount(), randomItem.GetMaxCount());
                            Aion.GameServer.Services.Items.ItemService.AddItem(player, randomId, count, true,
                                new Aion.GameServer.Services.Items.ItemService.ItemUpdatePredicate(Aion.GameServer.Services.Items.ItemPacketService.ItemAddType.DECOMPOSABLE, Aion.GameServer.Services.Items.ItemPacketService.ItemUpdateType.INC_ITEM_COLLECT));
                        }
                    }
                }
            }
            Aion.GameServer.Utils.PacketSendUtility.BroadcastPacket(player,
                new Aion.GameServer.Network.Aion.ServerPackets.SmItemUsageAnimation(player.GetObjectId(), parentItem.GetObjectId(), parentItem.GetItemId(), 0, validAction ? 1 : 2, 0), true);
            return ValueTask.CompletedTask;
        }, TimeSpan.FromMilliseconds(USAGE_DELAY)));
    }

    // Java parity: postValidate(player, parentItem) — nested in the anonymous Runnable; targetItem captured from enclosing scope.
    private bool PostValidate(Aion.GameServer.Model.GameObjects.Players.Player player, Item parentItem, Item targetItem)
    {
        if (!CanAct(player, parentItem, targetItem))
        {
            return false;
        }
        if (!player.GetInventory().DecreaseByObjectId(parentItem.GetObjectId(), 1))
        {
            Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, Aion.GameServer.Network.Aion.ServerPackets.SmSystemMessage.STR_DECOMPOSE_ITEM_NO_TARGET_ITEM());
            return false;
        }
        return true;
    }

    /// <summary>Add to result collection only items which suit player's level.</summary>
    private ICollection<ExtractedItemsCollection> FilterItemsByLevel(Aion.GameServer.Model.GameObjects.Players.Player player, List<ExtractedItemsCollection> itemsCollections)
    {
        if (itemsCollections == null)
        {
            return null;
        }
        int playerLevel = player.GetLevel();
        ICollection<ExtractedItemsCollection> result = new List<ExtractedItemsCollection>();
        foreach (ExtractedItemsCollection collection in itemsCollections)
        {
            if (collection.GetMinLevel() > playerLevel || collection.GetMaxLevel() < playerLevel)
            {
                continue;
            }
            result.Add(collection);
        }
        return result;
    }

    private bool ContainsSpecialCubeItems(List<ExtractedItemsCollection> itemGroups, Aion.GameServer.Model.GameObjects.Players.Player player)
    {
        foreach (ExtractedItemsCollection items in itemGroups)
        {
            if (items.GetMinLevel() > player.GetLevel() || items.GetMaxLevel() < player.GetLevel())
                continue;
            foreach (ResultedItem item in items.GetItems())
            {
                if (item.IsObtainableFor(player))
                {
                    ItemTemplate template = DataManager.ITEM_DATA.GetItemTemplate(item.GetItemId());
                    if (template == null)
                        log.LogError("Detected invalid item id during decompose action " + item.GetItemId());
                    else if (template.GetExtraInventoryId() > 0)
                        return true;
                }
            }
        }
        return false;
    }

    public static void ValidateRandomItemIds()
    {
        foreach (int[] itemIds in chunkEarth.Values)
            ValidateItemIds(itemIds);
        foreach (int[] itemIds in chunkSand.Values)
            ValidateItemIds(itemIds);
        ValidateItemIds(chunkRock, chunkGemstone, scrolls, potion, lesser_potions, potion_50, illusion_godstones);
    }

    private static void ValidateItemIds(params int[][] itemIds)
    {
        foreach (int[] ids in itemIds)
        {
            foreach (int itemId in ids)
                if (!IsValidItemId(itemId))
                    throw new ArgumentException("Decomposable random reward item ID is invalid: " + itemId);
        }
    }

    private static bool IsValidItemId(int itemId)
    {
        return DataManager.ITEM_DATA.GetItemTemplate(itemId) != null;
    }

    // Java parity: anonymous ItemUseObserver in act().
    private sealed class DecomposeUseObserver : ItemUseObserver
    {
        private readonly Aion.GameServer.Model.GameObjects.Players.Player player;
        private readonly Item parentItem;

        public DecomposeUseObserver(Aion.GameServer.Model.GameObjects.Players.Player player, Item parentItem)
        {
            this.player = player;
            this.parentItem = parentItem;
        }

        public override void Abort()
        {
            player.GetController().CancelTask(TaskId.ITEM_USE);
            player.RemoveItemCoolDown(parentItem.GetItemTemplate().GetUseLimits().GetDelayId());
            Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, Aion.GameServer.Network.Aion.ServerPackets.SmSystemMessage.STR_DECOMPOSE_ITEM_CANCELED(parentItem.GetL10n()));
            Aion.GameServer.Utils.PacketSendUtility.BroadcastPacket(player,
                new Aion.GameServer.Network.Aion.ServerPackets.SmItemUsageAnimation(player.GetObjectId(), parentItem.GetObjectId(), parentItem.GetItemTemplate().GetTemplateId(), 0, 2, 0), true);
            player.GetObserveController().RemoveObserver(this);
        }
    }
}
