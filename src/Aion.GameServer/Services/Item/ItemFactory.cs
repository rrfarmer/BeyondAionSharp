using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Templates.Item;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aion.GameServer.Services.Item;

/// <summary>Java parity: services/item/ItemFactory (ATracer).</summary>
public class ItemFactory
{
    private static readonly ILogger log = NullLogger.Instance;

    public static Item NewItem(int itemId)
    {
        ItemTemplate itemTemplate = DataManager.ITEM_DATA.GetItemTemplate(itemId);
        if (itemTemplate == null)
        {
            log.LogError("Item was not populated correctly. Item template is missing for item id: " + itemId);
            return null;
        }
        return new Item(Aion.GameServer.Utils.IdFactory.IDFactory.GetInstance().NextId(), itemTemplate);
    }

    public static Item NewItem(int itemId, long count)
    {
        Item item = NewItem(itemId);
        item.SetItemCount(CalculateCount(item.GetItemTemplate(), count));
        return item;
    }

    private static long CalculateCount(ItemTemplate itemTemplate, long count)
    {
        long maxStackCount = itemTemplate.GetMaxStackCount();
        if (count > maxStackCount && !itemTemplate.IsKinah())
            count = maxStackCount;
        return count;
    }
}
