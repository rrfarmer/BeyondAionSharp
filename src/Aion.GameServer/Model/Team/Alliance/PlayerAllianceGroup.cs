using Aion.GameServer.Model.Team;
using Aion.GameServer.Model.Team.Common.Legacy;

namespace Aion.GameServer.Model.Team.Alliance;

/// <summary>Java parity: model/team/alliance/PlayerAllianceGroup (ATracer). extends TemporaryPlayerTeam&lt;PlayerAllianceMember&gt;.</summary>
public class PlayerAllianceGroup : TemporaryPlayerTeam<PlayerAllianceMember>
{
    private readonly PlayerAlliance alliance;

    public PlayerAllianceGroup(PlayerAlliance alliance, int objId)
        : base(objId, false)
    {
        this.alliance = alliance;
    }

    public override void AddMember(PlayerAllianceMember member)
    {
        base.AddMember(member);
        member.SetPlayerAllianceGroup(this);
        member.SetAllianceId(GetTeamId());
    }

    protected override void OnRemoveMember(PlayerAllianceMember member)
    {
        member.SetPlayerAllianceGroup(null);
    }

    public override int GetMaxMemberCount()
    {
        return 6;
    }

    public override int GetMinExpPlayerLevel()
    {
        // TODO Auto-generated method stub
        return 0;
    }

    public override int GetMaxExpPlayerLevel()
    {
        // TODO Auto-generated method stub
        return 0;
    }

    public PlayerAlliance GetAlliance()
    {
        return alliance;
    }

    public override LootGroupRules GetLootGroupRules()
    {
        return alliance.GetLootGroupRules();
    }
}
