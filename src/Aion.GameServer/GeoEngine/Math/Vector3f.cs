using System;

namespace Aion.GameServer.GeoEngine.Math;

/// <summary>
/// <c>Vector3f</c> defines a Vector for a three float value tuple. Utility methods are
/// included to aid in mathematical calculations.
/// Java parity: geoEngine/math/Vector3f (jMonkeyEngine; Mark Powell, Joshua Slack).
/// </summary>
public sealed class Vector3f
{
    public object Create()
    {
        return new Vector3f();
    }

    public static readonly Vector3f ZERO = new(0, 0, 0);
    public static readonly Vector3f NAN = new(float.NaN, float.NaN, float.NaN);
    public static readonly Vector3f UNIT_X = new(1, 0, 0);
    public static readonly Vector3f UNIT_Y = new(0, 1, 0);
    public static readonly Vector3f UNIT_Z = new(0, 0, 1);
    public static readonly Vector3f UNIT_XYZ = new(1, 1, 1);
    public static readonly Vector3f POSITIVE_INFINITY = new(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
    public static readonly Vector3f NEGATIVE_INFINITY = new(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);

    /// <summary>the x value of the vector.</summary>
    public float X;

    /// <summary>the y value of the vector.</summary>
    public float Y;

    /// <summary>the z value of the vector.</summary>
    public float Z;

    public Vector3f()
    {
    }

    public Vector3f(float x, float y, float z)
    {
        X = x;
        Y = y;
        Z = z;
    }

    public Vector3f(Vector3f copy)
    {
        Set(copy);
    }

    public Vector3f Set(float x, float y, float z)
    {
        X = x;
        Y = y;
        Z = z;
        return this;
    }

    public Vector3f Set(Vector3f vect)
    {
        X = vect.X;
        Y = vect.Y;
        Z = vect.Z;
        return this;
    }

    public Vector3f Add(Vector3f vec)
    {
        return new Vector3f(X + vec.X, Y + vec.Y, Z + vec.Z);
    }

    public Vector3f Add(Vector3f vec, Vector3f result)
    {
        result.X = X + vec.X;
        result.Y = Y + vec.Y;
        result.Z = Z + vec.Z;
        return result;
    }

    public Vector3f AddLocal(Vector3f vec)
    {
        X += vec.X;
        Y += vec.Y;
        Z += vec.Z;
        return this;
    }

    public Vector3f Add(float addX, float addY, float addZ)
    {
        return new Vector3f(X + addX, Y + addY, Z + addZ);
    }

    public Vector3f AddLocal(float addX, float addY, float addZ)
    {
        X += addX;
        Y += addY;
        Z += addZ;
        return this;
    }

    public Vector3f ScaleAdd(float scalar, Vector3f add)
    {
        X = X * scalar + add.X;
        Y = Y * scalar + add.Y;
        Z = Z * scalar + add.Z;
        return this;
    }

    public Vector3f ScaleAdd(float scalar, Vector3f mult, Vector3f add)
    {
        X = mult.X * scalar + add.X;
        Y = mult.Y * scalar + add.Y;
        Z = mult.Z * scalar + add.Z;
        return this;
    }

    public float Dot(Vector3f vec)
    {
        return X * vec.X + Y * vec.Y + Z * vec.Z;
    }

    public Vector3f Cross(Vector3f v)
    {
        return Cross(v, null);
    }

    public Vector3f Cross(Vector3f v, Vector3f? result)
    {
        return Cross(v.X, v.Y, v.Z, result);
    }

    public Vector3f Cross(float otherX, float otherY, float otherZ, Vector3f? result)
    {
        if (result == null)
            result = new Vector3f();
        float resX = (Y * otherZ) - (Z * otherY);
        float resY = (Z * otherX) - (X * otherZ);
        float resZ = (X * otherY) - (Y * otherX);
        result.Set(resX, resY, resZ);
        return result;
    }

    public Vector3f CrossLocal(Vector3f v)
    {
        return CrossLocal(v.X, v.Y, v.Z);
    }

    public Vector3f CrossLocal(float otherX, float otherY, float otherZ)
    {
        float tempx = (Y * otherZ) - (Z * otherY);
        float tempy = (Z * otherX) - (X * otherZ);
        Z = (X * otherY) - (Y * otherX);
        X = tempx;
        Y = tempy;
        return this;
    }

    public Vector3f Project(Vector3f other)
    {
        float n = Dot(other); // A . B
        float d = other.LengthSquared(); // |B|^2
        return new Vector3f(other).NormalizeLocal().MultLocal(n / d);
    }

    public float Length()
    {
        return FastMath.Sqrt(LengthSquared());
    }

    public float LengthSquared()
    {
        return X * X + Y * Y + Z * Z;
    }

    public float DistanceSquared(Vector3f v)
    {
        double dx = X - v.X;
        double dy = Y - v.Y;
        double dz = Z - v.Z;
        return (float)(dx * dx + dy * dy + dz * dz);
    }

    public float Distance(Vector3f v)
    {
        return FastMath.Sqrt(DistanceSquared(v));
    }

    public Vector3f Mult(float scalar)
    {
        return new Vector3f(X * scalar, Y * scalar, Z * scalar);
    }

    public Vector3f Mult(float scalar, Vector3f? product)
    {
        if (null == product)
        {
            product = new Vector3f();
        }

        product.X = X * scalar;
        product.Y = Y * scalar;
        product.Z = Z * scalar;
        return product;
    }

    public Vector3f MultLocal(float scalar)
    {
        X *= scalar;
        Y *= scalar;
        Z *= scalar;
        return this;
    }

    public Vector3f MultLocal(Vector3f vec)
    {
        X *= vec.X;
        Y *= vec.Y;
        Z *= vec.Z;
        return this;
    }

    public Vector3f MultLocal(float x, float y, float z)
    {
        X *= x;
        Y *= y;
        Z *= z;
        return this;
    }

    public Vector3f Mult(Vector3f vec)
    {
        return Mult(vec, null);
    }

    public Vector3f Mult(Vector3f vec, Vector3f? store)
    {
        if (store == null)
            store = new Vector3f();
        return store.Set(X * vec.X, Y * vec.Y, Z * vec.Z);
    }

    public Vector3f Divide(float scalar)
    {
        scalar = 1f / scalar;
        return new Vector3f(X * scalar, Y * scalar, Z * scalar);
    }

    public Vector3f DivideLocal(float scalar)
    {
        scalar = 1f / scalar;
        X *= scalar;
        Y *= scalar;
        Z *= scalar;
        return this;
    }

    public Vector3f Divide(Vector3f scalar)
    {
        return new Vector3f(X / scalar.X, Y / scalar.Y, Z / scalar.Z);
    }

    public Vector3f DivideLocal(Vector3f scalar)
    {
        X /= scalar.X;
        Y /= scalar.Y;
        Z /= scalar.Z;
        return this;
    }

    public Vector3f Negate()
    {
        return new Vector3f(-X, -Y, -Z);
    }

    public Vector3f NegateLocal()
    {
        X = -X;
        Y = -Y;
        Z = -Z;
        return this;
    }

    public Vector3f Subtract(Vector3f vec)
    {
        return new Vector3f(X - vec.X, Y - vec.Y, Z - vec.Z);
    }

    public Vector3f SubtractLocal(Vector3f vec)
    {
        X -= vec.X;
        Y -= vec.Y;
        Z -= vec.Z;
        return this;
    }

    public Vector3f Subtract(Vector3f vec, Vector3f? result)
    {
        if (result == null)
        {
            result = new Vector3f();
        }
        result.X = X - vec.X;
        result.Y = Y - vec.Y;
        result.Z = Z - vec.Z;
        return result;
    }

    public Vector3f Subtract(float subtractX, float subtractY, float subtractZ)
    {
        return new Vector3f(X - subtractX, Y - subtractY, Z - subtractZ);
    }

    public Vector3f SubtractLocal(float subtractX, float subtractY, float subtractZ)
    {
        X -= subtractX;
        Y -= subtractY;
        Z -= subtractZ;
        return this;
    }

    public Vector3f Normalize()
    {
        float length = X * X + Y * Y + Z * Z;
        if (length != 1f && length != 0f)
        {
            length = 1.0f / FastMath.Sqrt(length);
            return new Vector3f(X * length, Y * length, Z * length);
        }
        return Clone();
    }

    public Vector3f NormalizeLocal()
    {
        // NOTE: this implementation is more optimized
        // than the old jme normalize as this method
        // is commonly used.
        float length = X * X + Y * Y + Z * Z;
        if (length != 1f && length != 0f)
        {
            length = 1.0f / FastMath.Sqrt(length);
            X *= length;
            Y *= length;
            Z *= length;
        }
        return this;
    }

    public void MaxLocal(Vector3f other)
    {
        X = other.X > X ? other.X : X;
        Y = other.Y > Y ? other.Y : Y;
        Z = other.Z > Z ? other.Z : Z;
    }

    public void MinLocal(Vector3f other)
    {
        X = other.X < X ? other.X : X;
        Y = other.Y < Y ? other.Y : Y;
        Z = other.Z < Z ? other.Z : Z;
    }

    public Vector3f Zero()
    {
        X = Y = Z = 0;
        return this;
    }

    public float AngleBetween(Vector3f otherVector)
    {
        float dotProduct = Dot(otherVector);
        float angle = FastMath.Acos(dotProduct);
        return angle;
    }

    public Vector3f Interpolate(Vector3f finalVec, float changeAmnt)
    {
        X = (1 - changeAmnt) * X + changeAmnt * finalVec.X;
        Y = (1 - changeAmnt) * Y + changeAmnt * finalVec.Y;
        Z = (1 - changeAmnt) * Z + changeAmnt * finalVec.Z;
        return this;
    }

    public Vector3f Interpolate(Vector3f beginVec, Vector3f finalVec, float changeAmnt)
    {
        X = (1 - changeAmnt) * beginVec.X + changeAmnt * finalVec.X;
        Y = (1 - changeAmnt) * beginVec.Y + changeAmnt * finalVec.Y;
        Z = (1 - changeAmnt) * beginVec.Z + changeAmnt * finalVec.Z;
        return this;
    }

    public static bool IsValidVector(Vector3f? vector)
    {
        if (vector == null)
            return false;
        if (float.IsNaN(vector.X) || float.IsNaN(vector.Y) || float.IsNaN(vector.Z))
            return false;
        if (float.IsInfinity(vector.X) || float.IsInfinity(vector.Y) || float.IsInfinity(vector.Z))
            return false;
        return true;
    }

    public static void GenerateOrthonormalBasis(Vector3f u, Vector3f v, Vector3f w)
    {
        w.NormalizeLocal();
        GenerateComplementBasis(u, v, w);
    }

    public static void GenerateComplementBasis(Vector3f u, Vector3f v, Vector3f w)
    {
        float fInvLength;

        if (FastMath.Abs(w.X) >= FastMath.Abs(w.Y))
        {
            // w.x or w.z is the largest magnitude component, swap them
            fInvLength = FastMath.InvSqrt(w.X * w.X + w.Z * w.Z);
            u.X = -w.Z * fInvLength;
            u.Y = 0.0f;
            u.Z = +w.X * fInvLength;
            v.X = w.Y * u.Z;
            v.Y = w.Z * u.X - w.X * u.Z;
            v.Z = -w.Y * u.X;
        }
        else
        {
            // w.y or w.z is the largest magnitude component, swap them
            fInvLength = FastMath.InvSqrt(w.Y * w.Y + w.Z * w.Z);
            u.X = 0.0f;
            u.Y = +w.Z * fInvLength;
            u.Z = -w.Y * fInvLength;
            v.X = w.Y * u.Z - w.Z * u.Y;
            v.Y = -w.X * u.Z;
            v.Z = w.X * u.Y;
        }
    }

    public Vector3f Clone()
    {
        return new Vector3f(this);
    }

    /// <summary>
    /// Saves this Vector3f into the given float[] object.
    /// </summary>
    public float[] ToArray(float[]? floats)
    {
        if (floats == null)
        {
            floats = new float[3];
        }
        floats[0] = X;
        floats[1] = Y;
        floats[2] = Z;
        return floats;
    }

    public override bool Equals(object? o)
    {
        if (o is not Vector3f)
        {
            return false;
        }

        if (this == o)
        {
            return true;
        }

        Vector3f comp = (Vector3f)o;
        if (X.CompareTo(comp.X) != 0)
            return false;
        if (Y.CompareTo(comp.Y) != 0)
            return false;
        if (Z.CompareTo(comp.Z) != 0)
            return false;
        return true;
    }

    public override int GetHashCode()
    {
        int hash = 37;
        hash += 37 * hash + BitConverter.SingleToInt32Bits(X);
        hash += 37 * hash + BitConverter.SingleToInt32Bits(Y);
        hash += 37 * hash + BitConverter.SingleToInt32Bits(Z);
        return hash;
    }

    public override string ToString()
    {
        return "(" + X + ", " + Y + ", " + Z + ")";
    }

    public float GetX()
    {
        return X;
    }

    public Vector3f SetX(float x)
    {
        X = x;
        return this;
    }

    public float GetY()
    {
        return Y;
    }

    public Vector3f SetY(float y)
    {
        Y = y;
        return this;
    }

    public float GetZ()
    {
        return Z;
    }

    public Vector3f SetZ(float z)
    {
        Z = z;
        return this;
    }

    public float Get(int index)
    {
        switch (index)
        {
            case 0:
                return X;
            case 1:
                return Y;
            case 2:
                return Z;
        }
        throw new ArgumentException("index must be either 0, 1 or 2");
    }

    public void Set(int index, float value)
    {
        switch (index)
        {
            case 0:
                X = value;
                return;
            case 1:
                Y = value;
                return;
            case 2:
                Z = value;
                return;
        }
        throw new ArgumentException("index must be either 0, 1 or 2");
    }
}
