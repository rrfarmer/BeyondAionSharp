using System.Collections.Generic;
using Aion.GameServer.Model.GameObjects.Player;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Services.Mail;
using State = Aion.GameServer.Network.Aion.AionConnection.State;

namespace Aion.GameServer.Network.Aion.Clientpackets;

/// <summary>Java parity: network/aion/clientpackets/CM_READ_MAIL (kosyachok). Marks a mail read. MailService red-tolerated.</summary>
public class CM_READ_MAIL : AionClientPacket
{
    int mailObjId;

    public CM_READ_MAIL(int opcode, ISet<State> validStates)
        : base(opcode, validStates)
    {
    }

    protected override void ReadImpl()
    {
        mailObjId = ReadD();
    }

    protected override void RunImpl()
    {
        Player player = GetConnection().GetActivePlayer();
        MailService.ReadMail(player, mailObjId);
    }
}
