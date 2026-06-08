using System;
using System.Collections.Generic;
using Aion.GameServer.GeoEngine.Bounding;
using Aion.GameServer.GeoEngine.Collision;
using Aion.GameServer.GeoEngine.Math;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aion.GameServer.GeoEngine.Scene;

/// <summary>
/// <c>Node</c> is an internal node of a scene graph: it maintains a collection of children and
/// merges their bounds for fast culling. Any number of children may be attached.
/// Java parity: geoEngine/scene/Node (jMonkeyEngine; Mark Powell, Gregg Patton, Joshua Slack).
/// </summary>
public class Node : Spatial
{
    private static readonly ILogger logger = NullLogger.Instance;

    /// <summary>This node's children.</summary>
    protected List<Spatial> children = new List<Spatial>(1);

    protected sbyte collisionIntentions;
    protected sbyte materialId;

    /// <summary>Do not use this constructor. Serialization purposes only.</summary>
    protected Node()
    {
    }

    public Node(string? name)
        : base(name)
    {
        collisionIntentions = CollisionIntention.ALL.GetId();
    }

    /// <summary>The number of children this node maintains.</summary>
    public int GetQuantity()
    {
        return children.Count;
    }

    public override int GetTriangleCount()
    {
        int count = 0;
        if (children != null)
        {
            foreach (Spatial child in children)
            {
                count += child.GetTriangleCount();
            }
        }

        return count;
    }

    public override int GetVertexCount()
    {
        int count = 0;
        if (children != null)
        {
            foreach (Spatial child in children)
            {
                count += child.GetVertexCount();
            }
        }

        return count;
    }

    /// <summary>
    /// Attaches a child to this node, becoming its parent (detaching it from any former parent).
    /// </summary>
    public int AttachChild(Spatial child)
    {
        if (child == null)
            throw new ArgumentNullException(nameof(child));

        if (child.GetParent() != this && child != this)
        {
            if (child.GetParent() != null)
            {
                child.GetParent()!.DetachChild(child);
            }
            child.SetParent(this);
            children.Add(child);
        }

        return children.Count;
    }

    /// <summary>
    /// Attaches a child to this node at an index, becoming its parent (detaching it from any former parent).
    /// </summary>
    public int AttachChildAt(Spatial child, int index)
    {
        if (child == null)
            throw new ArgumentNullException(nameof(child));

        if (child.GetParent() != this && child != this)
        {
            if (child.GetParent() != null)
            {
                child.GetParent()!.DetachChild(child);
            }
            child.SetParent(this);
            children.Insert(index, child);
        }

        return children.Count;
    }

    /// <summary>Removes a given child from the node's list.</summary>
    /// <returns>the index the child was at, or -1 if not in the list.</returns>
    public int DetachChild(Spatial child)
    {
        if (child == null)
            throw new ArgumentNullException(nameof(child));

        if (child.GetParent() == this)
        {
            int index = children.IndexOf(child);
            if (index != -1)
            {
                DetachChildAt(index);
            }
            return index;
        }

        return -1;
    }

    /// <summary>Removes the first child with a matching name from the node's list.</summary>
    public int DetachChildNamed(string childName)
    {
        if (childName == null)
            throw new ArgumentNullException(nameof(childName));

        for (int x = 0, max = children.Count; x < max; x++)
        {
            Spatial child = children[x];
            if (childName.Equals(child.GetName()))
            {
                DetachChildAt(x);
                return x;
            }
        }
        return -1;
    }

    /// <summary>Removes a child at a given index, returning it.</summary>
    public Spatial DetachChildAt(int index)
    {
        Spatial child = children[index];
        children.RemoveAt(index);
        if (child != null)
        {
            child.SetParent(null);
        }
        return child;
    }

    /// <summary>Removes all children attached to this node.</summary>
    public void DetachAllChildren()
    {
        for (int i = children.Count - 1; i >= 0; i--)
        {
            DetachChildAt(i);
        }
        logger.LogInformation("All children removed.");
    }

    public int GetChildIndex(Spatial sp)
    {
        return children.IndexOf(sp);
    }

    /// <summary>More efficient than detaching and attaching as no updates are needed.</summary>
    public void SwapChildren(int index1, int index2)
    {
        Spatial c2 = children[index2];
        Spatial c1 = children[index1];
        children.RemoveAt(index1);
        children.Insert(index1, c2);
        children.RemoveAt(index2);
        children.Insert(index2, c1);
    }

    /// <summary>Returns a child at a given index.</summary>
    public Spatial GetChild(int i)
    {
        return children[i];
    }

