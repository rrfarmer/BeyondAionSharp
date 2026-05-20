using Aion.Commons.Network;
using Aion.GameServer.Model.Account;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public sealed class SmCharacterList : GameServerPacket
{
	public const int PacketOpCode = 200;
	private const int CharacterNameMaxLength = 25;
	private readonly IReadOnlyList<CharacterSelectionEntry> _characters;

	public SmCharacterList(int playOk2)
		: this(playOk2, Array.Empty<CharacterSelectionEntry>())
	{
	}

	public SmCharacterList(int playOk2, IReadOnlyList<CharacterSelectionEntry>? characters)
		: base(PacketOpCode)
	{
		PlayOk2 = playOk2;
		_characters = characters ?? Array.Empty<CharacterSelectionEntry>();
	}

	public int PlayOk2 { get; }

	public int CharacterCount => _characters.Count;

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		// Java parity: network/aion/serverpackets/SM_CHARACTER_LIST.writeImpl.
		buffer.WriteD(PlayOk2);
		buffer.WriteC(CharacterCount);
		foreach (var character in _characters)
			WritePlayerInfo(buffer, character);
	}

	internal static void WritePlayerInfo(PacketBuffer buffer, CharacterSelectionEntry character)
	{
		// Java parity: network/aion/serverpackets/AbstractPlayerInfoPacket.writePlayerInfo.
		var appearance = character.Appearance;

		buffer.WriteD(character.ObjectId);
		WriteFixedS(buffer, character.Name, CharacterNameMaxLength);
		buffer.WriteD(character.GenderId);
		buffer.WriteD(character.RaceId);
		buffer.WriteD(character.ClassId);
		buffer.WriteD(appearance.Voice);
		buffer.WriteD(appearance.SkinRgb);
		buffer.WriteD(appearance.HairRgb);
		buffer.WriteD(appearance.EyeRgb);
		buffer.WriteD(appearance.LipRgb);
		buffer.WriteC(appearance.Face);
		buffer.WriteC(appearance.Hair);
		buffer.WriteC(appearance.Deco);
		buffer.WriteC(appearance.Tattoo);
		buffer.WriteC(appearance.FaceContour);
		buffer.WriteC(appearance.Expression);
		buffer.WriteC(5);
		buffer.WriteC(appearance.JawLine);
		buffer.WriteC(appearance.Forehead);
		buffer.WriteC(appearance.EyeHeight);
		buffer.WriteC(appearance.EyeSpace);
		buffer.WriteC(appearance.EyeWidth);
		buffer.WriteC(appearance.EyeSize);
		buffer.WriteC(appearance.EyeShape);
		buffer.WriteC(appearance.EyeAngle);
		buffer.WriteC(appearance.BrowHeight);
		buffer.WriteC(appearance.BrowAngle);
		buffer.WriteC(appearance.BrowShape);
		buffer.WriteC(appearance.Nose);
		buffer.WriteC(appearance.NoseBridge);
		buffer.WriteC(appearance.NoseWidth);
		buffer.WriteC(appearance.NoseTip);
		buffer.WriteC(appearance.Cheek);
		buffer.WriteC(appearance.LipHeight);
		buffer.WriteC(appearance.MouthSize);
		buffer.WriteC(appearance.LipSize);
		buffer.WriteC(appearance.Smile);
		buffer.WriteC(appearance.LipShape);
		buffer.WriteC(appearance.JawHeight);
		buffer.WriteC(appearance.ChinJut);
		buffer.WriteC(appearance.EarShape);
		buffer.WriteC(appearance.HeadSize);
		buffer.WriteC(appearance.Neck);
		buffer.WriteC(appearance.NeckLength);
		buffer.WriteC(appearance.ShoulderSize);
		buffer.WriteC(appearance.Torso);
		buffer.WriteC(appearance.Chest);
		buffer.WriteC(appearance.Waist);
		buffer.WriteC(appearance.Hips);
		buffer.WriteC(appearance.ArmThickness);
		buffer.WriteC(appearance.HandSize);
		buffer.WriteC(appearance.LegThickness);
		buffer.WriteC(appearance.FootSize);
		buffer.WriteC(appearance.FacialRate);
		buffer.WriteC(0);
		buffer.WriteC(appearance.ArmLength);
		buffer.WriteC(appearance.LegLength);
		buffer.WriteC(appearance.Shoulders);
		buffer.WriteC(appearance.FaceShape);
		buffer.WriteC(0);
		buffer.WriteC(0);
		buffer.WriteC(0);
		buffer.WriteF(appearance.Height);
		buffer.WriteD(character.TemplateId);
		buffer.WriteD(character.MapId);
		buffer.WriteF(character.X);
		buffer.WriteF(character.Y);
		buffer.WriteF(character.Z);
		buffer.WriteD(character.Heading);
		buffer.WriteH(character.Level);
		buffer.WriteH(0);
		buffer.WriteD(character.TitleId);
		buffer.WriteD(character.LegionId);
		WriteFixedS(buffer, character.LegionName, 40);
		buffer.WriteH(character.HasLegion ? 1 : 0);
		buffer.WriteD(character.LastOnlineEpochSeconds);

		for (var i = 0; i < 16; i++)
		{
			var item = i < character.VisibleItems.Count ? character.VisibleItems[i] : null;
			buffer.WriteC(item?.SlotType ?? 0);
			buffer.WriteD(item?.ItemId ?? 0);
			buffer.WriteD(item?.GodStoneId ?? 0);
			WriteDyeInfo(buffer, item?.Color);
		}

		buffer.WriteD(0);
		buffer.WriteD(0);
		buffer.WriteD(0);
		buffer.WriteD(0);
		buffer.WriteD(0);
		buffer.WriteD(0);
		buffer.WriteB(new byte[68]);
		buffer.WriteD(character.DeletionTimeSeconds);
		buffer.WriteH(character.Display);
		buffer.WriteH(0);
		buffer.WriteD(0);
		buffer.WriteD(character.UnreadMailCount > 0 ? 1 : 0);
		buffer.WriteD(0);
		buffer.WriteD(0);
		buffer.WriteQ(character.BrokerKinah);
		buffer.WriteD(0);
		buffer.WriteD(0);
		buffer.WriteD(0);
		buffer.WriteD(0);
		buffer.WriteD(0);
		buffer.WriteD(character.BanInfo?.StartEpochSeconds ?? 0);
		buffer.WriteD(character.BanInfo?.EndEpochSeconds ?? 0);
		buffer.WriteS(character.BanInfo?.Reason ?? string.Empty);
	}

	private static void WriteFixedS(PacketBuffer buffer, string? value, int fixedLength)
	{
		// Java parity: AionServerPacket.writeS fixed UTF-16 select-screen fields.
		for (var i = 0; i < fixedLength; i++)
		{
			var c = !string.IsNullOrEmpty(value) && i < value.Length ? value[i] : '\0';
			buffer.WriteH(c);
		}

		buffer.WriteH(0);
	}

	private static void WriteDyeInfo(PacketBuffer buffer, int? rgb)
	{
		// Java parity: AbstractPlayerInfoPacket visible item color block.
		if (!rgb.HasValue)
		{
			buffer.WriteB(new byte[4]);
			return;
		}

		buffer.WriteC(1);
		buffer.WriteC((rgb.Value & 0xFF0000) >> 16);
		buffer.WriteC((rgb.Value & 0xFF00) >> 8);
		buffer.WriteC(rgb.Value & 0xFF);
	}
}
