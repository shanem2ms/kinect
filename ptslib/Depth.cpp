// dllmain.cpp : Defines the entry point for the DLL application.
#include "pch.h"
#include <cmath>
#include <vector>


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

DXY operator - (const DXY& lhs, const DXY& rhs)
{
    return DXY(lhs.dx - rhs.dx, lhs.dy - rhs.dy);
}

DXY operator + (const DXY& lhs, const DXY& rhs)
{
    return DXY(lhs.dx + rhs.dx, lhs.dy + rhs.dy);
}

extern "C" {
    DXY* tmpDbuf = nullptr;
    DXY* tmpDbuf2 = nullptr;
    __declspec (dllexport) void DepthFindEdges(unsigned short* dbuf, float* outpts, int depthWidth, int depthHeight)
    {
        if (tmpDbuf == nullptr)
        {
            tmpDbuf = new DXY[depthWidth * depthHeight];
            tmpDbuf2 = new DXY[depthWidth * depthHeight];
        }
        for (int y = 0; y < depthHeight; ++y)
        {
            for (int x = 1; x < depthWidth; ++x)
            {
                unsigned short px = dbuf[y * depthWidth + x - 1];
                unsigned short nx = dbuf[y * depthWidth + x];
                tmpDbuf[y * depthWidth + x].dx = (px > 0 && nx > 0) ?
                    nx - px : badval;
            }
        }

        for (int y = 1; y < depthHeight; ++y)
        {
            for (int x = 0; x < depthWidth; ++x)
            {
                unsigned short py = dbuf[(y - 1) * depthWidth + x];
                unsigned short ny = dbuf[y * depthWidth + x];
                tmpDbuf[y * depthWidth + x].dy = (py > 0 && ny > 0) ?
                    ny - py : badval;
            }
        }

        int maxx = 0;
        int maxy = 0;
        for (int y = 1; y < depthHeight - 1; ++y)
        {
            for (int x = 1; x < depthWidth - 1; ++x)
            {
                DXY& d = tmpDbuf[y * depthWidth + x];
                DXY& dx1 = tmpDbuf[y * depthWidth + x - 1];
                DXY& dy1 = tmpDbuf[(y - 1) * depthWidth + x];
                if (d.IsValid() && dx1.IsValid() && dy1.IsValid())
                {
                    DXY ddx = d - dx1;
                    DXY ddy = d - dy1;
                    tmpDbuf2[y * depthWidth + x] = DXY(ddx.LengthSq(),
                        ddy.LengthSq());
                    maxx = max(maxx, tmpDbuf2[y * depthWidth + x].dx);
                    maxy = max(maxy, tmpDbuf2[y * depthWidth + x].dy);
                }
                else
                    tmpDbuf2[y * depthWidth + x] = DXY(badval, badval);
            }
        }

        float* outNrm = (float*)outpts;
        for (int y = 1; y < depthHeight - 1; ++y)
        {
            for (int x = 1; x < depthWidth - 1; ++x)
            {
                float fvx = (float)tmpDbuf2[y * depthWidth + x].dx;
                float fvy = (float)tmpDbuf2[y * depthWidth + x].dy;
                float fdd = tmpDbuf2->IsValid() ? (float)tmpDbuf2[y * depthWidth + x].LengthSq() : 0;
                float fd = tmpDbuf->IsValid() ? (float)tmpDbuf[y * depthWidth + x].LengthSq() : 0;
                int ptIdx = y * depthWidth + x;
                outNrm[ptIdx * 3] = 0;
                outNrm[ptIdx * 3 + 1] = fd - 3000.0f;
                outNrm[ptIdx * 3 + 2] = fdd - 3000.0f;
            }
        }
    }
}



