using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.Animations;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.House;
using Aion.GameServer.Model.Templates.Housing;
using Aion.GameServer.Model.Templates.Spawns;
using Aion.GameServer.Network.Aion.Serverpackets;
using Aion.GameServer.Services;
using Aion.GameServer.Services.Teleport;
using Aion.GameServer.Spawnengine;
using Aion.GameServer.Utils;
using Aion.GameServer.World.Geo;
using Aion.GameServer.World.Zone;

namespace Aion.GameServer.Controllers;

/// <summary>Java parity: controllers/HouseController (Rolandas, Neon) : VisibleObjectController&lt;House&gt;. **HouseObject&lt;?&gt;→HouseObject&lt;PlaceableHouseObject&gt;** (matches ported HouseRegistry.GetSpawnedObjects bound). instanceof Player→is Player; enum SpawnType ==; ZoneName.get→ZoneName.Get (class); Math.toRadians→*Math.PI/180; byte compound arithmetic (h-=30); Integer exitMapId→int?. House/HouseObject/SpawnEngine red-tolerated/converged.</summary>
public class HouseController : VisibleObjectController<House>
{
    private static readonly ILogger log = NullLoggerFactory.Instance.CreateLogger(nameof(HouseController));

    public override void See(VisibleObject obj)
    {
        if (obj is Player)
            SpawnObjects();
    }

    public void SpawnObjects()
    {
        if (GetOwner().GetPosition() != null && GetOwner().IsSpawned() && !GetOwner().IsInactive())
        {
            foreach (HouseObject<PlaceableHouseObject> obj in GetOwner().GetRegistry().GetSpawnedObjects())
                obj.Spawn();
        }
    }

    public override void OnAfterSpawn()
    {
        // loads scripts and registry from DB if not already initialized
        GetOwner().GetPlayerScripts();
        GetOwner().GetRegistry();
        UpdateSpawns();
        GeoService.GetInstance().SetHouseDoorState(GetOwner().GetWorldId(), GetOwner().GetInstanceId(), GetOwner().GetAddress().GetId(), GetOwner().GetDoorState());
    }

    private void UpdateSpawns()
    {
        HouseAddress address = GetOwner().GetAddress();
        List<HouseSpawn> templates = DataManager.HOUSE_NPCS_DATA.GetSpawnsByAddress(address.GetId());
        if (templates == null)
        {
            log.LogWarning("Missing npc spawns for house " + address.GetId());
            return;
        }
        foreach (HouseSpawn spawn in templates)
        {
            Npc npc;
            if (spawn.GetType_() == SpawnType.MANAGER)
            {
                SpawnTemplate t = SpawnEngine.NewSingleTimeSpawn(address.GetMapId(), address.GetLand().GetManagerNpcId(), spawn.GetX(), spawn.GetY(),
                    spawn.GetZ(), spawn.GetH());
                npc = VisibleObjectSpawner.SpawnHouseNpc(t, GetOwner().GetInstanceId(), GetOwner());
            }
            else if (spawn.GetType_() == SpawnType.TELEPORT)
            {
                SpawnTemplate t = SpawnEngine.NewSingleTimeSpawn(address.GetMapId(), address.GetLand().GetTeleportNpcId(), spawn.GetX(), spawn.GetY(),
                    spawn.GetZ(), spawn.GetH());
                npc = VisibleObjectSpawner.SpawnHouseNpc(t, GetOwner().GetInstanceId(), GetOwner());
            }
            else if (spawn.GetType_() == SpawnType.SIGN)
            {
                // Signs do not have master name displayed, but have creatorId
                int creatorId = address.GetId();
                SpawnTemplate t = SpawnEngine.NewSingleTimeSpawn(address.GetMapId(), GetCurrentSignNpcId(), spawn.GetX(), spawn.GetY(), spawn.GetZ(),
                    spawn.GetH(), creatorId);
                npc = (Npc)SpawnEngine.SpawnObject(t, GetOwner().GetInstanceId());
            }
            else
            {
                log.LogWarning("Unhandled spawn type " + spawn.GetType_());
                continue;
            }
            GetOwner().UpdateSpawn(spawn.GetType_(), npc);
        }
    }

    public override void OnDespawn()
    {
        base.OnDespawn();
        bool isReusableStudio = HousingService.GetInstance().FindStudio(GetOwner().GetObjectId()) == GetOwner();
        if (isReusableStudio) // save studio and release despawned npcs and the destroyed mapregion / worldmapinstance, since studio stays in RAM
        {
            GetOwner().Save();
            GetOwner().ClearSpawns();
            GetOwner().SetPosition(null);
        }
    }

    public void UpdateAppearance()
    {
        PacketSendUtility.BroadcastPacket(GetOwner(), new SM_HOUSE_UPDATE(GetOwner()));
    }

