using System;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.House;
using Aion.GameServer.Model.Templates.Housing;
using Aion.GameServer.Model.Templates.Items;
using Aion.GameServer.Model.Templates.Items.Actions;
using Aion.GameServer.Utils.Idfactory;

namespace Aion.GameServer.Services.Items;

/// <summary>Java parity: services/item/HouseObjectFactory (Rolandas). Instantiates the right HouseObject subclass from a placeable-house template (DB load) and transfers an inventory item into a house registry object with expiry. instanceof->is; Objects.requireNonNull->null-check+NullReferenceException; TimeUnit.DAYS.toSeconds->*86400; currentTimeMillis/1000->UtcNow.ToUnixTimeMilliseconds()/1000; HouseObject&lt;?&gt;->HouseObject&lt;PlaceableHouseObject&gt; (invariance bound). Housing templates / object subclasses red-tolerated.</summary>
public sealed class HouseObjectFactory
{
    /// <summary>
    /// For loading data from DB
    /// </summary>
    public static HouseObject<PlaceableHouseObject> CreateNew(HouseRegistry registry, int objectId, int objectTemplateId)
    {
        PlaceableHouseObject template = DataManager.HOUSING_OBJECT_DATA.GetTemplateById(objectTemplateId);
        if (template is HousingChair)
            return new ChairObject(registry, objectId, template.GetTemplateId());
        else if (template is HousingJukeBox)
            return new JukeBoxObject(registry, objectId, template.GetTemplateId());
        else if (template is HousingMoveableItem)
            return new MoveableObject(registry, objectId, template.GetTemplateId());
        else if (template is HousingNpc)
            return new NpcObject(registry, objectId, template.GetTemplateId());
        else if (template is HousingPicture)
            return new PictureObject(registry, objectId, template.GetTemplateId());
        else if (template is HousingPostbox)
            return new PostboxObject(registry, objectId, template.GetTemplateId());
        else if (template is HousingStorage)
            return new StorageObject(registry, objectId, template.GetTemplateId());
        else if (template is HousingUseableItem)
            return new UseableItemObject(registry, objectId, template.GetTemplateId());
        else if (template is HousingEmblem)
            return new EmblemObject(registry, objectId, template.GetTemplateId());
        return new PassiveObject(registry, objectId, template.GetTemplateId());
    }

    /// <summary>
    /// For transferring item from inventory to house registry
    /// </summary>
    public static HouseObject<PlaceableHouseObject> CreateNew(House house, ItemTemplate itemTemplate)
    {
        if (itemTemplate.GetActions() == null)
            throw new NullReferenceException("template actions null");

        SummonHouseObjectAction action = itemTemplate.GetActions().GetHouseObjectAction();
        if (action == null)
            throw new NullReferenceException("template actions miss SummonHouseObjectAction");

        int objectTemplateId = action.GetTemplateId();
        HouseObject<PlaceableHouseObject> obj = CreateNew(house.GetRegistry(), IDFactory.GetInstance().NextId(), objectTemplateId);
        int useDays = obj.GetObjectTemplate().GetUseDays();
        if (useDays > 0)
        {
            int expireEnd = (int)(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000 + useDays * 86400L);
            obj.SetExpireTime(expireEnd);
        }
        return obj;
    }
}
