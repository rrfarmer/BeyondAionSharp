using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Network.Aion.ServerPackets;

/// <summary>Java parity: network/aion/serverpackets/SM_CLOSE_QUESTION_WINDOW (Neon). Closes a question window with an optional reason message + up to 3 params (e.g. duel withdraw/reject). Object...->params object[]; String.valueOf->ToString; field `params`->`parameters` (C# keyword). AionServerPacket red-tolerated.</summary>
public class SM_CLOSE_QUESTION_WINDOW : AionServerPacket
{
    /// <summary>%0 has withdrawn the challenge for a duel.</summary>
    public static SM_CLOSE_QUESTION_WINDOW STR_DUEL_REQUESTER_WITHDRAW_REQUEST(string value0)
    {
        return new SM_CLOSE_QUESTION_WINDOW(1300134, value0);
    }

    /// <summary>%0 declined your challenge.</summary>
    public static SM_CLOSE_QUESTION_WINDOW STR_DUEL_HE_REJECT_DUEL(string value0)
    {
        return new SM_CLOSE_QUESTION_WINDOW(1300097, value0);
    }

    public static SM_CLOSE_QUESTION_WINDOW CLOSE_QUESTION_WINDOW()
    {
        return new SM_CLOSE_QUESTION_WINDOW(0);
    }

    private const int MAX_PARAM_COUNT = 3;

    private readonly int msgId;
    private readonly object[] parameters;

    public SM_CLOSE_QUESTION_WINDOW(int msgId, params object[] parameters)
    {
        this.msgId = msgId;
        this.parameters = parameters;
    }

    protected override void WriteImpl(AionConnection con)
    {
        WriteD(0); // maybe a target object id?
        WriteD(msgId); // reason
        for (int i = 0; i < MAX_PARAM_COUNT; i++) // client only supports three parameters in this package (fourth will not be rendered)
            WriteS(i < parameters.Length ? parameters[i].ToString() : null);
        // unknown what follows here
    }
}
