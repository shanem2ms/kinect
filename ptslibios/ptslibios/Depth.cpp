// dllmain.cpp : Defines the entry point for the DLL application.
#include "pch.h"
#include <cmath>
#include <vector>
#include <map>
#include <memory>
#include <algorithm>
#include "Pt.h"

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

    Pt* tmpNrm = nullptr;
    static float threshhold = .75;
    __declspec (dllexport) void DepthFindNormals(float* vals, float* outpts, int px, int py, int depthWidth, int depthHeight)
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
            Pt* outNrm = (Pt*)outpts;
            for (int y = 1; y < depthHeight - 1; ++y)
            {
                for (int x = 1; x < depthWidth - 1; ++x)
                {
                    outNrm[y * depthWidth + x] = Pt(0.4f, 0.4f, 0.4f);
                }
            }

            outNrm[py * depthWidth + px] = Pt(1, 1, 1);
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
}

