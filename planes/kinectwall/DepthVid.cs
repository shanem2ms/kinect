using System;
using System.Threading;
using System.Runtime.InteropServices;
using OpenTK.Graphics.ES30;
using OpenTK;
using GLObjects;
using System.IO;

namespace kinectwall
{
    class DepthVid
    {
        private byte[] colorBuffer = null;

        /// <summary>
        /// Intermediate storage for frame data converted to color
        /// </summary>
        private float[] depthPixels = null;
        private ushort[] depthVals = null;
        private Vector3[] depthQuads = null;
        private Vector3[] depthTexColors = null;
        private Vector3[] depthNormals = null;
        private uint[] depthindices = null;
        float[] normvals = null;
        float[] depthCamPts = null;

        Program program;
        Program programPlanes;
        private VertexArray vertexArray;
        private VertexArray ptsVertexArray;
        private VertexArray genVertexArray;
        /// <summary>
        /// Map depth range to byte range
        /// </summary>
        private const int MapDepthToByte = 8000 / 256;

        TextureFloat depthTexture;
        public bool needRefresh = false;
        Vector2? pickPt;
        public Vector2? PickPt
        {
            get => pickPt;
            set
            {
                pickPt = value;
                if (value.HasValue)
                    FindDepthPt(value.Value);
                needRefresh = true;
            }
        }


        int pickXPt = -1;
        int pickYPt = -1;

        public DepthVid()
        {
            // allocate space to put the pixels being received and converted
            this.depthPixels = new float[dWidth * dHeight];
            this.depthVals = new ushort[dWidth * dHeight];
            this.depthQuads = new Vector3[dWidth * dHeight * 6];
            this.depthindices = new uint[dWidth * dHeight * 6];
            this.depthTexColors = new Vector3[dWidth * dHeight * 6];
            this.depthNormals = new Vector3[dWidth * dHeight * 6];
            for (int idx = 0; idx < depthindices.Length; ++idx)
                depthindices[idx] = (uint)idx;

            depthTexture = new TextureFloat();

            program = Program.FromFiles("Depth.vert", "Depth.frag");
            programPlanes = Program.FromFiles("Planes.vert", "Planes.frag");
            vertexArray = new VertexArray(program, _Quad, _Indices, _TexCoords, null);
            ptsVertexArray = new VertexArray(this.program, depthQuads, depthindices, depthTexColors, depthNormals);
            genVertexArray = new VertexArray(this.programPlanes, depthQuads, depthindices, depthTexColors, null);
        }


        void CopyToVector(float[] floats, Vector3[] vectors)
        {
            for (int vecIdx = 0; vecIdx < vectors.Length; ++vecIdx)
            {
                vectors[vecIdx].X = floats[vecIdx * 3];
                vectors[vecIdx].Y = floats[vecIdx * 3 + 1];
                vectors[vecIdx].Z = floats[vecIdx * 3 + 2];
            }
        }

        [DllImport("ptslib.dll")]
        public static extern void DepthFindEdges(IntPtr pDepthBuffer, IntPtr pOutNormals, int depthWidth, int depthHeight);

        [DllImport("ptslib.dll")]
        public static extern void DepthFindNormals(IntPtr pDepthPts, IntPtr pOutNormals, int ptx, int pty, int depthWidth, int depthHeight);

        [DllImport("ptslib.dll")]
        public static extern void DepthMakePlanes(IntPtr pDepthPts, IntPtr pOutVertices, IntPtr pOutTexCoords, int numVertices, out int vertexCnt,
            int px, int py, int depthWidth, int depthHeight);

        [DllImport("msvcrt.dll", EntryPoint = "memcpy",
        CallingConvention = CallingConvention.Cdecl, SetLastError = false)]
        public static extern IntPtr memcpy(IntPtr dest, IntPtr src, UIntPtr count);

