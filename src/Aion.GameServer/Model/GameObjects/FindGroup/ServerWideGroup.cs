using System.Collections.Generic;
using System.Linq;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Player;

namespace Aion.GameServer.Model.GameObjects.FindGroup;

/// <summary>Java parity: model/gameobjects/findGroup/ServerWideGroup. final→sealed; currentTimeMillis/1000→(int)(UtcNow.ToUnixTimeMilliseconds()/1000); stream.max(comparing(level)).get→Max(level), max(reversed)→Min(level) (faithful: Java max-of-reversed picks the lowest). getCurrentTeam() returns TemporaryPlayerTeam&lt;?&gt; — bound to `var` to sidestep the C# wildcard gap. Player/TemporaryPlayerTeam red-tolerated.</summary>
public sealed class ServerWideGroup : FindGroupEntry
{
    private readonly List<Player> members = new();
    private readonly int instanceMaskId;
    private readonly int minMembers;
    private string message;
    private int lastUpdate;

    public ServerWideGroup(Player recruiter, int instanceMaskId, int minMembers, string message)
    {
        this.members.Add(recruiter);
        this.instanceMaskId = instanceMaskId;
        this.minMembers = minMembers;
        this.message = message;
        SetLastUpdate();
    }

    public List<Player> GetMembers()
    {
        // custom: use regular teams for server-wide instance group recruitment (on official servers the recruiter must not be in a team)
        var team = GetRecruiter().GetCurrentTeam();
        return team == null ? members : team.GetMembers();
    }

    public int GetInstanceMaskId()
    {
        return instanceMaskId;
    }

    public int GetMinMembers()
    {
        return minMembers;
    }

    public string GetMessage()
    {
        return message;
    }

    public void SetMessage(string message)
    {
        this.message = message;
    }

    public int GetLastUpdate()
    {
        return lastUpdate;
    }

    public void SetLastUpdate()
    {
        this.lastUpdate = (int)(System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000);
    }

    public Player GetRecruiter()
    {
        return members[0];
    }

    public int GetId()
    {
        return GetRecruiter().GetObjectId();
    }

    public Race GetRace()
    {
        return GetRecruiter().GetRace();
    }

    public int GetMinLevel()
    {
        return members.Max(p => p.GetLevel());
    }

    public int GetMaxLevel()
    {
        return members.Min(p => p.GetLevel());
    }
}
