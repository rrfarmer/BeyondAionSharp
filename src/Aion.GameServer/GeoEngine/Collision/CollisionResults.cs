using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace Aion.GameServer.GeoEngine.Collision;

/// <summary>
/// Java parity: geoEngine/collision/CollisionResults (jMonkeyEngine).
/// Java <c>byte intentions</c> is signed → modeled as <c>sbyte</c> (consistent with
/// <see cref="CollisionIntentions.GetId"/>).
/// </summary>
public class CollisionResults : IEnumerable<CollisionResult>
{
    private static readonly double SLOPING_SURFACE_ANGLE_RAD = 45.0 / 180.0 * System.Math.PI; // players can't walk or stand on surfaces with >= 45° elevation angle
    private readonly List<CollisionResult> _results = new();
    private bool _sorted = true;
    private readonly sbyte _intentions;
    private readonly int _instanceId;
    private readonly bool _onlyFirst;
    private readonly IgnoreProperties? _ignoreProperties;
    private bool _invalidateSlopingSurface;

    public CollisionResults(sbyte intentions, int instanceId, IgnoreProperties? ignoreProperties)
        : this(intentions, instanceId, false, ignoreProperties)
    {
    }

    public CollisionResults(sbyte intentions, int instanceId)
        : this(intentions, instanceId, false, null)
    {
    }

    public CollisionResults(sbyte intentions, int instanceId, bool searchFirst)
        : this(intentions, instanceId, searchFirst, null)
    {
    }

    public CollisionResults(sbyte intentions, int instanceId, bool searchFirst, IgnoreProperties? ignoreProperties)
    {
        _intentions = intentions;
        _instanceId = instanceId;
        _onlyFirst = searchFirst;
        _ignoreProperties = ignoreProperties;
    }

    public void Clear()
    {
        _results.Clear();
    }

    public IEnumerator<CollisionResult> GetEnumerator()
    {
        if (!_sorted)
        {
            _results.Sort();
            _sorted = true;
        }

        return _results.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public void AddCollision(CollisionResult result)
    {
        if (float.IsNaN(result.GetDistance()))
        {
            return;
        }
        _results.Add(result);
        if (!_onlyFirst)
            _sorted = false;
    }

    public int Size()
    {
        return _results.Count;
    }

    public CollisionResult? GetClosestCollision()
    {
        if (Size() == 0)
            return null;

        if (!_sorted)
        {
            _results.Sort();
            _sorted = true;
        }

        return _results[0];
    }

    public CollisionResult? GetFarthestCollision()
    {
        if (Size() == 0)
            return null;

        if (!_sorted)
        {
            _results.Sort();
            _sorted = true;
        }

        return _results[Size() - 1];
    }

    public CollisionResult GetCollision(int index)
    {
        if (!_sorted)
        {
            _results.Sort();
            _sorted = true;
        }

        return _results[index];
    }

    /// <summary>
    /// Internal use only.
    /// </summary>
    public CollisionResult GetCollisionDirect(int index)
    {
        return _results[index];
    }

    public override string ToString()
    {
        StringBuilder sb = new StringBuilder();
        sb.Append("CollisionResults[");
        foreach (CollisionResult result in _results)
        {
            sb.Append(result).Append(", ");
        }
        if (_results.Count > 0)
            sb.Length = sb.Length - 2;

        sb.Append(']');
        return sb.ToString();
    }

    /// <summary>
    /// True if the results should only contain one collision max.
    /// </summary>
    public bool IsOnlyFirst()
    {
        return _onlyFirst;
    }

    public sbyte GetIntentions()
    {
        return _intentions;
    }

    public int GetInstanceId()
    {
        return _instanceId;
    }

    public IgnoreProperties? GetIgnoreProperties()
    {
        return _ignoreProperties;
    }

    public bool ShouldInvalidateSlopingSurface()
    {
        return _invalidateSlopingSurface;
    }

    public double GetSlopingSurfaceAngleRad()
    {
        return SLOPING_SURFACE_ANGLE_RAD;
    }

    public void SetInvalidateSlopingSurface(bool invalidateSlopingSurface)
    {
        _invalidateSlopingSurface = invalidateSlopingSurface;
    }
}
