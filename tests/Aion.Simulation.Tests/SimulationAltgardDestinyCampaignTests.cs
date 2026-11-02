using Aion.Bots.Protocol;
using Aion.Bots.Scenarios;
using Aion.Bots.Tracing;
using Aion.Bots.World;
using Aion.GameServer.Model;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.TestKit;

namespace Aion.Simulation.Tests;

public sealed partial class SimulationFastScenarioTests
{
	/// <summary>ND-03: actual Q2900 movie/equip/campaign/reward, cleanup and a real bundle's valid permanent stigma. Free account 218.</summary>
	[SkippableFact]
	public async Task DestinyCampaignCompletesThroughMovieStigmaEquipAndAudReward()
	{
		Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);
		using var policy = NewPolicy("ND03", includeHistory: false);
		using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(20));
		CancellationToken token = timeout.Token;
		string run = Environment.GetEnvironmentVariable("AION_SIM_RUN_ID") ?? "nd03-campaign";
		string path = Path.Combine(RealStaticData.RepoRoot(), "run", $"{run}-nd03.trace.jsonl");
		using var trace = BotActionTraceWriter.Open(path, run, "b01", "sim-player-218", virtualTime: () => TimeSpan.FromMilliseconds(fixture.Clock.NowMillis));
		await using var session = new SimulationL0Session(fixture, policy, "b01", 218, "Asimdestquest", Race.ASMODIANS, trace, path);
		var probe = new DestinyProbe(this, fixture, session, token);
		await probe.InitializeAsync(100000); // Controlled funds for the later ordinary permanent-stigma fee.
		HashSet<int> learned = session.Api.World.Skills.Keys.ToHashSet();
		Assert.Contains(1842, learned);
		await probe.SetupAtFortressAsync();
		await probe.TransportAsync(120010000);
		foreach (string key in new[] { "heimdall", "munin" }) await probe.TalkAsync(key);
		await probe.FlyAsync(203545);
		foreach (string key in new[] { "urd", "verdandi", "enter", "movie", "stone" }) await probe.TalkAsync(key);
		Assert.Equal(99, probe.Var);
		Assert.Contains(session.PacketHistory, packet => packet.PacketType == typeof(SM_PLAY_MOVIE) && packet.Get<int>("cutsceneId") == 156);
		NaturalAltgardDestiny destiny = probe.Leg.Destiny!;
		Assert.Equal(1, Count(destiny.StoneItemId));
		Assert.DoesNotContain(destiny.StigmaSkillId, session.Api.World.Skills.Keys);
		int skuld = await probe.WalkNpcAsync(204264);
		long fee = await NaturalAltgardQuestSteps.EquipDestinyStigmaAsync(session, destiny, skuld, token);
		Assert.Equal(97, probe.Var);
		Assert.Equal(destiny.StigmaSlot, session.Api.World.Inventory.Values.Single(item => item.ItemId == destiny.StoneItemId).Details.EquippedSlot);
		Assert.Single(probe.Server.GetEquipment().GetEquippedItemsRegularStigma());
		Assert.True(probe.Server.GetSkillList().IsSkillPresent(destiny.StigmaSkillId));
		Console.WriteLine($"ND-03 tutorial equip: full var 99 -> 97, fee {fee}, skill {destiny.StigmaSkillId}, slot {destiny.StigmaSlot}");
		await probe.TalkAsync("spawn");
		await probe.KillControlledAsync();
		Assert.Equal(0, Count(destiny.StoneItemId));
		Assert.Empty(probe.Server.GetEquipment().GetEquippedItemsRegularStigma());
		Assert.DoesNotContain(destiny.StigmaSkillId, session.Api.World.Skills.Keys);
		Assert.False(probe.Server.GetSkillList().IsSkillPresent(destiny.StigmaSkillId));
		Assert.All(learned, id => Assert.Contains(id, session.Api.World.Skills.Keys));
		foreach (string key in new[] { "skuld-return", "munin-return" }) await probe.TalkAsync(key);
		long experience = probe.Server.GetCommonData().GetExp(), kinah = session.Api.World.Kinah;
		// Both Java and the shipped C# data leave use_class_reward at 0. The legacy class list
		// is inactive, so the actual client claims the fixed rewards without a selection.
		var questTemplate = fixture.DataManager.StaticData.Quests.GetQuestById(2900);
		Assert.False(questTemplate.IsClassRewardOnEveryRepeat());
		Assert.False(questTemplate.IsSingleTimeClassReward());
		await probe.TalkAsync("reward");
		Assert.Equal(QuestStatus.COMPLETE, probe.Server.GetQuestStateList().GetQuestState(2900).GetStatus());
		Assert.Contains(2900, session.Api.World.CompletedQuestIds);
		Assert.Equal(144, session.Api.World.CompletedQuestIds.Count);
		Assert.All(probe.Leg.Start.CompletedQuestIds, id => Assert.Contains(id, session.Api.World.CompletedQuestIds));
		Assert.All(probe.Leg.Start.CompletedQuestIds, id => Assert.Equal(QuestStatus.COMPLETE,
			probe.Server.GetQuestStateList().GetQuestState(id).GetStatus()));
		Assert.Equal(1, Count(destiny.RewardBundleId));
		Assert.Equal(0, Count(destiny.LegacyRewardId));
		foreach (int potion in new[] { 162000004, 162000009 }) Assert.Equal(10, Count(potion));
		Assert.Equal(5, Count(162001057));
		Console.WriteLine($"ND-03 Aud reward: EXP +{probe.Server.GetCommonData().GetExp() - experience}, Kinah +{session.Api.World.Kinah - kinah}, 144 completed journals, bundle retained; inactive legacy class reward {destiny.LegacyRewardId} absent");
		// Probe only: opening this actual reward demonstrates eligibility; the natural endpoint keeps its bundle sealed.
		BotInventoryItem bundle = session.Api.World.Inventory.Values.Single(item => item.ItemId == destiny.RewardBundleId);
		await session.SendPacketAsync(session.Api.UseItem(bundle.ObjectId, fixture.DataManager.StaticData.ItemDataDh.GetItemTemplate(bundle.ItemId)), token);
		var preview = await session.WaitForPacketAsync(typeof(SM_SHOW_DECOMPOSABLE), token, p => p.Get<int>("objectId") == bundle.ObjectId);
		BotDecomposableChoice[] choices = preview.Get<BotDecomposableChoice[]>("choices");
		Assert.NotEmpty(choices);
		BotDecomposableChoice choice = choices.First(c => fixture.DataManager.StaticData.ItemDataDh.GetItemTemplate(c.ItemId).IsStigma());
		await session.SendPacketAsync(GameClientPackets.SelectDecomposable(bundle.ObjectId, choice.Index), token);
		await session.SynchronizeAsync(token);
		Assert.Equal(0, Count(destiny.RewardBundleId));
		BotInventoryItem inert = session.Api.World.Inventory.Values.Single(item => item.ItemId == choice.ItemId);
		var template = fixture.DataManager.StaticData.ItemDataDh.GetItemTemplate(inert.ItemId);
		Assert.True(template.IsStigma());
		int aud = await probe.WalkNpcAsync(204061);
		await NaturalDialogProtocol.OpenAsync(session, aud, token);
		await session.WaitForPacketAsync(typeof(SM_DIALOG_WINDOW), token, p => p.Get<int>("targetObjectId") == aud);
		await NaturalDialogProtocol.SelectAsync(session, session.Api.SelectDialog(aud, DialogAction.OPEN_STIGMA_WINDOW), token);
		await session.WaitForPacketAsync(typeof(SM_DIALOG_WINDOW), token, p => p.Get<ushort>("dialogPageId") == 1);
		long before = session.Api.World.Kinah;
		await session.SendPacketAsync(GameClientPackets.EquipItem(0, destiny.StigmaSlot, inert.ObjectId), token);
		await session.SynchronizeAsync(token);
		Assert.Equal(destiny.StigmaSlot, session.Api.World.Inventory[inert.ObjectId].Details.EquippedSlot);
		Assert.Single(probe.Server.GetEquipment().GetEquippedItemsRegularStigma());
		Assert.Equal(session.Api.World.VendorPrices!.ServicePrice(25000), before - session.Api.World.Kinah);
		Assert.Equal(0, Count(destiny.LegacyRewardId));
		Console.WriteLine($"ND-03 actual bundle choice {choice.ItemId} installs in the one normal level-24 slot, fee {before - session.Api.World.Kinah}; legacy item {destiny.LegacyRewardId} remains absent");
		policy.AssertClean();

		long Count(int id) => session.Api.World.Inventory.Values.Where(item => item.ItemId == id).Sum(item => item.Count);
	}
}
