using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion.ClientPackets;

namespace Aion.GameServer.Services;

public sealed class PlayerCastSpellEarlyExitService
{
	public PlayerCastSpellEarlyExitResult Evaluate(
		Player player,
		CmCastSpell packet,
		PlayerCastSpellEarlyExitOptions options)
	{
		// Java parity: network/aion/clientpackets/CM_CASTSPELL.runImpl early-exit order before PlayerController.useSkill.
		var actions = new List<PlayerCastSpellEarlyExitAction>();

		if (player.LifeStats?.CurrentHp <= 0 || player.IsInState(PlayerCreatureState.Dead))
		{
			actions.Add(PlayerCastSpellEarlyExitAction.SendSkillCannotCastDead);
			options.SendSkillCannotCastDead?.Invoke();
			return new PlayerCastSpellEarlyExitResult(PlayerCastSpellEarlyExitStatus.DeadPlayer, actions);
		}

		if (packet.SpellId == 0)
		{
			actions.Add(PlayerCastSpellEarlyExitAction.CancelCurrentSkill);
			options.CancelCurrentSkill?.Invoke();
			return new PlayerCastSpellEarlyExitResult(PlayerCastSpellEarlyExitStatus.CancelCurrentSkill, actions);
		}

		if (options.IsPetOrderSkill?.Invoke(packet.SpellId) == true && !options.HasPetSummon)
		{
			actions.Add(PlayerCastSpellEarlyExitAction.SendPetRequired);
			options.SendPetRequired?.Invoke();
			return new PlayerCastSpellEarlyExitResult(PlayerCastSpellEarlyExitStatus.PetRequired, actions);
		}

		var template = options.GetSkillTemplate?.Invoke(packet.SpellId);
		if (template is not { IsPassive: false })
			return new PlayerCastSpellEarlyExitResult(PlayerCastSpellEarlyExitStatus.MissingOrPassiveTemplate, actions);

		if (player.IsProtectionActive())
		{
			player.StopProtectionActive();
			actions.Add(PlayerCastSpellEarlyExitAction.StopProtection);
			options.StopProtection?.Invoke();
		}

		actions.Add(PlayerCastSpellEarlyExitAction.CancelUseItem);
		options.CancelUseItem?.Invoke();

		if (options.NextSkillUseMilliseconds > packet.ReceiveTimeMilliseconds)
		{
			actions.Add(PlayerCastSpellEarlyExitAction.AuditCooldown);
			options.AuditCooldown?.Invoke(packet.SpellId, options.NextSkillUseMilliseconds - packet.ReceiveTimeMilliseconds, options.LastSkillId);
			if (options.NextSkillUseMilliseconds > options.CurrentTimeMilliseconds)
			{
				actions.Add(PlayerCastSpellEarlyExitAction.SendSkillNotReady);
				options.SendSkillNotReady?.Invoke();
				return new PlayerCastSpellEarlyExitResult(PlayerCastSpellEarlyExitStatus.SkillNotReady, actions);
			}
		}

		actions.Add(PlayerCastSpellEarlyExitAction.UseSkill);
		options.UseSkill?.Invoke(template, packet);
		return new PlayerCastSpellEarlyExitResult(PlayerCastSpellEarlyExitStatus.UseSkill, actions);
	}
}

public sealed record PlayerCastSpellEarlyExitOptions(
	Func<int, bool>? IsPetOrderSkill = null,
	bool HasPetSummon = false,
	Func<int, PlayerCastSpellSkillTemplate?>? GetSkillTemplate = null,
	long NextSkillUseMilliseconds = 0,
	long CurrentTimeMilliseconds = 0,
	int? LastSkillId = null,
	Action? SendSkillCannotCastDead = null,
	Action? CancelCurrentSkill = null,
	Action? SendPetRequired = null,
	Action? StopProtection = null,
	Action? CancelUseItem = null,
	Action<int, long, int?>? AuditCooldown = null,
	Action? SendSkillNotReady = null,
	Action<PlayerCastSpellSkillTemplate, CmCastSpell>? UseSkill = null);

public sealed record PlayerCastSpellSkillTemplate(int SkillId, bool IsPassive = false);

public sealed record PlayerCastSpellEarlyExitResult(
	PlayerCastSpellEarlyExitStatus Status,
	IReadOnlyList<PlayerCastSpellEarlyExitAction> Actions);

public enum PlayerCastSpellEarlyExitStatus
{
	DeadPlayer,
	CancelCurrentSkill,
	PetRequired,
	MissingOrPassiveTemplate,
	SkillNotReady,
	UseSkill,
}

public enum PlayerCastSpellEarlyExitAction
{
	SendSkillCannotCastDead,
	CancelCurrentSkill,
	SendPetRequired,
	StopProtection,
	CancelUseItem,
	AuditCooldown,
	SendSkillNotReady,
	UseSkill,
}
