using System;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Model.GameObjects.FindGroup;

/// <summary>Java parity: model/gameobjects/findGroup/GroupApplication. System.currentTimeMillis()/1000→(int)(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()/1000).</summary>
public sealed class GroupApplication : FindGroupEntry
{
    private readonly Player player;
    private string message;
    private int groupType, classId, level;
    private int lastUpdate = (int)(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000);

    public GroupApplication(Player player, string message, int groupType, int classId, int level)
    {
        this.player = player;
        this.message = message;
        this.groupType = groupType;
        this.classId = classId;
        this.level = level;
    }

    public Player GetPlayer()
    {
        return player;
    }

    public string GetMessage()
    {
        return message;
    }

    public void SetMessage(string message)
    {
        this.message = message;
    }

    public int GetGroupType()
    {
        return groupType;
    }

    public void SetGroupType(int groupType)
    {
        this.groupType = groupType;
    }

    public int GetClassId()
    {
        return classId;
    }

    public void SetClassId(int classId)
    {
        this.classId = classId;
    }

    public int GetLevel()
    {
        return level;
    }

    public void SetLevel(int level)
    {
        this.level = level;
    }

    public int GetLastUpdate()
    {
        return lastUpdate;
    }

    public void UpdateLastUpdate()
    {
        lastUpdate = (int)(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000);
    }
}
