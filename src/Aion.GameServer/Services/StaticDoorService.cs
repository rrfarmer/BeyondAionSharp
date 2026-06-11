using Aion.GameServer.Configs.Administration;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aion.GameServer.Services;

/// <summary>Java parity: services/StaticDoorService (Wakizashi).</summary>
public class StaticDoorService
{
    private static readonly ILogger log = NullLogger.Instance;

    public static StaticDoorService GetInstance()
    {
        return SingletonHolder.instance;
    }

    private static class SingletonHolder
    {
        internal static readonly StaticDoorService instance = new StaticDoorService();
    }

    public void OpenStaticDoor(Player player, int doorId)
    {
        StaticDoor door = GetDoor(player, doorId);
        if (door == null)
            return;
        int keyId = door.GetObjectTemplate().GetKeyId();

        if (player.HasAccess(AdminConfig.INSTANCE_DOOR_INFO))
            PacketSendUtility.SendMessage(player, "Door ID: " + doorId + ", key ID: " + keyId);

        bool opened = false;
        lock (door)
        {
            if (!door.IsOpen() && CheckStaticDoorKey(player, door, keyId))
            {
                door.SetOpen(true);
                opened = true;
            }
        }
        if (opened)
            player.GetPosition().GetWorldMapInstance().GetInstanceHandler().OnOpenDoor(doorId);
    }

    public void ChangeStaticDoorState(Player player, int doorId, bool open, int state)
    {
        StaticDoor door = GetDoor(player, doorId);
        if (door == null)
            return;
        door.ChangeState(open, state);
        PacketSendUtility.SendMessage(player, "Door states now are: " + door.GetStates());
    }

    private StaticDoor GetDoor(Player player, int doorId)
    {
        VisibleObject @object = player.GetPosition().GetWorldMapInstance().GetObjectByStaticId(doorId);
        if (!(@object is StaticDoor))
        {
            if (@object == null)
                log.LogWarning("Door (ID: " + doorId + ") is missing near " + player.GetPosition());
            else
                log.LogWarning("Door (ID: " + doorId + ") is not a static door but " + @object);
            return null;
        }
        return (StaticDoor) @object;
    }

    private bool CheckStaticDoorKey(Player player, StaticDoor door, int keyId)
    {
        if (player.HasAccess(AdminConfig.INSTANCE_OPEN_DOORS))
            return true;

        if (keyId == 0)
            return true;

        if (keyId == 1)
            return false;

        if (!door.IsLocked())
        {
            return true;
        }

        if (!player.GetInventory().DecreaseByItemId(keyId, 1))
        {
            PacketSendUtility.SendPacket(player, SmSystemMessage.CannotOpenDoorNeedKeyItem());
            return false;
        }

        door.SetLocked(false);

        return true;
    }
}