extern "C"
{

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
        void Normalize()
        {
            float invlen = 1.0f / sqrt(x * x + y * y + z * z);
            x *= invlen;
            y *= invlen;
            z *= invlen;
        }
    };

    Pt operator * (const Pt& lhs, float rhs)
    {
        return Pt(lhs.x * rhs, lhs.y * rhs, lhs.z * rhs);
    }

    Pt operator - (const Pt& lhs, const Pt& rhs)
    {
        return Pt(lhs.x - rhs.x, lhs.y - rhs.y, lhs.z - rhs.z);
    }

    Pt operator + (const Pt& lhs, const Pt& rhs)
    {
        return Pt(lhs.x + rhs.x, lhs.y + rhs.y, lhs.z + rhs.z);
    }

    Pt Cross(const Pt& lhs, const Pt& rhs)
    {
        Pt pt;
        pt.x = lhs.y * rhs.z - lhs.z * rhs.y;
        pt.y = lhs.z * rhs.x - lhs.x * rhs.z;
        pt.z = lhs.x * rhs.y - lhs.y * rhs.x;
        return pt;
    }

    float Dot(const Pt& lhs, const Pt& rhs)
    {
        return lhs.x * rhs.x + 
               lhs.y * rhs.y + 
               lhs.z * rhs.z;
    }

    Pt* tmpNrm = nullptr;
    static float threshhold = .75;
    __declspec (dllexport) void DepthFindNormals(float* vals, float* outpts, float px, float py, int depthWidth, int depthHeight)
    {
        tmpNrm = new Pt[depthWidth * depthHeight];


        Pt* depthPts = (Pt*)vals;
        for (int y = 1; y < depthHeight - 1; ++y)
        {
            for (int x = 1; x < depthWidth - 1; ++x)
            {
                Pt& ptx1 = depthPts[y * depthWidth + x + 1];
                Pt& ptx2 = depthPts[y * depthWidth + x - 1];
                Pt& pty1 = depthPts[(y - 1) * depthWidth + x];
                Pt& pty2 = depthPts[(y + 1) * depthWidth + x];
                Pt& outPt = tmpNrm[y * depthWidth + x];
                if (ptx1.IsValid() && ptx2.IsValid() &&
                    pty1.IsValid() && pty2.IsValid())
                {
                    Pt dx = ptx1 - ptx2;
                    Pt dy = pty1 - pty2;
                    outPt = Cross(dx, dy);
                    outPt.Normalize();
                }
                else
                {
                    outPt = Pt(0, 0, 0);
                }

            }
        }
        
        if (px >= 0)
        {
            int pickX = (int)((1 - px) * depthWidth + 0.5f);
            int pickY = (int)(py * depthHeight + 0.5f);
            Pt nrm = tmpNrm[pickY * depthWidth + pickX];
            Pt* outNrm = (Pt*)outpts;
            for (int y = 1; y < depthHeight - 1; ++y)
            {
                for (int x = 1; x < depthWidth - 1; ++x)
                {
                    float diff = (nrm - tmpNrm[y * depthWidth + x]).Length();
                    if (diff < threshhold)
                    {
                        Pt nrm = tmpNrm[y * depthWidth + x];
                        nrm += Pt(1, 1, 1);
                        nrm *= 0.5f;
                        outNrm[y * depthWidth + x] = nrm;
                    }
                    else
                        outNrm[y * depthWidth + x] = Pt(0, 0, 0);
                }
            }
        }
        else
        {
            Pt* outNrm = (Pt*)outpts;
            for (int y = 1; y < depthHeight - 1; ++y)
            {
                for (int x = 1; x < depthWidth - 1; ++x)
                {
                    Pt nrm = tmpNrm[y * depthWidth + x];
                    nrm += Pt(1, 1, 1);
                    nrm *= 0.5f;
                    outNrm[y * depthWidth + x] = nrm;
                }
            }
        }
    }

    struct Rect
    {
        Rect(int _x, int _y, int _w, int _h) :
            x(_x), y(_y), w(_w), h(_h) {}

        int x;
        int y;
        int w;
        int h;
    };

    struct Buffer
    {
        Pt* depthPths;
        int width;
        int height;
    };

    struct Quad
    {
        Pt pt[4];
    };

    struct Tile
    {
        Rect m_rect;
        Buffer& m_buffer;

        Tile *m_tiles;

        Tile(Buffer& buffer, const Rect& rect) :
            m_buffer(buffer),
            m_rect(rect),
            m_tiles(nullptr)
        {

        }

        inline Pt& GetPt(int x, int y)
        {
            return m_buffer.depthPths[(y + m_rect.y) *
                m_buffer.width + (x + m_rect.x)];
        }

        void Process(std::vector<Quad> &quads)
        {
            const Pt* ptl = nullptr, *ptr = nullptr,
                * pbl = nullptr, * pbr = nullptr;
            int height = m_rect.h;
            int width = m_rect.w;

            for (int y = 0; y < height && ptl == nullptr; ++y)
            {
                for (int x = 0; x < width && ptl == nullptr; ++x)
                {
                    Pt& pt = GetPt(x, y);
                    if (pt.IsValid())
                        ptl = &pt;
                }
            }

            for (int y = 0; y < height && ptr == nullptr; ++y)
            {
                for (int x = width; x >= 0 && ptr == nullptr; --x)
                {
                    Pt& pt = GetPt(x, y);
                    if (pt.IsValid())
                        ptr = &pt;
                }
            }

            for (int y = height - 1; y >= 0 && pbl == nullptr; --y)
            {
                for (int x = 0; x < width && pbl == nullptr; ++x)
                {
                    Pt& pt = GetPt(x, y);
                    if (pt.IsValid())
                        pbl = &pt;
                }
            }

            for (int y = height - 1; y >= 0 && pbr == nullptr; --y)
            {
                for (int x = width; x >= 0 && pbr == nullptr; --x)
                {
                    Pt& pt = GetPt(x, y);
                    if (pt.IsValid())
                        pbr = &pt;
                }
            }

            Pt vec1 = *pbr - *ptr;
            Pt vec2 = *ptr - *ptl;
            Pt nrm1 = Cross(vec1, vec2);
            nrm1.Normalize();
            float dist1 = Dot(*pbl - *ptl, nrm1);
            if (dist1 > 0.1)
            {
                if (m_rect.w > m_rect.h)
                {
                    m_tiles = new Tile[2] {
                         Tile(m_buffer, Rect(m_rect.x, m_rect.y, m_rect.w / 2, m_rect.h)),
                         Tile(m_buffer, Rect(m_rect.x + m_rect.w / 2, m_rect.y, m_rect.w / 2, m_rect.h)) };
                }
                else
                {
                    m_tiles = new Tile[2]{
                         Tile(m_buffer, Rect(m_rect.x, m_rect.y, m_rect.w, m_rect.h / 2)),
                         Tile(m_buffer, Rect(m_rect.x, m_rect.y + m_rect.h / 2, m_rect.w, m_rect.h / 2)) };
                }

                for (int tIdx = 0; tIdx < 2; ++tIdx)
                {
                    m_tiles[tIdx].Process(quads);
                }
            }
            else
            {
                Quad q;
                q.pt[0] = *ptl;
                q.pt[1] = *ptr;
                q.pt[2] = *pbl;
                q.pt[3] = *pbr;
                quads.push_back(q);
                /*
                wchar_t output[1024];
                wsprintf(output, L"Tile [%d, %d]\n", m_rect.w, m_rect.h);
                OutputDebugStringW(output);*/
            }
        }
    };

    __declspec (dllexport) void DepthMakePlanes(float* vals, Pt* outVertices, Pt* outTexCoords, int maxCount, int *outCount, int depthWidth, int depthHeight)
    {
        Buffer b;
        b.depthPths = (Pt*)vals;
        b.width = depthWidth;
        b.height = depthHeight;

        Rect top(0, 0, b.width, b.height);
        
        Tile t(b, top);
        std::vector<Quad> quads;
        t.Process(quads);
        size_t vIdx = 0;
        for (Quad& q : quads)
        {
            Pt rgb((float)std::rand() / RAND_MAX,
                (float)std::rand() / RAND_MAX,
                (float)std::rand() / RAND_MAX);

            outVertices[vIdx] = q.pt[0];
            outVertices[vIdx + 1] = q.pt[1];
            outVertices[vIdx + 2] = q.pt[2];
            outVertices[vIdx + 3] = q.pt[1];
            outVertices[vIdx + 4] = q.pt[3];
            outVertices[vIdx + 5] = q.pt[2];
            for (size_t idx = 0; idx < 6; ++idx)
            { 
                outTexCoords[vIdx + idx] = rgb;
            }

            vIdx += 6;
        }
        
        *outCount = quads.size() * 6;
    }
}