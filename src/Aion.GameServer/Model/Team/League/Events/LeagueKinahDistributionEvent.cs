using System.Collections.Generic;
using Aion.GameServer.Model.GameObjects.Player;
using Aion.GameServer.Model.Team.Common.Events;
using Aion.GameServer.Model.Team.League;
using Aion.GameServer.Network.Aion.Serverpackets;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Model.Team.League.Events;

/// <summary>
/// Java parity: model/team/league/events/LeagueKinahDistributionEvent (Source) : AlwaysTrueTeamEvent. Splits a kinah amount evenly
/// among the league's online members (mirrors TeamKinahDistributionEvent). SM_SYSTEM_MESSAGE.STR_* + League/inventory red-tolerated.
/// </summary>
public class LeagueKinahDistributionEvent : AlwaysTrueTeamEvent
{
    private readonly long amount;
    private readonly Player eventPlayer;

    public LeagueKinahDistributionEvent(Player player, long amount)
    {
        eventPlayer = player;
        this.amount = amount;
    }

    public override void HandleEvent()
    {
        if (eventPlayer.GetInventory().GetKinah() < amount)
        {
            PacketSendUtility.SendPacket(eventPlayer, SM_SYSTEM_MESSAGE.STR_NOT_ENOUGH_MONEY());
            return;
        }

        League league = eventPlayer.GetPlayerAlliance().GetLeague();
        List<Player> onlineMembers = league.GetOnlineMembers();
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
