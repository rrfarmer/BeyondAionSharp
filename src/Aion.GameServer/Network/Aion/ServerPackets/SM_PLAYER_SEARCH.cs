using System.Collections.Generic;
using Aion.GameServer.Model.GameObjects.Player;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Network.Aion.Serverpackets;

/// <summary>Java parity: network/aion/serverpackets/SM_PLAYER_SEARCH (Ben). Fills the social-window player search panel (world/pos/class/gender/level/group-status/faction-prefixed name). static-import CHARNAME_MAX_LENGTH; DeniedStatus/ChatUtil red-tolerated.</summary>
public class SM_PLAYER_SEARCH : AionServerPacket
{
    private List<Player> players;

    public SM_PLAYER_SEARCH(List<Player> players)
    {
        this.players = players;
    }

    protected override void WriteImpl(AionConnection con)
    {
        Player activePlayer = con.GetActivePlayer();
        WriteH(players.Count);
        foreach (Player player in players)
        {
            WriteD(player.GetWorldId());
            WriteF(player.GetX());
            WriteF(player.GetY());
            WriteF(player.GetZ());
            WriteC(player.GetPlayerClass().GetClassId());
            WriteC(player.GetGender().GetGenderId());
            WriteC(player.GetLevel());
            WriteC(player.GetPlayerSettings().IsInDeniedStatus(DeniedStatus.GROUP) ? 1 : player.IsInTeam() ? 3 : player.IsLookingForGroup() ? 2 : 0);
            WriteS(ChatUtil.ToFactionPrefixedName(activePlayer, player), AbstractPlayerInfoPacket.CHARNAME_MAX_LENGTH + 2);
        }
    }
}
