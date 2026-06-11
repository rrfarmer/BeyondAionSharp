using System;
using System.Collections.Generic;
using System.Text;
using System.Xml.Serialization;
using Aion.GameServer.Model;
using Aion.GameServer.Model.Templates.Items;

namespace Aion.GameServer.Dataholders;

/// <summary>Java parity: dataholders/PlayerInitialData (Aquanox). New-player data table. @XmlRootElement→[XmlRoot]; @XmlTransient Map→[XmlIgnore] Dictionary in AfterUnmarshal; nested static classes; @XmlAttribute→[XmlAttribute]; signed byte→sbyte; Collections.unmodifiableList→AsReadOnly; IllegalArgumentException→ArgumentException. NOTE: @XmlElement(required=true) has no C# equiv (dropped); @XmlIDREF (JAXB cross-ref ItemTemplate by id) has no XmlSerializer equiv — needs JAXB→XmlSerializer pillar.</summary>
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

    public void AfterUnmarshal(object parent)
    {
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
            // @XmlIDREF: JAXB cross-references ItemTemplate by its id; no XmlSerializer equivalent (JAXB→XmlSerializer pillar).
            [XmlAttribute("id")]
            public ItemTemplate template;

            [XmlAttribute("count")]
            public int count;

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
