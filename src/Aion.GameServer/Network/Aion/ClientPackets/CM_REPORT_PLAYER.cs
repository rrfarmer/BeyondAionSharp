using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Aion.GameServer.Model.GameObjects.Player;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.Serverpackets;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.Audit;
using Aion.GameServer.World;
using State = Aion.GameServer.Network.Aion.AionConnection.State;

namespace Aion.GameServer.Network.Aion.Clientpackets;

/// <summary>Java parity: network/aion/clientpackets/CM_REPORT_PLAYER (Jego, Neon). Handles /accuse (0) and /NumberofReports (1). The infinity glyph (U+221E) is built from its char code to keep source clean. World/AuditLogger/SM_SYSTEM_MESSAGE red-tolerated.</summary>
public class CM_REPORT_PLAYER : AionClientPacket
{
    private static readonly string INFINITY = ((char)0x221E).ToString();

    private int reportType;
    private string playerName;

    public CM_REPORT_PLAYER(int opcode, ISet<State> validStates)
        : base(opcode, validStates)
    {
    }

    protected override void ReadImpl()
    {
        reportType = ReadUC();
        playerName = ReadS(); // the name of the reported person.
    }

    protected override void RunImpl()
    {
        switch (reportType)
        {
            case 0: // /accuse, /AutoReportHunting
                Player activePlayer = GetConnection().GetActivePlayer();
                Player player = World.GetInstance().GetPlayer(ChatUtil.GetRealCharName(playerName));
                if (player != null && player.GetRace() != activePlayer.GetRace())
                {
                    SendPacket(SM_SYSTEM_MESSAGE.STR_MSG_DO_NOT_ACCUSE());
                }
                else if (activePlayer.Equals(player))
                {
                    SendPacket(SM_SYSTEM_MESSAGE.STR_INVALID_TARGET());
                }
                else
                {
                    AuditLogger.Log(activePlayer, "reported player " + playerName);
                    SendPacket(SM_SYSTEM_MESSAGE.STR_MSG_ACCUSE_SUBMIT(playerName, INFINITY));
                }
                break;
            case 1: // /NumberofReports
                SendPacket(SM_SYSTEM_MESSAGE.STR_MSG_ACCUSE_COUNT_INFO(INFINITY));
                break;
            default:
                NullLoggerFactory.Instance.CreateLogger(nameof(CM_REPORT_PLAYER)).LogWarning("Unhandled report type " + reportType + " (reported player: " + playerName + ")");
                break;
        }
    }
}
