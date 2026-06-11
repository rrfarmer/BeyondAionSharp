using System.Collections.Generic;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Services.Mail;
using State = Aion.GameServer.Network.Aion.AionConnection.State;

namespace Aion.GameServer.Network.Aion.ClientPackets;

/// <summary>Java parity: network/aion/clientpackets/CM_CHECK_MAIL_LIST (ginho1). Requests the mailbox list (optionally express-only). MailService red-tolerated.</summary>
public class CM_CHECK_MAIL_LIST : AionClientPacket
{
    public bool expressOnly;

    public CM_CHECK_MAIL_LIST(int opcode, ISet<State> validStates)
        : base(opcode, validStates)
    {
    }

    protected override void ReadImpl()
    {
        expressOnly = ReadC() == 1;
    }

    protected override void RunImpl()
    {
        Player player = GetConnection().GetActivePlayer();
        if (player != null)
            MailService.SendMailList(player, expressOnly, false);
    }
}
