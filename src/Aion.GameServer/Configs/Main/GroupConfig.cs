namespace Aion.GameServer.Configs.Main;

/// <summary>Java parity: configs/main/GroupConfig. SCREAMING_SNAKE field names + @Property defaults.</summary>
public static class GroupConfig
{
    /// <summary>Group remove time. Key: gameserver.playergroup.removetime</summary>
    public static int GROUP_REMOVE_TIME = 600;

    /// <summary>Group max distance. Key: gameserver.playergroup.maxdistance</summary>
    public static int GROUP_MAX_DISTANCE = 100;

    /// <summary>Enable Group Invite Other Faction. Key: gameserver.group.inviteotherfaction</summary>
    public static bool GROUP_INVITEOTHERFACTION = false;

    /// <summary>Alliance remove time. Key: gameserver.playeralliance.removetime</summary>
    public static int ALLIANCE_REMOVE_TIME = 600;

    /// <summary>Enable Alliance Invite Other Faction. Key: gameserver.playeralliance.inviteotherfaction</summary>
    public static bool ALLIANCE_INVITEOTHERFACTION = false;

    /// <summary>Allow applying/registering instance groups in Find Group window anywhere (6.2+). Key: gameserver.instance_group.form_anywhere</summary>
    public static bool FORM_INSTANCE_GROUP_ANYWHERE = false;
}
