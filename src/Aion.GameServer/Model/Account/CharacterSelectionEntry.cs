namespace Aion.GameServer.Model.Account;

public sealed class CharacterSelectionEntry
{
	public int ObjectId { get; init; }

	public string Name { get; init; } = string.Empty;

	public int GenderId { get; init; }

	public int RaceId { get; init; }

	public int ClassId { get; init; }

	public CharacterAppearance Appearance { get; init; } = new();

	public int TemplateId { get; init; }

	public int MapId { get; init; }

	public float X { get; init; }

	public float Y { get; init; }

	public float Z { get; init; }

	public int Heading { get; init; }

	public int Level { get; init; } = 1;

	public int TitleId { get; init; }

	public int LegionId { get; init; }

	public string? LegionName { get; init; }

	public bool HasLegion => LegionId > 0 && !string.IsNullOrEmpty(LegionName);

	public int LastOnlineEpochSeconds { get; init; }

	public IReadOnlyList<VisibleCharacterItem> VisibleItems { get; init; } = Array.Empty<VisibleCharacterItem>();

	public int DeletionTimeSeconds { get; init; }

	public int Display { get; init; }

	public int UnreadMailCount { get; init; }

	public long BrokerKinah { get; init; }

	public CharacterBanInfo? BanInfo { get; init; }
}

public sealed class CharacterAppearance
{
	public int Face { get; init; }
	public int Hair { get; init; }
	public int Deco { get; init; }
	public int Tattoo { get; init; }
	public int FaceContour { get; init; }
	public int Expression { get; init; }
	public int JawLine { get; init; }
	public int SkinRgb { get; init; }
	public int HairRgb { get; init; }
	public int EyeRgb { get; init; }
	public int LipRgb { get; init; }
	public int FaceShape { get; init; }
	public int Forehead { get; init; }
	public int EyeHeight { get; init; }
	public int EyeSpace { get; init; }
	public int EyeWidth { get; init; }
	public int EyeSize { get; init; }
	public int EyeShape { get; init; }
	public int EyeAngle { get; init; }
	public int BrowHeight { get; init; }
	public int BrowAngle { get; init; }
	public int BrowShape { get; init; }
	public int Nose { get; init; }
	public int NoseBridge { get; init; }
	public int NoseWidth { get; init; }
	public int NoseTip { get; init; }
	public int Cheek { get; init; }
	public int LipHeight { get; init; }
	public int MouthSize { get; init; }
	public int LipSize { get; init; }
	public int Smile { get; init; }
	public int LipShape { get; init; }
	public int JawHeight { get; init; }
	public int ChinJut { get; init; }
	public int EarShape { get; init; }
	public int HeadSize { get; init; }
	public int Neck { get; init; }
	public int NeckLength { get; init; }
	public int Shoulders { get; init; }
	public int ShoulderSize { get; init; }
	public int Torso { get; init; }
	public int Chest { get; init; }
	public int Waist { get; init; }
	public int Hips { get; init; }
	public int ArmThickness { get; init; }
	public int ArmLength { get; init; }
	public int HandSize { get; init; }
	public int LegThickness { get; init; }
	public int LegLength { get; init; }
	public int FootSize { get; init; }
	public int FacialRate { get; init; }
	public int Voice { get; init; }
	public float Height { get; init; }
}

public sealed record VisibleCharacterItem(byte SlotType, int ItemId, int GodStoneId, int? Color);

public sealed record CharacterBanInfo(int StartEpochSeconds, int EndEpochSeconds, string Reason);
