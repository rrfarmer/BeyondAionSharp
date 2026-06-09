using System.Collections.Generic;
using Aion.GameServer.Model;

namespace Aion.GameServer.Model.GameObjects.Player.Motion;

/// <summary>Java parity: model/gameobjects/player/motion/Motion implements Expirable.</summary>
public class Motion : IExpirable
{
    // Java parity: LinkedHashMap initialized in static block.
    internal static readonly Dictionary<int, int> motionType = new Dictionary<int, int>
    {
        { 1, 1 }, { 2, 2 }, { 3, 3 }, { 4, 4 }, { 5, 1 }, { 6, 2 }, { 7, 3 }, { 8, 4 },
        { 9, 5 }, { 10, 5 }, { 11, 1 }, { 12, 2 }, { 13, 3 }, { 14, 4 }, { 15, 1 }, { 16, 2 },
        { 17, 3 }, { 18, 4 }, { 19, 5 }, { 20, 1 }, { 21, 1 }, { 22, 1 }, { 23, 1 }, { 24, 2 },
        { 25, 4 }, { 26, 3 },
    };

    private int id;
    private int deletionTime = 0;
    private bool active = false;

    public Motion(int id, int deletionTime, bool isActive)
    {
        this.id = id;
        this.deletionTime = deletionTime;
        this.active = isActive;
    }

    /// <summary>the id</summary>
    public int GetId()
    {
        return id;
    }

    /// <summary>the active</summary>
    public bool IsActive()
    {
        return active;
    }

    /// <param name="active">the active to set</param>
    public void SetActive(bool active)
    {
        this.active = active;
    }

    public int GetExpireTime()
    {
        return deletionTime;
    }

    public void OnExpire(Aion.GameServer.Model.GameObjects.Player.Player player)
    {
        player.GetMotions().Remove(id);
        // TODO motion templates -> parse nameIds for system message, like 600533 for STR_CMOTION_CASH_NINJA_IDLE (Ninja Idle) etc.
        Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, Aion.GameServer.Network.Aion.ServerPackets.SmSystemMessage.STR_MSG_DELETE_CASH_CUSTOMANIMATION_BY_TIMEOUT(/* nameId */));
    }
}
