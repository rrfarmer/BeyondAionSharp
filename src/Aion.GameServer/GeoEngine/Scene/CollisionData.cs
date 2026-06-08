using Aion.GameServer.GeoEngine.Bounding;
using Aion.GameServer.GeoEngine.Collision;
using Aion.GameServer.GeoEngine.Math;

namespace Aion.GameServer.GeoEngine.Scene;

/// <summary>
/// <c>CollisionData</c> can be used to do triangle-accurate collision between bounding volumes and rays.
/// Java parity: geoEngine/scene/CollisionData (jMonkeyEngine; Kirill Vainer).
/// </summary>
public interface CollisionData
{
    int CollideWith(Collidable other,
        Matrix4f worldMatrix,
        BoundingVolume worldBound,
        CollisionResults results);
}
