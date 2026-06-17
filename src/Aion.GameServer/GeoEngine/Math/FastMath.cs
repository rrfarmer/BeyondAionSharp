using System;
using JMath = System.Math;

namespace Aion.GameServer.GeoEngine.Math;

/// <summary>
/// <c>FastMath</c> provides 'fast' math approximations and float equivalents of Math
/// functions. These are all used as static values and functions.
/// Java parity: geoEngine/math/FastMath (jMonkeyEngine).
/// </summary>
public static class FastMath
{
    /// <summary>A "close to zero" double epsilon value for use</summary>
    public const double DBL_EPSILON = 2.220446049250313E-16d;

    /// <summary>A "close to zero" float epsilon value for use</summary>
    public const float FLT_EPSILON = 1.1920928955078125E-7f;

    /// <summary>A "close to zero" float epsilon value for use</summary>
    public const float ZERO_TOLERANCE = 0.0001f;

    public const float ONE_THIRD = 1f / 3f;

    /// <summary>The value PI as a float. (180 degrees)</summary>
    public const float PI = (float)JMath.PI;

    /// <summary>The value 2PI as a float. (360 degrees)</summary>
    public const float TWO_PI = 2.0f * PI;

    /// <summary>The value PI/2 as a float. (90 degrees)</summary>
    public const float HALF_PI = 0.5f * PI;

    /// <summary>The value PI/4 as a float. (45 degrees)</summary>
    public const float QUARTER_PI = 0.25f * PI;

    /// <summary>The value 1/PI as a float.</summary>
    public const float INV_PI = 1.0f / PI;

    /// <summary>The value 1/(2PI) as a float.</summary>
    public const float INV_TWO_PI = 1.0f / TWO_PI;

    /// <summary>A value to multiply a degree value by, to convert it to radians.</summary>
    public const float DEG_TO_RAD = PI / 180.0f;

    /// <summary>A value to multiply a radian value by, to convert it to degrees.</summary>
    public const float RAD_TO_DEG = 180.0f / PI;

