using System;
using System.Linq;
using System.Text;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Team;
using Aion.GameServer.Model.Team.Alliance;
using Aion.GameServer.Model.Team.Group;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services.Teleport;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.ChatHandlers;
using Aion.GameServer.World;

namespace Aion.GameServer.Handlers.AdminCommands;

/// <summary>Java parity: data/handlers/admincommands/Event (Nathan, Estrayl, Neon). Manages event functions and player event-states.</summary>
public class Event : AdminCommand
{
    public Event()
        : base("event", "Manages event functions and player event-states.")
    {
        SetSyntaxInfo(
            "<setStatus> [name] - Disables ap gain/loss for the given player and sets him to event state.",
            "<setGroupStatus> [name] - Gets and sets the group of the given player to event state and disables ap gain/loss for them.",
            "<setEnemy> <cancel|team|ffa> [name] - Sets the specific state (cancel: normal, team: everyone outside the players team is an enemy, ffa: everyone is an enemy).",
            "<pvpSpawn> [asmo|elyos] - Sets a resurrection point for the given race.",
            "<clearInstance> - Clears the whole instance you have created.",
            "<announce> <text> - Sends a yellow message for all players in event state.",
            "<list> - Lists all players in event state.",
            "<removeAll> - Removes all players from event state."
        );
    }

