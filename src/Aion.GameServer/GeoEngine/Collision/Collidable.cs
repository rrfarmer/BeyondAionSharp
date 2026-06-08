namespace Aion.GameServer.GeoEngine.Collision;

/// <summary>
/// Interface for collidable objects.
/// Java parity: geoEngine/collision/Collidable (jMonkeyEngine; Kirill).
/// </summary>
public interface Collidable
{
    /// <summary>
    /// Check collision with another collidable.
    /// Java declares <c>throws UnsupportedCollisionException</c> (a runtime exception in C#).
    /// </summary>
    /// <returns>how many collisions were found</returns>
    int CollideWith(Collidable other, CollisionResults results);
}
