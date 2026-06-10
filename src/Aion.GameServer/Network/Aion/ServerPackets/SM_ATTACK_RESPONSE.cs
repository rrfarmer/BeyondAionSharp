using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Network.Aion.Serverpackets;

/// <summary>Java parity: network/aion/serverpackets/SM_ATTACK_RESPONSE. Attack-stop/notice reply built via static factories (different-area/invalid-target/too-far/obstacle/too-close/no-message). Writes message + attack count.</summary>
public class SM_ATTACK_RESPONSE : AionServerPacket
{
    private int message; // 3 unk
    private int attackCount;

    public static SM_ATTACK_RESPONSE TARGET_IN_DIFFERENT_AREA(int count)
    {
        return new SM_ATTACK_RESPONSE(1, count);
    }

    // stops attacks
    public static SM_ATTACK_RESPONSE STOP_INVALID_TARGET(int count)
    {
        return new SM_ATTACK_RESPONSE(2, count);
    }

    public static SM_ATTACK_RESPONSE TARGET_TOO_FAR_AWAY(int count)
    {
        return new SM_ATTACK_RESPONSE(4, count);
    }

    // stops attacks
    public static SM_ATTACK_RESPONSE STOP_OBSTACLE_IN_THE_WAY(int count)
    {
        return new SM_ATTACK_RESPONSE(5, count);
    }

    // stops attacks
    public static SM_ATTACK_RESPONSE STOP_TOO_CLOSE_TO_ATTACK(int count)
    {
        return new SM_ATTACK_RESPONSE(6, count);
    }

    // stops attacks
    public static SM_ATTACK_RESPONSE STOP_WITHOUT_MESSAGE(int count)
    {
        return new SM_ATTACK_RESPONSE(7, count);
    }

    private SM_ATTACK_RESPONSE(int message, int attackCount)
    {
        this.message = message;
        this.attackCount = attackCount;
    }

    protected override void WriteImpl(AionConnection con)
    {
        WriteC(message);
        WriteC(attackCount);
    }
}
