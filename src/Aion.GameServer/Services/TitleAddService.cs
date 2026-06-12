using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Services;

public static class TitleAddService
{
	public static TitleAddValidation ValidateCanAct(Player player, int titleId)
	{
		// Java parity: model/templates/item/actions/TitleAddAction.canAct.
		if (titleId == 0)
			return TitleAddValidation.Fail(TitleAddFailure.InvalidItem);

		if (player.Titles.Any(title => title.Id == titleId))
			return TitleAddValidation.Fail(TitleAddFailure.AlreadyKnown);

		return TitleAddValidation.Valid();
	}

	public static TitleAddValidation CreateTitle(
		Player player,
		int titleId,
		int minutes,
		bool hasMinutes,
		TitleTemplateTable titleTemplates,
		DateTimeOffset now)
	{
		// Java parity: model/gameobjects/player/title/TitleList.addTitle(titleId, false, time).
		var titleTemplate = titleTemplates.GetTitleTemplate(titleId);
		if (titleTemplate == null)
			return TitleAddValidation.Fail(TitleAddFailure.InvalidTitle);

		if (!string.Equals(titleTemplate.Race, "PC_ALL", StringComparison.Ordinal)
			&& !string.Equals(titleTemplate.Race.ToString(), player.Race.ToString(), StringComparison.Ordinal))
		{
			return TitleAddValidation.Fail(TitleAddFailure.InvalidRace, titleTemplate);
		}

		var expireTime = hasMinutes
			? (int)(now.ToUnixTimeSeconds() + (long)minutes * 60)
			: 0;
		return new TitleAddValidation(TitleAddFailure.None, new PlayerTitle(titleId, expireTime), titleTemplate);
	}
}

public sealed record TitleAddValidation(
	TitleAddFailure Failure,
	PlayerTitle? Title = null,
	TitleTemplateSummary? TitleTemplate = null)
{
	public bool Succeeded => Failure == TitleAddFailure.None;

	public static TitleAddValidation Valid()
	{
		return new TitleAddValidation(TitleAddFailure.None);
	}

	public static TitleAddValidation Fail(TitleAddFailure failure, TitleTemplateSummary? titleTemplate = null)
	{
		return new TitleAddValidation(failure, TitleTemplate: titleTemplate);
	}
}

public enum TitleAddFailure
{
	None,
	InvalidItem,
	InvalidTitle,
	InvalidRace,
	AlreadyKnown,
}
