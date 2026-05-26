using Aion.Commons.Network;
using Aion.GameServer.Controllers.Movement;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Services;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public sealed class SmPlayerInfo : GameServerPacket
{
	public const int PacketOpCode = 32;
	private const int FriendlyCreatureType = 0x26;
	private readonly Player _player;
	private readonly bool _enemy;
	private readonly PlayerExperienceTable? _experienceTable;

	public SmPlayerInfo(Player player, PlayerExperienceTable? experienceTable = null)
		: this(player, enemy: false, experienceTable)
	{
	}

	public SmPlayerInfo(Player player, bool enemy, PlayerExperienceTable? experienceTable = null)
		: base(PacketOpCode)
	{
		// Java parity: network/aion/serverpackets/SM_PLAYER_INFO(Player, boolean enemy).
		_player = player;
		_enemy = enemy;
		_experienceTable = experienceTable;
	}

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		// Java parity: network/aion/serverpackets/SM_PLAYER_INFO.writeImpl baseline path.
		var position = _player.Position;
		var appearance = _player.Appearance;
		var raceId = ToRaceId(_player.Race);
		var genderId = ToGenderId(_player.Gender);
		var templateId = 100000 + raceId * 2 + genderId;
		buffer.WriteF(position.X);
		buffer.WriteF(position.Y);
		buffer.WriteF(position.Z);
		buffer.WriteD(_player.ObjectId);
		buffer.WriteD(templateId);
		buffer.WriteD(0);
		buffer.WriteD(templateId);
		buffer.WriteC(0);
		buffer.WriteD(0);
		buffer.WriteC(_enemy ? 0 : FriendlyCreatureType);
		buffer.WriteC(raceId);
		buffer.WriteC(ToClassId(_player.PlayerClass));
		buffer.WriteC(genderId);
		buffer.WriteH(0);
		buffer.WriteD(0);
		buffer.WriteD(0);
		buffer.WriteC(position.Heading);
		buffer.WriteS(_player.Name);
		buffer.WriteH(_player.TitleId);
		buffer.WriteH(0);
		buffer.WriteH(0);
		WriteLegion(buffer);
		buffer.WriteC(100);
		buffer.WriteH(_player.Dp);
		buffer.WriteC(0);
		WriteEquippedItems(buffer);
		WriteAppearance(buffer, appearance);
		buffer.WriteF(appearance.Height);
		buffer.WriteF(0.25f);
		buffer.WriteF(2.0f);
		var movementSpeed = PlayerMovementSpeedResolver.ResolveKnownMovementSpeed(_player);
		buffer.WriteF(movementSpeed);
		buffer.WriteH(0);
		buffer.WriteH(0);
		// Java parity: SM_PLAYER_INFO.writeImpl writes Player.getPortAnimationId().
		buffer.WriteC((byte)_player.PortAnimation);
		buffer.WriteS(string.Empty);
		WriteMovement(buffer, position, movementSpeed);
		buffer.WriteC(0);
		buffer.WriteS(_player.Note);
		buffer.WriteH(GetLevel());
		buffer.WriteH(_player.Settings.Display);
		buffer.WriteH(_player.Settings.Deny);
		buffer.WriteH(_player.AbyssRank.Rank);
		buffer.WriteH(0);
		// Java parity: SM_PLAYER_INFO.writeImpl target/team/mentor/active-house tail.
		buffer.WriteD(Math.Max(0, _player.TargetObjectId));
		buffer.WriteC(0);
		buffer.WriteD(0);
		buffer.WriteC(0);
		buffer.WriteD(GetActiveHouseAddressId(_player));
		buffer.WriteD(_player.AccountMembership > 0 ? 3 + _player.AccountMembership : 1);
		buffer.WriteD(1);
		buffer.WriteC(3);
		buffer.WriteC(0);
		buffer.WriteC(0);
		buffer.WriteC(0);
	}

	private void WriteEquippedItems(PacketBuffer buffer)
	{
		// Java parity: network/aion/serverpackets/AbstractPlayerInfoPacket.writeEquippedItems.
		var items = _player.InventoryItems
			.Where(item => item is { Location: 0, IsEquipped: true })
			.OrderBy(item => item.Slot)
			.ThenBy(item => item.ObjectId)
			.ToArray();
		var mask = 0;
		foreach (var item in items)
			mask |= unchecked((int)item.Slot);

		buffer.WriteD(mask);
		foreach (var item in items)
		{
			buffer.WriteD(item.ItemSkin == 0 ? item.ItemId : item.ItemSkin);
			buffer.WriteD(item.Godstone?.ItemId ?? 0);
			WriteDyeInfo(buffer, item.Color);
			buffer.WriteH(item.Enchant);
			buffer.WriteH(0);
		}
	}

	private static void WriteAppearance(PacketBuffer buffer, Model.Account.CharacterAppearance appearance)
	{
		// Java parity: SM_PLAYER_INFO appearance tail from PlayerAppearance.
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
		buffer.WriteC(appearance.Voice);
	}

	private void WriteMovement(PacketBuffer buffer, global::Aion.GameServer.World.WorldPosition position, float movementSpeed)
	{
		// Java parity: SM_PLAYER_INFO movement-vector/current-position tail.
		var movement = _player.Movement;
		var movementMask = movement.Mask;
		if (MovementMask.Has(movementMask, MovementMask.Absolute))
		{
			WriteAbsoluteMovementVector(buffer, movement, position, movementSpeed);
			movementMask &= unchecked((byte)~MovementMask.Absolute);
		}
		else
		{
			buffer.WriteF(movement.VectorX);
			buffer.WriteF(movement.VectorY);
			buffer.WriteF(movement.VectorZ);
		}

		buffer.WriteF(position.X);
		buffer.WriteF(position.Y);
		buffer.WriteF(position.Z);
		buffer.WriteC(movementMask);
	}

	private static void WriteAbsoluteMovementVector(
		PacketBuffer buffer,
		PlayerMovementState movement,
		global::Aion.GameServer.World.WorldPosition position,
		float movementSpeed)
	{
		// Java parity: SM_PLAYER_INFO uses PlayerMoveController target coords when MovementMask.ABSOLUTE is set.
		var deltaX = movement.TargetX - position.X;
		var deltaY = movement.TargetY - position.Y;
		var deltaZ = movement.TargetZ - position.Z;
		var length = MathF.Sqrt(deltaX * deltaX + deltaY * deltaY + deltaZ * deltaZ);
		if (length <= 0)
		{
			buffer.WriteF(0);
			buffer.WriteF(0);
			buffer.WriteF(0);
			return;
		}

		var scale = movementSpeed / length;
		buffer.WriteF(deltaX * scale);
		buffer.WriteF(deltaY * scale);
		buffer.WriteF(deltaZ * scale);
	}

	private int GetLevel()
	{
		return Math.Max(1, _experienceTable?.GetLevelForExp(_player.Exp) ?? 1);
	}

	private static int GetActiveHouseAddressId(Player player)
	{
		// Java parity: SM_PLAYER_INFO.writeImpl player.getActiveHouse().getAddress().getId().
		return player.Houses.FirstOrDefault(house => !house.IsInactive)?.AddressId ?? 0;
	}

	private void WriteLegion(PacketBuffer buffer)
	{
		// Java parity: SM_PLAYER_INFO.writeImpl legion member/emblem block.
		if (_player.LegionId > 0 && _player.LegionName.Length > 0)
		{
			buffer.WriteD(_player.LegionId);
			buffer.WriteC(_player.LegionEmblemId);
			buffer.WriteC(_player.LegionEmblemType);
			buffer.WriteC(_player.LegionEmblemColorA);
			buffer.WriteC(_player.LegionEmblemColorR);
			buffer.WriteC(_player.LegionEmblemColorG);
			buffer.WriteC(_player.LegionEmblemColorB);
			buffer.WriteS(_player.LegionName);
			return;
		}

		buffer.WriteB(new byte[12]);
	}

	private static void WriteDyeInfo(PacketBuffer buffer, int? rgb)
	{
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

	private static int ToRaceId(string race)
	{
		return string.Equals(race, "ASMODIANS", StringComparison.OrdinalIgnoreCase) || string.Equals(race, "ASMODIAN", StringComparison.OrdinalIgnoreCase)
			? 1
			: 0;
	}

	private static int ToGenderId(string gender)
	{
		return string.Equals(gender, "FEMALE", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
	}

	private static int ToClassId(string playerClass)
	{
		return playerClass.ToUpperInvariant() switch
		{
			"WARRIOR" => 0,
			"GLADIATOR" => 1,
			"TEMPLAR" => 2,
			"SCOUT" => 3,
			"ASSASSIN" => 4,
			"RANGER" => 5,
			"MAGE" => 6,
			"SORCERER" => 7,
			"SPIRIT_MASTER" => 8,
			"PRIEST" => 9,
			"CLERIC" => 10,
			"CHANTER" => 11,
			"ENGINEER" => 12,
			"GUNNER" => 13,
			"ARTIST" => 14,
			"BARD" => 15,
			"RIDER" => 16,
			_ => 0,
		};
	}
}
