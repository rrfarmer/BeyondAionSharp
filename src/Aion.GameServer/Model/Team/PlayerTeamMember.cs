using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Model.Team;

/// <summary>
/// A player team member wrapper.
/// Java parity: model/team/PlayerTeamMember (implements TeamMember&lt;Player&gt;).
/// </summary>
public class PlayerTeamMember : ITeamMember<Player>
{
    internal readonly Player Player;
    private long _lastOnlineTime;

    public PlayerTeamMember(Player player)
    {
        Player = player;
    }

    // Java parity: getObjectId()
    public int GetObjectId() => Player.ObjectId;

    // Java parity: getName()
    public string GetName() => Player.Name;

    // Java parity: getObject()
    public Player GetObject() => Player;

    // Java parity: getLastOnlineTime()
    public long GetLastOnlineTime() => _lastOnlineTime;

    // Java parity: updateLastOnlineTime()
    public void UpdateLastOnlineTime() => _lastOnlineTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
}
