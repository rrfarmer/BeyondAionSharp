using System.Collections.Generic;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Services.Mail;
using State = Aion.GameServer.Network.Aion.AionConnection.State;

namespace Aion.GameServer.Network.Aion.ClientPackets;

/// <summary>Java parity: network/aion/clientpackets/CM_GET_MAIL_ATTACHMENT (kosyachok). Retrieves a mail's item (0) or kinah (1) attachment. MailService red-tolerated.</summary>
public class CM_GET_MAIL_ATTACHMENT : AionClientPacket
{
    private int mailObjId;
    private byte attachmentType;

    public CM_GET_MAIL_ATTACHMENT(int opcode, ISet<State> validStates)
        : base(opcode, validStates)
    {
    }

    protected override void ReadImpl()
    {
        mailObjId = ReadD();
        attachmentType = ReadC(); // 0 - item , 1 - kinah
    }

    protected override void RunImpl()
    {
        Player player = GetConnection().GetActivePlayer();
        MailService.GetAttachments(player, mailObjId, attachmentType);
    }
}
