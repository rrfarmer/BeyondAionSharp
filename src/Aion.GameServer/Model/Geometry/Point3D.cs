using Aion.GameServer.Model.Templates.Zone;

namespace Aion.GameServer.Model.Geometry;

/// <summary>Java parity: model/geometry/Point3D — 3D float coordinate, cloneable and serialisable.</summary>
public class Point3D : ICloneable
{
    private float _x;
    private float _y;
    private float _z;

    public Point3D() { }

    public Point3D(Point2D point, float z) : this(point.GetX(), point.GetY(), z) { }

    public Point3D(Point3D point) : this(point._x, point._y, point._z) { }

    public Point3D(float x, float y, float z)
    {
        _x = x; _y = y; _z = z;
    }

    public Point3D(double x, double y, double z)
    {
        _x = (float)x; _y = (float)y; _z = (float)z;
    }

    public float GetX() => _x;
    public float GetY() => _y;
    public float GetZ() => _z;

    public void SetX(float x) => _x = x;
    public void SetY(float y) => _y = y;
    public void SetZ(float z) => _z = z;

    public override bool Equals(object? o)
    {
        if (ReferenceEquals(this, o)) return true;
        if (o is not Point3D p) return false;
        return _x == p._x && _y == p._y && _z == p._z;
    }

    public override int GetHashCode()
    {
        float result = _x;
        result = 31 * result + _y;
        result = 31 * result + _z;
        return (int)(result * 100);
    }

    public object Clone() => new Point3D(this);

    public override string ToString() => $"Point3D{{x={_x}, y={_y}, z={_z}}}";
}
