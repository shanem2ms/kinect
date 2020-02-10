using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.ComponentModel;
using OpenTK.Graphics.ES30;
using OpenTK;
using K=Microsoft.Kinect;
using System.Windows.Input;

namespace kinectwall
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {

        /// <summary>
        /// Radius of drawn hand circles
        /// </summary>
        private const double HandSize = 30;

        /// <summary>
        /// Thickness of drawn joint lines
        /// </summary>
        private const double JointThickness = 3;

        /// <summary>
        /// Thickness of clip edge rectangles
        /// </summary>
        private const double ClipBoundsThickness = 10;

        /// <summary>
        /// Constant for clamping Z values of camera space points from being negative
        /// </summary>
        private const float InferredZPositionClamp = 0.1f;

        /// <summary>
        /// Active Kinect sensor
        /// </summary>
        private K.KinectSensor kinectSensor = null;

        /// <summary>
        /// Reader for body frames
        /// </summary>
        private K.BodyFrameReader bodyFrameReader = null;

        /// <summary>
        /// Array for the bodies
        /// </summary>
        private K.Body[] bodies = null;
        TimeSpan BodyFrameTime;

        /// <summary>
        /// Width of display (depth space)
        /// </summary>
        private int displayWidth;

        /// <summary>
        /// Height of display (depth space)
        /// </summary>
        private int displayHeight;

        BodyViz bodyViz = null;
        RoomViz roomViz = null;
        Matrix4 projectionMat;
        Matrix4 viewMat;

        DepthVid depthVid = null;
        /// <summary>
        /// Initializes a new instance of the MainWindow class.
        /// </summary>
        public MainWindow()
        {
            // one sensor is currently supported
            this.kinectSensor = K.KinectSensor.GetDefault();

            // get the depth (display) extents
            K.FrameDescription frameDescription = this.kinectSensor.DepthFrameSource.FrameDescription;

            // get size of joint space
            this.displayWidth = frameDescription.Width;
            this.displayHeight = frameDescription.Height;

            // open the reader for the body frames
            this.bodyFrameReader = this.kinectSensor.BodyFrameSource.OpenReader();

            // set IsAvailableChanged event notifier
            this.kinectSensor.IsAvailableChanged += this.Sensor_IsAvailableChanged;

            // open the sensor
            this.kinectSensor.Open();

            // use the window object as the view model in this simple example
            this.DataContext = this;

            // initialize the components (controls) of the window
            this.InitializeComponent();
        }

        System.Drawing.Point? mouseDownPt;
        System.Drawing.Point? pickPt;
        float xRot = 0.0f;
        float xRotDn;

        float yRot = (float).72;
        float yRotDn;
        private void GlControl_MouseUp(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            mouseDownPt = null;
        }

        private void GlControl_MouseMove(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            System.Drawing.Point curPt = e.Location;
            if (mouseDownPt != null)
            {
                yRot = yRotDn + (float)(curPt.X - mouseDownPt.Value.X) * 0.001f;
                xRot = xRotDn + (float)(curPt.Y - mouseDownPt.Value.Y) * 0.001f;
                glControl.Invalidate();
            }
        }

        private void GlControl_MouseDown(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            if (System.Windows.Input.Keyboard.IsKeyDown(Key.LeftCtrl))
            {
                pickPt = e.Location;
                Vector2 ipt = new Vector2((float)pickPt.Value.X / (float)glControl.ClientSize.Width,
                    (float)pickPt.Value.Y / (float)glControl.ClientSize.Height);
                this.depthVid.pickPt = ipt;
            }
            else
            {
                mouseDownPt = e.Location;
                yRotDn = yRot;
                xRotDn = xRot;
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (this.bodyFrameReader != null)
            {
                this.bodyFrameReader.FrameArrived += this.Reader_FrameArrived;
            }

            bodyViz = new BodyViz();
            roomViz = new RoomViz();
            depthVid = new DepthVid(this.kinectSensor);
            this.projectionMat = Matrix4.CreatePerspectiveFieldOfView(60 * (float)Math.PI / 180.0f, 1, 0.5f, 50.0f);
            //this.projectionMat = Matrix4.CreatePerspectiveFieldOfView(60 * (float)Math.PI / 180.0f, 1.0f, 0.5f, 100.0f);
            
            //this.viewMat = Matrix4.LookAt(new Vector3(0, 0, -2), new Vector3(0, 0, 0), new Vector3(0, 1, 0));
            glControl.Paint += GlControl_Paint;
            glControl.MouseDown += GlControl_MouseDown;
            glControl.MouseMove += GlControl_MouseMove;
            glControl.MouseUp += GlControl_MouseUp;
        }
        Vector3 curPos = Vector3.Zero;
        protected override void OnKeyDown(KeyEventArgs e)
        {
            Quaternion qy = new Quaternion(Vector3.UnitY, yRot);
            Vector3 zdir = Vector3.Transform(-Vector3.UnitZ, qy).Normalized();
            Vector3 xdir = Vector3.Cross(Vector3.UnitY, zdir).Normalized();
            Quaternion qx = new Quaternion(xdir, xRot);
            Quaternion q = qy * qx;
            Vector3 zd = (q * Vector3.UnitZ).Normalized();
            Vector3 xd = (q * Vector3.UnitX).Normalized();

            switch (e.Key)
            {
                case Key.W:
                    curPos -= zd * 0.1f;
                    break;
                case Key.A:
                    curPos += xd * 0.1f;
                    break;
                case Key.S:
                    curPos += zd * 0.1f;
                    break;
                case Key.D:
                    curPos -= xd * 0.1f;
                    break;
                case Key.R:
                    App.Recording = !App.Recording;
                    break;
            }
            glControl.Invalidate();
            base.OnKeyDown(e);
        }

        private void GlControl_Paint(object sender, System.Windows.Forms.PaintEventArgs e)
        {
            GL.Clear(ClearBufferMask.ColorBufferBit);
            GL.Clear(ClearBufferMask.DepthBufferBit);
            GL.Enable(EnableCap.Blend);
            GL.Enable(EnableCap.DepthTest);

            //GL.Enable(EnableCap.CullFace);
            //GL.CullFace(CullFaceMode.Front);
            GL.BlendFunc(BlendingFactorSrc.One, BlendingFactorDest.OneMinusSrcAlpha);

            Quaternion qy = new Quaternion(Vector3.UnitY, yRot);
            Vector3 zdir = Vector3.Transform(-Vector3.UnitZ, qy).Normalized();
            Vector3 xdir = Vector3.Cross(Vector3.UnitY, zdir).Normalized();
            Quaternion qx = new Quaternion(xdir, xRot);
            Quaternion q = qy * qx;
            Matrix4 rot =  Matrix4.CreateFromQuaternion(q);

            Matrix4 lookTrans = Matrix4.CreateScale(-1, -1, 1) * rot * Matrix4.CreateTranslation(curPos);
            this.viewMat = lookTrans.Inverted();

            Matrix4 viewProj = viewMat * projectionMat;
            depthVid.Render(viewProj);
            roomViz.Render(viewProj);
            bodyViz.Render(viewProj, this.bodies, BodyFrameTime);
            glControl.SwapBuffers();
        }

        private void glControl_Resize(object sender, EventArgs e)
        {
            GL.Viewport(0, 0, this.glControl.ClientRectangle.Width,
                this.glControl.ClientRectangle.Height);
        }

        /// <summary>
        /// Vertex position array.
        /// </summary>
        private static readonly float[] _ArrayPosition = new float[] {
			0.0f, 0.0f,
			0.5f, 1.0f,
			1.0f, 0.0f
		};

		/// <summary>
		/// Vertex color array.
		/// </summary>
		private static readonly float[] _ArrayColor = new float[] {
			1.0f, 0.0f, 0.0f,
			0.0f, 1.0f, 0.0f,
			0.0f, 0.0f, 1.0f
		};

        /// <summary>
        /// Execute shutdown tasks
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void MainWindow_Closing(object sender, CancelEventArgs e)
        {
            depthVid.Close();
            if (this.bodyFrameReader != null)
            {
                // BodyFrameReader is IDisposable
                this.bodyFrameReader.Dispose();
                this.bodyFrameReader = null;
            }

            if (this.kinectSensor != null)
            {
                this.kinectSensor.Close();
                this.kinectSensor = null;
            }
        }

        /// <summary>
        /// Handles the body frame data arriving from the sensor
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void Reader_FrameArrived(object sender, K.BodyFrameArrivedEventArgs e)
        {
            using (K.BodyFrame bodyFrame = e.FrameReference.AcquireFrame())
            {
                if (bodyFrame != null)
                {
                    if (this.bodies == null)
                    {
                        this.bodies = new K.Body[bodyFrame.BodyCount];
                    }

                    // The first time GetAndRefreshBodyData is called, Kinect will allocate each Body in the array.
                    // As long as those body objects are not disposed and not set to null in the array,
                    // those body objects will be re-used.
                    bodyFrame.GetAndRefreshBodyData(this.bodies);
                    this.BodyFrameTime = e.FrameReference.RelativeTime;
                    glControl.Invalidate();
                }
            }

        }

        /// <summary>
        /// Handles the event which the sensor becomes unavailable (E.g. paused, closed, unplugged).
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void Sensor_IsAvailableChanged(object sender, K.IsAvailableChangedEventArgs e)
        {
        }

    }
}
