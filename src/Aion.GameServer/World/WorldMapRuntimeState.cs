using Aion.GameServer.Dataholders;

namespace Aion.GameServer.World;

public sealed class WorldMapRuntimeState
{
	private WorldZoneAttributes _currentFlags;

	public WorldMapRuntimeState(WorldMapSummary summary)
	{
		// Java parity: world/WorldMap constructor initializes worldOptions from WorldMapTemplate.flags.
		Summary = summary;
		_currentFlags = summary.Flags;
	}

	public WorldMapSummary Summary { get; }

	public WorldZoneAttributes CurrentFlags => _currentFlags;

	public bool IsFlightAllowed => Summary.IsFlightAllowed(_currentFlags);

	public bool CanGlide => Summary.CanGlide(_currentFlags);

	public bool CanPutKisk => Summary.CanPutKisk(_currentFlags);

	public bool CanRecall => Summary.CanRecall(_currentFlags);

	public bool CanRide => Summary.CanRide(_currentFlags);

	public bool CanFlyRide => Summary.CanFlyRide(_currentFlags);

	public bool IsPvpAllowed => Summary.IsPvpAllowed(_currentFlags);

	public bool IsSameRaceDuelsAllowed => Summary.IsSameRaceDuelsAllowed(_currentFlags);

	public bool IsOtherRaceDuelsAllowed => Summary.IsOtherRaceDuelsAllowed(_currentFlags);

	public bool CanReturnToBattle => Summary.CanReturnToBattle(_currentFlags);

	public bool HasOverriddenOption(WorldZoneAttributes option)
	{
		return Summary.HasOverriddenOption(option, _currentFlags);
	}

	public void SetWorldOption(WorldZoneAttributes option)
	{
		// Java parity: world/WorldMap.setWorldOption.
		_currentFlags = Summary.SetWorldOption(_currentFlags, option);
	}

	public void RemoveWorldOption(WorldZoneAttributes option)
	{
		// Java parity: world/WorldMap.removeWorldOption.
		_currentFlags = Summary.RemoveWorldOption(_currentFlags, option);
	}
}
