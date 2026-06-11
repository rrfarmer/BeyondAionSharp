using System;
using Aion.GameServer.Commons.Nio;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using JMath = System.Math;

namespace Aion.GameServer.GeoEngine.Math;

/// <summary>
/// Java parity: geoEngine/math/Matrix4f (jMonkeyEngine). 4x4 single-precision matrix. Public mXX fields; Vector3f.x/.getX() -> .X;
/// FastMath.cos/sin/abs -> Cos/Sin/Abs; FloatBuffer from Commons.Nio; Float.floatToIntBits -> BitConverter.SingleToInt32Bits;
/// Float.compare -> CompareTo; Cloneable.clone -> Clone() copy. Pure math, all deps present.
/// </summary>
public sealed class Matrix4f
{
    private static readonly ILogger logger = NullLoggerFactory.Instance.CreateLogger(nameof(Matrix4f));

    public float m00, m01, m02, m03;
    public float m10, m11, m12, m13;
    public float m20, m21, m22, m23;
    public float m30, m31, m32, m33;

    public static readonly Matrix4f IDENTITY = new Matrix4f();

    /// <summary>Constructs a new Matrix set to the identity matrix.</summary>
    public Matrix4f()
    {
        LoadIdentity();
    }

    /// <summary>constructs a matrix with the given values.</summary>
    public Matrix4f(float m00, float m01, float m02, float m03,
            float m10, float m11, float m12, float m13,
            float m20, float m21, float m22, float m23,
            float m30, float m31, float m32, float m33)
    {
        this.m00 = m00;
        this.m01 = m01;
        this.m02 = m02;
        this.m03 = m03;
        this.m10 = m10;
        this.m11 = m11;
        this.m12 = m12;
        this.m13 = m13;
        this.m20 = m20;
        this.m21 = m21;
        this.m22 = m22;
        this.m23 = m23;
        this.m30 = m30;
        this.m31 = m31;
        this.m32 = m32;
        this.m33 = m33;
    }

    /// <summary>Create a new Matrix4f, given data in column-major format (translation in elements 12, 13 and 14).</summary>
    public Matrix4f(float[] array)
    {
        Set(array, false);
    }

    /// <summary>Copies a given Matrix. If the provided matrix is null, sets the matrix to the identity.</summary>
    public Matrix4f(Matrix4f mat)
    {
        Copy(mat);
    }

    /// <summary>Transfers the contents of a given matrix to this matrix. If null, this matrix is set to the identity matrix.</summary>
    public void Copy(Matrix4f matrix)
    {
        if (null == matrix)
        {
            LoadIdentity();
        }
        else
        {
            m00 = matrix.m00;
            m01 = matrix.m01;
            m02 = matrix.m02;
            m03 = matrix.m03;
            m10 = matrix.m10;
            m11 = matrix.m11;
            m12 = matrix.m12;
            m13 = matrix.m13;
            m20 = matrix.m20;
            m21 = matrix.m21;
            m22 = matrix.m22;
            m23 = matrix.m23;
            m30 = matrix.m30;
            m31 = matrix.m31;
            m32 = matrix.m32;
            m33 = matrix.m33;
        }
    }

    /// <summary>Retrieves the values of this object into a float array in row-major order.</summary>
    public void Get(float[] matrix)
    {
        Get(matrix, true);
    }

    /// <summary>Retrieves the values of this object into a float array (row or column major).</summary>
    public void Get(float[] matrix, bool rowMajor)
    {
        if (matrix.Length != 16)
            throw new ArgumentException("Array must be of size 16.");

        if (rowMajor)
        {
            matrix[0] = m00;
            matrix[1] = m01;
            matrix[2] = m02;
            matrix[3] = m03;
            matrix[4] = m10;
            matrix[5] = m11;
            matrix[6] = m12;
            matrix[7] = m13;
            matrix[8] = m20;
            matrix[9] = m21;
            matrix[10] = m22;
            matrix[11] = m23;
            matrix[12] = m30;
            matrix[13] = m31;
            matrix[14] = m32;
            matrix[15] = m33;
        }
        else
        {
            matrix[0] = m00;
            matrix[4] = m01;
            matrix[8] = m02;
            matrix[12] = m03;
            matrix[1] = m10;
            matrix[5] = m11;
            matrix[9] = m12;
            matrix[13] = m13;
            matrix[2] = m20;
            matrix[6] = m21;
            matrix[10] = m22;
            matrix[14] = m23;
            matrix[3] = m30;
            matrix[7] = m31;
            matrix[11] = m32;
            matrix[15] = m33;
        }
    }

    /// <summary>Retrieves a value from the matrix at the given position (row i, column j).</summary>
    public float Get(int i, int j)
    {
        switch (i)
        {
            case 0:
                switch (j)
                {
                    case 0: return m00;
                    case 1: return m01;
                    case 2: return m02;
                    case 3: return m03;
                }
                break;
            case 1:
                switch (j)
                {
                    case 0: return m10;
                    case 1: return m11;
                    case 2: return m12;
                    case 3: return m13;
                }
                break;
            case 2:
                switch (j)
                {
                    case 0: return m20;
                    case 1: return m21;
                    case 2: return m22;
                    case 3: return m23;
                }
                break;
            case 3:
                switch (j)
                {
                    case 0: return m30;
                    case 1: return m31;
                    case 2: return m32;
                    case 3: return m33;
                }
                break;
        }

        logger.LogWarning("Invalid matrix index.");
        throw new ArgumentException("Invalid indices into matrix.");
    }

    /// <summary>Returns one of four columns specified by the parameter, as a float array of length 4.</summary>
    public float[] GetColumn(int i)
    {
        return GetColumn(i, null);
    }

    /// <summary>Returns one of four columns specified by the parameter, as a float[4].</summary>
    public float[] GetColumn(int i, float[] store)
    {
        if (store == null) store = new float[4];
        switch (i)
        {
            case 0:
                store[0] = m00;
                store[1] = m10;
                store[2] = m20;
                store[3] = m30;
                break;
            case 1:
                store[0] = m01;
                store[1] = m11;
                store[2] = m21;
                store[3] = m31;
                break;
            case 2:
                store[0] = m02;
                store[1] = m12;
                store[2] = m22;
                store[3] = m32;
                break;
            case 3:
                store[0] = m03;
                store[1] = m13;
                store[2] = m23;
                store[3] = m33;
                break;
            default:
                logger.LogWarning("Invalid column index.");
                throw new ArgumentException("Invalid column index. " + i);
        }
        return store;
    }

