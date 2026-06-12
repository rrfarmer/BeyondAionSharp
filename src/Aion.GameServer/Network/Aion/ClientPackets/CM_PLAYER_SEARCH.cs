using System.Collections.Generic;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils;
using Aion.GameServer.World;
using Status = global::Aion.GameServer.Model.GameObjects.Players.FriendList.Status;
using State = global::Aion.GameServer.Network.Aion.AionConnection.State;

namespace Aion.GameServer.Network.Aion.ClientPackets;

/// <summary>Java parity: network/aion/clientpackets/CM_PLAYER_SEARCH (Ben). Social-panel / who search with race/status/lfg/name/level/class/region filters. World/SM_PLAYER_SEARCH/FriendList.Status red-tolerated.</summary>
public class CM_PLAYER_SEARCH : AionClientPacket
{
    /// <summary>The max number of players to return as results</summary>
    public const int MAX_RESULTS = 104; // 3.0

    private string name;
    private int region;
    private int classMask;
    private int minLevel;
    private int maxLevel;
    private int lfgOnly;

    public CM_PLAYER_SEARCH(int opcode, ISet<State> validStates)
        : base(opcode, validStates)
    {
    }

    protected override void ReadImpl()
    {
        name = Util.ConvertName(ReadS(25));
        region = ReadD();
        classMask = ReadD();
        minLevel = ReadUC();
        maxLevel = ReadUC();
        lfgOnly = ReadUC();
        ReadC(); // 0x00 in search pane 0x30 in /who?
    }

    protected override void RunImpl()
    {
        Player activePlayer = GetConnection().GetActivePlayer();

        if (activePlayer.GetLevel() < CustomConfig.LEVEL_TO_SEARCH)
        {
            SendPacket(SM_SYSTEM_MESSAGE.STR_CANT_WHO_LEVEL(CustomConfig.LEVEL_TO_SEARCH));
            return;
        }

        List<Player> matches = new List<Player>();
        foreach (Player player in global::Aion.GameServer.World.World.GetInstance().GetAllPlayers())
        {
            if (!activePlayer.IsStaff())
            { // staff can find all players
                if (player.GetRace() != activePlayer.GetRace() && !CustomConfig.FACTIONS_SEARCH_MODE)
                    continue;
                if (player.GetFriendList().GetStatus() == Status.OFFLINE)
                    continue;
                if (player.IsStaff() && !CustomConfig.SEARCH_GM_LIST)
                    continue;
            }
            if (lfgOnly == 1 && !player.IsLookingForGroup())
                continue;
            if (name.Length != 0 && !player.GetName().ToLower().Contains(name.ToLower()))
                continue;
            if (minLevel != 0xFF && player.GetLevel() < minLevel)
                continue;
            if (maxLevel != 0xFF && player.GetLevel() > maxLevel)
                continue;
            if (classMask > 0 && (1 << player.GetPlayerClass().GetClassId() & classMask) == 0)
                continue;
            if (region > 0 && player.GetWorldId() != region)
                continue;
            if (player.Equals(activePlayer))
                continue;

            matches.Add(player);

            if (matches.Count == MAX_RESULTS)
                break;
        }

        SendPacket(new SM_PLAYER_SEARCH(matches));
    }
}
