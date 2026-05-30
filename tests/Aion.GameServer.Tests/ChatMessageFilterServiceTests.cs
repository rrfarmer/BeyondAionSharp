using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class ChatMessageFilterServiceTests
{
	private static readonly IReadOnlyList<string> ForbiddenWords = ["badword", "spam"];

	[Fact]
	public void CreatePlan_CleanMessageIsUnchanged()
	{
		var plan = ChatMessageFilterService.CreatePlan("Hello World", ForbiddenWords);

		Assert.Equal("Hello World", plan.FilteredMessage);
		Assert.False(plan.WasFiltered);
		Assert.False(plan.IsLive);
	}

	[Fact]
	public void CreatePlan_ForbiddenWordIsReplacedWithAsterisks()
	{
		// Java: message.replace(word, "*".repeat(word.length()))
		var plan = ChatMessageFilterService.CreatePlan("Hello badword World", ForbiddenWords);

		Assert.Equal("Hello ******* World", plan.FilteredMessage);
		Assert.True(plan.WasFiltered);
	}

	[Fact]
	public void CreatePlan_ForbiddenWordCheckIsCaseInsensitive()
	{
		// Java isForbiddenWord is case-insensitive, but replace uses original token casing
		var plan = ChatMessageFilterService.CreatePlan("Hello Badword World", ForbiddenWords);

		Assert.Equal("Hello ******* World", plan.FilteredMessage);
		Assert.True(plan.WasFiltered);
	}

	[Fact]
	public void CreatePlan_MultipleForbiddenWordsAreAllReplaced()
	{
		var plan = ChatMessageFilterService.CreatePlan("spam is badword here", ForbiddenWords);

		Assert.Equal("**** is ******* here", plan.FilteredMessage);
		Assert.True(plan.WasFiltered);
	}

	[Fact]
	public void CreatePlan_EmptyMessageIsUnchanged()
	{
		var plan = ChatMessageFilterService.CreatePlan("", ForbiddenWords);

		Assert.Equal("", plan.FilteredMessage);
		Assert.False(plan.WasFiltered);
	}

	[Fact]
	public void CreatePlan_EmptyForbiddenListIsUnchanged()
	{
		var plan = ChatMessageFilterService.CreatePlan("badword", []);

		Assert.Equal("badword", plan.FilteredMessage);
		Assert.False(plan.WasFiltered);
	}

	[Fact]
	public void IsForbiddenWord_ExactCaseMatch()
	{
		Assert.True(ChatMessageFilterService.IsForbiddenWord("badword", ForbiddenWords));
	}

	[Fact]
	public void IsForbiddenWord_CaseInsensitiveMatch()
	{
		Assert.True(ChatMessageFilterService.IsForbiddenWord("BADWORD", ForbiddenWords));
		Assert.True(ChatMessageFilterService.IsForbiddenWord("BadWord", ForbiddenWords));
	}

	[Fact]
	public void IsForbiddenWord_PartialMatchIsNotForbidden()
	{
		// Java: exact word match, not substring
		Assert.False(ChatMessageFilterService.IsForbiddenWord("badwordextra", ForbiddenWords));
	}
}