    /// <summary>Sets a particular column of this matrix to that represented by the provided vector.</summary>
    public void SetColumn(int i, float[] column)
    {
        if (column == null)
        {
            logger.LogWarning("Column is null. Ignoring.");
            return;
        }
        switch (i)
        {
            case 0:
                m00 = column[0];
                m10 = column[1];
                m20 = column[2];
                m30 = column[3];
                break;
            case 1:
                m01 = column[0];
                m11 = column[1];
                m21 = column[2];
                m31 = column[3];
                break;
            case 2:
                m02 = column[0];
                m12 = column[1];
                m22 = column[2];
                m32 = column[3];
                break;
            case 3:
                m03 = column[0];
                m13 = column[1];
                m23 = column[2];
                m33 = column[3];
                break;
            default:
                logger.LogWarning("Invalid column index.");
                throw new ArgumentException("Invalid column index. " + i);
        }
    }

    /// <summary>Places a given value into the matrix at the given position (row i, column j).</summary>
    public void Set(int i, int j, float value)
    {
        switch (i)
        {
            case 0:
                switch (j)
                {
                    case 0: m00 = value; return;
                    case 1: m01 = value; return;
                    case 2: m02 = value; return;
                    case 3: m03 = value; return;
                }
                break;
            case 1:
                switch (j)
                {
                    case 0: m10 = value; return;
                    case 1: m11 = value; return;
                    case 2: m12 = value; return;
                    case 3: m13 = value; return;
                }
                break;
            case 2:
                switch (j)
                {
                    case 0: m20 = value; return;
                    case 1: m21 = value; return;
                    case 2: m22 = value; return;
                    case 3: m23 = value; return;
                }
                break;
            case 3:
                switch (j)
                {
                    case 0: m30 = value; return;
                    case 1: m31 = value; return;
                    case 2: m32 = value; return;
                    case 3: m33 = value; return;
                }
                break;
        }

        logger.LogWarning("Invalid matrix index.");
        throw new ArgumentException("Invalid indices into matrix.");
    }

    /// <summary>Sets the values of this matrix from a 4x4 array.</summary>
    public void Set(float[][] matrix)
    {
        if (matrix.Length != 4 || matrix[0].Length != 4)
        {
            throw new ArgumentException("Array must be of size 16.");
        }

        m00 = matrix[0][0];
        m01 = matrix[0][1];
        m02 = matrix[0][2];
        m03 = matrix[0][3];
        m10 = matrix[1][0];
        m11 = matrix[1][1];
        m12 = matrix[1][2];
        m13 = matrix[1][3];
        m20 = matrix[2][0];
        m21 = matrix[2][1];
        m22 = matrix[2][2];
        m23 = matrix[2][3];
        m30 = matrix[3][0];
        m31 = matrix[3][1];
        m32 = matrix[3][2];
        m33 = matrix[3][3];
    }

    /// <summary>Sets the values of this matrix from another matrix.</summary>
    public Matrix4f Set(Matrix4f matrix)
    {
        m00 = matrix.m00; m01 = matrix.m01; m02 = matrix.m02; m03 = matrix.m03;
        m10 = matrix.m10; m11 = matrix.m11; m12 = matrix.m12; m13 = matrix.m13;
        m20 = matrix.m20; m21 = matrix.m21; m22 = matrix.m22; m23 = matrix.m23;
        m30 = matrix.m30; m31 = matrix.m31; m32 = matrix.m32; m33 = matrix.m33;
        return this;
    }

    /// <summary>Sets the values of this matrix from a row-major array of 16 values.</summary>
    public void Set(float[] matrix)
    {
        Set(matrix, true);
    }

    /// <summary>Sets the values of this matrix from an array of 16 values (row or column major).</summary>
    public void Set(float[] matrix, bool rowMajor)
    {
        if (matrix.Length != 16)
            throw new ArgumentException("Array must be of size 16.");

        if (rowMajor)
        {
            m00 = matrix[0];
            m01 = matrix[1];
            m02 = matrix[2];
            m03 = matrix[3];
            m10 = matrix[4];
            m11 = matrix[5];
            m12 = matrix[6];
            m13 = matrix[7];
            m20 = matrix[8];
            m21 = matrix[9];
            m22 = matrix[10];
            m23 = matrix[11];
            m30 = matrix[12];
            m31 = matrix[13];
            m32 = matrix[14];
            m33 = matrix[15];
        }
        else
        {
            m00 = matrix[0];
            m01 = matrix[4];
            m02 = matrix[8];
            m03 = matrix[12];
            m10 = matrix[1];
            m11 = matrix[5];
            m12 = matrix[9];
            m13 = matrix[13];
            m20 = matrix[2];
            m21 = matrix[6];
            m22 = matrix[10];
            m23 = matrix[14];
            m30 = matrix[3];
            m31 = matrix[7];
            m32 = matrix[11];
            m33 = matrix[15];
        }
    }

    public Matrix4f Transpose()
    {
        float[] tmp = new float[16];
        Get(tmp, true);
        Matrix4f mat = new Matrix4f(tmp);
        return mat;
    }

    /// <summary>Locally transposes this Matrix.</summary>
    public Matrix4f TransposeLocal()
    {
        float tmp = m01;
        m01 = m10;
        m10 = tmp;

        tmp = m02;
        m02 = m20;
        m20 = tmp;

        tmp = m03;
        m03 = m30;
        m30 = tmp;

        tmp = m12;
        m12 = m21;
        m21 = tmp;

        tmp = m13;
        m13 = m31;
        m31 = tmp;

        tmp = m23;
        m23 = m32;
        m32 = tmp;

        return this;
    }

    /// <summary>Fills a FloatBuffer object with the matrix data.</summary>
    public FloatBuffer FillFloatBuffer(FloatBuffer fb)
    {
        return FillFloatBuffer(fb, false);
    }

    /// <summary>Fills a FloatBuffer object with the matrix data (column- or row-major), advancing its position by 16.</summary>
    public FloatBuffer FillFloatBuffer(FloatBuffer fb, bool columnMajor)
    {
        if (columnMajor)
        {
            fb.Put(m00).Put(m10).Put(m20).Put(m30);
            fb.Put(m01).Put(m11).Put(m21).Put(m31);
            fb.Put(m02).Put(m12).Put(m22).Put(m32);
            fb.Put(m03).Put(m13).Put(m23).Put(m33);
        }
        else
        {
            fb.Put(m00).Put(m01).Put(m02).Put(m03);
            fb.Put(m10).Put(m11).Put(m12).Put(m13);
            fb.Put(m20).Put(m21).Put(m22).Put(m23);
            fb.Put(m30).Put(m31).Put(m32).Put(m33);
        }
        return fb;
    }

