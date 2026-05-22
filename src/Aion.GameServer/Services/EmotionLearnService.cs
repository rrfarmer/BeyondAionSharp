using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Services;

public static class EmotionLearnService
{
	public static EmotionLearnValidation ValidateNewEmotion(
		Player player,
		int emotionId,
		int minutes,
		DateTimeOffset now)
	{
		// Java parity: model/templates/item/actions/EmotionLearnAction.canAct + EmotionList.add expire-time math.
		if (emotionId == 0)
			return EmotionLearnValidation.Fail(EmotionLearnFailure.InvalidItem);

		if (player.Emotions.Any(emotion => emotion.Id == emotionId))
			return EmotionLearnValidation.Fail(EmotionLearnFailure.AlreadyKnown);

		var expireTime = minutes == 0
			? 0
			: (int)(now.ToUnixTimeSeconds() + (long)minutes * 60);
		return new EmotionLearnValidation(EmotionLearnFailure.None, new PlayerEmotion(emotionId, expireTime));
	}
}

public sealed record EmotionLearnValidation(
	EmotionLearnFailure Failure,
	PlayerEmotion? Emotion = null)
{
	public bool Succeeded => Failure == EmotionLearnFailure.None && Emotion != null;

	public static EmotionLearnValidation Fail(EmotionLearnFailure failure)
	{
		return new EmotionLearnValidation(failure);
	}
}

public enum EmotionLearnFailure
{
	None,
	InvalidItem,
	AlreadyKnown,
}
