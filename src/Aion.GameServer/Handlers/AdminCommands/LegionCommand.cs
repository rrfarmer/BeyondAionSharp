using System;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Team.Legion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;
using Aion.GameServer.Services.Players;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.ChatHandlers;
using Aion.GameServer.World;

namespace Aion.GameServer.Handlers.AdminCommands;

/// <summary>Java parity: data/handlers/admincommands/LegionCommand (KID).</summary>
public class LegionCommand : AdminCommand
{
    public LegionCommand()
        : base("legion", "Modifies a legion.")
    {
        SetSyntaxInfo(
            "info <legion name> - List legion members.",
            "add <legion name> <player name> - Adds the player to the legion.",
            "kick <player name> - Kicks the player from their legion.",
            "disband <legion name> - Disbands the legion.",
            "rename <legion name> <new name> - Changes the legion's name.",
            "setbg <player name> - Appoints the player as brigade general of their legion.",
            "setlevel <legion name> <level> - Changes the legion's level.",
            "setpoints <legion name> <points> - Changes the legion's contributing points."
        );
    }

    protected override void Execute(Player player, params string[] paramsArr)
    {
        if (paramsArr.Length < 2)
        {
            SendInfo(player);
            return;
        }

        if (paramsArr[0].Equals("disband", StringComparison.OrdinalIgnoreCase))
        {
            Legion legion = GetLegion(paramsArr[1]);
            LegionService.GetInstance().DisbandLegion(legion);
            SendInfo(player, "Legion " + legion.GetName() + " was disbanded.");
        }
        else if (paramsArr[0].Equals("setlevel", StringComparison.OrdinalIgnoreCase) && paramsArr.Length >= 3)
        {
            Legion legion = GetLegion(paramsArr[1]);
            int level = int.Parse(paramsArr[2]);
            if (level < 1 || level > 8)
            {
                SendInfo(player, "Legion level must be between 1 and 8.");
                return;
            }
            else if (level == legion.GetLegionLevel())
            {
                SendInfo(player, "Legion " + paramsArr[1] + " is already on level " + level);
                return;
            }
            int old = legion.GetLegionLevel();
            LegionService.GetInstance().ChangeLevel(legion, level, true);
            SendInfo(player, "Legion " + legion.GetName() + " level was changed from " + old + " to " + level);
        }
        else if (paramsArr[0].Equals("setpoints", StringComparison.OrdinalIgnoreCase) && paramsArr.Length >= 3)
        {
            Legion legion = GetLegion(paramsArr[1]);
            long points = long.Parse(paramsArr[2]);
            if (points < 1)
            {
                SendInfo(player, "Points must be larger than zero.");
                return;
            }
            long old = legion.GetContributionPoints();
            LegionService.GetInstance().SetContributionPoints(legion, points, true);
            SendInfo(player, "Legion " + legion.GetName() + " contribution points were changed from " + old + " to " + points);
        }
        else if (paramsArr[0].Equals("rename", StringComparison.OrdinalIgnoreCase) && paramsArr.Length >= 3)
        {
            Legion legion = GetLegion(paramsArr[1]);
            string old = legion.GetName();
            if (LegionService.GetInstance().TryRename(legion, paramsArr[2], player, null))
                SendInfo(player, "Legion " + old + " was renamed to " + legion.GetName() + ".");
        }
        else if (paramsArr[0].Equals("info", StringComparison.OrdinalIgnoreCase))
        {
            Legion legion = GetLegion(paramsArr[1]);
            SendInfo(player, "Legion name: " + legion.GetName());
            SendInfo(player, "Level: " + legion.GetLegionLevel());
            SendInfo(player, "Contribution points: " + legion.GetContributionPoints());
            SendInfo(player, "Members (" + legion.GetMemberIds().Count + "):");
            foreach (LegionMember lm in legion.GetMembers())
            {
                string brigadeGeneralInfo = lm.IsBrigadeGeneral() ? ", brigade general" : "";
                SendInfo(player, "\t" + lm.GetName() + " (lv " + lm.GetLevel() + " " + lm.GetPlayerClass() + brigadeGeneralInfo + ")");
            }
        }
        else if (paramsArr[0].Equals("kick", StringComparison.OrdinalIgnoreCase))
        {
            Player target = World.World.GetInstance().GetPlayer(Util.ConvertName(paramsArr[1]));
            if (target == null)
                PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_NO_SUCH_USER(paramsArr[1]));
            else if (target.GetLegionMember().IsBrigadeGeneral())
                PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_GUILD_BANISH_CAN_BANISH_MASTER());
            else if (LegionService.GetInstance().LeaveLegion(target, true))
                SendInfo(player, target.GetName() + " was kicked from the legion.");
            else
                SendInfo(player, target.GetName() + " could not be kicked from the legion.");
        }
        else if (paramsArr[0].Equals("add", StringComparison.OrdinalIgnoreCase) && paramsArr.Length >= 3)
        {
            Legion legion = GetLegion(paramsArr[1]);
            Player target = World.World.GetInstance().GetPlayer(Util.ConvertName(paramsArr[2]));
            if (target == null)
                PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_NO_SUCH_USER(paramsArr[2]));
            else if (target.IsLegionMember())
                PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_GUILD_INVITE_HE_IS_OTHER_GUILD_MEMBER(target.GetName()));
            else if (LegionService.GetInstance().AddToLegion(legion, target, player))
                SendInfo(player, target.GetName() + " was added to " + legion.GetName());
        }
        else if (paramsArr[0].Equals("setbg", StringComparison.OrdinalIgnoreCase))
        {
            PlayerCommonData playerCommonData = PlayerService.GetOrLoadPlayerCommonData(Util.ConvertName(paramsArr[1]));
            if (playerCommonData == null)
            {
                PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_NO_USER_NAMED(paramsArr[1]));
                return;
            }
            LegionMember legionMember = LegionService.GetInstance().GetLegionMember(playerCommonData);
            if (legionMember == null)
            {
                SendInfo(player, playerCommonData.GetName() + " is not a member of any legion.");
                return;
            }
            string legionName = legionMember.GetLegion().GetName();
            string previousBrigadeGeneral = legionMember.GetLegion().GetBrigadeGeneral().GetName();
            LegionService.GetInstance().AppointBrigadeGeneral(legionMember);
            string newBrigadeGeneral = legionMember.GetLegion().GetBrigadeGeneral().GetName();
            SendInfo(player, "The brigade general of legion " + legionName + " was changed from " + previousBrigadeGeneral + " to " + newBrigadeGeneral + ".");
        }
        else
        {
            SendInfo(player);
        }
    }

    private Legion GetLegion(string name)
    {
        if (name.Contains("_"))
            name = name.Replace("_", " ");
        Legion legion = LegionService.GetInstance().GetLegion(name.ToLower());
        if (legion == null)
        {
            throw new ArgumentException("Legion " + name + " does not exist.");
        }
        return legion;
    }
}
