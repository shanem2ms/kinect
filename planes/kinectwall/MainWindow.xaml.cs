using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.ComponentModel;
using OpenTK.Graphics.ES30;
using OpenTK;
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
        /// Width of display (depth space)
        /// </summary>
        private int displayWidth;

        /// <summary>
        /// Height of display (depth space)
        /// </summary>
        private int displayHeight;

        RoomViz roomViz = null;
        BodyViz bodyViz = null;
        Matrix4 projectionMat;
        Matrix4 viewMat = Matrix4.Identity;
        CharViz character;

        DepthVid depthVid = null;
        /// <summary>
        /// Initializes a new instance of the MainWindow class.
        /// </summary>
        public MainWindow()
        {
            // get size of joint space
            this.displayWidth = 1024;
            this.displayHeight = 768;

            // use the window object as the view model in this simple example
            this.DataContext = this;

            // initialize the components (controls) of the window
            this.InitializeComponent();
        }

        System.Drawing.Point? mouseDownPt;
        System.Drawing.Point? pickPt;
        float xRot = 0.0f;
        float xRotDn;

        float yRot = 0;
        float yRotDn;

        Matrix4 rotMatrix = Matrix4.CreateRotationY((float)Math.PI);
        Matrix4 rotMatrixDn;
        private void GlControl_MouseUp(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            mouseDownPt = null;
        }

        private void GlControl_MouseMove(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            System.Drawing.Point curPt = e.Location;
            if (mouseDownPt != null)
            {
                yRot = (float)(curPt.X - mouseDownPt.Value.X) * 0.001f;
                xRot = (float)(curPt.Y - mouseDownPt.Value.Y) * 0.001f;

                Vector3 xd = Vector3.TransformNormal(Vector3.UnitX, rotMatrixDn).Normalized();
                Vector3 yd = Vector3.TransformNormal(Vector3.UnitY, rotMatrixDn).Normalized();

                Quaternion qy = new Quaternion(xd, yRot);
                Quaternion qx = new Quaternion(yd, xRot);
                Quaternion q = qy * qx;
                Matrix4 rotdiff =
                    Matrix4.CreateRotationX(xRot) *
                    Matrix4.CreateRotationY(yRot);
                this.rotMatrix = this.rotMatrixDn * rotdiff;

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
                this.depthVid.PickPt = ipt;
            }
            else
            {
                mouseDownPt = e.Location;
                this.rotMatrixDn = this.rotMatrix;
                yRotDn = yRot;
                xRotDn = xRot;
            }
        }
        System.Timers.Timer t = new System.Timers.Timer();
        KinectData.BodyData bodyData;
        long bodyTimeStart = 0;
        long bodyTimeLength = 0;

        KinectBody liveBodies;

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            roomViz = new RoomViz();
            if (App.DepthFile != null)
                depthVid = new DepthVid();
            if (App.BodyFile != null)
            {
                bodyData = new KinectData.BodyData(App.BodyFile);
                var tr = bodyData.TimeRange;
                bodyTimeStart = tr.Item1;
                bodyTimeLength = tr.Item2 - bodyTimeStart;
            }
            bodyViz = new BodyViz();

            character = new CharViz(App.CharacterFile);

            this.projectionMat = Matrix4.CreatePerspectiveFieldOfView(60 * (float)Math.PI / 180.0f, 1, 0.5f, 50.0f);
            //this.projectionMat = Matrix4.CreatePerspectiveFieldOfView(60 * (float)Math.PI / 180.0f, 1.0f, 0.5f, 100.0f);

            //this.viewMat = Matrix4.LookAt(new Vector3(0, 0, -2), new Vector3(0, 0, 0), new Vector3(0, 1, 0));
            glControl.Paint += GlControl_Paint;
            glControl.MouseDown += GlControl_MouseDown;
            glControl.MouseMove += GlControl_MouseMove;
            glControl.MouseUp += GlControl_MouseUp;

            t.Elapsed += T_Elapsed;
            t.AutoReset = true;
            t.Interval = 10;
            t.Start();
        }

        private void T_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            glControl.Invalidate();
        }

        Vector3 curPos = Vector3.Zero;
        int visibleBits = 1;
        KinectData.JointType jtSelected = 0;
        protected override void OnKeyDown(KeyEventArgs e)
        {
            //Quaternion qy = new Quaternion(Vector3.UnitY, yRot);
            Matrix4 viewInv = this.viewMat.Inverted();
            Vector3 zd = Vector3.TransformNormal(Vector3.UnitZ, viewInv).Normalized();
            Vector3 xd = Vector3.TransformNormal(Vector3.UnitX, viewInv).Normalized();
            Vector3 yd = Vector3.TransformNormal(Vector3.UnitY, viewInv).Normalized();

            switch (e.Key)
            {
                case Key.W:
                    curPos -= zd * 0.1f;
                    break;
                case Key.A:
                    curPos -= xd * 0.1f;
                    break;
                case Key.S:
                    curPos += zd * 0.1f;
                    break;
                case Key.D:
                    curPos += xd * 0.1f;
                    break;
                case Key.Q:
                    curPos += yd * 0.1f;
                    break;
                case Key.Z:
                    curPos -= yd * 0.1f;
                    break;
                case Key.O:
                    framerate *= 2;
                    break;
                case Key.P:
                    framerate /= 2;
                    break;
                case Key.L:
                    live = !live;
                    break;
                case Key.R:
                    App.Recording = !App.Recording;
                    break;
                case Key.Space:
                    isPlaying = !isPlaying;
                    if (depthVid != null) depthVid.isPlaying = !depthVid.isPlaying;
                    break;
                case Key.Right:
                    if (!isPlaying) frametime += framerate;
                    break;
                case Key.Left:
                    if (!isPlaying) frametime -= framerate;
                    break;
                case Key.B:
                    visibleBits = (visibleBits + 1) % 4;
                    break;
                case Key.M:
                    curFrame.DumpDebugInfo();
                    break;
                case Key.NumPad1:
                    jtSelected = (KinectData.JointType)(((int)jtSelected + 1) % 24);
                    System.Diagnostics.Debug.WriteLine($"{jtSelected}");
                    break;

            }
            glControl.Invalidate();
            base.OnKeyDown(e);
        }

        bool isPlaying = true;
        long frametime = 0;
        long framerate = 10000000 / 60;
        KinectData.Frame curFrame = null;
        bool live = false;
        private void GlControl_Paint(object sender, System.Windows.Forms.PaintEventArgs e)
        {
            if (live && liveBodies == null)
                liveBodies = new KinectBody();

            GL.Clear(ClearBufferMask.ColorBufferBit);
            GL.Clear(ClearBufferMask.DepthBufferBit);
            GL.Enable(EnableCap.Blend);
            GL.Enable(EnableCap.DepthTest);

            //GL.Enable(EnableCap.CullFace);
            //GL.CullFace(CullFaceMode.Front);
            GL.BlendFunc(BlendingFactorSrc.One, BlendingFactorDest.OneMinusSrcAlpha);

            Matrix4 lookTrans = rotMatrix.Inverted() * Matrix4.CreateTranslation(curPos);
            this.viewMat = lookTrans.Inverted();

            Matrix4 viewProj = viewMat * projectionMat;
            roomViz.Render(viewProj);
            long timeStamp = 0;
            if (depthVid != null)
                timeStamp = depthVid.Render(viewProj);
            if (live)
                curFrame = liveBodies.CurrentFrame;
            else
                curFrame = bodyData?.GetInterpolatedFrame(bodyTimeStart +
                    (frametime % bodyTimeLength));
            if (jtSelected > 0)
                curFrame.SetJointColor(jtSelected, new Vector3(1, 1, 0));
            if (curFrame != null && (visibleBits & 1) != 0)
                bodyViz.Render(curFrame, viewProj, timeStamp);
            if ((visibleBits & 2) != 0)
                character.Render(curFrame, viewProj);
            glControl.SwapBuffers();
            
            if (isPlaying) frametime += framerate;
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
        }


    }
}
