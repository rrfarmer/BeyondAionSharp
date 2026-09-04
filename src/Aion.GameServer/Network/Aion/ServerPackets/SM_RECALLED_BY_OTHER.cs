using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Network.Aion.ServerPackets;

/// <summary>
/// Java parity: network/aion/serverpackets/SM_RECALLED_BY_OTHER (SVDNESS). Opens the window which asks a player whether he wants to be teleported
/// to the caster of a summon skill, and closes it again when the request is no longer valid. The client answers with CM_RECALLED_BY_OTHER_ANSWER.
/// </summary>
public class SM_RECALLED_BY_OTHER : AionServerPacket
{
    private readonly string casterName;
    private readonly int skillId;
    private readonly int seconds;

    /// <summary>
    /// Closes the window on the client.
    /// </summary>
    public SM_RECALLED_BY_OTHER()
        : this(null, 0, 0)
    {
    }

    /// <param name="casterName">name of the summoning player</param>
    /// <param name="skillId">skill he used, its name is displayed in the window</param>
    /// <param name="seconds">time the player has to answer</param>
    public SM_RECALLED_BY_OTHER(string casterName, int skillId, int seconds)
    {
        this.casterName = casterName;
        this.skillId = skillId;
        this.seconds = seconds;
    }

    protected override void WriteImpl(AionConnection con)
    {
        WriteC(casterName == null ? 1 : 0); // 0 = open the window, 1 = close it
        WriteS(casterName!); // WriteS handles null (writes an empty string)
        WriteH(skillId);
        WriteH(seconds);
    }
}