        const int dWidth = 512;
        const int dHeight = 424;
        byte[][] depthPtBytes;
        long[] timeStamps;
        IntPtr depthPtsPtr;
        IntPtr depthNrmPtr;
        IntPtr genVerticesPtr;
        IntPtr genTexCoordsPtr;
        float[] genverticesf;
        float[] gentexcoordsf;
        Vector3[] genvertices;
        Vector3[] gentexcoords;
        int genCount;
        int depthQuadCount;
        int numframes = 0;

        bool first = true;

        public bool isPlaying = false;

        int frameIdx = 0;
        int lastFrame = -1;
        const int bytesPerFrame = dWidth * dHeight * 3 * 4;
        const int maxQuads = 1000;
        const int bytesPerQuad = 4 * 3 * 4;
        Matrix4 lastViewProj;
        public long Render(Matrix4 viewProj)
        {
            if (first)
            {
                depthPtsPtr = Marshal.AllocHGlobal(bytesPerFrame);
                depthNrmPtr = Marshal.AllocHGlobal(bytesPerFrame);
                genVerticesPtr = Marshal.AllocHGlobal(depthQuads.Length * 12);
                genTexCoordsPtr = Marshal.AllocHGlobal(depthQuads.Length * 12);
                FileStream fs = new FileStream(App.DepthFile, FileMode.Open, FileAccess.Read);
                numframes = (int)fs.Length / (bytesPerFrame + 8);
                depthPtBytes = new byte[numframes][];
                timeStamps = new long[numframes];
                byte[] timestampbytes = new byte[8];
                for (int idx = 0; idx < depthPtBytes.Length; ++idx)
                {
                    fs.Read(timestampbytes, 0, timestampbytes.Length);
                    timeStamps[idx] = BitConverter.ToInt64(timestampbytes, 0);
                    depthPtBytes[idx] = new byte[bytesPerFrame];
                    fs.Read(depthPtBytes[idx], 0, bytesPerFrame);
                }
                normvals = new float[dWidth * dHeight * 3];
                depthCamPts = new float[dWidth * dHeight * 3];
                fs.Close();
                genverticesf = new float[depthQuads.Length * 3];
                gentexcoordsf = new float[depthQuads.Length * 3];
                genvertices = new Vector3[depthQuads.Length];
                gentexcoords = new Vector3[depthQuads.Length];
                first = false;
            }

            if (frameIdx != lastFrame || needRefresh)
            {

                Marshal.Copy(depthPtBytes[frameIdx % numframes], 0, depthPtsPtr, depthPtBytes[frameIdx % numframes].Length);
                DepthFindNormals(depthPtsPtr, depthNrmPtr, this.pickXPt, this.pickYPt, dWidth, dHeight);
                DepthMakePlanes(depthPtsPtr, genVerticesPtr, genTexCoordsPtr, depthQuads.Length, out genCount,
                    this.pickXPt, this.pickYPt, dWidth, dHeight);

                Marshal.Copy(genVerticesPtr, genverticesf, 0, genvertices.Length);
                CopyToVector(genverticesf, genvertices);
                Marshal.Copy(genTexCoordsPtr, gentexcoordsf, 0, gentexcoords.Length);
                CopyToVector(gentexcoordsf, gentexcoords);
                genVertexArray.UpdatePositions(genvertices);
                genVertexArray.UpdateTexCoords(gentexcoords);

                Marshal.Copy(depthNrmPtr, normvals, 0, normvals.Length);
                Marshal.Copy(depthPtsPtr, depthCamPts, 0, depthCamPts.Length);


                int depthQuadIdx = 0;
                int npts = depthCamPts.Length / 3;
                for (int idx = 0; idx < npts; ++idx)
                {
                    if (!(float.IsInfinity(depthCamPts[idx * 3])))
                    {
                        Vector3 dpth = new Vector3(depthCamPts[idx * 3],
                            depthCamPts[idx * 3 + 1],
                            depthCamPts[idx * 3 + 2]);

                        Vector3 texCol = Vector3.Zero;

                        for (int qIdx = 0; qIdx < _Quad.Length; ++qIdx)
                        {
                            depthNormals[depthQuadIdx + qIdx] = new Vector3(normvals[idx * 3],
                                normvals[idx * 3 + 1],
                                normvals[idx * 3 + 2]);
                            depthQuads[depthQuadIdx + qIdx] = dpth + _Quad[qIdx] * 0.005f;
                            depthTexColors[depthQuadIdx + qIdx] = texCol;
                        }
                        depthQuadIdx += _Quad.Length;
                    }

                }
                ptsVertexArray.UpdateNormals(depthNormals);
                ptsVertexArray.UpdateTexCoords(depthTexColors);
                ptsVertexArray.UpdatePositions(depthQuads);
                this.depthQuadCount = depthQuadIdx;
                lastFrame = frameIdx;
                needRefresh = false;
            }

            programPlanes.Use(0);
            programPlanes.SetMat4("uMVP", ref viewProj);
            genVertexArray.Draw(0, genCount);
            /*
            GL.UseProgram(program.ProgramName);
            GL.UniformMatrix4(program.LocationMVP, false, ref viewProj);

            ptsVertexArray.Draw(depthQuadCount);*/
            lastViewProj = viewProj;
            long timestamp = timeStamps[frameIdx % numframes];
            if (isPlaying)
                frameIdx++;
            return timestamp;
        }

