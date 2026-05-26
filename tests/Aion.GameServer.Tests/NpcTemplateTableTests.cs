using Aion.GameServer.Dataholders;

namespace Aion.GameServer.Tests;

public sealed class NpcTemplateTableTests
{
	[Fact]
	public void IsFunctionDialog_ReturnsTrueWhenAnyTemplateDeclaresDialogAction()
	{
		var firstNpc = CreateTemplate(203001, [33]);
		var secondNpc = CreateTemplate(203002, [47, 33]);
		var table = new NpcTemplateTable([firstNpc, secondNpc, CreateTemplate(203003)]);

		Assert.True(table.IsFunctionDialog(33));
		Assert.True(table.IsFunctionDialog(47));
		Assert.False(table.IsFunctionDialog(2));
	}

	[Fact]
	public void IsFunctionDialog_DoesNotMakeEveryNpcSupportTheGlobalDialogAction()
	{
		var merchant = CreateTemplate(203001, [33]);
		var ordinaryNpc = CreateTemplate(203002);
		var table = new NpcTemplateTable([merchant, ordinaryNpc]);

		Assert.True(table.IsFunctionDialog(33));
		Assert.True(merchant.SupportsDialogAction(33));
		Assert.False(ordinaryNpc.SupportsDialogAction(33));
	}

	[Fact]
	public void GetUnknownFunctionDialogIds_ReportsJavaDialogActionNameOfGaps()
	{
		var table = new NpcTemplateTable(
		[
			CreateTemplate(203001, [33, 102]),
			CreateTemplate(203002, [1011, 1010]),
		]);

		Assert.Equal(
			[
				new NpcTemplateUnknownDialogActionSummary(203001, 102),
				new NpcTemplateUnknownDialogActionSummary(203002, 1010),
			],
			table.GetUnknownFunctionDialogIds());
		Assert.True(table.IsFunctionDialog(102));
		Assert.True(table.IsFunctionDialog(1010));
	}

	private static NpcTemplateSummary CreateTemplate(int templateId, IReadOnlyList<int>? functionDialogIds = null)
	{
		return new NpcTemplateSummary(
			templateId,
			$"npc_{templateId}",
			NameId: 0,
			Level: 1,
			Rank: "NORMAL",
			Rating: "NORMAL",
			Race: "NONE",
			Tribe: "NONE",
			Type: "NPC",
			FunctionDialogIds: functionDialogIds);
	}
}
