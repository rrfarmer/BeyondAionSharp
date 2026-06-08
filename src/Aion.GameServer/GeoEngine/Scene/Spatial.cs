using System;
using System.Text.RegularExpressions;
using Aion.GameServer.GeoEngine.Bounding;
using Aion.GameServer.GeoEngine.Collision;
using Aion.GameServer.GeoEngine.Math;

namespace Aion.GameServer.GeoEngine.Scene;

/// <summary>
/// <c>Spatial</c> is the base class for scene graph nodes. It maintains a link to a parent, its
/// local transforms and the world's transforms. <see cref="Node"/> and Geometry are subclasses.
/// Java parity: geoEngine/scene/Spatial (jMonkeyEngine; Mark Powell, Joshua Slack, Rolandas).
/// </summary>
public abstract class Spatial : Collidable
{
    public enum CullHint
    {
        /// <summary>Do whatever our parent does. If no parent, defaults to dynamic.</summary>
        Inherit,

        /// <summary>Do not draw if not at least partially within the camera view frustum.</summary>
        Dynamic,

        /// <summary>Always cull this from view.</summary>
        Always,

        /// <summary>Never cull this from view (still culled if our parent is culled).</summary>
        Never,
    }

    /// <summary>Spatial's bounding volume relative to the world.</summary>
    protected BoundingVolume? worldBound;

    /// <summary>This spatial's name.</summary>
    protected internal string? name; // Java 'protected' (package-accessible) → C# protected internal

    /// <summary>Spatial's parent, or null if it has none.</summary>
    protected Node? parent;

    /// <summary>Do not use this constructor. Serialization purposes only.</summary>
    protected Spatial()
        : this(null)
    {
    }

    public Spatial(string? name)
    {
        this.name = name;
    }

    public void SetName(string? name)
    {
        if (name != null)
            this.name = name;
    }

    public string? GetName()
    {
        return name;
    }

    /// <summary>Retrieves this node's parent. If null, this is the root node.</summary>
    public Node? GetParent()
    {
        return parent;
    }

    /// <summary>
    /// Called by Node.AttachChild/DetachChild — don't call directly. Sets the parent of this node.
    /// </summary>
    protected internal void SetParent(Node? parent)
    {
        this.parent = parent;
    }

    /// <summary>Removes this Spatial from its parent.</summary>
    public bool RemoveFromParent()
    {
        if (parent != null)
        {
            parent.DetachChild(this);
            return true;
        }
        return false;
    }

    /// <summary>Determines if the provided Node is the parent, or parent's parent, etc. of this Spatial.</summary>
    public bool HasAncestor(Node ancestor)
    {
        if (parent == null)
        {
            return false;
        }
        else if (parent.Equals(ancestor))
        {
            return true;
        }
        else
        {
            return parent.HasAncestor(ancestor);
        }
    }

    /// <summary>Recalculates the bounding object for this Spatial.</summary>
    public abstract void UpdateModelBound();

    /// <summary>Sets the bounding object for this Spatial.</summary>
    public abstract void SetModelBound(BoundingVolume? modelBound);

    /// <summary>The sum of all vertices under this Spatial.</summary>
    public abstract int GetVertexCount();

    /// <summary>The sum of all triangles under this Spatial.</summary>
    public abstract int GetTriangleCount();

    public abstract void SetCollisionIntentions(sbyte collisionIntentions);

    public abstract void SetMaterialId(sbyte materialId);

    public abstract sbyte GetCollisionIntentions();

    public abstract int GetMaterialId();

    /// <summary>Java parity: collideWith (inherited from Collidable; implemented by subclasses).</summary>
    public abstract int CollideWith(Collidable other, CollisionResults results);

    /// <summary>
    /// Matches the pattern against the entire name (anchored, as Java String.matches). A null
    /// subclass qualifies all Spatials; a null nameRegex qualifies all names.
    /// </summary>
    public bool Matches(Type? spatialSubclass, string? nameRegex)
    {
        if (spatialSubclass != null && !spatialSubclass.IsInstanceOfType(this))
            return false;

        if (nameRegex != null && (name == null || !Regex.IsMatch(name, "^(?:" + nameRegex + ")$")))
            return false;

        return true;
    }

    /// <summary>Retrieves the world bound at this node level.</summary>
    public BoundingVolume? GetWorldBound()
    {
        return worldBound;
    }

    public override string ToString()
    {
        return name + " (" + GetType().Name + ") use " + CollisionIntentions.ToString(GetCollisionIntentions());
    }

    public abstract void SetTransform(Matrix3f rotation, Vector3f loc, Vector3f scale);

    public virtual Spatial Clone()
    {
        return (Spatial)MemberwiseClone();
    }
}
