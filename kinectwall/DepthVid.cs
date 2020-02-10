using System;
using System.Threading;
using System.Runtime.InteropServices;
using OpenTK.Graphics.ES30;
using OpenTK;
using K = Microsoft.Kinect;
using GLObjects;
using System.IO;


namespace kinectwall
{
    class DepthVid
    {
        private K.CoordinateMapper coordinateMapper = null;
        private K.DepthFrameReader depthFrameReader = null;
        private K.FrameDescription depthFrameDescription = null;
        private K.ColorFrameReader colorFrameReader = null;
        private K.FrameDescription colorFrameDescription = null;
        private byte[] colorBuffer = null;

        /// <summary>
        /// Intermediate storage for frame data converted to color
        /// </summary>
        private float[] depthPixels = null;
        private ushort[] depthVals = null;
        private K.ColorSpacePoint[] depthColors = null;
        private Vector3[] depthQuads = null;
        private Vector3[] depthTexColors = null;
        private Vector3[] depthNormals = null;
        private uint[] depthindices = null;
        float[] normvals = null;
        float[] depthCamPts = null;

        Program program;
        private VertexArray vertexArray;
        private VertexArray ptsVertexArray;
        /// <summary>
        /// Map depth range to byte range
        /// </summary>
        private const int MapDepthToByte = 8000 / 256;

        TextureFloat depthTexture;

        Thread depthThread;

        public Vector2? pickPt;

        public DepthVid(K.KinectSensor kinectSensor)
        {
            this.coordinateMapper = kinectSensor.CoordinateMapper;
            // open the reader for the depth frames
            this.depthFrameReader = kinectSensor.DepthFrameSource.OpenReader();
            // wire handler for frame arrival
            this.depthFrameReader.FrameArrived += this.Reader_FrameArrived;
            // get FrameDescription from DepthFrameSource
            this.depthFrameDescription = kinectSensor.DepthFrameSource.FrameDescription;

            this.colorFrameReader = kinectSensor.ColorFrameSource.OpenReader();
            this.colorFrameReader.FrameArrived += ColorFrameReader_FrameArrived;
            this.colorFrameDescription = kinectSensor.ColorFrameSource.FrameDescription;
            this.colorBuffer = new byte[colorFrameDescription.Width *
                colorFrameDescription.Height * 4];

            // allocate space to put the pixels being received and converted
            this.depthPixels = new float[this.depthFrameDescription.Width * this.depthFrameDescription.Height];
            this.depthVals = new ushort[this.depthFrameDescription.Width * this.depthFrameDescription.Height];
            this.depthColors = new K.ColorSpacePoint[this.depthFrameDescription.Width * this.depthFrameDescription.Height];
            this.depthQuads = new Vector3[this.depthFrameDescription.Width * this.depthFrameDescription.Height * 6];
            this.depthindices = new uint[this.depthFrameDescription.Width * this.depthFrameDescription.Height * 6];
            this.depthTexColors = new Vector3[this.depthFrameDescription.Width * this.depthFrameDescription.Height * 6];
            this.depthNormals = new Vector3[this.depthFrameDescription.Width * this.depthFrameDescription.Height * 6];
            for (int idx = 0; idx < depthindices.Length; ++idx)
                depthindices[idx] = (uint)idx;

            depthTexture = new TextureFloat();

            program = Program.FromFiles("Depth.vert", "Depth.frag");
            vertexArray = new VertexArray(program, _Quad, _Indices, _TexCoords, null);
            ptsVertexArray = new VertexArray(this.program, depthQuads, depthindices, depthTexColors, depthNormals);
            depthThread = new Thread(ProcessDepthThread);
            depthThread.Start();
        }

