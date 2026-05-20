using Aion.Commons.Network;
using Aion.GameServer.Model.Account;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public sealed class SmCreateCharacter : GameServerPacket
{
	public const int PacketOpCode = 201;
	public const int ResponseOk = 0;
	public const int FailedToCreateCharacter = 1;
	public const int ResponseDbError = 2;
	public const int ResponseServerLimitExceeded = 4;
	public const int ResponseInvalidName = 5;
	public const int ResponseForbiddenCharacterName = 9;
	public const int ResponseNameAlreadyUsed = 10;
	public const int ResponseNameReserved = 11;
	public const int ResponseOtherRace = 12;
	public const int ResponseForbiddenClass = 20;
	public const int ResponseOpenCreationWindow = 22;
	private readonly CharacterSelectionEntry? _character;

	public SmCreateCharacter(int responseCode, CharacterSelectionEntry? character = null)
		: base(PacketOpCode)
	{
		ResponseCode = responseCode;
		_character = character;
	}

	public int ResponseCode { get; }

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		// Java parity: network/aion/serverpackets/SM_CREATE_CHARACTER.writeImpl.
		buffer.WriteD(ResponseCode);
		if (ResponseCode == ResponseOk && _character != null)
			SmCharacterList.WritePlayerInfo(buffer, _character);
	}
}
