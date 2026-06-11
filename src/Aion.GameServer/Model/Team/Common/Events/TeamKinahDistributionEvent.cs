using System.Collections.Generic;
using Aion.GameServer.Model.GameObjects.Player;
using Aion.GameServer.Network.Aion.Serverpackets;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Model.Team.Common.Events;

/// <summary>
/// Java parity: model/team/common/events/TeamKinahDistributionEvent (ATracer) : AbstractTeamPlayerEvent. Splits a kinah amount evenly
/// among online members. Java &lt;T extends TemporaryPlayerTeam&lt;? extends TeamMember&lt;Player&gt;&gt;&gt; -> where T : class (pillar
/// convention). team.HasMember/GetOnlineMembers + SM_SYSTEM_MESSAGE.STR_* red-tolerated.
/// </summary>
public class TeamKinahDistributionEvent<T> : AbstractTeamPlayerEvent<T>
    where T : class
{
    private readonly long amount;

    public TeamKinahDistributionEvent(T team, Player distributor, long amount)
        : base(team, distributor)
    {
        this.amount = amount;
    }

    public override bool CheckCondition()
    {
        return team.HasMember(eventPlayer.GetObjectId());
    }

    public override void HandleEvent()
    {
        if (eventPlayer.GetInventory().GetKinah() < amount)
        {
            PacketSendUtility.SendPacket(eventPlayer, SM_SYSTEM_MESSAGE.STR_NOT_ENOUGH_MONEY());
            return;
        }

        List<Player> onlineMembers = team.GetOnlineMembers();
        if (onlineMembers.Count > 1 && amount >= onlineMembers.Count)
        {
            long rewardPerPlayer = amount / onlineMembers.Count;
            if (eventPlayer.GetInventory().TryDecreaseKinah(amount))
            {
                foreach (Player member in onlineMembers)
                {
                    member.GetInventory().IncreaseKinah(rewardPerPlayer);
                    if (member.Equals(eventPlayer))
                    {
                        PacketSendUtility.SendPacket(member, SM_SYSTEM_MESSAGE.STR_MSG_SPLIT_ME_TO_B(amount, onlineMembers.Count, rewardPerPlayer));
                    }
                    else
                    {
                        PacketSendUtility.SendPacket(member,
                            SM_SYSTEM_MESSAGE.STR_MSG_SPLIT_B_TO_ME(eventPlayer.GetName(), amount, onlineMembers.Count, rewardPerPlayer));
                    }
                }
            }
        }
    }
}
