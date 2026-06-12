using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Team.Alliance;
using Aion.GameServer.Model.Team.Common.Events;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Model.Team.Alliance.Events;

/// <summary>
/// Java parity: model/team/alliance/events/ChangeMemberGroupEvent (ATracer) : AlwaysTrueTeamEvent. Swaps two members between alliance
/// groups, or moves one to a target group. PlayerAllianceEvent.MEMBER_GROUP_CHANGE -> MemberGroupChange; alliance.sendPackets(varargs)
/// -> SendPackets(params). PlayerAllianceGroup/SM_ALLIANCE_MEMBER_INFO red-tolerated.
/// </summary>
public class ChangeMemberGroupEvent : AlwaysTrueTeamEvent
{
    private readonly PlayerAlliance alliance;
    private readonly int firstMemberId;
    private readonly int secondMemberId;
    private readonly int allianceGroupId;

    public ChangeMemberGroupEvent(PlayerAlliance alliance, int firstMemberId, int secondMemberId, int allianceGroupId)
    {
        this.alliance = alliance;
        this.firstMemberId = firstMemberId;
        this.secondMemberId = secondMemberId;
        this.allianceGroupId = allianceGroupId;
    }

    public override void HandleEvent()
    {
        PlayerAllianceMember firstMember = alliance.GetMember(firstMemberId);
        if (firstMember == null) // probably left or got kicked right before handleEvent() was called
            return;
        if (secondMemberId != 0)
        {
            PlayerAllianceMember secondMember = alliance.GetMember(secondMemberId);
            if (secondMember == null) // probably left or got kicked right before handleEvent() was called
                return;
            SwapMembersInGroup(firstMember, secondMember);
        }
        else
        {
            MoveMemberToGroup(firstMember, allianceGroupId);
        }
    }

    private void SwapMembersInGroup(PlayerAllianceMember firstMember, PlayerAllianceMember secondMember)
    {
        PlayerAllianceGroup firstAllianceGroup = firstMember.GetPlayerAllianceGroup();
        PlayerAllianceGroup secondAllianceGroup = secondMember.GetPlayerAllianceGroup();
        firstAllianceGroup.RemoveMember(firstMember);
        secondAllianceGroup.RemoveMember(secondMember);
        firstAllianceGroup.AddMember(secondMember);
        secondAllianceGroup.AddMember(firstMember);
        alliance.SendPackets(new SM_ALLIANCE_MEMBER_INFO(firstMember, PlayerAllianceEvent.MEMBER_GROUP_CHANGE),
            new SM_ALLIANCE_MEMBER_INFO(secondMember, PlayerAllianceEvent.MEMBER_GROUP_CHANGE));
    }

    private void MoveMemberToGroup(PlayerAllianceMember firstMember, int allianceGroupId)
    {
        PlayerAllianceGroup firstAllianceGroup = firstMember.GetPlayerAllianceGroup();
        firstAllianceGroup.RemoveMember(firstMember);
        PlayerAllianceGroup newAllianceGroup = alliance.GetAllianceGroup(allianceGroupId);
        newAllianceGroup.AddMember(firstMember);
        alliance.SendPackets(new SM_ALLIANCE_MEMBER_INFO(firstMember, PlayerAllianceEvent.MEMBER_GROUP_CHANGE));
    }
}