    public void FillFloatArray(float[] f, bool columnMajor)
    {
        if (columnMajor)
        {
            f[0] = m00; f[1] = m10; f[2] = m20; f[3] = m30;
            f[4] = m01; f[5] = m11; f[6] = m21; f[7] = m31;
            f[8] = m02; f[9] = m12; f[10] = m22; f[11] = m32;
            f[12] = m03; f[13] = m13; f[14] = m23; f[15] = m33;
        }
        else
        {
            f[0] = m00; f[1] = m01; f[2] = m02; f[3] = m03;
            f[4] = m10; f[5] = m11; f[6] = m12; f[7] = m13;
            f[8] = m20; f[9] = m21; f[10] = m22; f[11] = m23;
            f[12] = m30; f[13] = m31; f[14] = m32; f[15] = m33;
        }
    }

    /// <summary>Reads value for this matrix from a FloatBuffer.</summary>
    public Matrix4f ReadFloatBuffer(FloatBuffer fb)
    {
        return ReadFloatBuffer(fb, false);
    }

    /// <summary>Reads value for this matrix from a FloatBuffer (column- or row-major).</summary>
    public Matrix4f ReadFloatBuffer(FloatBuffer fb, bool columnMajor)
    {
        if (columnMajor)
        {
            m00 = fb.Get(); m10 = fb.Get(); m20 = fb.Get(); m30 = fb.Get();
            m01 = fb.Get(); m11 = fb.Get(); m21 = fb.Get(); m31 = fb.Get();
            m02 = fb.Get(); m12 = fb.Get(); m22 = fb.Get(); m32 = fb.Get();
            m03 = fb.Get(); m13 = fb.Get(); m23 = fb.Get(); m33 = fb.Get();
        }
        else
        {
            m00 = fb.Get(); m01 = fb.Get(); m02 = fb.Get(); m03 = fb.Get();
            m10 = fb.Get(); m11 = fb.Get(); m12 = fb.Get(); m13 = fb.Get();
            m20 = fb.Get(); m21 = fb.Get(); m22 = fb.Get(); m23 = fb.Get();
            m30 = fb.Get(); m31 = fb.Get(); m32 = fb.Get(); m33 = fb.Get();
        }
        return this;
    }

    /// <summary>Sets this matrix to the identity matrix (all zeros with ones along the diagonal).</summary>
    public void LoadIdentity()
    {
        m01 = m02 = m03 = 0.0f;
        m10 = m12 = m13 = 0.0f;
        m20 = m21 = m23 = 0.0f;
        m30 = m31 = m32 = 0.0f;
        m00 = m11 = m22 = m33 = 1.0f;
    }

    public void FromFrustum(float near, float far, float left, float right, float top, float bottom, bool parallel)
    {
        LoadIdentity();
        if (parallel)
        {
            // scale
            m00 = 2.0f / (right - left);
            //m11 = 2.0f / (bottom - top);
            m11 = 2.0f / (top - bottom);
            m22 = -2.0f / (far - near);
            m33 = 1f;

            // translation
            m03 = -(right + left) / (right - left);
            //m31 = -(bottom + top) / (bottom - top);
            m13 = -(top + bottom) / (top - bottom);
            m23 = -(far + near) / (far - near);
        }
        else
        {
            m00 = (2.0f * near) / (right - left);
            m11 = (2.0f * near) / (top - bottom);
            m32 = -1.0f;
            m33 = -0.0f;

            // A
            m02 = (right + left) / (right - left);

            // B
            m12 = (top + bottom) / (top - bottom);

            // C
            m22 = -(far + near) / (far - near);

            // D
            m23 = -(2.0f * far * near) / (far - near);
        }
    }

    /// <summary>Sets this matrix4f to the values specified by an angle and an axis of rotation (creates an object).</summary>
    public void FromAngleAxis(float angle, Vector3f axis)
    {
        Vector3f normAxis = axis.Normalize();
        FromAngleNormalAxis(angle, normAxis);
    }

    /// <summary>Sets this matrix4f to the values specified by an angle and a normalized axis of rotation.</summary>
    public void FromAngleNormalAxis(float angle, Vector3f axis)
    {
        Zero();
        m33 = 1;

        float fCos = FastMath.Cos(angle);
        float fSin = FastMath.Sin(angle);
        float fOneMinusCos = ((float)1.0) - fCos;
        float fX2 = axis.X * axis.X;
        float fY2 = axis.Y * axis.Y;
        float fZ2 = axis.Z * axis.Z;
        float fXYM = axis.X * axis.Y * fOneMinusCos;
        float fXZM = axis.X * axis.Z * fOneMinusCos;
        float fYZM = axis.Y * axis.Z * fOneMinusCos;
        float fXSin = axis.X * fSin;
        float fYSin = axis.Y * fSin;
        float fZSin = axis.Z * fSin;

        m00 = fX2 * fOneMinusCos + fCos;
        m01 = fXYM - fZSin;
        m02 = fXZM + fYSin;
        m10 = fXYM + fZSin;
        m11 = fY2 * fOneMinusCos + fCos;
        m12 = fYZM - fXSin;
        m20 = fXZM - fYSin;
        m21 = fYZM + fXSin;
        m22 = fZ2 * fOneMinusCos + fCos;
    }

    /// <summary>Multiplies this matrix by a scalar.</summary>
    public void MultLocal(float scalar)
    {
        m00 *= scalar;
        m01 *= scalar;
        m02 *= scalar;
        m03 *= scalar;
        m10 *= scalar;
        m11 *= scalar;
        m12 *= scalar;
        m13 *= scalar;
        m20 *= scalar;
        m21 *= scalar;
        m22 *= scalar;
        m23 *= scalar;
        m30 *= scalar;
        m31 *= scalar;
        m32 *= scalar;
        m33 *= scalar;
    }

    public Matrix4f Mult(float scalar)
    {
        Matrix4f outm = new Matrix4f();
        outm.Set(this);
        outm.MultLocal(scalar);
        return outm;
    }

    public Matrix4f Mult(float scalar, Matrix4f store)
    {
        store.Set(this);
        store.MultLocal(scalar);
        return store;
    }

    /// <summary>Multiplies this matrix with another matrix (this on the left, parameter on the right).</summary>
    public Matrix4f Mult(Matrix4f in2)
    {
        return Mult(in2, null);
    }

