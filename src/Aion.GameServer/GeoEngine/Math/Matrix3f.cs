using System;
using System.Text;
using Aion.Commons.Nio;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using JMath = System.Math;

namespace Aion.GameServer.GeoEngine.Math;

/// <summary>
/// <c>Matrix3f</c> defines a 3x3 matrix. Matrix data is internally maintained and accessible
/// via get/set methods.
/// Java parity: geoEngine/math/Matrix3f (jMonkeyEngine; Mark Powell, Joshua Slack).
/// </summary>
public sealed class Matrix3f
{
    private static readonly ILogger logger = NullLogger.Instance;

    internal float m00, m01, m02;
    internal float m10, m11, m12;
    internal float m20, m21, m22;

    /// <summary>Instantiates a new identity <c>Matrix3f</c>.</summary>
    public Matrix3f()
    {
        LoadIdentity();
    }

    public Matrix3f(float m00, float m01, float m02, float m10, float m11,
        float m12, float m20, float m21, float m22)
    {
        this.m00 = m00;
        this.m01 = m01;
        this.m02 = m02;
        this.m10 = m10;
        this.m11 = m11;
        this.m12 = m12;
        this.m20 = m20;
        this.m21 = m21;
        this.m22 = m22;
    }

    public Matrix3f(Matrix3f mat)
    {
        Set(mat);
    }

    /// <summary>Takes the absolute value of all matrix fields locally.</summary>
    public void AbsoluteLocal()
    {
        m00 = FastMath.Abs(m00);
        m01 = FastMath.Abs(m01);
        m02 = FastMath.Abs(m02);
        m10 = FastMath.Abs(m10);
        m11 = FastMath.Abs(m11);
        m12 = FastMath.Abs(m12);
        m20 = FastMath.Abs(m20);
        m21 = FastMath.Abs(m21);
        m22 = FastMath.Abs(m22);
    }

