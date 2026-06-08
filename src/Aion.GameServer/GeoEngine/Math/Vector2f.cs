using System;
using System.IO;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aion.GameServer.GeoEngine.Math;

/// <summary>
/// <c>Vector2f</c> defines a Vector for a two float value vector.
/// Java parity: geoEngine/math/Vector2f (jMonkeyEngine; Mark Powell, Joshua Slack).
/// </summary>
public sealed class Vector2f
{
    private static readonly ILogger Logger = NullLogger.Instance;

    public static readonly Vector2f ZERO = new(0f, 0f);
    public static readonly Vector2f UNIT_XY = new(1f, 1f);

    /// <summary>the x value of the vector.</summary>
    public float X;

    /// <summary>the y value of the vector.</summary>
    public float Y;

    public Vector2f(float x, float y)
    {
        X = x;
        Y = y;
    }

    public Vector2f()
    {
        X = Y = 0;
    }

    public Vector2f(Vector2f vector2f)
    {
        X = vector2f.X;
        Y = vector2f.Y;
    }

    public Vector2f Set(float x, float y)
    {
        X = x;
        Y = y;
        return this;
    }

    public Vector2f Set(Vector2f vec)
    {
        X = vec.X;
        Y = vec.Y;
        return this;
    }

    public Vector2f? Add(Vector2f vec)
    {
        if (null == vec)
        {
            Logger.LogWarning("Provided vector is null, null returned.");
            return null;
        }
        return new Vector2f(X + vec.X, Y + vec.Y);
    }

    public Vector2f? AddLocal(Vector2f vec)
    {
        if (null == vec)
        {
            Logger.LogWarning("Provided vector is null, null returned.");
            return null;
        }
        X += vec.X;
        Y += vec.Y;
        return this;
    }

    public Vector2f AddLocal(float addX, float addY)
    {
        X += addX;
        Y += addY;
        return this;
    }

    public Vector2f? Add(Vector2f vec, Vector2f? result)
    {
        if (null == vec)
        {
            Logger.LogWarning("Provided vector is null, null returned.");
            return null;
        }
        if (result == null)
            result = new Vector2f();
        result.X = X + vec.X;
        result.Y = Y + vec.Y;
        return result;
    }

    public float Dot(Vector2f vec)
    {
        if (null == vec)
        {
            Logger.LogWarning("Provided vector is null, 0 returned.");
            return 0;
        }
        return X * vec.X + Y * vec.Y;
    }

    public Vector3f Cross(Vector2f v)
    {
        return new Vector3f(0, 0, Determinant(v));
    }

    public float Determinant(Vector2f v)
    {
        return (X * v.Y) - (Y * v.X);
    }

    public Vector2f Interpolate(Vector2f finalVec, float changeAmnt)
    {
        X = (1 - changeAmnt) * X + changeAmnt * finalVec.X;
        Y = (1 - changeAmnt) * Y + changeAmnt * finalVec.Y;
        return this;
    }

    public Vector2f Interpolate(Vector2f beginVec, Vector2f finalVec, float changeAmnt)
    {
        X = (1 - changeAmnt) * beginVec.X + changeAmnt * finalVec.X;
        Y = (1 - changeAmnt) * beginVec.Y + changeAmnt * finalVec.Y;
        return this;
    }

    public static bool IsValidVector(Vector2f? vector)
    {
        if (vector == null)
            return false;
        if (float.IsNaN(vector.X) || float.IsNaN(vector.Y))
            return false;
        if (float.IsInfinity(vector.X) || float.IsInfinity(vector.Y))
            return false;
        return true;
    }

    public float Length()
    {
        return FastMath.Sqrt(LengthSquared());
    }

    public float LengthSquared()
    {
        return X * X + Y * Y;
    }

    public float DistanceSquared(Vector2f v)
    {
        double dx = X - v.X;
        double dy = Y - v.Y;
        return (float)(dx * dx + dy * dy);
    }

    public float DistanceSquared(float otherX, float otherY)
    {
        double dx = X - otherX;
        double dy = Y - otherY;
        return (float)(dx * dx + dy * dy);
    }

    public float Distance(Vector2f v)
    {
        return FastMath.Sqrt(DistanceSquared(v));
    }

    public Vector2f Mult(float scalar)
    {
        return new Vector2f(X * scalar, Y * scalar);
    }

    public Vector2f MultLocal(float scalar)
    {
        X *= scalar;
        Y *= scalar;
        return this;
    }

    public Vector2f? MultLocal(Vector2f vec)
    {
        if (null == vec)
        {
            Logger.LogWarning("Provided vector is null, null returned.");
            return null;
        }
        X *= vec.X;
        Y *= vec.Y;
        return this;
    }

    public Vector2f Mult(float scalar, Vector2f? product)
    {
        if (null == product)
        {
            product = new Vector2f();
        }

        product.X = X * scalar;
        product.Y = Y * scalar;
        return product;
    }