    /// <summary>Multiplies this matrix with another matrix. Safe for in2 and store to be the same object.</summary>
    public Matrix4f Mult(Matrix4f in2, Matrix4f store)
    {
        if (store == null) store = new Matrix4f();

        float temp00, temp01, temp02, temp03;
        float temp10, temp11, temp12, temp13;
        float temp20, temp21, temp22, temp23;
        float temp30, temp31, temp32, temp33;

        temp00 = m00 * in2.m00 +
                m01 * in2.m10 +
                m02 * in2.m20 +
                m03 * in2.m30;
        temp01 = m00 * in2.m01 +
                m01 * in2.m11 +
                m02 * in2.m21 +
                m03 * in2.m31;
        temp02 = m00 * in2.m02 +
                m01 * in2.m12 +
                m02 * in2.m22 +
                m03 * in2.m32;
        temp03 = m00 * in2.m03 +
                m01 * in2.m13 +
                m02 * in2.m23 +
                m03 * in2.m33;

        temp10 = m10 * in2.m00 +
                m11 * in2.m10 +
                m12 * in2.m20 +
                m13 * in2.m30;
        temp11 = m10 * in2.m01 +
                m11 * in2.m11 +
                m12 * in2.m21 +
                m13 * in2.m31;
        temp12 = m10 * in2.m02 +
                m11 * in2.m12 +
                m12 * in2.m22 +
                m13 * in2.m32;
        temp13 = m10 * in2.m03 +
                m11 * in2.m13 +
                m12 * in2.m23 +
                m13 * in2.m33;

        temp20 = m20 * in2.m00 +
                m21 * in2.m10 +
                m22 * in2.m20 +
                m23 * in2.m30;
        temp21 = m20 * in2.m01 +
                m21 * in2.m11 +
                m22 * in2.m21 +
                m23 * in2.m31;
        temp22 = m20 * in2.m02 +
                m21 * in2.m12 +
                m22 * in2.m22 +
                m23 * in2.m32;
        temp23 = m20 * in2.m03 +
                m21 * in2.m13 +
                m22 * in2.m23 +
                m23 * in2.m33;

        temp30 = m30 * in2.m00 +
                m31 * in2.m10 +
                m32 * in2.m20 +
                m33 * in2.m30;
        temp31 = m30 * in2.m01 +
                m31 * in2.m11 +
                m32 * in2.m21 +
                m33 * in2.m31;
        temp32 = m30 * in2.m02 +
                m31 * in2.m12 +
                m32 * in2.m22 +
                m33 * in2.m32;
        temp33 = m30 * in2.m03 +
                m31 * in2.m13 +
                m32 * in2.m23 +
                m33 * in2.m33;

        store.m00 = temp00; store.m01 = temp01; store.m02 = temp02; store.m03 = temp03;
        store.m10 = temp10; store.m11 = temp11; store.m12 = temp12; store.m13 = temp13;
        store.m20 = temp20; store.m21 = temp21; store.m22 = temp22; store.m23 = temp23;
        store.m30 = temp30; store.m31 = temp31; store.m32 = temp32; store.m33 = temp33;

        return store;
    }

    /// <summary>Multiplies this matrix with another matrix, storing internally and returning this.</summary>
    public Matrix4f MultLocal(Matrix4f in2)
    {
        return Mult(in2, this);
    }

    /// <summary>Multiplies a vector about a rotation matrix. The resulting vector is returned as a new Vector3f.</summary>
    public Vector3f Mult(Vector3f vec)
    {
        return Mult(vec, null);
    }

    /// <summary>Multiplies a vector about a rotation matrix and adds translation.</summary>
    public Vector3f Mult(Vector3f vec, Vector3f store)
    {
        if (store == null) store = new Vector3f();

        float vx = vec.X, vy = vec.Y, vz = vec.Z;
        store.X = m00 * vx + m01 * vy + m02 * vz + m03;
        store.Y = m10 * vx + m11 * vy + m12 * vz + m13;
        store.Z = m20 * vx + m21 * vy + m22 * vz + m23;

        return store;
    }

    /// <summary>Multiplies a vector about a rotation matrix, without adding translation.</summary>
    public Vector3f MultNormal(Vector3f vec, Vector3f store)
    {
        if (store == null) store = new Vector3f();

        float vx = vec.X, vy = vec.Y, vz = vec.Z;
        store.X = m00 * vx + m01 * vy + m02 * vz;
        store.Y = m10 * vx + m11 * vy + m12 * vz;
        store.Z = m20 * vx + m21 * vy + m22 * vz;

        return store;
    }

    /// <summary>Multiplies a vector about a rotation matrix (transposed), without adding translation.</summary>
    public Vector3f MultNormalAcross(Vector3f vec, Vector3f store)
    {
        if (store == null) store = new Vector3f();

        float vx = vec.X, vy = vec.Y, vz = vec.Z;
        store.X = m00 * vx + m10 * vy + m20 * vz;
        store.Y = m01 * vx + m11 * vy + m21 * vz;
        store.Z = m02 * vx + m12 * vy + m22 * vz;

        return store;
    }

    /// <summary>Multiplies a vector about a rotation matrix and adds translation, returning the W value.</summary>
    public float MultProj(Vector3f vec, Vector3f store)
    {
        float vx = vec.X, vy = vec.Y, vz = vec.Z;
        store.X = m00 * vx + m01 * vy + m02 * vz + m03;
        store.Y = m10 * vx + m11 * vy + m12 * vz + m13;
        store.Z = m20 * vx + m21 * vy + m22 * vz + m23;
        return m30 * vx + m31 * vy + m32 * vz + m33;
    }

    /// <summary>Multiplies a vector about a rotation matrix (transposed). The resulting vector is returned.</summary>
    public Vector3f MultAcross(Vector3f vec, Vector3f store)
    {
        if (null == vec)
        {
            logger.LogInformation("Source vector is null, null result returned.");
            return null;
        }
        if (store == null) store = new Vector3f();

        float vx = vec.X, vy = vec.Y, vz = vec.Z;
        store.X = m00 * vx + m10 * vy + m20 * vz + m30 * 1;
        store.Y = m01 * vx + m11 * vy + m21 * vz + m31 * 1;
        store.Z = m02 * vx + m12 * vy + m22 * vz + m32 * 1;

        return store;
    }

