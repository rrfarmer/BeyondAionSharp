using Aion.GameServer.Model.Account;
using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Network.Aion.Serverpackets;

/// <summary>Java parity: network/aion/serverpackets/SM_CREATE_CHARACTER (Nemesiss, AEJTester, Neon). Response for CM_CREATE_CHARACTER: writes response code, then WritePlayerInfo for the new character if RESPONSE_OK. Extends AbstractPlayerInfoPacket.</summary>
public class SM_CREATE_CHARACTER : AbstractPlayerInfoPacket
{
    /// <summary>If response is ok</summary>
    public const int RESPONSE_OK = 0x00;
    /// <summary>Failed to create the character</summary>
    public const int FAILED_TO_CREATE_THE_CHARACTER = 1;
    /// <summary>Failed to create the character due to world db error</summary>
    public const int RESPONSE_DB_ERROR = 2;
    /// <summary>The number of characters exceeds the maximum allowed for the server</summary>
    public const int RESPONSE_SERVER_LIMIT_EXCEEDED = 4;
    /// <summary>Invalid character name</summary>
    public const int RESPONSE_INVALID_NAME = 5;
    /// <summary>The name includes forbidden words</summary>
    public const int RESPONSE_FORBIDDEN_CHAR_NAME = 9;
    /// <summary>A character with that name already exists</summary>
    public const int RESPONSE_NAME_ALREADY_USED = 10;
    /// <summary>The name is already reserved</summary>
    public const int RESPONSE_NAME_RESERVED = 11;
    /// <summary>You cannot create characters of other races in the same server</summary>
    public const int RESPONSE_OTHER_RACE = 12;
    /// <summary>You cannot create the selected class in the current server</summary>
    public const int RESPONSE_FORBIDDEN_CLASS = 20;
    /// <summary>open create characters window</summary>
    public const int RESPONSE_OPEN_CREATION_WINDOW = 22;

    /// <summary>
    /// response code
    /// </summary>
    private readonly int responseCode;

    /// <summary>
    /// Newly created player.
    /// </summary>
    private readonly PlayerAccountData playerAccData;

    /// <summary>
    /// Constructs new <c>SM_CREATE_CHARACTER</c> packet
    /// </summary>
    /// <param name="accPlData">playerAccountData of player that was created</param>
    /// <param name="responseCode">response code (invalid nickname, nickname is already taken, ok)</param>
    public SM_CREATE_CHARACTER(PlayerAccountData accPlData, int responseCode)
    {
        this.playerAccData = accPlData;
        this.responseCode = responseCode;
    }

    protected override void WriteImpl(AionConnection con)
    {
        WriteD(responseCode);

        if (responseCode != RESPONSE_OK)
            return;

        WritePlayerInfo(playerAccData, con);
    }
}
