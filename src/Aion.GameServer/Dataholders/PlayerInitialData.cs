using System;
using System.Collections.Generic;
using System.Text;
using System.Xml.Serialization;
using Aion.GameServer.Model;
using Aion.GameServer.Model.Templates.Items;

namespace Aion.GameServer.Dataholders;

/// <summary>Java parity: dataholders/PlayerInitialData (Aquanox). New-player data table. @XmlRootElement→[XmlRoot]; @XmlTransient Map→[XmlIgnore] Dictionary in AfterUnmarshal; nested static classes; @XmlAttribute→[XmlAttribute]; signed byte→sbyte; Collections.unmodifiableList→AsReadOnly; IllegalArgumentException→ArgumentException. @XmlElement(required=true) has no C# equiv (dropped). @XmlIDREF (JAXB cross-ref ItemTemplate by id) has no XmlSerializer equiv → resolved via the proven id-string-proxy pattern: ItemType.template is [XmlIgnore], a [XmlAttribute("id")] string proxy holds the raw id, and a children-first AfterUnmarshal(ItemData,parent) cascade resolves each via the in-progress ITEM_DATA (exactly as JAXB's @XmlIDREF resolves against the unmarshalled ItemTemplate set).</summary>
[XmlRoot("player_initial_data")]
public class PlayerInitialData
{
    [XmlElement("player_data")]
    public List<PlayerCreationData> dataList;

    [XmlElement("elyos_spawn_location")]
    public LocationData elyosSpawnLocation;
    [XmlElement("asmodian_spawn_location")]
    public LocationData asmodianSpawnLocation;

    [XmlIgnore]
    private readonly Dictionary<PlayerClass, PlayerCreationData> data = new();

    // Java parity: afterUnmarshal(Unmarshaller, Object). The StaticDataListener (Unmarshaller-keyed) has no C# analog;
    // this object-arg form falls back to the registered DataManager.ITEM_DATA for any non-boot caller.
    public void AfterUnmarshal(object parent)
    {
        AfterUnmarshal(DataManager.ITEM_DATA, parent);
    }

    // Boot-time overload: during LoadLeafHoldersFromFiles the DataManager singleton bridge is not yet registered, so
    // StaticData passes the in-progress ItemData explicitly (mirrors Java's StaticDataListener handing afterUnmarshal
    // the StaticData currently being unmarshalled). Resolves each item's @XmlIDREF id against the in-progress ITEM_DATA
    // children-first, then performs the parent class-indexing (XmlSerializer does not auto-fire nested JAXB callbacks).
    public void AfterUnmarshal(ItemData itemData, object parent)
    {
        foreach (PlayerCreationData pt in dataList)
        {
            if (pt.itemsType != null && pt.itemsType.items != null)
                foreach (PlayerCreationData.ItemType item in pt.itemsType.items)
                    item.AfterUnmarshal(itemData, parent);
        }

        foreach (PlayerCreationData pt in dataList)
        {
            data[pt.GetRequiredPlayerClass()] = pt;
        }
        dataList = null;
    }

    public PlayerCreationData GetPlayerCreationData(PlayerClass cls)
    {
        return data.GetValueOrDefault(cls);
    }

    public int Size()
    {
        return data.Count;
    }

    public LocationData GetSpawnLocation(Race race)
    {
        switch (race)
        {
            case Race.ASMODIANS:
                return asmodianSpawnLocation;
            case Race.ELYOS:
                return elyosSpawnLocation;
            default:
                throw new ArgumentException();
        }
    }

    /// <summary>Player creation data holder.</summary>
    public class PlayerCreationData
    {
        [XmlAttribute("class")]
        public PlayerClass requiredPlayerClass;

        [XmlElement("items")]
        public ItemsType itemsType;

        internal PlayerClass GetRequiredPlayerClass()
        {
            return requiredPlayerClass;
        }

        public IList<ItemType> GetItems()
        {
            return itemsType.items.AsReadOnly();
        }

        public class ItemsType
        {
            [XmlElement("item")]
            public List<ItemType> items = new();
        }

        public class ItemType
        {
            // Java @XmlAttribute(name="id") @XmlIDREF ItemTemplate template — JAXB binds the id attr to the matching
            // ItemTemplate object. XmlSerializer cannot bind an object to an attribute, so hold the raw id in a string
            // proxy and resolve the real ItemTemplate in AfterUnmarshal via the live ITEM_DATA (faithful @XmlIDREF).
            [XmlIgnore]
            public ItemTemplate template;

            [XmlAttribute("id")]
            public string idRaw;

            [XmlAttribute("count")]
            public int count;

            // Boot-time @XmlIDREF resolution: look up the raw id in the in-progress ITEM_DATA, exactly as JAXB resolves
            // the IDREF against the unmarshalled ItemTemplate set. Throws on an invalid id (no silent drop).
            public void AfterUnmarshal(ItemData itemData, object parent)
            {
                int id = int.Parse(idRaw);
                template = itemData.GetItemTemplate(id);
                if (template == null)
                    throw new ArgumentException("Player initial item id is invalid (no ItemTemplate): " + id);
            }

            public ItemTemplate GetTemplate()
            {
                return template;
            }

            public int GetCount()
            {
                return count;
            }

            public override string ToString()
            {
                StringBuilder sb = new StringBuilder();
                sb.Append("ItemType");
                sb.Append("{template=").Append(template);
                sb.Append(", count=").Append(count);
                sb.Append('}');
                return sb.ToString();
            }
        }
    }

    /// <summary>Location data holder.</summary>
    public class LocationData
    {
        [XmlAttribute("map_id")]
        public int mapId;
        [XmlAttribute("x")]
        public float x;
        [XmlAttribute("y")]
        public float y;
        [XmlAttribute("z")]
        public float z;
        [XmlAttribute("heading")]
        public sbyte heading;

        public LocationData()
        {
            //
        }

        public int GetMapId()
        {
            return mapId;
        }

        public float GetX()
        {
            return x;
        }

        public float GetY()
        {
            return y;
        }

        public float GetZ()
        {
            return z;
        }

        public sbyte GetHeading()
        {
            return heading;
        }
    }
}