    /// <summary>Multiplies an array of 4 floats against this rotation matrix (vec4f x mat4f), stored directly in the array.</summary>
    public float[] Mult(float[] vec4f)
    {
        if (null == vec4f || vec4f.Length != 4)
        {
            logger.LogWarning("invalid array given, must be nonnull and length 4");
            return null;
        }

        float x = vec4f[0], y = vec4f[1], z = vec4f[2], w = vec4f[3];

        vec4f[0] = m00 * x + m01 * y + m02 * z + m03 * w;
        vec4f[1] = m10 * x + m11 * y + m12 * z + m13 * w;
        vec4f[2] = m20 * x + m21 * y + m22 * z + m23 * w;
        vec4f[3] = m30 * x + m31 * y + m32 * z + m33 * w;

        return vec4f;
    }

    /// <summary>Multiplies an array of 4 floats against this rotation matrix (transposed), stored directly in the array.</summary>
    public float[] MultAcross(float[] vec4f)
    {
        if (null == vec4f || vec4f.Length != 4)
        {
            logger.LogWarning("invalid array given, must be nonnull and length 4");
            return null;
        }

        float x = vec4f[0], y = vec4f[1], z = vec4f[2], w = vec4f[3];

        vec4f[0] = m00 * x + m10 * y + m20 * z + m30 * w;
        vec4f[1] = m01 * x + m11 * y + m21 * z + m31 * w;
        vec4f[2] = m02 * x + m12 * y + m22 * z + m32 * w;
        vec4f[3] = m03 * x + m13 * y + m23 * z + m33 * w;

        return vec4f;
    }

    /// <summary>Inverts this matrix as a new Matrix4f.</summary>
    public Matrix4f Invert()
    {
        return Invert(null);
    }

    /// <summary>Inverts this matrix and stores it in the given store.</summary>
    public Matrix4f Invert(Matrix4f store)
    {
        if (store == null) store = new Matrix4f();

        float fA0 = m00 * m11 - m01 * m10;
        float fA1 = m00 * m12 - m02 * m10;
        float fA2 = m00 * m13 - m03 * m10;
        float fA3 = m01 * m12 - m02 * m11;
        float fA4 = m01 * m13 - m03 * m11;
        float fA5 = m02 * m13 - m03 * m12;
        float fB0 = m20 * m31 - m21 * m30;
        float fB1 = m20 * m32 - m22 * m30;
        float fB2 = m20 * m33 - m23 * m30;
        float fB3 = m21 * m32 - m22 * m31;
        float fB4 = m21 * m33 - m23 * m31;
        float fB5 = m22 * m33 - m23 * m32;
        float fDet = fA0 * fB5 - fA1 * fB4 + fA2 * fB3 + fA3 * fB2 - fA4 * fB1 + fA5 * fB0;

        if (FastMath.Abs(fDet) <= 0f)
            throw new ArithmeticException("This matrix cannot be inverted");

        store.m00 = +m11 * fB5 - m12 * fB4 + m13 * fB3;
        store.m10 = -m10 * fB5 + m12 * fB2 - m13 * fB1;
        store.m20 = +m10 * fB4 - m11 * fB2 + m13 * fB0;
        store.m30 = -m10 * fB3 + m11 * fB1 - m12 * fB0;
        store.m01 = -m01 * fB5 + m02 * fB4 - m03 * fB3;
        store.m11 = +m00 * fB5 - m02 * fB2 + m03 * fB1;
        store.m21 = -m00 * fB4 + m01 * fB2 - m03 * fB0;
        store.m31 = +m00 * fB3 - m01 * fB1 + m02 * fB0;
        store.m02 = +m31 * fA5 - m32 * fA4 + m33 * fA3;
        store.m12 = -m30 * fA5 + m32 * fA2 - m33 * fA1;
        store.m22 = +m30 * fA4 - m31 * fA2 + m33 * fA0;
        store.m32 = -m30 * fA3 + m31 * fA1 - m32 * fA0;
        store.m03 = -m21 * fA5 + m22 * fA4 - m23 * fA3;
        store.m13 = +m20 * fA5 - m22 * fA2 + m23 * fA1;
        store.m23 = -m20 * fA4 + m21 * fA2 - m23 * fA0;
        store.m33 = +m20 * fA3 - m21 * fA1 + m22 * fA0;

        float fInvDet = 1.0f / fDet;
        store.MultLocal(fInvDet);

        return store;
    }

    /// <summary>Inverts this matrix locally.</summary>
    public Matrix4f InvertLocal()
    {
        float fA0 = m00 * m11 - m01 * m10;
        float fA1 = m00 * m12 - m02 * m10;
        float fA2 = m00 * m13 - m03 * m10;
        float fA3 = m01 * m12 - m02 * m11;
        float fA4 = m01 * m13 - m03 * m11;
        float fA5 = m02 * m13 - m03 * m12;
        float fB0 = m20 * m31 - m21 * m30;
        float fB1 = m20 * m32 - m22 * m30;
        float fB2 = m20 * m33 - m23 * m30;
        float fB3 = m21 * m32 - m22 * m31;
        float fB4 = m21 * m33 - m23 * m31;
        float fB5 = m22 * m33 - m23 * m32;
        float fDet = fA0 * fB5 - fA1 * fB4 + fA2 * fB3 + fA3 * fB2 - fA4 * fB1 + fA5 * fB0;

        if (FastMath.Abs(fDet) <= 0f)
            return Zero();

        float f00 = +m11 * fB5 - m12 * fB4 + m13 * fB3;
        float f10 = -m10 * fB5 + m12 * fB2 - m13 * fB1;
        float f20 = +m10 * fB4 - m11 * fB2 + m13 * fB0;
        float f30 = -m10 * fB3 + m11 * fB1 - m12 * fB0;
        float f01 = -m01 * fB5 + m02 * fB4 - m03 * fB3;
        float f11 = +m00 * fB5 - m02 * fB2 + m03 * fB1;
        float f21 = -m00 * fB4 + m01 * fB2 - m03 * fB0;
        float f31 = +m00 * fB3 - m01 * fB1 + m02 * fB0;
        float f02 = +m31 * fA5 - m32 * fA4 + m33 * fA3;
        float f12 = -m30 * fA5 + m32 * fA2 - m33 * fA1;
        float f22 = +m30 * fA4 - m31 * fA2 + m33 * fA0;
        float f32 = -m30 * fA3 + m31 * fA1 - m32 * fA0;
        float f03 = -m21 * fA5 + m22 * fA4 - m23 * fA3;
        float f13 = +m20 * fA5 - m22 * fA2 + m23 * fA1;
        float f23 = -m20 * fA4 + m21 * fA2 - m23 * fA0;
        float f33 = +m20 * fA3 - m21 * fA1 + m22 * fA0;

        m00 = f00;
        m01 = f01;
        m02 = f02;
        m03 = f03;
        m10 = f10;
        m11 = f11;
        m12 = f12;
        m13 = f13;
        m20 = f20;
        m21 = f21;
        m22 = f22;
        m23 = f23;
        m30 = f30;
        m31 = f31;
        m32 = f32;
        m33 = f33;

        float fInvDet = 1.0f / fDet;
        MultLocal(fInvDet);

        return this;
    }

