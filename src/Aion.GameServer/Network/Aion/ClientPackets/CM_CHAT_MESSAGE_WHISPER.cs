using System.Collections.Generic;
using System.Globalization;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Restrictions;
using Aion.GameServer.Services;
using Aion.GameServer.Services.Players;
using Aion.GameServer.Utils;
using Aion.GameServer.World;
using State = global::Aion.GameServer.Network.Aion.AionConnection.State;

namespace Aion.GameServer.Network.Aion.ClientPackets;

/// <summary>Java parity: network/aion/clientpackets/CM_CHAT_MESSAGE_WHISPER (SoulKeeper). Reads whisper chat messages and routes to the named receiver with refusal/level/block/faction guards. World/SM_MESSAGE/SM_SYSTEM_MESSAGE red-tolerated.</summary>
public class CM_CHAT_MESSAGE_WHISPER : AionClientPacket
{
    /// <summary>To whom this message is sent</summary>
    private string name;

    /// <summary>Message text</summary>
    private string message;

    public CM_CHAT_MESSAGE_WHISPER(int opcode, ISet<State> validStates)
        : base(opcode, validStates)
    {
    }

    protected override void ReadImpl()
    {
        name = ReadS();
        message = ReadS();
    }

    protected override void RunImpl()
    {
        string realName = ChatUtil.GetRealCharName(name);
        Player sender = GetConnection().GetActivePlayer();
        Player receiver = global::Aion.GameServer.World.World.GetInstance().GetPlayer(realName);

        if (receiver == null)
        {
            SendPacket(SM_SYSTEM_MESSAGE.STR_NO_SUCH_USER(realName));
        }
        else if (receiver.IsInCustomState(CustomPlayerState.NO_WHISPERS_MODE) && !sender.IsStaff())
        {
            SendPacket(SM_SYSTEM_MESSAGE.STR_WHISPER_REFUSE(receiver.GetName(true)));
        }
        else if (sender.GetLevel() < CustomConfig.LEVEL_TO_WHISPER && !receiver.IsStaff())
        {
            SendPacket(SM_SYSTEM_MESSAGE.STR_CANT_WHISPER_LEVEL(CustomConfig.LEVEL_TO_WHISPER.ToString(CultureInfo.InvariantCulture)));
        }
        else if (receiver.GetBlockList().Contains(sender.GetObjectId()))
        {
            SendPacket(SM_SYSTEM_MESSAGE.STR_YOU_EXCLUDED(receiver.GetName()));
        }
        else if (sender.GetRace() != receiver.GetRace() && !CustomConfig.SPEAKING_BETWEEN_FACTIONS && !sender.IsStaff() && !receiver.IsStaff())
        {
            SendPacket(SM_SYSTEM_MESSAGE.STR_MSG_CANT_WHISPER_OTHER_RACE());
        }
        else
        {
            if (!PlayerRestrictions.CanChat(sender))
                return;
            PlayerChatService.LogWhisper(sender, receiver, message);
            PacketSendUtility.SendPacket(receiver, new SM_MESSAGE(sender, NameRestrictionService.FilterMessage(message), ChatType.WHISPER));
        }
    }
}
