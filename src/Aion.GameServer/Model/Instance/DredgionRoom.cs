using Aion.GameServer.Model;

namespace Aion.GameServer.Model.Instance;

/// <summary>Java parity: model/instance/DredgionRoom.</summary>
public class DredgionRoom
{
    private readonly int roomId;
    private int state = 0xFF;

    public DredgionRoom(int roomId)
    {
        this.roomId = roomId;
    }

    public int GetRoomId()
    {
        return roomId;
    }

    public void CaptureRoom(Race race)
    {
        state = race == Race.ASMODIANS ? 0x01 : 0x00;
    }

    public int GetState()
    {
        return state;
    }
}
