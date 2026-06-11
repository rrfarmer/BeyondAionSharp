using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Skillengine.Effects;

namespace Aion.GameServer.Network.Aion.Serverpackets;

/// <summary>Java parity: network/aion/serverpackets/SM_MESSAGE (-Nemesiss-, Sweetkr, Neon). Chat/system message packet (sender id/name/race filter, type, shout coords); truncates over hardcap. Converges SystemMailService/MailService/PlayerEnterWorldService. instanceof->is; getName(true)/isSysMsg()/getId()->PascalCase; substring->Substring. ChatType/AbnormalState red-tolerated.</summary>
public class SM_MESSAGE : AionServerPacket
{
    private static readonly ILogger log = NullLoggerFactory.Instance.CreateLogger(nameof(SM_MESSAGE));

    /// <summary>Client can't handle more than 4000 chars in one packet (4001+ disables chat processing).</summary>
    public const int MESSAGE_SIZE_HARDCAP = 4000;
    /// <summary>Max chars the client will display from a single chat message packet (4.8).</summary>
    public const int MESSAGE_SIZE_LIMIT = 1022;

    private int senderObjectId;
    private string message;
    private string senderName;
    private byte senderRace; // 0: all, 1: elyos, 2: asmodian
    private ChatType chatType;
    private float x;
    private float y;
    private float z;

    public SM_MESSAGE(Player sender, string message, ChatType chatType)
        : this(sender, sender.GetEffectController().IsAbnormalSet(AbnormalState.HIDE) ? 0 : sender.GetObjectId(), sender.GetName(true), message, chatType)
    {
    }

    public SM_MESSAGE(Npc sender, string message, ChatType chatType)
        : this(sender, sender.GetObjectId(), sender.GetName(), message, chatType)
    {
    }

    public SM_MESSAGE(int senderObjectId, string senderName, string message, ChatType chatType)
        : this(null, senderObjectId, senderName, message, chatType)
    {
    }

    private SM_MESSAGE(Creature sender, int senderObjectId, string senderName, string message, ChatType chatType)
    {
        if (message.Length > MESSAGE_SIZE_LIMIT)
        {
            log.LogWarning("Exceeded maximum string size for packet SM_MESSAGE.\nSize: " + message.Length + "\nMessage: " + message);
            if (message.Length > MESSAGE_SIZE_HARDCAP)
                message = message.Substring(0, MESSAGE_SIZE_HARDCAP); // shorten message to avoid send log error
        }
        if (sender != null)
        {
            if (sender is Player && !chatType.IsSysMsg() && !CustomConfig.SPEAKING_BETWEEN_FACTIONS && !((Player)sender).IsStaff())
            {
                this.senderRace = (byte)(((Player)sender).GetRace().GetRaceId() + 1);
            }
            this.x = sender.GetX();
            this.y = sender.GetY();
            this.z = sender.GetZ();
        }
        this.senderObjectId = senderObjectId;
        this.senderName = senderName;
        this.message = message;
        this.chatType = chatType;
    }

    protected override void WriteImpl(AionConnection con)
    {
        Player activePlayer = con.GetActivePlayer();
        if (activePlayer == null)
            return;
        WriteC(chatType.GetId());
        WriteC(activePlayer.IsStaff() ? 0 : senderRace);
        WriteD(senderObjectId);
        WriteS(senderName);
        WriteS(message);
        if (chatType == ChatType.SHOUT)
        {
            WriteF(x);
            WriteF(y);
            WriteF(z);
        }
    }
}