    /// <summary>A precreated random object for random numbers.</summary>
    public static readonly Random rand = new Random((int)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

    /// <summary>
    /// Returns true if the number is a power of 2 (2,4,8,16...)
    /// </summary>
    public static bool IsPowerOfTwo(int number)
    {
        return (number > 0) && (number & (number - 1)) == 0;
    }

    public static int NearestPowerOfTwo(int number)
    {
        return (int)JMath.Pow(2, JMath.Ceiling(JMath.Log(number) / JMath.Log(2)));
    }

    /// <summary>
    /// Linear interpolation from startValue to endValue by the given percent.
    /// </summary>
    public static float InterpolateLinear(float scale, float startValue, float endValue)
    {
        if (startValue == endValue)
        {
            return startValue;
        }
        if (scale <= 0f)
        {
            return startValue;
        }
        if (scale >= 1f)
        {
            return endValue;
        }
        return ((1f - scale) * startValue) + (scale * endValue);
    }

    /// <summary>
    /// Linear interpolation from startValue to endValue by the given percent.
    /// </summary>
    public static Vector3f InterpolateLinear(float scale, Vector3f startValue, Vector3f endValue)
    {
        Vector3f res = new Vector3f();
        res.X = InterpolateLinear(scale, startValue.X, endValue.X);
        res.Y = InterpolateLinear(scale, startValue.Y, endValue.Y);
        res.Z = InterpolateLinear(scale, startValue.Z, endValue.Z);
        return res;
    }

    /// <summary>
    /// Interpolate a spline between at least 4 control points following the Catmull-Rom equation.
    /// </summary>
    public static float InterpolateCatmullRom(float u, float T, float p0, float p1, float p2, float p3)
    {
        double c1, c2, c3, c4;
        c1 = p1;
        c2 = -1.0 * T * p0 + T * p2;
        c3 = 2 * T * p0 + (T - 3) * p1 + (3 - 2 * T) * p2 + -T * p3;
        c4 = -T * p0 + (2 - T) * p1 + (T - 2) * p2 + T * p3;

        return (float)(((c4 * u + c3) * u + c2) * u + c1);
    }

    /// <summary>
    /// Interpolate a spline between at least 4 control points following the Catmull-Rom equation.
    /// </summary>
    public static Vector3f InterpolateCatmullRom(float u, float T, Vector3f p0, Vector3f p1, Vector3f p2, Vector3f p3)
    {
        Vector3f res = new Vector3f();
        res.X = InterpolateCatmullRom(u, T, p0.X, p1.X, p2.X, p3.X);
        res.Y = InterpolateCatmullRom(u, T, p0.Y, p1.Y, p2.Y, p3.Y);
        res.Z = InterpolateCatmullRom(u, T, p0.Z, p1.Z, p2.Z, p3.Z);
        return res;
    }

    /// <summary>
    /// Returns the arc cosine of an angle given in radians.
    /// </summary>
    public static float Acos(float fValue)
    {
        if (-1.0f < fValue)
        {
            if (fValue < 1.0f)
            {
                return (float)JMath.Acos(fValue);
            }

            return 0.0f;
        }

        return PI;
    }

    /// <summary>
    /// Returns the arc sine of an angle given in radians.
    /// </summary>
    public static float Asin(float fValue)
    {
        if (-1.0f < fValue)
        {
            if (fValue < 1.0f)
            {
                return (float)JMath.Asin(fValue);
            }

            return HALF_PI;
        }

        return -HALF_PI;
    }

    /// <summary>
    /// Returns the arc tangent of an angle given in radians.
    /// </summary>
    public static float Atan(float fValue)
    {
        return (float)JMath.Atan(fValue);
    }

    /// <summary>
    /// A direct call to Math.atan2.
    /// </summary>
    public static float Atan2(float fY, float fX)
    {
        return (float)JMath.Atan2(fY, fX);
    }

    /// <summary>
    /// Rounds a fValue up. A call to Math.ceil
    /// </summary>
    public static float Ceil(float fValue)
    {
        return (float)JMath.Ceiling(fValue);
    }

    /// <summary>
    /// Fast Trig functions for x86. This forces the trig functions to stay
    /// within the safe area on the x86 processor (-45 degrees to +45 degrees).
    /// </summary>
    public static float ReduceSinAngle(float radians)
    {
        radians %= TWO_PI; // put us in -2PI to +2PI space
        if (JMath.Abs(radians) > PI)
        { // put us in -PI to +PI space
            radians = radians - (TWO_PI);
        }
        if (JMath.Abs(radians) > HALF_PI)
        { // put us in -PI/2 to +PI/2 space
            radians = PI - radians;
        }

        return radians;
    }

    /// <summary>
    /// Returns sine of a value.
    /// </summary>
    public static float Sin2(float fValue)
    {
        fValue = ReduceSinAngle(fValue); // limits angle to between -PI/2 and +PI/2
        if (JMath.Abs(fValue) <= JMath.PI / 4)
        {
            return (float)JMath.Sin(fValue);
        }

        return (float)JMath.Cos(JMath.PI / 2 - fValue);
    }

    /// <summary>
    /// Returns cos of a value.
    /// </summary>
    public static float Cos2(float fValue)
    {
        return Sin2(fValue + HALF_PI);
    }

    public static float Cos(float v)
    {
        return (float)JMath.Cos(v);
    }

    public static float Sin(float v)
    {
        return (float)JMath.Sin(v);
    }

    /// <summary>
    /// Returns E^fValue
    /// </summary>
    public static float Exp(float fValue)
    {
        return (float)JMath.Exp(fValue);
    }

    /// <summary>
    /// Returns Absolute value of a float.
    /// </summary>
    public static float Abs(float fValue)
    {
        if (fValue < 0)
        {
            return -fValue;
        }
        return fValue;
    }

    /// <summary>
    /// Returns a number rounded down.
    /// </summary>
    public static float Floor(float fValue)
    {
        return (float)JMath.Floor(fValue);
    }

    /// <summary>
    /// Returns 1/sqrt(fValue)
    /// </summary>
    public static float InvSqrt(float fValue)
    {
        return (float)(1.0f / JMath.Sqrt(fValue));
    }

    public static float FastInvSqrt(float x)
    {
        float xhalf = 0.5f * x;
        int i = BitConverter.SingleToInt32Bits(x); // get bits for floating value
        i = 0x5f375a86 - (i >> 1); // gives initial guess y0
        x = BitConverter.Int32BitsToSingle(i); // convert bits back to float
        x = x * (1.5f - xhalf * x * x); // Newton step, repeating increases accuracy
        return x;
    }

    /// <summary>
    /// Returns the log base E of a value.
    /// </summary>
    public static float Log(float fValue)
    {
        return (float)JMath.Log(fValue);
    }

    /// <summary>
    /// Returns the logarithm of value with given base, calculated as log(value)/log(base).
    /// </summary>
    public static float Log(float value, float baseValue)
    {
        return (float)(JMath.Log(value) / JMath.Log(baseValue));
    }

    /// <summary>
    /// Returns a number raised to an exponent power. fBase^fExponent
    /// </summary>
    public static float Pow(float fBase, float fExponent)
    {
        return (float)JMath.Pow(fBase, fExponent);
    }

    /// <summary>
    /// Returns the value squared. fValue ^ 2
    /// </summary>
    public static float Sqr(float fValue)
    {
        return fValue * fValue;
    }

    /// <summary>
    /// Returns the square root of a given value.
    /// </summary>
    public static float Sqrt(float fValue)
    {
        return (float)JMath.Sqrt(fValue);
    }

    /// <summary>
    /// Returns the tangent of a value.
    /// </summary>
    public static float Tan(float fValue)
    {
        return (float)JMath.Tan(fValue);
    }

    /// <summary>
    /// Returns 1 if the number is positive, -1 if the number is negative, and 0 otherwise
    /// </summary>
    public static int Sign(int iValue)
    {
        if (iValue > 0)
        {
            return 1;
        }
        if (iValue < 0)
        {
            return -1;
        }
        return 0;
    }

    /// <summary>
    /// Returns 1 if the number is positive, -1 if the number is negative, and 0 otherwise.
    /// Java parity: Math.signum (preserves signed zero and NaN).
    /// </summary>
    public static float Sign(float fValue)
    {
        if (float.IsNaN(fValue))
            return fValue;
        return fValue == 0f ? fValue : (fValue > 0f ? 1f : -1f);
    }

    /// <summary>
    /// Given 3 points in a 2d plane, computes if the points going from A-B-C are moving counter clock wise.
    /// </summary>
    public static int CounterClockwise(Vector2f p0, Vector2f p1, Vector2f p2)
    {
        float dx1, dx2, dy1, dy2;
        dx1 = p1.X - p0.X;
        dy1 = p1.Y - p0.Y;
        dx2 = p2.X - p0.X;
        dy2 = p2.Y - p0.Y;
        if (dx1 * dy2 > dy1 * dx2)
        {
            return 1;
        }
        if (dx1 * dy2 < dy1 * dx2)
        {
            return -1;
        }
        if ((dx1 * dx2 < 0) || (dy1 * dy2 < 0))
        {
            return -1;
        }
        if ((dx1 * dx1 + dy1 * dy1) < (dx2 * dx2 + dy2 * dy2))
        {
            return 1;
        }
        return 0;
    }

    /// <summary>
    /// Test if a point is inside a triangle.
    /// </summary>
    public static int PointInsideTriangle(Vector2f t0, Vector2f t1, Vector2f t2, Vector2f p)
    {
        int val1 = CounterClockwise(t0, t1, p);
        if (val1 == 0)
        {
            return 1;
        }
        int val2 = CounterClockwise(t1, t2, p);
        if (val2 == 0)
        {
            return 1;
        }
        if (val2 != val1)
        {
            return 0;
        }
        int val3 = CounterClockwise(t2, t0, p);
        if (val3 == 0)
        {
            return 1;
        }
        if (val3 != val1)
        {
            return 0;
        }
        return val3;
    }

    /// <summary>
    /// Returns the determinant of a 4x4 matrix.
    /// </summary>
    public static float Determinant(double m00, double m01, double m02,
        double m03, double m10, double m11, double m12, double m13,
        double m20, double m21, double m22, double m23, double m30,
        double m31, double m32, double m33)
    {
        double det01 = m20 * m31 - m21 * m30;
        double det02 = m20 * m32 - m22 * m30;
        double det03 = m20 * m33 - m23 * m30;
        double det12 = m21 * m32 - m22 * m31;
        double det13 = m21 * m33 - m23 * m31;
        double det23 = m22 * m33 - m23 * m32;
        return (float)(m00 * (m11 * det23 - m12 * det13 + m13 * det12) - m01
            * (m10 * det23 - m12 * det03 + m13 * det02) + m02
            * (m10 * det13 - m11 * det03 + m13 * det01) - m03
            * (m10 * det12 - m11 * det02 + m12 * det01));
    }

    /// <summary>
    /// Returns a random float between 0 and 1.
    /// </summary>
    public static float NextRandomFloat()
    {
        return rand.NextSingle();
    }

    /// <summary>
    /// Returns a random int between min and max.
    /// </summary>
    public static int NextRandomInt(int min, int max)
    {
        return (int)(NextRandomFloat() * (max - min + 1)) + min;
    }

    public static int NextRandomInt()
    {
        return (int)rand.NextInt64(int.MinValue, (long)int.MaxValue + 1L);
    }

    /// <summary>
    /// Converts a point from Spherical coordinates to Cartesian (using positive Y as up).
    /// </summary>
    public static Vector3f SphericalToCartesian(Vector3f sphereCoords, Vector3f store)
    {
        store.Y = sphereCoords.X * FastMath.Sin(sphereCoords.Z);
        float a = sphereCoords.X * FastMath.Cos(sphereCoords.Z);
        store.X = a * FastMath.Cos(sphereCoords.Y);
        store.Z = a * FastMath.Sin(sphereCoords.Y);

        return store;
    }

    /// <summary>
    /// Converts a point from Cartesian coordinates (using positive Y as up) to Spherical.
    /// </summary>
    public static Vector3f CartesianToSpherical(Vector3f cartCoords, Vector3f store)
    {
        if (cartCoords.X == 0)
        {
            cartCoords.X = FastMath.FLT_EPSILON;
        }
        store.X = FastMath.Sqrt((cartCoords.X * cartCoords.X)
            + (cartCoords.Y * cartCoords.Y)
            + (cartCoords.Z * cartCoords.Z));
        store.Y = FastMath.Atan(cartCoords.Z / cartCoords.X);
        if (cartCoords.X < 0)
        {
            store.Y += FastMath.PI;
        }
        store.Z = FastMath.Asin(cartCoords.Y / store.X);
        return store;
    }

    /// <summary>
    /// Converts a point from Spherical coordinates to Cartesian (using positive Z as up).
    /// </summary>
    public static Vector3f SphericalToCartesianZ(Vector3f sphereCoords, Vector3f store)
    {
        store.Z = sphereCoords.X * FastMath.Sin(sphereCoords.Z);
        float a = sphereCoords.X * FastMath.Cos(sphereCoords.Z);
        store.X = a * FastMath.Cos(sphereCoords.Y);
        store.Y = a * FastMath.Sin(sphereCoords.Y);

        return store;
    }

    /// <summary>
    /// Converts a point from Cartesian coordinates (using positive Z as up) to Spherical.
    /// </summary>
    public static Vector3f CartesianZToSpherical(Vector3f cartCoords, Vector3f store)
    {
        if (cartCoords.X == 0)
        {
            cartCoords.X = FastMath.FLT_EPSILON;
        }
        store.X = FastMath.Sqrt((cartCoords.X * cartCoords.X)
            + (cartCoords.Y * cartCoords.Y)
            + (cartCoords.Z * cartCoords.Z));
        store.Z = FastMath.Atan(cartCoords.Z / cartCoords.X);
        if (cartCoords.X < 0)
        {
            store.Z += FastMath.PI;
        }
        store.Y = FastMath.Asin(cartCoords.Y / store.X);
        return store;
    }

    /// <summary>
    /// Takes a value and expresses it in terms of min to max.
    /// </summary>
    public static float Normalize(float val, float min, float max)
    {
        if (float.IsInfinity(val) || float.IsNaN(val))
        {
            return 0f;
        }
        float range = max - min;
        while (val > max)
        {
            val -= range;
        }
        while (val < min)
        {
            val += range;
        }
        return val;
    }

    /// <summary>
    /// Returns x with its sign changed to match the sign of y.
    /// </summary>
    public static float Copysign(float x, float y)
    {
        if (y >= 0 && x <= -0)
        {
            return -x;
        }
        else if (y < 0 && x >= 0)
        {
            return -x;
        }
        else
        {
            return x;
        }
    }

    /// <summary>
    /// Take a float input and clamp it between min and max.
    /// </summary>
    public static float Clamp(float input, float min, float max)
    {
        return (input < min) ? min : (input > max) ? max : input;
    }

    /// <summary>
    /// Clamps the given float to be between 0 and 1.
    /// </summary>
    public static float Saturate(float input)
    {
        return Clamp(input, 0f, 1f);
    }

    /// <summary>
    /// Converts a single precision (32 bit) floating point value into half precision (16 bit).
    /// </summary>
    public static float ConvertHalfToFloat(short half)
    {
        switch (half)
        {
            case 0x0000:
                return 0f;
            case unchecked((short)0x8000):
                return -0f;
            case 0x7c00:
                return float.PositiveInfinity;
            case unchecked((short)0xfc00):
                return float.NegativeInfinity;
            // TODO: Support for NaN?
            default:
                return BitConverter.Int32BitsToSingle(((half & 0x8000) << 16)
                    | (((half & 0x7c00) + 0x1C000) << 13)
                    | ((half & 0x03FF) << 13));
        }
    }

    public static short ConvertFloatToHalf(float flt)
    {
        if (float.IsNaN(flt))
        {
            // Java parity: geoEngine/math/FastMath.java::convertFloatToHalf throws UnsupportedOperationException
            throw new NotSupportedException("NaN to half conversion not supported!");
        }
        else if (flt == float.PositiveInfinity)
        {
            return 0x7c00;
        }
        else if (flt == float.NegativeInfinity)
        {
            return unchecked((short)0xfc00);
        }
        else if (flt == 0f)
        {
            return 0x0000;
        }
        else if (flt == -0f)
        {
            return unchecked((short)0x8000);
        }
        else if (flt > 65504f)
        {
            // max value supported by half float
            return 0x7bff;
        }
        else if (flt < -65504f)
        {
            return unchecked((short)(0x7bff | 0x8000));
        }
        else if (flt > 0f && flt < 5.96046E-8f)
        {
            return 0x0001;
        }
        else if (flt < 0f && flt > -5.96046E-8f)
        {
            return unchecked((short)0x8001);
        }

        int f = BitConverter.SingleToInt32Bits(flt);
        return (short)(((f >> 16) & 0x8000)
            | ((((f & 0x7f800000) - 0x38000000) >> 13) & 0x7c00)
            | ((f >> 13) & 0x03ff));
    }
}
