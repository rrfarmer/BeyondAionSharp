namespace Aion.GameServer.Model.GameObjects;

/// <summary>
/// Base class for all in-game objects a player can interact with (npcs, monsters, players, items).
/// Each AionObject is uniquely identified by its objectId.
/// Java parity: model/gameobjects/AionObject.
/// </summary>
/// <remarks>
/// This is the dependency-free core of the Java class. Java also has an
/// <c>AionObject(int objId, boolean autoReleaseObjectId)</c> constructor that registers a GC
/// Cleaner to release the objectId via RespawnService.setAutoReleaseId / IDFactory.releaseId.
/// That behavior belongs to the respawn/id-release layer (not a foundation leaf) and has no
/// caller yet; it is ported as its own unit when RespawnService is faithfully ported. It is
/// intentionally absent here rather than stubbed.
/// </remarks>
public abstract class AionObject
{
	private readonly int _objectId;

	// Java parity: AionObject(int objId) { this(objId, false); }
	protected AionObject(int objectId) : this(objectId, false)
	{
	}

	// Java parity: AionObject(int objId, boolean autoReleaseObjectId).
	protected AionObject(int objectId, bool autoReleaseObjectId)
	{
		_objectId = objectId;
		// Java parity: when autoReleaseObjectId, AionObject registers a Cleaner to auto-release the id on GC
		// (RespawnService.setAutoReleaseId else IDFactory.releaseId). TODO-backlog: wire the Cleaner-equivalent
		// (finalizer/ConditionalWeakTable) once IDFactory/RespawnService auto-release is reconciled.
		_ = autoReleaseObjectId;
	}

	/// <summary>Unique objectId of this AionObject. Java parity: getObjectId().</summary>
	public int ObjectId => _objectId;

	// Java parity: getObjectId() (method form, bridges Java callers to the C# ObjectId property).
	public int GetObjectId() => _objectId;

	/// <summary>
	/// Name of the object. Unique for players, common for NPCs/items/etc.
	/// Java parity: abstract getName().
	/// </summary>
	public abstract string Name { get; }

	// Java parity: getName() (method form, bridges Java callers to the C# Name property).
	public string GetName() => Name;

	// Java parity: final hashCode() returns objectId.
	public sealed override int GetHashCode() => _objectId;

	// Java parity: final equals(Object) — reference-equal, type-check, objectId==0 dummy guard, then objectId compare.
	public sealed override bool Equals(object? obj)
	{
		if (ReferenceEquals(this, obj))
			return true;

		if (obj is not AionObject)
			return false;

		if (_objectId == 0) // object is a dummy (no unique ID from IDFactory)
			return false;

		return GetHashCode() == obj.GetHashCode();
	}

	// Java parity: toString() -> "SimpleClassName [name=..., objectId=...]".
	public override string ToString() => $"{GetType().Name} [name={Name}, objectId={_objectId}]";
}