    public void KickVisitors(Player kicker, bool kickFriends, bool ownerChanged)
    {
        ZoneName houseZone = ZoneName.Get(GetOwner().GetName());
        GetOwner().GetKnownList().ForEachPlayer(player =>
        {
            if (player.GetObjectId() == GetOwner().GetOwnerId())
                return;
            if (!kickFriends && kicker != null && kicker.GetFriendList().GetFriend(player.GetObjectId()) != null)
                return;
            if (player.IsInsideZone(houseZone))
                MoveOutside(player, ownerChanged);
        });
        if (kicker != null)
        {
            if (!kickFriends)
            {
                PacketSendUtility.SendPacket(kicker, SM_SYSTEM_MESSAGE.STR_MSG_HOUSING_ORDER_OUT_WITHOUT_FRIENDS());
            }
            else
            {
                PacketSendUtility.SendPacket(kicker, SM_SYSTEM_MESSAGE.STR_MSG_HOUSING_ORDER_OUT_ALL());
            }
        }
    }

    private void MoveOutside(Player player, bool ownerChanged)
    {
        if (GetOwner().GetAddress().GetExitMapId() != null)
        {
            HouseAddress address = GetOwner().GetAddress();
            TeleportService.TeleportTo(player, address.GetExitMapId().Value, address.GetExitX(), address.GetExitY(), address.GetExitZ(), (byte)0,
                TeleportAnimation.FADE_OUT_BEAM);
        }
        else
        {
            TeleportNearHouseDoor(player, true);
        }
        if (ownerChanged)
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_HOUSING_CHANGE_OWNER());
        else
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_HOUSING_REQUEST_OUT());
    }

    public void TeleportNearHouseDoor(Player player, bool outsideHouse)
    {
        SpawnTemplate butler = GetOwner().GetButler().GetSpawn(), relationshipCrystal = GetOwner().GetRelationshipCrystal().GetSpawn();
        float x, y, z; // midpoint between butler and relationship crystal, since we currently have no door coordinates in templates
        byte h = PositionUtil.GetHeadingTowards(GetOwner().GetRelationshipCrystal(), butler.GetX(), butler.GetY());
        h -= 30; // this is the supposed heading towards the door (crystal is right from the door, so offset direction towards butler by 90 degrees)
        x = (butler.GetX() + relationshipCrystal.GetX()) / 2;
        y = (butler.GetY() + relationshipCrystal.GetY()) / 2;
        z = Math.Max(butler.GetZ(), relationshipCrystal.GetZ());
        if (outsideHouse) // offset the midpoint 2.5m behind the butler, to get coords outside the house, near the door
        {
            double radian = PositionUtil.ConvertHeadingToAngle(h) * Math.PI / 180;
            x += (float)(Math.Cos(radian) * 2.5f);
            y += (float)(Math.Sin(radian) * 2.5f);
        }
        else
        {
            h += (byte)(h < 60 ? 60 : -60); // opposite direction (player should look inside house)
        }
        TeleportService.TeleportTo(player, GetOwner().GetWorldId(), GetOwner().GetInstanceId(), x, y, z, h, TeleportAnimation.FADE_OUT_BEAM);
    }

    public void UpdateSign()
    {
        if (GetOwner().GetCurrentSign() == null)
            return;
        int newNpcId = GetCurrentSignNpcId();
        if (newNpcId != GetOwner().GetCurrentSign().GetNpcId())
        {
            SpawnTemplate t = GetOwner().GetCurrentSign().GetSpawn();
            t = SpawnEngine.NewSingleTimeSpawn(t.GetWorldId(), newNpcId, t.GetX(), t.GetY(), t.GetZ(), t.GetHeading(), t.GetCreatorId());
            GetOwner().UpdateSpawn(SpawnType.SIGN, (Npc)SpawnEngine.SpawnObject(t, GetOwner().GetInstanceId()));
        }
    }

    public void UpdateHouseSpawns()
    {
        // only update spawns in active studios
        if (GetOwner().GetHouseType() == HouseType.STUDIO && (GetOwner().GetPosition() == null || !GetOwner().IsSpawned()))
            return;
        GetOwner().UpdateSpawn(SpawnType.MANAGER, null); // remove old butler, otherwise new npcs spawn with old owner name
        UpdateSpawns();
        UpdateAppearance();
    }

    private int GetCurrentSignNpcId()
    {
        if (GetOwner().GetBids() != null)
            return GetOwner().GetLand().GetSaleSignNpcId();
        if (GetOwner().GetOwnerId() == 0)
            return GetOwner().GetLand().GetNosaleSignNpcId(); // invisible npc
        return GetOwner().IsInactive() ? GetOwner().GetLand().GetWaitingSignNpcId() : GetOwner().GetLand().GetHomeSignNpcId();
    }
}
