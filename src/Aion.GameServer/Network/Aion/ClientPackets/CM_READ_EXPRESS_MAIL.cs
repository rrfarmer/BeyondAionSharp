using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.Serverpackets;
using Aion.GameServer.SpawnEngine;
using Aion.GameServer.Utils;
using State = Aion.GameServer.Network.Aion.AionConnection.State;

namespace Aion.GameServer.Network.Aion.Clientpackets;

/// <summary>Java parity: network/aion/clientpackets/CM_READ_EXPRESS_MAIL (antness, Guapo). Closes postman (0) or summons it on icon click (1) with express/blackcloud unread + cooldown handling. VisibleObjectSpawner/ThreadPoolManager/LetterType red-tolerated.</summary>
public class CM_READ_EXPRESS_MAIL : AionClientPacket
{
    private static readonly ILogger log = NullLoggerFactory.Instance.CreateLogger(nameof(CM_READ_EXPRESS_MAIL));
    private byte action;

    public CM_READ_EXPRESS_MAIL(int opcode, ISet<State> validStates)
        : base(opcode, validStates)
    {
    }

    protected override void ReadImpl()
    {
        action = ReadC();
    }

    protected override void RunImpl()
    {
        Player player = GetConnection().GetActivePlayer();

        switch (action)
        {
            case 0: // window is closed
                if (player.GetPostman() != null)
                {
                    player.GetPostman().GetController().Delete();
                    player.SetPostman(null);
                }
                break;
            case 1: // click on icon
                bool haveUnreadExpress = player.GetMailbox().HaveUnreadByType(LetterType.EXPRESS);
                bool haveUnreadBlackcloud = player.GetMailbox().HaveUnreadByType(LetterType.BLACKCLOUD);
                if (player.GetPostman() != null)
                {
                    PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_POSTMAN_ALREADY_SUMMONED());
                }
                else if (player.IsFlying())
                {
                    PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_POSTMAN_UNABLE_IN_FLIGHT());
                }
                else if (haveUnreadBlackcloud)
                {
                    VisibleObjectSpawner.SpawnPostman(player);
                }
                else if (haveUnreadExpress)
                {
                    if (player.GetController().HasTask(TaskId.EXPRESS_MAIL_USE))
                    {
                        PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_POSTMAN_UNABLE_IN_COOLTIME());
                        return;
                    }
                    VisibleObjectSpawner.SpawnPostman(player);
                    player.GetController().AddTask(TaskId.EXPRESS_MAIL_USE,
                        ThreadPoolManager.GetInstance().Schedule(() => player.GetController().CancelTask(TaskId.EXPRESS_MAIL_USE), 600000)); // 10 min
                }
                break;
            default:
                log.LogWarning(player + " sent unknown read express mail action type: " + action);
                break;
        }
    }
}
