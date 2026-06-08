using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.World.Knownlist;

/// <summary>
/// A single entry in a <see cref="KnownList"/>: the known object + its cached visibility.
/// Java parity: world/knownlist/KnownObject.
/// </summary>
public class KnownObject
{
    private readonly VisibleObject _object;
    private bool _visible;

    public KnownObject(VisibleObject obj)
    {
        _object = obj;
    }

    // Java parity: get()
    public VisibleObject Get() => _object;

    // Java parity: isVisible()
    public bool IsVisible() => _visible;

    // Java parity: updateVisible(boolean) — package-private; returns true if the state changed.
    internal bool UpdateVisible(bool visible)
    {
        lock (this)
        {
            if (_visible != visible)
            {
                _visible = visible;
                return true;
            }
        }
        return false;
    }

    // Java parity: equals(Object)
    public override bool Equals(object? o)
    {
        if (o == null || GetType() != o.GetType())
            return false;
        var that = (KnownObject)o;
        return Equals(_object, that._object);
    }

    // Java parity: hashCode()
    public override int GetHashCode() => _object?.GetHashCode() ?? 0;

    // Java parity: toString()
    public override string ToString() =>
        _object.Name + " (objectId: " + _object.ObjectId + ", visible: " + _visible + ")";
}