    /// <summary>Returns a new matrix representing the adjoint of this matrix.</summary>
    public Matrix4f Adjoint()
    {
        return Adjoint(null);
    }

    /// <summary>Places the adjoint of this matrix in store (creates store if null).</summary>
    public Matrix4f Adjoint(Matrix4f store)
    {
        if (store == null) store = new Matrix4f();

        float fA0 = m00 * m11 - m01 * m10;
        float fA1 = m00 * m12 - m02 * m10;
        float fA2 = m00 * m13 - m03 * m10;
        float fA3 = m01 * m12 - m02 * m11;
        float fA4 = m01 * m13 - m03 * m11;
        float fA5 = m02 * m13 - m03 * m12;
        float fB0 = m20 * m31 - m21 * m30;
        float fB1 = m20 * m32 - m22 * m30;
        float fB2 = m20 * m33 - m23 * m30;
        float fB3 = m21 * m32 - m22 * m31;
        float fB4 = m21 * m33 - m23 * m31;
        float fB5 = m22 * m33 - m23 * m32;

        store.m00 = +m11 * fB5 - m12 * fB4 + m13 * fB3;
        store.m10 = -m10 * fB5 + m12 * fB2 - m13 * fB1;
        store.m20 = +m10 * fB4 - m11 * fB2 + m13 * fB0;
        store.m30 = -m10 * fB3 + m11 * fB1 - m12 * fB0;
        store.m01 = -m01 * fB5 + m02 * fB4 - m03 * fB3;
        store.m11 = +m00 * fB5 - m02 * fB2 + m03 * fB1;
        store.m21 = -m00 * fB4 + m01 * fB2 - m03 * fB0;
        store.m31 = +m00 * fB3 - m01 * fB1 + m02 * fB0;
        store.m02 = +m31 * fA5 - m32 * fA4 + m33 * fA3;
        store.m12 = -m30 * fA5 + m32 * fA2 - m33 * fA1;
        store.m22 = +m30 * fA4 - m31 * fA2 + m33 * fA0;
        store.m32 = -m30 * fA3 + m31 * fA1 - m32 * fA0;
        store.m03 = -m21 * fA5 + m22 * fA4 - m23 * fA3;
        store.m13 = +m20 * fA5 - m22 * fA2 + m23 * fA1;
        store.m23 = -m20 * fA4 + m21 * fA2 - m23 * fA0;
        store.m33 = +m20 * fA3 - m21 * fA1 + m22 * fA0;

        return store;
    }

    /// <summary>Generates the determinant of this matrix.</summary>
    public float Determinant()
    {
        float fA0 = m00 * m11 - m01 * m10;
        float fA1 = m00 * m12 - m02 * m10;
        float fA2 = m00 * m13 - m03 * m10;
        float fA3 = m01 * m12 - m02 * m11;
        float fA4 = m01 * m13 - m03 * m11;
        float fA5 = m02 * m13 - m03 * m12;
        float fB0 = m20 * m31 - m21 * m30;
        float fB1 = m20 * m32 - m22 * m30;
        float fB2 = m20 * m33 - m23 * m30;
        float fB3 = m21 * m32 - m22 * m31;
        float fB4 = m21 * m33 - m23 * m31;
        float fB5 = m22 * m33 - m23 * m32;
        float fDet = fA0 * fB5 - fA1 * fB4 + fA2 * fB3 + fA3 * fB2 - fA4 * fB1 + fA5 * fB0;
        return fDet;
    }

    /// <summary>Sets all of the values in this matrix to zero.</summary>
    public Matrix4f Zero()
    {
        m00 = m01 = m02 = m03 = 0.0f;
        m10 = m11 = m12 = m13 = 0.0f;
        m20 = m21 = m22 = m23 = 0.0f;
        m30 = m31 = m32 = m33 = 0.0f;
        return this;
    }

    public Matrix4f Add(Matrix4f mat)
    {
        Matrix4f result = new Matrix4f();
        result.m00 = this.m00 + mat.m00;
        result.m01 = this.m01 + mat.m01;
        result.m02 = this.m02 + mat.m02;
        result.m03 = this.m03 + mat.m03;
        result.m10 = this.m10 + mat.m10;
        result.m11 = this.m11 + mat.m11;
        result.m12 = this.m12 + mat.m12;
        result.m13 = this.m13 + mat.m13;
        result.m20 = this.m20 + mat.m20;
        result.m21 = this.m21 + mat.m21;
        result.m22 = this.m22 + mat.m22;
        result.m23 = this.m23 + mat.m23;
        result.m30 = this.m30 + mat.m30;
        result.m31 = this.m31 + mat.m31;
        result.m32 = this.m32 + mat.m32;
        result.m33 = this.m33 + mat.m33;
        return result;
    }

    /// <summary>Adds the values of a parameter matrix to this matrix.</summary>
    public void AddLocal(Matrix4f mat)
    {
        m00 += mat.m00;
        m01 += mat.m01;
        m02 += mat.m02;
        m03 += mat.m03;
        m10 += mat.m10;
        m11 += mat.m11;
        m12 += mat.m12;
        m13 += mat.m13;
        m20 += mat.m20;
        m21 += mat.m21;
        m22 += mat.m22;
        m23 += mat.m23;
        m30 += mat.m30;
        m31 += mat.m31;
        m32 += mat.m32;
        m33 += mat.m33;
    }

    public Vector3f ToTranslationVector()
    {
        return new Vector3f(m03, m13, m23);
    }

    public void ToTranslationVector(Vector3f vector)
    {
        vector.Set(m03, m13, m23);
    }

    public Matrix3f ToRotationMatrix()
    {
        return new Matrix3f(m00, m01, m02, m10, m11, m12, m20, m21, m22);
    }

    public void ToRotationMatrix(Matrix3f mat)
    {
        mat.m00 = m00;
        mat.m01 = m01;
        mat.m02 = m02;
        mat.m10 = m10;
        mat.m11 = m11;
        mat.m12 = m12;
        mat.m20 = m20;
        mat.m21 = m21;
        mat.m22 = m22;
    }

