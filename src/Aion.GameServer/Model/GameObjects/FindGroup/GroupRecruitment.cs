using System;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Team;

namespace Aion.GameServer.Model.GameObjects.FindGroup;

/// <summary>Java parity: model/gameobjects/findGroup/GroupRecruitment (MrPoke) implements FindGroupEntry. A "Recruit Group Members" tab entry wrapping a Player or team (AionObject): message/group-type, class/level (explicit or derived from player/team leader), name/size/race, last-update timestamp. instanceof X x->is X x; TemporaryPlayerTeam&lt;?&gt;-><TeamMember<Player>>; currentTimeMillis/1000->UtcNow.ToUnixTimeMilliseconds()/1000; getName(true)->GetName(true); getRace nullable. AionObject/TemporaryPlayerTeam red-tolerated.</summary>
public sealed class GroupRecruitment : FindGroupEntry
{
    private readonly AionObject obj;
    private string message;
    private int groupType, classId = -1, level = -1;
    private int lastUpdate = (int)(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000);

    public GroupRecruitment(AionObject obj, string message, int groupType)
    {
        this.obj = obj;
        this.message = message;
        this.groupType = groupType;
    }

    public AionObject GetObject()
    {
        return obj;
    }

    public int GetObjectId()
    {
        return obj.GetObjectId();
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
        if (classId != -1)
            return classId;
        if (obj is Player player)
            return player.GetPlayerClass().GetClassId();
        if (obj is TemporaryPlayerTeam<TeamMember<Player>> team)
            return team.GetLeaderObject().GetPlayerClass().GetClassId();
        return 0;
    }

    public void SetClassId(int classId)
    {
        this.classId = classId;
    }

    public int GetMinLevel()
    {
        if (level != -1)
            return level;
        if (obj is Player player)
            return player.GetLevel();
        if (obj is TemporaryPlayerTeam<TeamMember<Player>> team)
            return team.GetMinExpPlayerLevel();
        return 1;
    }

    public void SetLevel(int level)
    {
        this.level = level;
    }

    public int GetMaxLevel()
    {
        if (obj is Player player)
            return player.GetLevel();
        else if (obj is TemporaryPlayerTeam<TeamMember<Player>> team)
            return team.GetMaxExpPlayerLevel();
        return 1;
    }

    public string GetName()
    {
        return obj is TemporaryPlayerTeam<TeamMember<Player>> team ? team.GetLeaderObject().GetName(true) : ((Player)obj).GetName(true);
    }

    public int GetSize()
    {
        return obj is TemporaryPlayerTeam<TeamMember<Player>> team ? team.Size() : 1;
    }

    public int GetLastUpdate()
    {
        return lastUpdate;
    }

    public void UpdateLastUpdate()
    {
        lastUpdate = (int)(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000);
    }

    public Race? GetRace()
    {
        return obj is Player player ? player.GetRace() : obj is TemporaryPlayerTeam<TeamMember<Player>> team ? team.GetRace() : (Race?)null;
    }
}
