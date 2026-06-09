using Aion.GameServer.Model.Team;

namespace Aion.GameServer.Model.Team.Group;

/// <summary>Java parity: model/team/group/PlayerGroup (ATracer). extends TemporaryPlayerTeam&lt;PlayerGroupMember&gt;.</summary>
public class PlayerGroup : TemporaryPlayerTeam<PlayerGroupMember>
{
    private readonly PlayerGroupStats playerGroupStats;
    private TeamType type;

    public PlayerGroup(PlayerGroupMember leader, TeamType type, int id)
        : base(id == 0 ? Aion.GameServer.Utils.IdFactory.IDFactory.GetInstance().NextId() : id, id == 0)
    {
        this.playerGroupStats = new PlayerGroupStats(this);
        this.type = type;
        SetLeader(leader);
    }

    public override void AddMember(PlayerGroupMember member)
    {
        base.AddMember(member);
        playerGroupStats.OnAddPlayer(member);
        member.GetObject().SetPlayerGroup(this);
    }

    protected override void OnRemoveMember(PlayerGroupMember member)
    {
        playerGroupStats.OnRemovePlayer(member);
        member.GetObject().SetPlayerGroup(null);
    }

    public override int GetMaxMemberCount()
    {
        return 6;
    }

    public override int GetMinExpPlayerLevel()
    {
        return playerGroupStats.GetMinExpPlayerLevel();
    }

    public override int GetMaxExpPlayerLevel()
    {
        return playerGroupStats.GetMaxExpPlayerLevel();
    }

    public TeamType GetTeamType()
    {
        return type;
    }
}