        private void ColorFrameReader_FrameArrived(object sender, K.ColorFrameArrivedEventArgs e)
        {
            bool colorFrameProcessed = false;

            using (K.ColorFrame colorFrame = e.FrameReference.AcquireFrame())
            {
                if (colorFrame != null)
                {
                    // the fastest way to process the body index data is to directly access 
                    // the underlying buffer
                    using (Microsoft.Kinect.KinectBuffer colorBuffer = colorFrame.LockRawImageBuffer())
                    {
                        // verify data and write the color data to the display bitmap
                        if (((this.colorFrameDescription.Width * this.colorFrameDescription.Height) == (colorBuffer.Size / this.colorFrameDescription.BytesPerPixel)))
                        {
                            colorFrame.CopyConvertedFrameDataToArray(
                                this.colorBuffer,
                                K.ColorImageFormat.Bgra);

                            // Note: In order to see the full range of color (including the less reliable far field color)
                            // we are setting maxcolor to the extreme potential color threshold
                            ushort maxcolor = ushort.MaxValue;

                            colorFrameProcessed = true;
                        }
                    }
                }
            }
        }

        IntPtr depthInfo = new IntPtr(0);
        TimeSpan depthFrameTime;
        uint depthDataSize;
        AutoResetEvent depthReady = new AutoResetEvent(false);
        bool isReady = false;
        bool isalive = true;
        void ProcessDepthThread(object obj)
        {
            FileStream fs = null;
            while (true)
            {
                isReady = true;
                depthReady.WaitOne();
                if (!isalive)
                    break;
                isReady = false;
                if (depthCamPts == null)
                    depthCamPts = new float[this.depthFrameDescription.Width * this.depthFrameDescription.Height * 3];
                if (normvals == null)
                    normvals = new float[this.depthFrameDescription.Width * this.depthFrameDescription.Height * 3];

                uint ptsSize = depthDataSize * 6;
                IntPtr dapthCamPtsPtr = Marshal.AllocHGlobal((int)ptsSize);
                IntPtr outNormals = Marshal.AllocHGlobal((int)ptsSize);
                this.coordinateMapper.MapDepthFrameToCameraSpaceUsingIntPtr(depthInfo, 
                    depthDataSize, dapthCamPtsPtr, ptsSize);
                if (App.Recording)
                {
                    if (fs == null) fs = new FileStream("depth.out", FileMode.Create, FileAccess.Write);
                    byte[] timebytes = BitConverter.GetBytes(depthFrameTime.Ticks);
                    fs.Write(timebytes, 0, timebytes.Length);

                    byte[] bytes = new byte[ptsSize];
                    Marshal.Copy(dapthCamPtsPtr, bytes, 0, bytes.Length);
                    fs.Write(bytes, 0, bytes.Length);
                }
                Vector2 ppt = this.pickPt != null ? this.pickPt.Value : new Vector2(-1, 1);
                DepthFindNormals(dapthCamPtsPtr, outNormals, ppt.X, ppt.Y,
                    this.depthFrameDescription.Width, this.depthFrameDescription.Height);
                Marshal.Copy(outNormals, normvals, 0, normvals.Length);
                Marshal.Copy(dapthCamPtsPtr, depthCamPts, 0, normvals.Length);
                Marshal.FreeHGlobal(dapthCamPtsPtr);
                Marshal.FreeHGlobal(outNormals);

                this.coordinateMapper.MapDepthFrameToColorSpaceUsingIntPtr(depthInfo, depthDataSize,
                    depthColors);
            }

            fs?.Close();
        }

        public void Close()
        {
            isalive = false;
            depthReady.Set();
            while (depthThread.IsAlive) ;
        }


        [DllImport("ptslib.dll")]
        public static extern void DepthFindEdges(IntPtr pDepthBuffer, IntPtr pOutNormals, int depthWidth, int depthHeight);

        [DllImport("ptslib.dll")]
        public static extern void DepthFindNormals(IntPtr pDepthPts, IntPtr pOutNormals, float px, float py, int depthWidth, int depthHeight);
        
        [DllImport("msvcrt.dll", EntryPoint = "memcpy",
        CallingConvention = CallingConvention.Cdecl, SetLastError = false)]
        public static extern IntPtr memcpy(IntPtr dest, IntPtr src, UIntPtr count);