    public Vector2f Divide(float scalar)
    {
        return new Vector2f(X / scalar, Y / scalar);
    }

    public Vector2f DivideLocal(float scalar)
    {
        X /= scalar;
        Y /= scalar;
        return this;
    }

    public Vector2f Negate()
    {
        return new Vector2f(-X, -Y);
    }

    public Vector2f NegateLocal()
    {
        X = -X;
        Y = -Y;
        return this;
    }

    public Vector2f Subtract(Vector2f vec)
    {
        return Subtract(vec, null);
    }

    public Vector2f Subtract(Vector2f vec, Vector2f? store)
    {
        if (store == null)
            store = new Vector2f();
        store.X = X - vec.X;
        store.Y = Y - vec.Y;
        return store;
    }

    public Vector2f Subtract(float valX, float valY)
    {
        return new Vector2f(X - valX, Y - valY);
    }

    public Vector2f? SubtractLocal(Vector2f vec)
    {
        if (null == vec)
        {
            Logger.LogWarning("Provided vector is null, null returned.");
            return null;
        }
        X -= vec.X;
        Y -= vec.Y;
        return this;
    }

    public Vector2f SubtractLocal(float valX, float valY)
    {
        X -= valX;
        Y -= valY;
        return this;
    }

    public Vector2f Normalize()
    {
        float length = Length();
        if (length != 0)
        {
            return Divide(length);
        }

        return Divide(1);
    }

    public Vector2f NormalizeLocal()
    {
        float length = Length();
        if (length != 0)
        {
            return DivideLocal(length);
        }

        return DivideLocal(1);
    }

    public float SmallestAngleBetween(Vector2f otherVector)
    {
        float dotProduct = Dot(otherVector);
        float angle = FastMath.Acos(dotProduct);
        return angle;
    }

    public float AngleBetween(Vector2f otherVector)
    {
        float angle = FastMath.Atan2(otherVector.Y, otherVector.X) - FastMath.Atan2(Y, X);
        return angle;
    }

    public float GetX()
    {
        return X;
    }

    public Vector2f SetX(float x)
    {
        X = x;
        return this;
    }

    public float GetY()
    {
        return Y;
    }

    public Vector2f SetY(float y)
    {
        Y = y;
        return this;
    }

    /// <summary>
    /// Returns (in radians) the angle represented by this Vector2f, converting rectangular
    /// coordinates (x, y) to polar coordinates (r, theta). [-pi, pi)
    /// </summary>
    public float GetAngle()
    {
        return -FastMath.Atan2(Y, X);
    }

    public Vector2f Zero()
    {
        X = Y = 0;
        return this;
    }

    public override int GetHashCode()
    {
        int hash = 37;
        hash += 37 * hash + BitConverter.SingleToInt32Bits(X);
        hash += 37 * hash + BitConverter.SingleToInt32Bits(Y);
        return hash;
    }

    public Vector2f Clone()
    {
        return new Vector2f(this);
    }

    /// <summary>
    /// Saves this Vector2f into the given float[] object.
    /// </summary>
    public float[] ToArray(float[]? floats)
    {
        if (floats == null)
        {
            floats = new float[2];
        }
        floats[0] = X;
        floats[1] = Y;
        return floats;
    }

    public override bool Equals(object? o)
    {
        if (o is not Vector2f)
        {
            return false;
        }

        if (this == o)
        {
            return true;
        }

        Vector2f comp = (Vector2f)o;
        if (X.CompareTo(comp.X) != 0)
            return false;
        if (Y.CompareTo(comp.Y) != 0)
            return false;
        return true;
    }

    public override string ToString()
    {
        return "(" + X + ", " + Y + ")";
    }

    /// <summary>
    /// Used with serialization. Not to be called manually.
    /// Java parity: readExternal(ObjectInput) — ObjectInput.readFloat() → BinaryReader.ReadSingle().
    /// </summary>
    public void ReadExternal(BinaryReader input)
    {
        X = input.ReadSingle();
        Y = input.ReadSingle();
    }

    /// <summary>
    /// Used with serialization. Not to be called manually.
    /// Java parity: writeExternal(ObjectOutput) — ObjectOutput.writeFloat() → BinaryWriter.Write(float).
    /// </summary>
    public void WriteExternal(BinaryWriter output)
    {
        output.Write(X);
        output.Write(Y);
    }

    public Type GetClassTag()
    {
        return GetType();
    }

    public void RotateAroundOrigin(float angle, bool cw)
    {
        if (cw)
            angle = -angle;
        float newX = FastMath.Cos(angle) * X - FastMath.Sin(angle) * Y;
        float newY = FastMath.Sin(angle) * X + FastMath.Cos(angle) * Y;
        X = newX;
        Y = newY;
    }
}
