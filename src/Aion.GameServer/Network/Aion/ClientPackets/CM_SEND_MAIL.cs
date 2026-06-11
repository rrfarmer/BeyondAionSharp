using System.Collections.Generic;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Services.Mail;
using State = Aion.GameServer.Network.Aion.AionConnection.State;

namespace Aion.GameServer.Network.Aion.ClientPackets;

/// <summary>Java parity: network/aion/clientpackets/CM_SEND_MAIL (Aion Gates, xTz). Sends a mail with optional item + kinah. MailService/LetterType red-tolerated.</summary>
public class CM_SEND_MAIL : AionClientPacket
{
    private string recipientName;
    private string title;
    private string message;
    private int itemObjId;
    private long itemCount;
    private long kinahCount;
    private int idLetterType;

    public CM_SEND_MAIL(int opcode, ISet<State> validStates)
        : base(opcode, validStates)
    {
    }

    protected override void ReadImpl()
    {
        recipientName = ReadS();
        title = ReadS();
        message = ReadS();
        itemObjId = ReadD();
        itemCount = ReadQ();
        kinahCount = ReadQ();
        idLetterType = ReadUC();
    }

    protected override void RunImpl()
    {
        Player player = GetConnection().GetActivePlayer();
        MailService.SendMail(player, recipientName, title, message, itemObjId, itemCount, kinahCount, LetterTypeExtensions.GetLetterTypeById(idLetterType));
    }
}