    public override void Execute(Player admin, params string[] paramsArr)
    {
        if (paramsArr.Length == 0)
        {
            SendInfo(admin);
            return;
        }

        if (paramsArr[0].Equals("pvpSpawn", StringComparison.OrdinalIgnoreCase))
        {
            if (paramsArr[1].Equals("asmo", StringComparison.OrdinalIgnoreCase))
            {
                TeleportService.SetEventPos(admin.GetPosition(), Race.ASMODIANS);
                SendInfo(admin, "Eventspawn for Asmodians was set!");
            }
            else if (paramsArr[1].Equals("elyos", StringComparison.OrdinalIgnoreCase) || paramsArr[1].Equals("ely", StringComparison.OrdinalIgnoreCase))
            {
                TeleportService.SetEventPos(admin.GetPosition(), Race.ELYOS);
                SendInfo(admin, "Eventspawn for Elyos was set!");
            }
            else
            {
                SendInfo(admin, "Invalid race parameter!");
            }
        }
        else if (paramsArr[0].Equals("clearInstance", StringComparison.OrdinalIgnoreCase))
        {
            ClearInstance(admin);
        }
        else if (paramsArr[0].Equals("announce", StringComparison.OrdinalIgnoreCase))
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(ChatUtil.Name(admin)).Append(':');
            for (int i = 1; i < paramsArr.Length; i++)
                sb.Append(" ").Append(paramsArr[i]);

            World.World.GetInstance().ForEachPlayer(p =>
            {
                if (p.IsInCustomState(CustomPlayerState.EVENT_MODE) || p == admin)
                    PacketSendUtility.SendMessage(p, sb.ToString(), ChatType.BRIGHT_YELLOW_CENTER);
            });
        }
        else if (paramsArr[0].Equals("list", StringComparison.OrdinalIgnoreCase))
        {
            StringBuilder sb = new StringBuilder("Players in event state:");
            foreach (Player p in World.World.GetInstance().GetAllPlayers().Where(p => p.IsInCustomState(CustomPlayerState.EVENT_MODE)))
                sb.Append("\n\t").Append(ChatUtil.Name(p));
            SendInfo(admin, sb.ToString());
        }
        else if (paramsArr[0].Equals("removeAll", StringComparison.OrdinalIgnoreCase))
        {
            foreach (Player player in World.World.GetInstance().GetAllPlayers())
                SetEventState(admin, player, true);
        }
        else if (paramsArr[0].Equals("setStatus", StringComparison.OrdinalIgnoreCase))
        {
            Player player = GetPlayer(admin, paramsArr.Length > 1 ? paramsArr[1] : null);
            if (player == null)
                return;
            SetEventState(admin, player, false);
        }
        else if (paramsArr[0].Equals("setGroupStatus", StringComparison.OrdinalIgnoreCase))
        {
            Player player = GetPlayer(admin, paramsArr.Length > 1 ? paramsArr[1] : null);
            if (player == null)
                return;
            TemporaryPlayerTeam team = player.GetCurrentTeam();
            if (team == null)
            {
                SendInfo(admin, "The target is not in a group or alliance!");
                return;
            }
            foreach (Player p in team.GetOnlineMembers())
                SetEventState(admin, p, false);
        }
        else if (paramsArr.Length > 1 && paramsArr[0].Equals("setEnemy", StringComparison.OrdinalIgnoreCase))
        {
            Player player = GetPlayer(admin, paramsArr.Length > 2 ? paramsArr[2] : null);
            if (player == null)
                return;
            if (!player.IsInCustomState(CustomPlayerState.EVENT_MODE))
            {
                SendInfo(admin, player.GetName() + " is not in event state");
                return;
            }
            bool ffaTeamMode = false;
            string msg = "no longer in FFA state.";
            if (paramsArr[1].Equals("cancel", StringComparison.OrdinalIgnoreCase))
            {
                player.UnsetCustomState(CustomPlayerState.ENEMY_OF_ALL_PLAYERS);
            }
            else if (paramsArr[1].Equals("team", StringComparison.OrdinalIgnoreCase))
            {
                player.SetCustomState(CustomPlayerState.ENEMY_OF_ALL_PLAYERS);
                msg = "in Team-FFA state now.";
                ffaTeamMode = true;
            }
            else if (paramsArr[1].Equals("ffa", StringComparison.OrdinalIgnoreCase))
            {
                player.SetCustomState(CustomPlayerState.ENEMY_OF_ALL_PLAYERS);
                msg = "in FFA state now.";
                PlayerGroupService.RemovePlayer(player);
                PlayerAllianceService.RemovePlayer(player);
            }
            else
            {
                SendInfo(admin);
                return;
            }
            player.SetInFfaTeamMode(ffaTeamMode);
            player.GetController().OnChangedPlayerAttributes();
            SendInfo(admin, ChatUtil.Name(player) + " is " + msg);
            PacketSendUtility.SendMessage(player, "You are " + msg, ChatType.BRIGHT_YELLOW_CENTER);
        }
        else
        {
            SendInfo(admin);
        }
    }

    private Player GetPlayer(Player admin, string name)
    {
        Player player = null;
        if (name != null)
        {
            string playerName = Util.ConvertName(name);
            player = World.World.GetInstance().GetPlayer(playerName);
            if (player == null)
            {
                PacketSendUtility.SendPacket(admin, SM_SYSTEM_MESSAGE.STR_NO_SUCH_USER(playerName));
            }
        }
        else if (admin.GetTarget() is Player target)
        {
            player = target;
        }
        else
        {
            PacketSendUtility.SendPacket(admin, SM_SYSTEM_MESSAGE.STR_INVALID_TARGET());
        }
        return player;
    }

    private void ClearInstance(Player admin)
    {
        WorldMapInstance map = admin.GetPosition().GetWorldMapInstance();
        if (!map.GetParent().IsInstanceType())
        {
            SendInfo(admin, "This map is not an instance!");
            return;
        }
        if (map.GetRegisteredCount() != 1 || !map.IsRegistered(admin.GetObjectId()))
        {
            SendInfo(admin, "This instance was not created by you, you cannot delete NPCs here. Use //goto to create a new one!");
            return;
        }
        int[] count = { 0 };
        map.ForEachNpc(npc =>
        {
            npc.GetController().Delete();
            count[0]++;
        });
        map.ForEachDoor(door => door.SetOpen(true));

        SendInfo(admin, "Deleted " + count[0] + " NPCs.");
    }

    private void SetEventState(Player admin, Player player, bool onlyRemove)
    {
        if (player.IsInCustomState(CustomPlayerState.EVENT_MODE))
        {
            player.UnsetCustomState(CustomPlayerState.EVENT_MODE);
            player.UnsetCustomState(CustomPlayerState.ENEMY_OF_ALL_PLAYERS);
            player.SetInFfaTeamMode(false);
            player.GetController().OnChangedPlayerAttributes();
            SendInfo(admin, ChatUtil.Name(player) + " was removed from event state.");
            PacketSendUtility.SendMessage(player, "You were removed from event state!", ChatType.BRIGHT_YELLOW_CENTER);
        }
        else if (!onlyRemove)
        {
            player.SetCustomState(CustomPlayerState.EVENT_MODE);
            SendInfo(admin, ChatUtil.Name(player) + " was set in event state.");
            PacketSendUtility.SendMessage(player,
                "You are in event state now. Please notice that you are not allowed to leave the event without removal of this state!",
                ChatType.BRIGHT_YELLOW_CENTER);
        }
    }
}
