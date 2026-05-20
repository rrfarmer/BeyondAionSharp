using Aion.Commons.Network;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public sealed class SmSkillCooldown : GameServerPacket
{
	public const int PacketOpCode = 51;

	private readonly IReadOnlyList<CooldownEntry> _cooldowns;
	private readonly bool _notify;
	private readonly Func<DateTimeOffset> _clock;

	public SmSkillCooldown(
		IReadOnlyList<PlayerSkill> skills,
		IReadOnlyDictionary<int, long> cooldownExpirationMillisByCooldownId,
		SkillTemplateTable skillTemplates,
		bool notify,
		Func<DateTimeOffset>? clock = null)
		: base(PacketOpCode)
	{
		// Java parity: network/aion/serverpackets/SM_SKILL_COOLDOWN(Player, Map<Integer, Long>, boolean).
		_cooldowns = ResolveCooldowns(skills, cooldownExpirationMillisByCooldownId, skillTemplates);
		_notify = notify;
		_clock = clock ?? (() => DateTimeOffset.Now);
	}

	public bool HasCooldowns => _cooldowns.Count > 0;

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		// Java parity: network/aion/serverpackets/SM_SKILL_COOLDOWN.writeImpl.
		buffer.WriteH(_cooldowns.Count);
		buffer.WriteC(_notify ? 1 : 0);
		var nowMillis = _clock().ToUnixTimeMilliseconds();
		foreach (var cooldown in _cooldowns)
		{
			buffer.WriteH(cooldown.SkillId);
			buffer.WriteD(GetRemainingSeconds(cooldown.ExpirationTimeMillis, nowMillis));
			buffer.WriteD(cooldown.DurationMillis);
		}
	}

	private static IReadOnlyList<CooldownEntry> ResolveCooldowns(
		IReadOnlyList<PlayerSkill> skills,
		IReadOnlyDictionary<int, long> cooldownExpirationMillisByCooldownId,
		SkillTemplateTable skillTemplates)
	{
		var cooldowns = new List<CooldownEntry>();
		foreach (var skill in skills)
		{
			var template = skillTemplates.GetSkillTemplate(skill.SkillId);
			if (template == null)
				continue;

			if (!cooldownExpirationMillisByCooldownId.TryGetValue(template.CooldownId, out var expirationTimeMillis))
				continue;

			cooldowns.Add(new CooldownEntry(skill.SkillId, expirationTimeMillis, template.Cooldown * 100));
		}

		return cooldowns.OrderBy(cooldown => cooldown.DurationMillis).ToArray();
	}

	private static int GetRemainingSeconds(long expirationTimeMillis, long nowMillis)
	{
		return expirationTimeMillis == 0
			? 0
			: (int)Math.Max(0, (expirationTimeMillis - nowMillis) / 1000);
	}

	private sealed record CooldownEntry(int SkillId, long ExpirationTimeMillis, int DurationMillis);
}
