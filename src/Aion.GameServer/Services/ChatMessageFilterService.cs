namespace Aion.GameServer.Services;

public sealed record ChatMessageFilterPlan(
	string OriginalMessage,
	string FilteredMessage,
	bool WasFiltered,
	string JavaSource
)
{
	public bool IsLive => false;
}

public static class ChatMessageFilterService
{
	public static ChatMessageFilterPlan CreatePlan(string message, IReadOnlyList<string> forbiddenWords)
	{
		// Java parity: services/NameRestrictionService.filterMessage.
		// Splits by space, replaces each forbidden word with '*' of same length.
		// isForbiddenWord is case-insensitive; String.replace is case-sensitive (replaces the matched token).
		if (string.IsNullOrEmpty(message) || forbiddenWords.Count == 0)
		{
			return new ChatMessageFilterPlan(
				message ?? string.Empty,
				message ?? string.Empty,
				WasFiltered: false,
				"NameRestrictionService.filterMessage -> no forbidden words or empty message -> unchanged"
			);
		}

		var filtered = message;
		foreach (var word in message.Split(' '))
		{
			if (IsForbiddenWord(word, forbiddenWords))
				filtered = filtered.Replace(word, new string('*', word.Length), StringComparison.Ordinal);
		}

		return new ChatMessageFilterPlan(
			message,
			filtered,
			WasFiltered: !string.Equals(filtered, message, StringComparison.Ordinal),
			"NameRestrictionService.filterMessage -> split by space, replace forbidden words with '*'"
		);
	}

	public static bool IsForbiddenWord(string word, IReadOnlyList<string> forbiddenWords)
	{
		// Java parity: services/NameRestrictionService.isForbiddenWord - case-insensitive exact match.
		return forbiddenWords.Any(fw => string.Equals(fw, word, StringComparison.OrdinalIgnoreCase));
	}
}