    /// <summary>Transfers the contents of the given matrix to this one (null → identity).</summary>
    public Matrix3f Set(Matrix3f? matrix)
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
            m10 = matrix.m10;
            m11 = matrix.m11;
            m12 = matrix.m12;
            m20 = matrix.m20;
            m21 = matrix.m21;
            m22 = matrix.m22;
        }
        return this;
    }

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
                }
                break;
            case 1:
                switch (j)
                {
                    case 0: return m10;
                    case 1: return m11;
                    case 2: return m12;
                }
                break;
            case 2:
                switch (j)
                {
                    case 0: return m20;
                    case 1: return m21;
                    case 2: return m22;
                }
                break;
        }

        logger.LogWarning("Invalid matrix index.");
        throw new ArgumentException("Invalid indices into matrix.");
    }

    /// <summary>
    /// Returns the matrix in row-major or column-major order into a 9- or 16-element array.
    /// </summary>
    public void Get(float[] data, bool rowMajor)
    {
        if (data.Length == 9)
        {
            if (rowMajor)
            {
                data[0] = m00;
                data[1] = m01;
                data[2] = m02;
                data[3] = m10;
                data[4] = m11;
                data[5] = m12;
                data[6] = m20;
                data[7] = m21;
                data[8] = m22;
            }
            else
            {
                data[0] = m00;
                data[1] = m10;
                data[2] = m20;
                data[3] = m01;
                data[4] = m11;
                data[5] = m21;
                data[6] = m02;
                data[7] = m12;
                data[8] = m22;
            }
        }
        else if (data.Length == 16)
        {
            if (rowMajor)
            {
                data[0] = m00;
                data[1] = m01;
                data[2] = m02;
                data[4] = m10;
                data[5] = m11;
                data[6] = m12;
                data[8] = m20;
                data[9] = m21;
                data[10] = m22;
            }
            else
            {
                data[0] = m00;
                data[1] = m10;
                data[2] = m20;
                data[4] = m01;
                data[5] = m11;
                data[6] = m21;
                data[8] = m02;
                data[9] = m12;
                data[10] = m22;
            }
        }
        else
        {
            throw new IndexOutOfRangeException("Array size must be 9 or 16 in Matrix3f.get().");
        }
    }

    public Vector3f GetColumn(int i)
    {
        return GetColumn(i, null);
    }

    public Vector3f GetColumn(int i, Vector3f? store)
    {
        if (store == null) store = new Vector3f();
        switch (i)
        {
            case 0:
                store.X = m00;
                store.Y = m10;
                store.Z = m20;
                break;
            case 1:
                store.X = m01;
                store.Y = m11;
                store.Z = m21;
                break;
            case 2:
                store.X = m02;
                store.Y = m12;
                store.Z = m22;
                break;
            default:
                logger.LogWarning("Invalid column index.");
                throw new ArgumentException("Invalid column index. " + i);
        }
        return store;
    }

    public Vector3f GetRow(int i)
    {
        return GetRow(i, null);
    }

    public Vector3f GetRow(int i, Vector3f? store)
    {
        if (store == null) store = new Vector3f();
        switch (i)
        {
            case 0:
                store.X = m00;
                store.Y = m01;
                store.Z = m02;
                break;
            case 1:
                store.X = m10;
                store.Y = m11;
                store.Z = m12;
                break;
            case 2:
                store.X = m20;
                store.Y = m21;
                store.Z = m22;
                break;
            default:
                logger.LogWarning("Invalid row index.");
                throw new ArgumentException("Invalid row index. " + i);
        }
        return store;
    }

    /// <summary>
    /// Fills a FloatBuffer with the matrix data (column- or row-major), advancing its position by 9.
    /// </summary>
    public FloatBuffer FillFloatBuffer(FloatBuffer fb, bool columnMajor)
    {
        if (columnMajor)
        {
            fb.Put(m00).Put(m10).Put(m20);
            fb.Put(m01).Put(m11).Put(m21);
            fb.Put(m02).Put(m12).Put(m22);
        }
        else
        {
            fb.Put(m00).Put(m01).Put(m02);
            fb.Put(m10).Put(m11).Put(m12);
            fb.Put(m20).Put(m21).Put(m22);
        }
        return fb;
    }

    public Matrix3f SetColumn(int i, Vector3f? column)
    {
        if (column == null)
        {
            logger.LogWarning("Column is null. Ignoring.");
            return this;
        }
        switch (i)
        {
            case 0:
                m00 = column.X;
                m10 = column.Y;
                m20 = column.Z;
                break;
            case 1:
                m01 = column.X;
                m11 = column.Y;
                m21 = column.Z;
                break;
            case 2:
                m02 = column.X;
                m12 = column.Y;
                m22 = column.Z;
                break;
            default:
                logger.LogWarning("Invalid column index.");
                throw new ArgumentException("Invalid column index. " + i);
        }
        return this;
    }

    public Matrix3f SetRow(int i, Vector3f? row)
    {
        if (row == null)
        {
            logger.LogWarning("Row is null. Ignoring.");
            return this;
        }
        switch (i)
        {
            case 0:
                m00 = row.X;
                m01 = row.Y;
                m02 = row.Z;
                break;
            case 1:
                m10 = row.X;
                m11 = row.Y;
                m12 = row.Z;
                break;
            case 2:
                m20 = row.X;
                m21 = row.Y;
                m22 = row.Z;
                break;
            default:
                logger.LogWarning("Invalid row index.");
                throw new ArgumentException("Invalid row index. " + i);
        }
        return this;
    }

    public Matrix3f Set(int i, int j, float value)
    {
        switch (i)
        {
            case 0:
                switch (j)
                {
                    case 0: m00 = value; return this;
                    case 1: m01 = value; return this;
                    case 2: m02 = value; return this;
                }
                break;
            case 1:
                switch (j)
                {
                    case 0: m10 = value; return this;
                    case 1: m11 = value; return this;
                    case 2: m12 = value; return this;
                }
                break;
            case 2:
                switch (j)
                {
                    case 0: m20 = value; return this;
                    case 1: m21 = value; return this;
                    case 2: m22 = value; return this;
                }
                break;
        }

        logger.LogWarning("Invalid matrix index.");
        throw new ArgumentException("Invalid indices into matrix.");
    }

    public Matrix3f Set(float[][] matrix)
    {
        if (matrix.Length != 3 || matrix[0].Length != 3)
        {
            throw new ArgumentException("Array must be of size 9.");
        }

        m00 = matrix[0][0];
        m01 = matrix[0][1];
        m02 = matrix[0][2];
        m10 = matrix[1][0];
        m11 = matrix[1][1];
        m12 = matrix[1][2];
        m20 = matrix[2][0];
        m21 = matrix[2][1];
        m22 = matrix[2][2];

        return this;
    }

    /// <summary>Recreate Matrix using the provided axes.</summary>
    public void FromAxes(Vector3f uAxis, Vector3f vAxis, Vector3f wAxis)
    {
        m00 = uAxis.X;
        m10 = uAxis.Y;
        m20 = uAxis.Z;

        m01 = vAxis.X;
        m11 = vAxis.Y;
        m21 = vAxis.Z;

        m02 = wAxis.X;
        m12 = wAxis.Y;
        m22 = wAxis.Z;
    }

    public Matrix3f Set(float[] matrix)
    {
        return Set(matrix, true);
    }

    public Matrix3f Set(float[] matrix, bool rowMajor)
    {
        if (matrix.Length != 9)
            throw new ArgumentException("Array must be of size 9.");

        if (rowMajor)
        {
            m00 = matrix[0];
            m01 = matrix[1];
            m02 = matrix[2];
            m10 = matrix[3];
            m11 = matrix[4];
            m12 = matrix[5];
            m20 = matrix[6];
            m21 = matrix[7];
            m22 = matrix[8];
        }
        else
        {
            m00 = matrix[0];
            m01 = matrix[3];
            m02 = matrix[6];
            m10 = matrix[1];
            m11 = matrix[4];
            m12 = matrix[7];
            m20 = matrix[2];
            m21 = matrix[5];
            m22 = matrix[8];
        }
        return this;
    }

    /// <summary>Sets this matrix to the identity matrix.</summary>
    public void LoadIdentity()
    {
        m01 = m02 = m10 = m12 = m20 = m21 = 0;
        m00 = m11 = m22 = 1;
    }

    public bool IsIdentity()
    {
        return
            (m00 == 1 && m01 == 0 && m02 == 0) &&
            (m10 == 0 && m11 == 1 && m12 == 0) &&
            (m20 == 0 && m21 == 0 && m22 == 1);
    }

    /// <summary>
    /// Sets this matrix to the values specified by an angle and an axis of rotation
    /// (creates an object; use FromAngleNormalAxis if the axis is already normalized).
    /// </summary>
    public void FromAngleAxis(float angle, Vector3f axis)
    {
        Vector3f normAxis = axis.Normalize();
        FromAngleNormalAxis(angle, normAxis);
    }

    /// <summary>
    /// Sets this matrix to the values specified by an angle and a normalized axis of rotation.
    /// </summary>
    public void FromAngleNormalAxis(float angle, Vector3f axis)
    {
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

    public Matrix3f Mult(Matrix3f mat)
    {
        return Mult(mat, null);
    }

    public Matrix3f Mult(Matrix3f mat, Matrix3f? product)
    {
        float temp00, temp01, temp02;
        float temp10, temp11, temp12;
        float temp20, temp21, temp22;

        if (product == null) product = new Matrix3f();
        temp00 = m00 * mat.m00 + m01 * mat.m10 + m02 * mat.m20;
        temp01 = m00 * mat.m01 + m01 * mat.m11 + m02 * mat.m21;
        temp02 = m00 * mat.m02 + m01 * mat.m12 + m02 * mat.m22;
        temp10 = m10 * mat.m00 + m11 * mat.m10 + m12 * mat.m20;
        temp11 = m10 * mat.m01 + m11 * mat.m11 + m12 * mat.m21;
        temp12 = m10 * mat.m02 + m11 * mat.m12 + m12 * mat.m22;
        temp20 = m20 * mat.m00 + m21 * mat.m10 + m22 * mat.m20;
        temp21 = m20 * mat.m01 + m21 * mat.m11 + m22 * mat.m21;
        temp22 = m20 * mat.m02 + m21 * mat.m12 + m22 * mat.m22;

        product.m00 = temp00;
        product.m01 = temp01;
        product.m02 = temp02;
        product.m10 = temp10;
        product.m11 = temp11;
        product.m12 = temp12;
        product.m20 = temp20;
        product.m21 = temp21;
        product.m22 = temp22;

        return product;
    }

    public Vector3f Mult(Vector3f vec)
    {
        return Mult(vec, null);
    }

    /// <summary>Multiplies this 3x3 matrix by the 1x3 Vector vec and stores the result in product.</summary>
    public Vector3f Mult(Vector3f vec, Vector3f? product)
    {
        if (null == product)
        {
            product = new Vector3f();
        }

        float x = vec.X;
        float y = vec.Y;
        float z = vec.Z;

        product.X = m00 * x + m01 * y + m02 * z;
        product.Y = m10 * x + m11 * y + m12 * z;
        product.Z = m20 * x + m21 * y + m22 * z;
        return product;
    }

    /// <summary>Multiplies this matrix internally by a given float scale factor.</summary>
    public Matrix3f MultLocal(float scale)
    {
        m00 *= scale;
        m01 *= scale;
        m02 *= scale;
        m10 *= scale;
        m11 *= scale;
        m12 *= scale;
        m20 *= scale;
        m21 *= scale;
        m22 *= scale;
        return this;
    }

    /// <summary>Multiplies this matrix by a vector, storing the result in (and returning) vec.</summary>
    public Vector3f? MultLocal(Vector3f vec)
    {
        if (vec == null) return null;
        float x = vec.X;
        float y = vec.Y;
        vec.X = m00 * x + m01 * y + m02 * vec.Z;
        vec.Y = m10 * x + m11 * y + m12 * vec.Z;
        vec.Z = m20 * x + m21 * y + m22 * vec.Z;
        return vec;
    }

    /// <summary>Equivalent to this *= mat.</summary>
    public Matrix3f MultLocal(Matrix3f mat)
    {
        return Mult(mat, this);
    }

    /// <summary>Transposes this matrix in place.</summary>
    public Matrix3f TransposeLocal()
    {
        float tmp = m01;
        m01 = m10;
        m10 = tmp;

        tmp = m02;
        m02 = m20;
        m20 = tmp;

        tmp = m12;
        m12 = m21;
        m21 = tmp;

        return this;
    }

    public Matrix3f Invert()
    {
        return Invert(null);
    }

    public Matrix3f Invert(Matrix3f? store)
    {
        if (store == null) store = new Matrix3f();

        float det = Determinant();
        if (FastMath.Abs(det) <= FastMath.FLT_EPSILON)
            return store.Zero();

        store.m00 = m11 * m22 - m12 * m21;
        store.m01 = m02 * m21 - m01 * m22;
        store.m02 = m01 * m12 - m02 * m11;
        store.m10 = m12 * m20 - m10 * m22;
        store.m11 = m00 * m22 - m02 * m20;
        store.m12 = m02 * m10 - m00 * m12;
        store.m20 = m10 * m21 - m11 * m20;
        store.m21 = m01 * m20 - m00 * m21;
        store.m22 = m00 * m11 - m01 * m10;

        store.MultLocal(1f / det);
        return store;
    }

    public Matrix3f InvertLocal()
    {
        float det = Determinant();
        if (FastMath.Abs(det) <= FastMath.FLT_EPSILON)
            return Zero();

        float f00 = m11 * m22 - m12 * m21;
        float f01 = m02 * m21 - m01 * m22;
        float f02 = m01 * m12 - m02 * m11;
        float f10 = m12 * m20 - m10 * m22;
        float f11 = m00 * m22 - m02 * m20;
        float f12 = m02 * m10 - m00 * m12;
        float f20 = m10 * m21 - m11 * m20;
        float f21 = m01 * m20 - m00 * m21;
        float f22 = m00 * m11 - m01 * m10;

        m00 = f00;
        m01 = f01;
        m02 = f02;
        m10 = f10;
        m11 = f11;
        m12 = f12;
        m20 = f20;
        m21 = f21;
        m22 = f22;

        MultLocal(1f / det);
        return this;
    }

    /// <summary>Returns a new matrix representing the adjoint of this matrix.</summary>
    public Matrix3f Adjoint()
    {
        return Adjoint(null);
    }

    public Matrix3f Adjoint(Matrix3f? store)
    {
        if (store == null) store = new Matrix3f();

        store.m00 = m11 * m22 - m12 * m21;
        store.m01 = m02 * m21 - m01 * m22;
        store.m02 = m01 * m12 - m02 * m11;
        store.m10 = m12 * m20 - m10 * m22;
        store.m11 = m00 * m22 - m02 * m20;
        store.m12 = m02 * m10 - m00 * m12;
        store.m20 = m10 * m21 - m11 * m20;
        store.m21 = m01 * m20 - m00 * m21;
        store.m22 = m00 * m11 - m01 * m10;

        return store;
    }

    /// <summary>Generates the determinant of this matrix.</summary>
    public float Determinant()
    {
        float fCo00 = m11 * m22 - m12 * m21;
        float fCo10 = m12 * m20 - m10 * m22;
        float fCo20 = m10 * m21 - m11 * m20;
        float fDet = m00 * fCo00 + m01 * fCo10 + m02 * fCo20;
        return fDet;
    }

    /// <summary>Sets all of the values in this matrix to zero.</summary>
    public Matrix3f Zero()
    {
        m00 = m01 = m02 = m10 = m11 = m12 = m20 = m21 = m22 = 0.0f;
        return this;
    }

    /// <summary>Adds the values of a parameter matrix to this matrix.</summary>
    [Obsolete]
    public void Add(Matrix3f mat)
    {
        m00 += mat.m00;
        m01 += mat.m01;
        m02 += mat.m02;
        m10 += mat.m10;
        m11 += mat.m11;
        m12 += mat.m12;
        m20 += mat.m20;
        m21 += mat.m21;
        m22 += mat.m22;
    }

    /// <summary>
    /// Locally transposes this Matrix (inconsistent value-vs-local semantics, preserved for
    /// backwards compatibility). Use TransposeNew() to transpose to a new object.
    /// </summary>
    public Matrix3f Transpose()
    {
        return TransposeLocal();
    }

    /// <summary>Returns a transposed version of this matrix.</summary>
    public Matrix3f TransposeNew()
    {
        Matrix3f ret = new Matrix3f(m00, m10, m20, m01, m11, m21, m02, m12, m22);
        return ret;
    }

    public override string ToString()
    {
        StringBuilder result = new StringBuilder("Matrix3f\n[\n");
        result.Append(' ');
        result.Append(m00);
        result.Append("  ");
        result.Append(m01);
        result.Append("  ");
        result.Append(m02);
        result.Append(" \n");
        result.Append(' ');
        result.Append(m10);
        result.Append("  ");
        result.Append(m11);
        result.Append("  ");
        result.Append(m12);
        result.Append(" \n");
        result.Append(' ');
        result.Append(m20);
        result.Append("  ");
        result.Append(m21);
        result.Append("  ");
        result.Append(m22);
        result.Append(" \n]");
        return result.ToString();
    }

    public override int GetHashCode()
    {
        int hash = 37;
        hash = 37 * hash + BitConverter.SingleToInt32Bits(m00);
        hash = 37 * hash + BitConverter.SingleToInt32Bits(m01);
        hash = 37 * hash + BitConverter.SingleToInt32Bits(m02);

        hash = 37 * hash + BitConverter.SingleToInt32Bits(m10);
        hash = 37 * hash + BitConverter.SingleToInt32Bits(m11);
        hash = 37 * hash + BitConverter.SingleToInt32Bits(m12);

        hash = 37 * hash + BitConverter.SingleToInt32Bits(m20);
        hash = 37 * hash + BitConverter.SingleToInt32Bits(m21);
        hash = 37 * hash + BitConverter.SingleToInt32Bits(m22);

        return hash;
    }

    public override bool Equals(object? o)
    {
        if (o is not Matrix3f)
        {
            return false;
        }

        if (this == o)
        {
            return true;
        }

        Matrix3f comp = (Matrix3f)o;
        if (m00.CompareTo(comp.m00) != 0) return false;
        if (m01.CompareTo(comp.m01) != 0) return false;
        if (m02.CompareTo(comp.m02) != 0) return false;

        if (m10.CompareTo(comp.m10) != 0) return false;
        if (m11.CompareTo(comp.m11) != 0) return false;
        if (m12.CompareTo(comp.m12) != 0) return false;

        if (m20.CompareTo(comp.m20) != 0) return false;
        if (m21.CompareTo(comp.m21) != 0) return false;
        if (m22.CompareTo(comp.m22) != 0) return false;

        return true;
    }

    public Type GetClassTag()
    {
        return GetType();
    }

    /// <summary>
    /// Creates a rotation matrix that rotates the vector "start" into the vector "end".
    /// (Tomas Möller, John Hughes — Journal of Graphics Tools, 4(4):1-4, 1999.)
    /// </summary>
    public void FromStartEndVectors(Vector3f start, Vector3f end)
    {
        Vector3f v = new Vector3f();
        float e, h, f;

        start.Cross(end, v);
        e = start.Dot(end);
        f = (e < 0) ? -e : e;

        // if "from" and "to" vectors are nearly parallel
        if (f > 1.0f - FastMath.ZERO_TOLERANCE)
        {
            Vector3f u = new Vector3f();
            Vector3f x = new Vector3f();
            float c1, c2, c3; /* coefficients for later use */
            int i, j;

            x.X = (start.X > 0.0) ? start.X : -start.X;
            x.Y = (start.Y > 0.0) ? start.Y : -start.Y;
            x.Z = (start.Z > 0.0) ? start.Z : -start.Z;

            if (x.X < x.Y)
            {
                if (x.X < x.Z)
                {
                    x.X = 1.0f;
                    x.Y = x.Z = 0.0f;
                }
                else
                {
                    x.Z = 1.0f;
                    x.X = x.Y = 0.0f;
                }
            }
            else
            {
                if (x.Y < x.Z)
                {
                    x.Y = 1.0f;
                    x.X = x.Z = 0.0f;
                }
                else
                {
                    x.Z = 1.0f;
                    x.X = x.Y = 0.0f;
                }
            }

            u.X = x.X - start.X;
            u.Y = x.Y - start.Y;
            u.Z = x.Z - start.Z;
            v.X = x.X - end.X;
            v.Y = x.Y - end.Y;
            v.Z = x.Z - end.Z;

            c1 = 2.0f / u.Dot(u);
            c2 = 2.0f / v.Dot(v);
            c3 = c1 * c2 * u.Dot(v);

            for (i = 0; i < 3; i++)
            {
                for (j = 0; j < 3; j++)
                {
                    float val = -c1 * u.Get(i) * u.Get(j) - c2 * v.Get(i)
                        * v.Get(j) + c3 * v.Get(i) * u.Get(j);
                    Set(i, j, val);
                }
                float valDiag = Get(i, i);
                Set(i, i, valDiag + 1.0f);
            }
        }
        else
        {
            // the most common case, unless "start"="end", or "start"=-"end"
            float hvx, hvz, hvxy, hvxz, hvyz;
            h = 1.0f / (1.0f + e);
            hvx = h * v.X;
            hvz = h * v.Z;
            hvxy = hvx * v.Y;
            hvxz = hvx * v.Z;
            hvyz = hvz * v.Y;
            Set(0, 0, e + hvx * v.X);
            Set(0, 1, hvxy - v.Z);
            Set(0, 2, hvxz + v.Y);

            Set(1, 0, hvxy + v.Z);
            Set(1, 1, e + h * v.Y * v.Y);
            Set(1, 2, hvyz - v.X);

            Set(2, 0, hvxz - v.Y);
            Set(2, 1, hvyz + v.X);
            Set(2, 2, e + hvz * v.Z);
        }
    }

    /// <summary>Scales the operation performed by this matrix on a per-component basis.</summary>
    public void Scale(Vector3f scale)
    {
        m00 *= scale.X;
        m10 *= scale.X;
        m20 *= scale.X;
        m01 *= scale.Y;
        m11 *= scale.Y;
        m21 *= scale.Y;
        m02 *= scale.Z;
        m12 *= scale.Z;
        m22 *= scale.Z;
    }

    internal static bool EqualIdentity(Matrix3f mat)
    {
        if (JMath.Abs(mat.m00 - 1) > 1e-4) return false;
        if (JMath.Abs(mat.m11 - 1) > 1e-4) return false;
        if (JMath.Abs(mat.m22 - 1) > 1e-4) return false;

        if (JMath.Abs(mat.m01) > 1e-4) return false;
        if (JMath.Abs(mat.m02) > 1e-4) return false;

        if (JMath.Abs(mat.m10) > 1e-4) return false;
        if (JMath.Abs(mat.m12) > 1e-4) return false;

        if (JMath.Abs(mat.m20) > 1e-4) return false;
        if (JMath.Abs(mat.m21) > 1e-4) return false;

        return true;
    }

    public Matrix3f Clone()
    {
        return new Matrix3f(this);
    }
}
