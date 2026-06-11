namespace Aion.GameServer.Model.GameObjects.Players;

/// <summary>Java parity: model/gameobjects/player/Friend.</summary>
public class Friend
{
    private PlayerCommonData pcd;
    private string memo;
    private readonly object _lock = new object();

    public Friend(PlayerCommonData pcd, string memo)
    {
        this.pcd = pcd;
        this.memo = memo;
    }

    public FriendList.Status GetStatus()
    {
        if (!pcd.IsOnline())
            return FriendList.Status.OFFLINE;
        Player player = Aion.GameServer.World.World.GetInstance().GetPlayer(GetObjectId());
        if (player == null)
            return FriendList.Status.OFFLINE;
        return player.GetFriendList().GetStatus();
    }

    public void SetPCD(PlayerCommonData pcd)
    {
        this.pcd = pcd;
    }

    public string GetName()
    {
        return pcd.GetName();
    }

    public int GetLevel()
    {
        return pcd.GetLevel();
    }

    public string GetNote()
    {
        return pcd.GetNote();
    }

    public Aion.GameServer.Model.PlayerClass GetPlayerClass()
    {
        return pcd.GetPlayerClass();
    }

    public Aion.GameServer.Model.Gender GetGender()
    {
        return pcd.GetGender();
    }

    public int GetMapId()
    {
        return pcd.GetMapId();
    }

    public int GetLastOnlineEpochSeconds()
    {
        return pcd.GetLastOnlineEpochSeconds();
    }

    public int GetObjectId()
    {
        return pcd.GetPlayerObjId();
    }

    public string GetFriendMemo()
    {
        lock (_lock)
        {
            return memo;
        }
    }

    public void SetFriendMemo(string memo)
    {
        lock (_lock)
        {
            this.memo = memo;
        }
    }
}
