using System.Text;
using Aion.Commons.Network;
using Aion.GameServer.Model.Account;

namespace Aion.GameServer.Network.Aion.ClientPackets;

public sealed class CmCreateCharacter : GameClientPacket
{
	public CmCreateCharacter(int opCode, IReadOnlySet<GameConnectionState> validStates)
		: base(opCode, validStates)
	{
	}

	public int AccountId { get; private set; }

	public string AccountName { get; private set; } = string.Empty;

	public string CharacterName { get; private set; } = string.Empty;

	public int GenderId { get; private set; }

	public int RaceId { get; private set; }

	public int ClassId { get; private set; }

	public CharacterAppearance Appearance { get; private set; } = new();

	public int Type { get; private set; }

	protected override void ReadPayload(PacketBuffer buffer)
	{
		// Java parity: network/aion/clientpackets/CM_CREATE_CHARACTER.readImpl.
		AccountId = buffer.ReadD();
		AccountName = buffer.ReadS();
		CharacterName = ReadFixedS(buffer, 25);
		GenderId = buffer.ReadD();
		RaceId = buffer.ReadD();
		ClassId = buffer.ReadD();
		Appearance = ReadAppearance(buffer);
		Type = buffer.ReadC();
	}

	private static CharacterAppearance ReadAppearance(PacketBuffer buffer)
	{
		// Java parity: network/aion/clientpackets/AbstractCharacterEditPacket.readAppearance.
		return new CharacterAppearance
		{
			Voice = buffer.ReadD(),
			SkinRgb = buffer.ReadD(),
			HairRgb = buffer.ReadD(),
			EyeRgb = buffer.ReadD(),
			LipRgb = buffer.ReadD(),
			Face = buffer.ReadC(),
			Hair = buffer.ReadC(),
			Deco = buffer.ReadC(),
			Tattoo = buffer.ReadC(),
			FaceContour = buffer.ReadC(),
			Expression = buffer.ReadC(),
			JawLine = SkipAndReadC(buffer),
			Forehead = buffer.ReadC(),
			EyeHeight = buffer.ReadC(),
			EyeSpace = buffer.ReadC(),
			EyeWidth = buffer.ReadC(),
			EyeSize = buffer.ReadC(),
			EyeShape = buffer.ReadC(),
			EyeAngle = buffer.ReadC(),
			BrowHeight = buffer.ReadC(),
			BrowAngle = buffer.ReadC(),
			BrowShape = buffer.ReadC(),
			Nose = buffer.ReadC(),
			NoseBridge = buffer.ReadC(),
			NoseWidth = buffer.ReadC(),
			NoseTip = buffer.ReadC(),
			Cheek = buffer.ReadC(),
			LipHeight = buffer.ReadC(),
			MouthSize = buffer.ReadC(),
			LipSize = buffer.ReadC(),
			Smile = buffer.ReadC(),
			LipShape = buffer.ReadC(),
			JawHeight = buffer.ReadC(),
			ChinJut = buffer.ReadC(),
			EarShape = buffer.ReadC(),
			HeadSize = buffer.ReadC(),
			Neck = buffer.ReadC(),
			NeckLength = buffer.ReadC(),
			ShoulderSize = buffer.ReadC(),
			Torso = buffer.ReadC(),
			Chest = buffer.ReadC(),
			Waist = buffer.ReadC(),
			Hips = buffer.ReadC(),
			ArmThickness = buffer.ReadC(),
			HandSize = buffer.ReadC(),
			LegThickness = buffer.ReadC(),
			FootSize = buffer.ReadC(),
			FacialRate = buffer.ReadC(),
			ArmLength = SkipAndReadC(buffer),
			LegLength = buffer.ReadC(),
			Shoulders = buffer.ReadC(),
			FaceShape = buffer.ReadC(),
			Height = SkipBytesAndReadF(buffer, 3),
		};
	}

	private static int SkipAndReadC(PacketBuffer buffer)
	{
		buffer.ReadC();
		return buffer.ReadC();
	}

	private static float SkipBytesAndReadF(PacketBuffer buffer, int byteCount)
	{
		for (var i = 0; i < byteCount; i++)
			buffer.ReadC();
		return buffer.ReadF();
	}

	private static string ReadFixedS(PacketBuffer buffer, int fixedLength)
	{
		// Java parity: AbstractCharacterEditPacket.readBasicInfo fixed character-name field.
		var builder = new StringBuilder();
		for (var i = 0; i < fixedLength; i++)
		{
			var value = buffer.ReadH();
			if (value != 0)
				builder.Append((char)value);
		}

		buffer.ReadH();
		return builder.ToString();
	}
}