    public void SetRotationMatrix(Matrix3f mat)
    {
        this.m00 = mat.m00;
        this.m01 = mat.m01;
        this.m02 = mat.m02;
        this.m10 = mat.m10;
        this.m11 = mat.m11;
        this.m12 = mat.m12;
        this.m20 = mat.m20;
        this.m21 = mat.m21;
        this.m22 = mat.m22;
    }

    public void SetScale(float x, float y, float z)
    {
        m00 *= x;
        m11 *= y;
        m22 *= z;
    }

    public void SetScale(Vector3f scale)
    {
        m00 *= scale.X;
        m11 *= scale.Y;
        m22 *= scale.Z;
    }

    /// <summary>Sets the matrix's translation values.</summary>
    public void SetTranslation(float[] translation)
    {
        if (translation.Length != 3)
        {
            throw new ArgumentException("Translation size must be 3.");
        }
        m03 = translation[0];
        m13 = translation[1];
        m23 = translation[2];
    }

    /// <summary>Sets the matrix's translation values.</summary>
    public void SetTranslation(float x, float y, float z)
    {
        m03 = x;
        m13 = y;
        m23 = z;
    }

    /// <summary>Sets the matrix's translation values.</summary>
    public void SetTranslation(Vector3f translation)
    {
        m03 = translation.X;
        m13 = translation.Y;
        m23 = translation.Z;
    }

    /// <summary>Sets the matrix's inverse translation values.</summary>
    public void SetInverseTranslation(float[] translation)
    {
        if (translation.Length != 3)
        {
            throw new ArgumentException("Translation size must be 3.");
        }
        m03 = -translation[0];
        m13 = -translation[1];
        m23 = -translation[2];
    }

    /// <summary>Sets this matrix to that of a rotation about three axes (x, y, z) expressed in degrees in a single Vector3f.</summary>
    public void AngleRotation(Vector3f angles)
    {
        float angle;
        float sr, sp, sy, cr, cp, cy;

        angle = (angles.Z * FastMath.DEG_TO_RAD);
        sy = FastMath.Sin(angle);
        cy = FastMath.Cos(angle);
        angle = (angles.Y * FastMath.DEG_TO_RAD);
        sp = FastMath.Sin(angle);
        cp = FastMath.Cos(angle);
        angle = (angles.X * FastMath.DEG_TO_RAD);
        sr = FastMath.Sin(angle);
        cr = FastMath.Cos(angle);

        // matrix = (Z * Y) * X
        m00 = cp * cy;
        m10 = cp * sy;
        m20 = -sp;
        m01 = sr * sp * cy + cr * -sy;
        m11 = sr * sp * sy + cr * cy;
        m21 = sr * cp;
        m02 = (cr * sp * cy + -sr * -sy);
        m12 = (cr * sp * sy + -sr * cy);
        m22 = cr * cp;
        m03 = 0.0f;
        m13 = 0.0f;
        m23 = 0.0f;
    }

    /// <summary>Builds an inverted rotation from Euler angles that are in radians.</summary>
    public void SetInverseRotationRadians(float[] angles)
    {
        if (angles.Length != 3)
        {
            throw new ArgumentException("Angles must be of size 3.");
        }
        double cr = FastMath.Cos(angles[0]);
        double sr = FastMath.Sin(angles[0]);
        double cp = FastMath.Cos(angles[1]);
        double sp = FastMath.Sin(angles[1]);
        double cy = FastMath.Cos(angles[2]);
        double sy = FastMath.Sin(angles[2]);

        m00 = (float)(cp * cy);
        m10 = (float)(cp * sy);
        m20 = (float)(-sp);

        double srsp = sr * sp;
        double crsp = cr * sp;

        m01 = (float)(srsp * cy - cr * sy);
        m11 = (float)(srsp * sy + cr * cy);
        m21 = (float)(sr * cp);

        m02 = (float)(crsp * cy + sr * sy);
        m12 = (float)(crsp * sy - sr * cy);
        m22 = (float)(cr * cp);
    }

    /// <summary>Builds an inverted rotation from Euler angles that are in degrees.</summary>
    public void SetInverseRotationDegrees(float[] angles)
    {
        if (angles.Length != 3)
        {
            throw new ArgumentException("Angles must be of size 3.");
        }
        float[] vec = new float[3];
        vec[0] = (angles[0] * FastMath.RAD_TO_DEG);
        vec[1] = (angles[1] * FastMath.RAD_TO_DEG);
        vec[2] = (angles[2] * FastMath.RAD_TO_DEG);
        SetInverseRotationRadians(vec);
    }

    /// <summary>Translates a given vec[3] by the translation part of this matrix.</summary>
    public void InverseTranslateVect(float[] vec)
    {
        if (vec.Length != 3)
        {
            throw new ArgumentException("vec must be of size 3.");
        }

        vec[0] = vec[0] - m03;
        vec[1] = vec[1] - m13;
        vec[2] = vec[2] - m23;
    }

    /// <summary>Translates a given Vector3f by the (inverse) translation part of this matrix.</summary>
    public void InverseTranslateVect(Vector3f data)
    {
        data.X -= m03;
        data.Y -= m13;
        data.Z -= m23;
    }

    /// <summary>Translates a given Vector3f by the translation part of this matrix.</summary>
    public void TranslateVect(Vector3f data)
    {
        data.X += m03;
        data.Y += m13;
        data.Z += m23;
    }

    /// <summary>Rotates a given Vector3f by the (inverse) rotation part of this matrix.</summary>
    public void InverseRotateVect(Vector3f vec)
    {
        float vx = vec.X, vy = vec.Y, vz = vec.Z;

        vec.X = vx * m00 + vy * m10 + vz * m20;
        vec.Y = vx * m01 + vy * m11 + vz * m21;
        vec.Z = vx * m02 + vy * m12 + vz * m22;
    }

    public void RotateVect(Vector3f vec)
    {
        float vx = vec.X, vy = vec.Y, vz = vec.Z;

        vec.X = vx * m00 + vy * m01 + vz * m02;
        vec.Y = vx * m10 + vy * m11 + vz * m12;
        vec.Z = vx * m20 + vy * m21 + vz * m22;
    }

