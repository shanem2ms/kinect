#pragma once

const int badval = -0xFFFF;

struct DXY
{
    int dx;
    int dy;

    DXY() : dx(0), dy(0) {}
    DXY(int _dx, int _dy) : dx(_dx), dy(_dy) {}

    bool IsValid()
    {
        return dx != badval && dy != badval;
    }

    int LengthSq() { return dx * dx + dy * dy; }
};

inline DXY operator - (const DXY& lhs, const DXY& rhs)
{
    return DXY(lhs.dx - rhs.dx, lhs.dy - rhs.dy);
}

inline DXY operator + (const DXY& lhs, const DXY& rhs)
{
    return DXY(lhs.dx + rhs.dx, lhs.dy + rhs.dy);
}


struct Pt
{
    Pt(float _x, float _y, float _z) : x(_x),
        y(_y), z(_z) {}

    Pt() : x(0), y(0), z(0) {}
    float x;
    float y;
    float z;

    Pt& operator += (const Pt& rhs)
    {
        x += rhs.x;
        y += rhs.y;
        z += rhs.z;

        return *this;
    }

    Pt& operator *= (const float rhs)
    {
        x *= rhs;
        y *= rhs;
        z *= rhs;

        return *this;
    }

    inline bool IsValid()
    {
        return !isinf(x) && (x != 0 && y != 0 && x != 0);
    }

    float Length()
    {
        return sqrt(x * x + y * y + z * z);
    }
    float LengthSq()
    {
        return x * x + y * y + z * z;
    }
    void Normalize()
    {
        float invlen = 1.0f / sqrt(x * x + y * y + z * z);
        x *= invlen;
        y *= invlen;
        z *= invlen;
    }
};

inline Pt operator * (const Pt& lhs, float rhs)
{
    return Pt(lhs.x * rhs, lhs.y * rhs, lhs.z * rhs);
}

inline Pt operator - (const Pt& lhs, const Pt& rhs)
{
    return Pt(lhs.x - rhs.x, lhs.y - rhs.y, lhs.z - rhs.z);
}
inline Pt operator + (const Pt& lhs, const Pt& rhs)
{
    return Pt(lhs.x + rhs.x, lhs.y + rhs.y, lhs.z + rhs.z);
}

inline Pt Cross(const Pt& lhs, const Pt& rhs)
{
    Pt pt;
    pt.x = lhs.y * rhs.z - lhs.z * rhs.y;
    pt.y = lhs.z * rhs.x - lhs.x * rhs.z;
    pt.z = lhs.x * rhs.y - lhs.y * rhs.x;
    return pt;
}

inline float Dot(const Pt& lhs, const Pt& rhs)
{
    return lhs.x * rhs.x +
        lhs.y * rhs.y +
        lhs.z * rhs.z;
}

