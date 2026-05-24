using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion.ClientPackets;

namespace Aion.GameServer.Services;

public sealed class GameServerCastSpellHandlerHooks
{
	public Func<Player, int, bool> IsPetOrderSkill { get; init; } = (_, _) => false;

	public Func<Player, bool> HasPetSummon { get; init; } = _ => false;

	public Func<Player, int, PlayerCastSpellSkillTemplate?> GetSkillTemplate { get; init; } = (_, _) => null;

	public Func<Player, long> GetNextSkillUseMilliseconds { get; init; } = _ => 0;

	public Func<long> GetCurrentTimeMilliseconds { get; init; } = () => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

	public Func<Player, int?> GetLastSkillId { get; init; } = _ => null;

	public Action<Player, CmCastSpell> CancelCurrentSkill { get; init; } = (_, _) => { };

	public Action<Player> StopProtection { get; init; } = _ => { };

	public Action<Player> CancelUseItem { get; init; } = _ => { };

	public Action<Player, int, long, int?> AuditCooldown { get; init; } = (_, _, _, _) => { };

	public Action<Player, PlayerCastSpellSkillTemplate, CmCastSpell> UseSkill { get; init; } = (_, _, _) => { };
}
