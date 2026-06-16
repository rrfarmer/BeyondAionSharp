using System.Collections.Generic;
using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Housing;

/// <summary>
/// Java parity: model/templates/housing/HousingLand (Rolandas).
/// propOrder = addresses, buildings, sale, fee, caps.
/// </summary>
[XmlType("Land")]
public class HousingLand
{
    [XmlArray("addresses")]
    [XmlArrayItem("address")]
    public List<HouseAddress> addresses;

    [XmlArray("buildings")]
    [XmlArrayItem("building")]
    public List<Building> buildings;

    [XmlElement("sale")] public Sale sale;

    [XmlElement("fee")] public long fee;

    [XmlElement("caps")] public BuildingCapabilities caps;

    [XmlAttribute("sign_nosale")] public int signNosale;

    [XmlAttribute("sign_sale")] public int signSale;

    [XmlAttribute("sign_waiting")] public int signWaiting;

    [XmlAttribute("sign_home")] public int signHome;

    [XmlAttribute("manager_npc")] public int managerNpc;

    [XmlAttribute("teleport_npc")] public int teleportNpc;

    [XmlAttribute("id")] public int id;

    public List<HouseAddress> GetAddresses()
    {
        return addresses;
    }

    public List<Building> GetBuildings()
    {
        return buildings;
    }

    public Building GetDefaultBuilding()
    {
        foreach (Building building in buildings)
        {
            if (building.IsDefault())
                return building;
        }
        return buildings[0]; // fail
    }

    public Sale GetSaleOptions()
    {
        return sale;
    }

    public long GetMaintenanceFee()
    {
        return fee;
    }

    public BuildingCapabilities GetCapabilities()
    {
        return caps;
    }

    public int GetNosaleSignNpcId()
    {
        return signNosale;
    }

    public int GetSaleSignNpcId()
    {
        return signSale;
    }

    public int GetWaitingSignNpcId()
    {
        return signWaiting;
    }

    public int GetHomeSignNpcId()
    {
        return signHome;
    }

    public int GetManagerNpcId()
    {
        return managerNpc;
    }

    public int GetTeleportNpcId()
    {
        return teleportNpc;
    }

    public int GetId()
    {
        return id;
    }

    public override int GetHashCode()
    {
        return id;
    }
}
