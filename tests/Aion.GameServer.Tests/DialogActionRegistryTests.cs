using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class DialogActionRegistryTests
{
	[Theory]
	[InlineData(-1, "USE_OBJECT")]
	[InlineData(2, "BUY")]
	[InlineData(24, "RESURRECT_PET")]
	[InlineData(33, "OPEN_VENDOR")]
	[InlineData(54, "OPEN_PERSONAL_WAREHOUSE")]
	[InlineData(78, "TRADE_IN")]
	[InlineData(101, "AP_SELL")]
	[InlineData(107, "TRADE_IN_UPGRADE")]
	[InlineData(125, "OPEN_STIGMA_ENCHANT")]
	[InlineData(10255, "SET_SUCCEED")]
	[InlineData(20004, "CHECK_AP")]
	[InlineData(100000, "OPEN_WEB")]
	public void NameOf_ReturnsExactNamesForPortedFixedJavaConstants(int dialogActionId, string expectedName)
	{
		var result = DialogActionRegistry.NameOf(dialogActionId);

		Assert.True(result.IsKnown);
		Assert.True(result.NameIsExact);
		Assert.Equal(expectedName, result.Name);
		Assert.False(result.IsLive);
	}

	[Theory]
	[InlineData(9, "SELECTED_QUEST_REWARD2")]
	[InlineData(22, "SELECTED_QUEST_REWARD15")]
	[InlineData(110, "SELECTED_QUEST_AUTO_REWARD1")]
	[InlineData(124, "SELECTED_QUEST_AUTO_REWARD15")]
	[InlineData(10000, "SETPRO1")]
	[InlineData(10254, "SETPRO255")]
	public void NameOf_DerivesExactNamesForLinearJavaConstantFamilies(int dialogActionId, string expectedName)
	{
		var result = DialogActionRegistry.NameOf(dialogActionId);

		Assert.True(result.IsKnown);
		Assert.True(result.NameIsExact);
		Assert.Equal(expectedName, result.Name);
	}

	[Theory]
	[InlineData(1011)]
	[InlineData(8204)]
	public void NameOf_RecognizesGeneratedSelectRangeAsJavaKnown(int dialogActionId)
	{
		var result = DialogActionRegistry.NameOf(dialogActionId);

		Assert.True(result.IsKnown);
		Assert.False(result.NameIsExact);
		Assert.Equal($"SELECT_RANGE_{dialogActionId}", result.Name);
	}

	[Theory]
	[InlineData(0)]
	[InlineData(102)]
	[InlineData(1010)]
	[InlineData(8205)]
	[InlineData(99999)]
	public void NameOf_ReturnsUnknownForJavaGaps(int dialogActionId)
	{
		var result = DialogActionRegistry.NameOf(dialogActionId);

		Assert.False(result.IsKnown);
		Assert.False(result.NameIsExact);
		Assert.Null(result.Name);
	}
}
