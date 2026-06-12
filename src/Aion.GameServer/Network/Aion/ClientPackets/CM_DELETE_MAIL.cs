using System.Collections.Generic;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Services.Mail;
using State = global::Aion.GameServer.Network.Aion.AionConnection.State;

namespace Aion.GameServer.Network.Aion.ClientPackets;

/// <summary>Java parity: network/aion/clientpackets/CM_DELETE_MAIL (kosyachok). Deletes a list of mails by object id. MailService red-tolerated.</summary>
public class CM_DELETE_MAIL : AionClientPacket
{
    private int[] mailObjIds;

    public CM_DELETE_MAIL(int opcode, ISet<State> validStates)
        : base(opcode, validStates)
    {
    }

    protected override void ReadImpl()
    {
        mailObjIds = new int[ReadUH()];
        for (int i = 0; i < mailObjIds.Length; i++)
        {
            mailObjIds[i] = ReadD();
            ReadC(); // unk
        }
    }

    protected override void RunImpl()
    {
        Player player = GetConnection().GetActivePlayer();
        MailService.DeleteMail(player, mailObjIds);
    }
}
