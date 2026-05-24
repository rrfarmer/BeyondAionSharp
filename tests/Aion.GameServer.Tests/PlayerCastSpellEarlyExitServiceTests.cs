using Aion.Commons.Network;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ClientPackets;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public class PlayerCastSpellEarlyExitServiceTests
{
	[Fact]
	public void Evaluate_DeadPlayerSendsCannotCastBeforeOtherChecks()
	{
		var events = new List<string>();
		var player = CreatePlayer(currentHp: 0);
		var packet = CreateCastSpell(spellId: 100);

		var result = new PlayerCastSpellEarlyExitService().Evaluate(
			player,
			packet,
			new PlayerCastSpellEarlyExitOptions(
				IsPetOrderSkill: _ =>
				{
					events.Add("pet-check");
					return true;
				},
				SendSkillCannotCastDead: () => events.Add("dead-message"),
				CancelCurrentSkill: () => events.Add("cancel-current")));

		Assert.Equal(PlayerCastSpellEarlyExitStatus.DeadPlayer, result.Status);
		Assert.Equal([PlayerCastSpellEarlyExitAction.SendSkillCannotCastDead], result.Actions);
		Assert.Equal(["dead-message"], events);
	}

	[Fact]
	public void Evaluate_ZeroSpellIdCancelsCurrentSkillAfterDeadCheck()
	{
		var events = new List<string>();
		var player = CreatePlayer();
		var packet = CreateCastSpell(spellId: 0);

		var result = new PlayerCastSpellEarlyExitService().Evaluate(
			player,
			packet,
			new PlayerCastSpellEarlyExitOptions(
				CancelCurrentSkill: () => events.Add("cancel-current"),
				IsPetOrderSkill: _ =>
				{
					events.Add("pet-check");
					return true;
				}));

		Assert.Equal(PlayerCastSpellEarlyExitStatus.CancelCurrentSkill, result.Status);
		Assert.Equal([PlayerCastSpellEarlyExitAction.CancelCurrentSkill], result.Actions);
		Assert.Equal(["cancel-current"], events);
	}

	[Fact]
	public void Evaluate_PetOrderWithoutPetSendsPetRequiredBeforeTemplateLookup()
	{
		var events = new List<string>();
		var player = CreatePlayer();
		var packet = CreateCastSpell(spellId: 200);

		var result = new PlayerCastSpellEarlyExitService().Evaluate(
			player,
			packet,
			new PlayerCastSpellEarlyExitOptions(
				IsPetOrderSkill: _ => true,
				HasPetSummon: false,
				SendPetRequired: () => events.Add("pet-required"),
				GetSkillTemplate: _ =>
				{
					events.Add("template");
					return new PlayerCastSpellSkillTemplate(200);
				}));

		Assert.Equal(PlayerCastSpellEarlyExitStatus.PetRequired, result.Status);
		Assert.Equal([PlayerCastSpellEarlyExitAction.SendPetRequired], result.Actions);
		Assert.Equal(["pet-required"], events);
	}

	[Fact]
	public void Evaluate_MissingOrPassiveTemplateStopsBeforeProtectionAndUseItemCancellation()
	{
		var events = new List<string>();
		var player = CreatePlayer();
		player.SetVisualState(PlayerVisualStates.Blinking);
		var packet = CreateCastSpell(spellId: 300);

		var missing = new PlayerCastSpellEarlyExitService().Evaluate(
			player,
			packet,
			new PlayerCastSpellEarlyExitOptions(
				GetSkillTemplate: _ => null,
				StopProtection: () => events.Add("stop-protection"),
				CancelUseItem: () => events.Add("cancel-use-item")));

		var passive = new PlayerCastSpellEarlyExitService().Evaluate(
			player,
			packet,
			new PlayerCastSpellEarlyExitOptions(
				GetSkillTemplate: _ => new PlayerCastSpellSkillTemplate(300, IsPassive: true),
				StopProtection: () => events.Add("stop-protection"),
				CancelUseItem: () => events.Add("cancel-use-item")));

		Assert.Equal(PlayerCastSpellEarlyExitStatus.MissingOrPassiveTemplate, missing.Status);
		Assert.Equal(PlayerCastSpellEarlyExitStatus.MissingOrPassiveTemplate, passive.Status);
		Assert.Empty(missing.Actions);
		Assert.Empty(passive.Actions);
		Assert.Empty(events);
		Assert.True(player.IsProtectionActive());
	}

	[Fact]
	public void Evaluate_ReadySkillStopsProtectionCancelsUseItemAndDispatchesSkill()
	{
		var events = new List<string>();
		var player = CreatePlayer();
		player.SetVisualState(PlayerVisualStates.Blinking);
		var packet = CreateCastSpell(spellId: 400);
		var template = new PlayerCastSpellSkillTemplate(400);

		var result = new PlayerCastSpellEarlyExitService().Evaluate(
			player,
			packet,
			new PlayerCastSpellEarlyExitOptions(
				GetSkillTemplate: _ => template,
				StopProtection: () => events.Add("stop-protection"),
				CancelUseItem: () => events.Add("cancel-use-item"),
				UseSkill: (skill, request) => events.Add($"use-skill:{skill.SkillId}:{request.TargetType}")));

		Assert.Equal(PlayerCastSpellEarlyExitStatus.UseSkill, result.Status);
		Assert.Equal(
			[
				PlayerCastSpellEarlyExitAction.StopProtection,
				PlayerCastSpellEarlyExitAction.CancelUseItem,
				PlayerCastSpellEarlyExitAction.UseSkill,
			],
			result.Actions);
		Assert.Equal(["stop-protection", "cancel-use-item", "use-skill:400:0"], events);
		Assert.False(player.IsProtectionActive());
	}

	[Fact]
	public void Evaluate_CooldownAuditCanRejectNotReadyAfterCancelUseItem()
	{
		var events = new List<string>();
		var player = CreatePlayer();
		var packet = CreateCastSpell(spellId: 500, receiveTimeMilliseconds: 1_000);

		var result = new PlayerCastSpellEarlyExitService().Evaluate(
			player,
			packet,
			new PlayerCastSpellEarlyExitOptions(
				GetSkillTemplate: _ => new PlayerCastSpellSkillTemplate(500),
				NextSkillUseMilliseconds: 2_000,
				CurrentTimeMilliseconds: 1_500,
				LastSkillId: 499,
				CancelUseItem: () => events.Add("cancel-use-item"),
				AuditCooldown: (skillId, delta, lastSkillId) => events.Add($"audit:{skillId}:{delta}:{lastSkillId}"),
				SendSkillNotReady: () => events.Add("not-ready"),
				UseSkill: (_, _) => events.Add("use-skill")));

		Assert.Equal(PlayerCastSpellEarlyExitStatus.SkillNotReady, result.Status);
		Assert.Equal(
			[
				PlayerCastSpellEarlyExitAction.CancelUseItem,
				PlayerCastSpellEarlyExitAction.AuditCooldown,
				PlayerCastSpellEarlyExitAction.SendSkillNotReady,
			],
			result.Actions);
		Assert.Equal(["cancel-use-item", "audit:500:1000:499", "not-ready"], events);
	}

	private static Player CreatePlayer(int currentHp = 100)
	{
		return new Player
		{
			ObjectId = 1,
			LifeStats = new PlayerLifeStats(currentHp, CurrentMp: 100, CurrentFp: 100),
		};
	}

	private static CmCastSpell CreateCastSpell(int spellId, long receiveTimeMilliseconds = 0)
	{
		var packet = new CmCastSpell(33, new HashSet<GameConnectionState> { GameConnectionState.InGame }, receiveTimeMilliseconds);
		using var buffer = new PacketBuffer();
		buffer.WriteH(spellId);
		buffer.WriteC(1);
		buffer.WriteC(0);
		buffer.WriteD(7001);
		buffer.WriteH(300);
		buffer.WriteD(0);
		packet.ReadFrom(new PacketBuffer(buffer.ToArray()));
		return packet;
	}
}
