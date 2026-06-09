using System;

namespace Aion.GameServer.Model.Team.Legion;

/// <summary>Java parity: model/team/legion/LegionMember.</summary>
public class LegionMember
{
    private readonly int objectId;
    private readonly Legion legion;
    private LegionRank rank = LegionRank.VOLUNTEER;
    private string nickname = "";
    private string selfIntro = "";
    private int challengeScore;
    // --- below are cached player fields (not in legion_members table) ---
    private string name;
    private Aion.GameServer.Model.PlayerClass playerClass;
    private int level;
    private int worldId;
    private int lastOnlineEpochSeconds;
    private bool online = false;

    public LegionMember(int objectId, Legion legion)
    {
        this.objectId = objectId;
        this.legion = legion;
    }

    public int GetObjectId()
    {
        return objectId;
    }

    public Legion GetLegion()
    {
        return legion;
    }

    public void SetRank(LegionRank rank)
    {
        this.rank = rank;
    }

    public LegionRank GetRank()
    {
        return rank;
    }

    public bool IsBrigadeGeneral()
    {
        return rank == LegionRank.BRIGADE_GENERAL;
    }

    public void SetNickname(string nickname)
    {
        this.nickname = nickname;
    }

    public string GetNickname()
    {
        return nickname;
    }

    public void SetSelfIntro(string selfIntro)
    {
        this.selfIntro = selfIntro;
    }

    public string GetSelfIntro()
    {
        return selfIntro;
    }

    public int GetChallengeScore()
    {
        return challengeScore;
    }

    public void SetChallengeScore(int challengeScore)
    {
        this.challengeScore = challengeScore;
    }

    public void IncreaseChallengeScore(int amount)
    {
        this.challengeScore += amount;
    }

    public void SetPlayerData(Aion.GameServer.Model.GameObjects.Player.Player player)
    {
        SetPlayerData(player.GetCommonData());
    }

    public void SetPlayerData(Aion.GameServer.Model.GameObjects.Player.PlayerCommonData playerCommonData)
    {
        name = playerCommonData.GetName();
        playerClass = playerCommonData.GetPlayerClass();
        level = playerCommonData.GetLevel();
        worldId = playerCommonData.GetMapId();
        lastOnlineEpochSeconds = playerCommonData.GetLastOnline() == null ? 0 : (int)(ToMillis(playerCommonData.GetLastOnline().Value) / 1000);
        online = playerCommonData.IsOnline();
    }

    public string GetName()
    {
        return name;
    }

    public Aion.GameServer.Model.PlayerClass GetPlayerClass()
    {
        return playerClass;
    }

    public int GetLevel()
    {
        return level;
    }

    public int GetWorldId()
    {
        return worldId;
    }

    public int GetLastOnlineEpochSeconds()
    {
        return lastOnlineEpochSeconds;
    }

    public bool IsOnline()
    {
        return online;
    }

    public bool HasRights(LegionPermissionsMask permissions)
    {
        return rank switch
        {
            LegionRank.BRIGADE_GENERAL => true,
            LegionRank.DEPUTY => permissions.Can(legion.GetDeputyPermission()),
            LegionRank.CENTURION => permissions.Can(legion.GetCenturionPermission()),
            LegionRank.LEGIONARY => permissions.Can(legion.GetLegionaryPermission()),
            LegionRank.VOLUNTEER => permissions.Can(legion.GetVolunteerPermission()),
            _ => false,
        };
    }

    // Java parity: java.sql.Timestamp.getTime() returns epoch millis.
    private static long ToMillis(DateTime dt)
    {
        return new DateTimeOffset(DateTime.SpecifyKind(dt, DateTimeKind.Utc)).ToUnixTimeMilliseconds();
    }
}
