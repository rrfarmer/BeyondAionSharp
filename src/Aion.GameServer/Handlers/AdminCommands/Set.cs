using System;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.ChatHandlers;

namespace Aion.GameServer.Handlers.AdminCommands;

/// <summary>Java parity: data/handlers/admincommands/Set (Nemiroff, ATracer, IceReaper, Sarynth, Artur). Changes various player attributes.</summary>
public class Set : AdminCommand
{
    public Set()
        : base("set", "Changes various player attributes.")
    {
        SetSyntaxInfo(
            "class <value> - Sets the class of the selected player.",
            "level <value> - Sets the level of the selected player.",
            "exp <value> - Sets the experience points of the selected player.",
            "ap <value> - Sets the abyss points of the selected player.",
            "gp <value> - Sets the glory points of the selected player."
        );
    }

    public override void Execute(Player admin, params string[] paramsArr)
    {
        if (paramsArr.Length < 2)
        {
            SendInfo(admin);
            return;
        }
        if (!(admin.GetTarget() is Player target))
        {
            PacketSendUtility.SendPacket(admin, SM_SYSTEM_MESSAGE.STR_INVALID_TARGET());
            return;
        }

        if (paramsArr[0].Equals("class"))
        {
            PlayerClass playerClass = ParseEnumName<PlayerClass>(paramsArr[1].ToUpper());
            ClassChangeService.SetClass(target, playerClass, true, true);
        }
        else if (paramsArr[0].Equals("level"))
        {
            int level = Math.Min(GSConfig.PLAYER_MAX_LEVEL, ParseInt(paramsArr[1]));
            target.GetCommonData().SetLevel(level);
            SendInfo(admin, "Set " + target.GetName() + " level to " + target.GetLevel());
        }
        else if (paramsArr[0].Equals("exp"))
        {
            long exp = ParseLong(paramsArr[1]);
            target.GetCommonData().SetExp(exp);
            PacketSendUtility.SendMessage(admin, "Set exp of target to " + target.GetCommonData().GetExp());
        }
        else if (paramsArr[0].Equals("ap"))
        {
            int ap = ParseInt(paramsArr[1]);
            Aion.GameServer.Services.Abyss.AbyssPointsService.AddAp(target, ap - target.GetAbyssRank().GetAp());
            if (target != admin)
            {
                SendInfo(admin, "Set " + target.GetName() + "'s abyss points to " + target.GetAbyssRank().GetAp() + ".");
                SendInfo(target, "Admin set your abyss points to " + target.GetAbyssRank().GetAp() + ".");
            }
        }
        else if (paramsArr[0].Equals("gp"))
        {
            int gp = ParseInt(paramsArr[1]);
            Aion.GameServer.Services.Abyss.GloryPointsService.AddGp(target.GetObjectId(), gp - target.GetAbyssRank().GetCurrentGP());
            if (target != admin)
            {
                SendInfo(admin, "Set " + target.GetName() + "'s glory points to " + target.GetAbyssRank().GetCurrentGP() + ".");
                SendInfo(target, "Admin set your glory points to " + target.GetAbyssRank().GetCurrentGP() + ".");
            }
        }
    }
}