        /// <summary>
        /// Directly accesses the underlying image buffer of the DepthFrame to 
        /// create a displayable bitmap.
        /// This function requires the /unsafe compiler option as we make use of direct
        /// access to the native memory pointed to by the depthFrameData pointer.
        /// </summary>
        /// <param name="depthFrameData">Pointer to the DepthFrame image data</param>
        /// <param name="depthFrameDataSize">Size of the DepthFrame image data</param>
        /// <param name="minDepth">The minimum reliable depth value for the frame</param>
        /// <param name="maxDepth">The maximum reliable depth value for the frame</param>
        private void ProcessDepthFrameData(IntPtr depthFrameData, uint depthFrameDataSize, ushort minDepth, ushort maxDepth,
            TimeSpan relativeTime)
        {
            if (depthInfo == new IntPtr(0))
            {
                this.depthInfo = Marshal.AllocHGlobal((int)depthFrameDataSize);
                this.depthDataSize = depthFrameDataSize;
            }
            if (isReady)
            {
                memcpy(depthInfo, depthFrameData, (UIntPtr)depthFrameDataSize);
                this.depthFrameTime = relativeTime;
                depthReady.Set();
            }
        }

        /// <summary>
        /// Handles the depth frame data arriving from the sensor
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void Reader_FrameArrived(object sender, K.DepthFrameArrivedEventArgs e)
        {
            using (K.DepthFrame depthFrame = e.FrameReference.AcquireFrame())
            {
                if (depthFrame != null)
                {
                    // the fastest way to process the body index data is to directly access 
                    // the underlying buffer
                    using (Microsoft.Kinect.KinectBuffer depthBuffer = depthFrame.LockImageBuffer())
                    {
                        // verify data and write the color data to the display bitmap
                        if (((this.depthFrameDescription.Width * this.depthFrameDescription.Height) == (depthBuffer.Size / this.depthFrameDescription.BytesPerPixel)))
                        {
                            // Note: In order to see the full range of depth (including the less reliable far field depth)
                            // we are setting maxDepth to the extreme potential depth threshold
                            ushort maxDepth = ushort.MaxValue;

                            // If you wish to filter by reliable depth distance, uncomment the following line:
                            //// maxDepth = depthFrame.DepthMaxReliableDistance                            
                            this.ProcessDepthFrameData(depthBuffer.UnderlyingBuffer, depthBuffer.Size, depthFrame.DepthMinReliableDistance, maxDepth,
                                e.FrameReference.RelativeTime);
                        }
                    }
                }
            }
        }

        public void Render(Matrix4 viewProj)
        {
            GL.UseProgram(program.ProgramName);
            GL.UniformMatrix4(program.LocationMVP, false, ref viewProj);
            int depthQuadIdx = 0;
            if (depthCamPts == null)
                return;
            int npts = depthCamPts.Length / 3;
            for (int idx = 0; idx < npts; ++idx)
            {
                if (!(float.IsInfinity(depthCamPts[idx * 3])))
                {
                    Vector3 dpth = new Vector3(depthCamPts[idx * 3],
                        depthCamPts[idx * 3 + 1],
                        depthCamPts[idx * 3 + 2]);

                    Vector3 texCol = Vector3.Zero;
                    K.ColorSpacePoint csp = this.depthColors[idx];
                    if (csp.X >= 0 && csp.Y >= 0 && csp.X < colorFrameDescription.Width &&
                        csp.Y < colorFrameDescription.Height)
                    {
                        int offset = (colorFrameDescription.Width * (int)csp.Y + (int)csp.X) * 4;
                        byte b = colorBuffer[offset];
                        byte g = colorBuffer[offset + 1];
                        byte r = colorBuffer[offset + 2];
                        texCol = new Vector3((float)r / 255.0f, (float)g / 255.0f, (float)b / 255.0f);
                    }

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
            ptsVertexArray.Draw(depthQuadIdx);
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
