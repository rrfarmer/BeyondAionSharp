using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Services;

public sealed class WorldNpcSkillDamageFanoutService
{
	private readonly WorldNpcSkillDamageService _skillDamageService;
	private readonly IGameClientConnectionRegistry? _connectionRegistry;

	public WorldNpcSkillDamageFanoutService(
		WorldNpcSkillDamageService skillDamageService,
		IGameClientConnectionRegistry? connectionRegistry = null)
	{
		_skillDamageService = skillDamageService;
		_connectionRegistry = connectionRegistry;
	}

	public async ValueTask<WorldNpcSkillDamageFanoutResult> ApplyDamageEffectAndSendObserverPacketsAsync(
		WorldNpcSkillDamageRequest request,
		CancellationToken cancellationToken = default)
	{
		// Java parity: DamageEffect.applyEffect reaches CreatureLifeStats.reduceHp before equipment observers update item charge packets.
		var damageResult = await _skillDamageService.ApplyDamageEffectAsync(request, cancellationToken);
		var packets = damageResult.EquipmentObserverBurns?.Packets ?? Array.Empty<GameServerPacket>();
		var sentCount = 0;

		if (_connectionRegistry != null && request.Effector != null)
		{
			foreach (var packet in packets)
			{
				if (await _connectionRegistry.SendPacketToPlayerAsync(request.Effector.ObjectId, packet))
					sentCount++;
			}
		}

		return new WorldNpcSkillDamageFanoutResult(damageResult, packets, sentCount);
	}
}

public sealed record WorldNpcSkillDamageFanoutResult(
	WorldNpcSkillDamageResult DamageResult,
	IReadOnlyList<GameServerPacket> ObserverBurnPackets,
	int ObserverBurnPacketSentCount);
