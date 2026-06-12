using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.Account;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Services;

public static class CosmeticItemService
{
	public static CosmeticItemPlan CreatePlan(Player player, CosmeticItemSummary? template)
	{
		// Java parity: model/templates/item/actions/CosmeticItemAction.canAct + act.
		if (template == null)
			return CosmeticItemPlan.Fail(CosmeticItemFailure.MissingTemplate);

		if (!string.Equals(template.Race.ToString(), player.Race.ToString(), StringComparison.Ordinal))
			return CosmeticItemPlan.Fail(CosmeticItemFailure.InvalidRace);

		if (!string.Equals(template.GenderPermitted, "ALL", StringComparison.Ordinal)
			&& !string.Equals(template.GenderPermitted, player.Gender, StringComparison.Ordinal))
		{
			return CosmeticItemPlan.Fail(CosmeticItemFailure.InvalidGender);
		}

		if (player.IsInRideMode)
			return CosmeticItemPlan.Fail(CosmeticItemFailure.Ride);

		var appearance = player.Appearance;
		return template.Type switch
		{
			"hair_color" => CosmeticItemPlan.Success(CopyAppearance(appearance, hairRgb: template.Id)),
			"face_color" => CosmeticItemPlan.Success(CopyAppearance(appearance, skinRgb: template.Id)),
			"lip_color" => CosmeticItemPlan.Success(CopyAppearance(appearance, lipRgb: template.Id)),
			"eye_color" => CosmeticItemPlan.Success(CopyAppearance(appearance, eyeRgb: template.Id)),
			"hair_type" => CosmeticItemPlan.Success(CopyAppearance(appearance, hair: template.Id)),
			"face_type" => CosmeticItemPlan.Success(CopyAppearance(appearance, face: template.Id)),
			"voice_type" => CosmeticItemPlan.Success(CopyAppearance(appearance, voice: template.Id)),
			"makeup_type" => CosmeticItemPlan.Success(CopyAppearance(appearance, tattoo: template.Id)),
			"tattoo_type" => CosmeticItemPlan.Success(CopyAppearance(appearance, deco: template.Id)),
			"preset_name" when template.Preset != null => CosmeticItemPlan.Success(ApplyPreset(appearance, template.Preset)),
			_ => CosmeticItemPlan.Fail(CosmeticItemFailure.UnsupportedType),
		};
	}

	private static CharacterAppearance ApplyPreset(CharacterAppearance appearance, CosmeticPresetSummary preset)
	{
		// Java parity: CosmeticItemAction.act preset branch sets skin RGB from eyeColor, not skinColor.
		return CopyAppearance(
			appearance,
			skinRgb: preset.EyeColor,
			hairRgb: preset.HairColor,
			eyeRgb: preset.EyeColor,
			lipRgb: preset.LipColor,
			hair: preset.HairType,
			face: preset.FaceType,
			height: preset.Scale);
	}

	private static CharacterAppearance CopyAppearance(
		CharacterAppearance appearance,
		int? face = null,
		int? hair = null,
		int? deco = null,
		int? tattoo = null,
		int? skinRgb = null,
		int? hairRgb = null,
		int? eyeRgb = null,
		int? lipRgb = null,
		int? voice = null,
		float? height = null)
	{
		return new CharacterAppearance
		{
			Face = face ?? appearance.Face,
			Hair = hair ?? appearance.Hair,
			Deco = deco ?? appearance.Deco,
			Tattoo = tattoo ?? appearance.Tattoo,
			FaceContour = appearance.FaceContour,
			Expression = appearance.Expression,
			JawLine = appearance.JawLine,
			SkinRgb = skinRgb ?? appearance.SkinRgb,
			HairRgb = hairRgb ?? appearance.HairRgb,
			EyeRgb = eyeRgb ?? appearance.EyeRgb,
			LipRgb = lipRgb ?? appearance.LipRgb,
			FaceShape = appearance.FaceShape,
			Forehead = appearance.Forehead,
			EyeHeight = appearance.EyeHeight,
			EyeSpace = appearance.EyeSpace,
			EyeWidth = appearance.EyeWidth,
			EyeSize = appearance.EyeSize,
			EyeShape = appearance.EyeShape,
			EyeAngle = appearance.EyeAngle,
			BrowHeight = appearance.BrowHeight,
			BrowAngle = appearance.BrowAngle,
			BrowShape = appearance.BrowShape,
			Nose = appearance.Nose,
			NoseBridge = appearance.NoseBridge,
			NoseWidth = appearance.NoseWidth,
			NoseTip = appearance.NoseTip,
			Cheek = appearance.Cheek,
			LipHeight = appearance.LipHeight,
			MouthSize = appearance.MouthSize,
			LipSize = appearance.LipSize,
			Smile = appearance.Smile,
			LipShape = appearance.LipShape,
			JawHeight = appearance.JawHeight,
			ChinJut = appearance.ChinJut,
			EarShape = appearance.EarShape,
			HeadSize = appearance.HeadSize,
			Neck = appearance.Neck,
			NeckLength = appearance.NeckLength,
			Shoulders = appearance.Shoulders,
			ShoulderSize = appearance.ShoulderSize,
			Torso = appearance.Torso,
			Chest = appearance.Chest,
			Waist = appearance.Waist,
			Hips = appearance.Hips,
			ArmThickness = appearance.ArmThickness,
			ArmLength = appearance.ArmLength,
			HandSize = appearance.HandSize,
			LegThickness = appearance.LegThickness,
			LegLength = appearance.LegLength,
			FootSize = appearance.FootSize,
			FacialRate = appearance.FacialRate,
			Voice = voice ?? appearance.Voice,
			Height = height ?? appearance.Height,
		};
	}
}

public sealed record CosmeticItemPlan(CosmeticItemFailure Failure, CharacterAppearance? Appearance)
{
	public bool Succeeded => Failure == CosmeticItemFailure.None && Appearance != null;

	public static CosmeticItemPlan Success(CharacterAppearance appearance)
	{
		return new CosmeticItemPlan(CosmeticItemFailure.None, appearance);
	}

	public static CosmeticItemPlan Fail(CosmeticItemFailure failure)
	{
		return new CosmeticItemPlan(failure, null);
	}
}

public enum CosmeticItemFailure
{
	None,
	MissingTemplate,
	InvalidRace,
	InvalidGender,
	Ride,
	UnsupportedType,
}