    /// <summary>Returns the string representation of this 4x4 matrix.</summary>
    public override string ToString()
    {
        System.Text.StringBuilder result = new System.Text.StringBuilder("Matrix4f\n[\n");
        result.Append(" ");
        result.Append(m00);
        result.Append("  ");
        result.Append(m01);
        result.Append("  ");
        result.Append(m02);
        result.Append("  ");
        result.Append(m03);
        result.Append(" \n");
        result.Append(" ");
        result.Append(m10);
        result.Append("  ");
        result.Append(m11);
        result.Append("  ");
        result.Append(m12);
        result.Append("  ");
        result.Append(m13);
        result.Append(" \n");
        result.Append(" ");
        result.Append(m20);
        result.Append("  ");
        result.Append(m21);
        result.Append("  ");
        result.Append(m22);
        result.Append("  ");
        result.Append(m23);
        result.Append(" \n");
        result.Append(" ");
        result.Append(m30);
        result.Append("  ");
        result.Append(m31);
        result.Append("  ");
        result.Append(m32);
        result.Append("  ");
        result.Append(m33);
        result.Append(" \n]");
        return result.ToString();
    }

    /// <summary>Hash code based on the bit representations of all 16 matrix values.</summary>
    public override int GetHashCode()
    {
        int hash = 37;
        hash = 37 * hash + BitConverter.SingleToInt32Bits(m00);
        hash = 37 * hash + BitConverter.SingleToInt32Bits(m01);
        hash = 37 * hash + BitConverter.SingleToInt32Bits(m02);
        hash = 37 * hash + BitConverter.SingleToInt32Bits(m03);

        hash = 37 * hash + BitConverter.SingleToInt32Bits(m10);
        hash = 37 * hash + BitConverter.SingleToInt32Bits(m11);
        hash = 37 * hash + BitConverter.SingleToInt32Bits(m12);
        hash = 37 * hash + BitConverter.SingleToInt32Bits(m13);

        hash = 37 * hash + BitConverter.SingleToInt32Bits(m20);
        hash = 37 * hash + BitConverter.SingleToInt32Bits(m21);
        hash = 37 * hash + BitConverter.SingleToInt32Bits(m22);
        hash = 37 * hash + BitConverter.SingleToInt32Bits(m23);

        hash = 37 * hash + BitConverter.SingleToInt32Bits(m30);
        hash = 37 * hash + BitConverter.SingleToInt32Bits(m31);
        hash = 37 * hash + BitConverter.SingleToInt32Bits(m32);
        hash = 37 * hash + BitConverter.SingleToInt32Bits(m33);

        return hash;
    }

    /// <summary>True if both have the same mXX values.</summary>
    public override bool Equals(object o)
    {
        if (!(o is Matrix4f))
        {
            return false;
        }

        if (this == o)
        {
            return true;
        }

        Matrix4f comp = (Matrix4f)o;
        if (m00.CompareTo(comp.m00) != 0) return false;
        if (m01.CompareTo(comp.m01) != 0) return false;
        if (m02.CompareTo(comp.m02) != 0) return false;
        if (m03.CompareTo(comp.m03) != 0) return false;

        if (m10.CompareTo(comp.m10) != 0) return false;
        if (m11.CompareTo(comp.m11) != 0) return false;
        if (m12.CompareTo(comp.m12) != 0) return false;
        if (m13.CompareTo(comp.m13) != 0) return false;

        if (m20.CompareTo(comp.m20) != 0) return false;
        if (m21.CompareTo(comp.m21) != 0) return false;
        if (m22.CompareTo(comp.m22) != 0) return false;
        if (m23.CompareTo(comp.m23) != 0) return false;

        if (m30.CompareTo(comp.m30) != 0) return false;
        if (m31.CompareTo(comp.m31) != 0) return false;
        if (m32.CompareTo(comp.m32) != 0) return false;
        if (m33.CompareTo(comp.m33) != 0) return false;

        return true;
    }

    public Type GetClassTag()
    {
        return GetType();
    }

    /// <summary>True if this matrix is identity.</summary>
    public bool IsIdentity()
    {
        return
        (m00 == 1 && m01 == 0 && m02 == 0 && m03 == 0) &&
        (m10 == 0 && m11 == 1 && m12 == 0 && m13 == 0) &&
        (m20 == 0 && m21 == 0 && m22 == 1 && m23 == 0) &&
        (m30 == 0 && m31 == 0 && m32 == 0 && m33 == 1);
    }

    /// <summary>Apply a scale to this matrix.</summary>
    public void Scale(Vector3f scale)
    {
        m00 *= scale.GetX();
        m10 *= scale.GetX();
        m20 *= scale.GetX();
        m30 *= scale.GetX();
        m01 *= scale.GetY();
        m11 *= scale.GetY();
        m21 *= scale.GetY();
        m31 *= scale.GetY();
        m02 *= scale.GetZ();
        m12 *= scale.GetZ();
        m22 *= scale.GetZ();
        m32 *= scale.GetZ();
    }

    /// <summary>Apply a scale to this matrix.</summary>
    public void Scale(float scale)
    {
        m00 *= scale;
        m10 *= scale;
        m20 *= scale;
        m30 *= scale;
        m01 *= scale;
        m11 *= scale;
        m21 *= scale;
        m31 *= scale;
        m02 *= scale;
        m12 *= scale;
        m22 *= scale;
        m32 *= scale;
    }

    internal static bool EqualIdentity(Matrix4f mat)
    {
        if (JMath.Abs(mat.m00 - 1) > 1e-4) return false;
        if (JMath.Abs(mat.m11 - 1) > 1e-4) return false;
        if (JMath.Abs(mat.m22 - 1) > 1e-4) return false;
        if (JMath.Abs(mat.m33 - 1) > 1e-4) return false;

        if (JMath.Abs(mat.m01) > 1e-4) return false;
        if (JMath.Abs(mat.m02) > 1e-4) return false;
        if (JMath.Abs(mat.m03) > 1e-4) return false;

        if (JMath.Abs(mat.m10) > 1e-4) return false;
        if (JMath.Abs(mat.m12) > 1e-4) return false;
        if (JMath.Abs(mat.m13) > 1e-4) return false;

        if (JMath.Abs(mat.m20) > 1e-4) return false;
        if (JMath.Abs(mat.m21) > 1e-4) return false;
        if (JMath.Abs(mat.m23) > 1e-4) return false;

        if (JMath.Abs(mat.m30) > 1e-4) return false;
        if (JMath.Abs(mat.m31) > 1e-4) return false;
        if (JMath.Abs(mat.m32) > 1e-4) return false;

        return true;
    }

    /// <summary>Java parity: Cloneable.clone() — shallow copy of the value fields.</summary>
    public Matrix4f Clone()
    {
        return new Matrix4f(this);
    }
}