        void FindDepthPt(Vector2 pt)
        {
            float[] pts = new float[dWidth * dHeight * 3];
            IntPtr tmpBuf = Marshal.AllocHGlobal(depthPtBytes[frameIdx % numframes].Length);
            Marshal.Copy(depthPtBytes[frameIdx % numframes], 0, tmpBuf, depthPtBytes[frameIdx % numframes].Length);
            Marshal.Copy(tmpBuf, pts, 0, pts.Length);
            Marshal.FreeHGlobal(tmpBuf);
            pt = (pt - new Vector2(0.5f, 0.5f)) * 2;
            pt.Y = -pt.Y;
            float minLenSq = float.MaxValue;
            int minx = -1, miny = -1;
            pickXPt = pickYPt = -1;
            for (int y = 0; y < dHeight; ++y)
            {
                for (int x = 0; x < dWidth; ++x)
                {
                    int flIdx = (y * dWidth + x) * 3;
                    Vector4 dpt = new Vector4(pts[flIdx], pts[flIdx + 1],
                        pts[flIdx + 2], 1);
                    if (float.IsInfinity(dpt.X))
                        continue;
                    Vector4 screenPt = Vector4.Transform(dpt, lastViewProj);
                    Vector2 dspt = new Vector2(screenPt.X / screenPt.W, screenPt.Y / screenPt.W);
                    float lnsq = (dspt - pt).LengthSquared;
                    if (lnsq < minLenSq)
                    {
                        minx = x;
                        miny = y;
                        minLenSq = lnsq;
                    }
                }
            }
            this.pickXPt = minx;
            this.pickYPt = miny;
        }


        private static readonly Vector3[] _Quad = new Vector3[] {
            new Vector3(1.0f, 0.0f, 0.0f),  // 0 
            new Vector3(0.0f, 0.0f, 0.0f),  // 1
            new Vector3(0.0f, 1.0f, 0.0f),  // 2

            new Vector3(1.0f, 0.0f, 0.0f),  // 0 
            new Vector3(0.0f, 1.0f, 0.0f),  // 2
            new Vector3(1.0f, 1.0f, 0.0f)  // 3 
        };

        private static readonly uint[] _Indices = new uint[]
        {
            0,1,2,3,4,5
        };


        private static readonly Vector3[] _TexCoords = new Vector3[] {
            new Vector3(0.0f, 1.0f, 0.0f),  // 0 
            new Vector3(1.0f, 1.0f, 0.0f),  // 1
            new Vector3(1.0f, 0.0f, 0.0f),  // 2

            new Vector3(0.0f, 1.0f, 0.0f),  // 0 
            new Vector3(1.0f, 0.0f, 0.0f),  // 2
            new Vector3(0.0f, 0.0f, 0.0f)  // 3 
        };
    }
}
