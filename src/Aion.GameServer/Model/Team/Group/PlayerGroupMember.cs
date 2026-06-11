using Aion.GameServer.Model.Team;

namespace Aion.GameServer.Model.Team.Group;

/// <summary>Java parity: model/team/group/PlayerGroupMember (ATracer).</summary>
public class PlayerGroupMember : PlayerTeamMember
{
    public PlayerGroupMember(Aion.GameServer.Model.GameObjects.Players.Player player)
        : base(player)
    {
    }
}