    /// <summary>Returns the first child found with exactly the given name (case sensitive).</summary>
    public Spatial? GetChild(string? name)
    {
        if (name == null)
            return null;

        foreach (Spatial child in children)
        {
            if (name.Equals(child.GetName()))
            {
                return child;
            }
            else if (child is Node node)
            {
                Spatial? @out = node.GetChild(name);
                if (@out != null)
                {
                    return @out;
                }
            }
        }
        return null;
    }

    /// <summary>Determines if the provided Spatial is contained in the children list of this node.</summary>
    public bool HasChild(Spatial spat)
    {
        if (children.Contains(spat))
            return true;

        foreach (Spatial child in children)
        {
            if (child is Node node && node.HasChild(spat))
                return true;
        }

        return false;
    }

    /// <summary>Returns all children of this node.</summary>
    public List<Spatial> GetChildren()
    {
        return children;
    }

    public void ChildChange(Geometry geometry, int index1, int index2)
    {
        // just pass to parent
        if (parent != null)
        {
            parent.ChildChange(geometry, index1, index2);
        }
    }

    public override int CollideWith(Collidable other, CollisionResults results)
    {
        if ((GetCollisionIntentions() & results.GetIntentions()) == 0)
        {
            return 0;
        }

        if (other is Ray)
        {
            if (worldBound == null || !worldBound.Intersects((Ray)other))
            {
                return 0;
            }
        }

        int total = 0;
        foreach (Spatial child in children)
        {
            if (child is Geometry)
            {
                // not used materialIds do not have collision intention for materials set
                if ((child.GetCollisionIntentions() & results.GetIntentions()) == 0)
                {
                    continue;
                }
            }
            total += child.CollideWith(other, results);
            if (total > 0 && results.IsOnlyFirst())
                break;
        }
        return total;
    }

    /// <summary>
    /// Returns a flat list of Spatials implementing T AND with name matching the pattern (anchored).
    /// "Descendants" does not include self. (Java's String-only overload maps to
    /// <c>DescendantMatches&lt;Spatial&gt;(regex)</c>.)
    /// </summary>
    public List<T> DescendantMatches<T>(string? nameRegex)
        where T : Spatial
    {
        List<T> newList = new List<T>();
        if (GetQuantity() < 1)
            return newList;
        foreach (Spatial child in children)
        {
            if (child.Matches(typeof(T), nameRegex))
                newList.Add((T)child);
            if (child is Node node)
                newList.AddRange(node.DescendantMatches<T>(nameRegex));
        }
        return newList;
    }

    /// <summary>Convenience wrapper.</summary>
    public List<T> DescendantMatches<T>()
        where T : Spatial
    {
        return DescendantMatches<T>(null);
    }

    public override void SetModelBound(BoundingVolume? modelBound)
    {
        if (children != null)
        {
            foreach (Spatial child in children)
            {
                child.SetModelBound(modelBound != null ? modelBound.Clone(null) : null);
            }
        }
    }

    public override void UpdateModelBound()
    {
        BoundingVolume? resultBound = null;
        if (children != null)
        {
            foreach (Spatial child in children)
            {
                child.UpdateModelBound();
                if (resultBound != null)
                {
                    // merge current world bound with child world bound
                    resultBound.MergeLocal(child.GetWorldBound()!);
                }
                else
                {
                    // set world bound to first non-null child world bound
                    if (child.GetWorldBound() != null)
                    {
                        resultBound = child.GetWorldBound()!.Clone(worldBound);
                    }
                }
            }
        }
        worldBound = resultBound;
    }

    public override void SetTransform(Matrix3f rotation, Vector3f loc, Vector3f scale)
    {
        if (children != null)
        {
            foreach (Spatial child in children)
            {
                child.SetTransform(rotation, loc, scale);
            }
        }
    }

    public override Spatial Clone()
    {
        Node node = new Node(name);
        node.collisionIntentions = collisionIntentions;
        node.materialId = materialId;
        foreach (Spatial spatial in children)
            if (spatial is Geometry)
            {
                Geometry geom = new Geometry(spatial.GetName(), ((Geometry)spatial).GetMesh());
                node.AttachChild(geom);
            }
            else if (spatial is Node n)
                node.AttachChild(n.Clone());
            else
                throw new NotSupportedException();
        return node;
    }

    public override sbyte GetCollisionIntentions()
    {
        return collisionIntentions;
    }

    public override void SetCollisionIntentions(sbyte collisionIntentions)
    {
        this.collisionIntentions = collisionIntentions;
    }

    public override int GetMaterialId()
    {
        return (materialId & 0xFF);
    }

    public override void SetMaterialId(sbyte materialId)
    {
        this.materialId = materialId;
    }
}
