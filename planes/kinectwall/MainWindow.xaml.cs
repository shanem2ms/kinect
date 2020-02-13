using System;
using System.Linq;
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

        Scene scene = null;
        BodyViz bodyViz = null;
        Matrix4 projectionMat;
        Matrix4 viewMat = Matrix4.Identity;
        CharViz character;
        private GLObjects.Program pickProgram;
        BulletSimulation bulletSimulation;

        public enum Tools
        {
            Camera,
            Pick
        };

        Tools currentTool = Tools.Camera;
        public string[] ToolNames { get => Enum.GetNames(typeof(Tools)); }

        BulletDebugDraw[] bulletDebugDraw;
        public BulletDebugDraw[] BulletDraw { get => bulletDebugDraw; }

        DepthVid depthVid = null;

        /// <summary>
        /// Initializes a new instance of the MainWindow class.
        /// </summary>
        public MainWindow()
        {

            BulletSharp.DebugDrawModes[] dmodes = (BulletSharp.DebugDrawModes[])Enum.GetValues(typeof(BulletSharp.DebugDrawModes));
            bulletDebugDraw =
                dmodes.Select(dm => new BulletDebugDraw() { debugDrawMode = dm, 
                    OnCheckedChanged = OnBulletDebugCheckChange
                }).ToArray();
            // get size of joint space
            this.displayWidth = 1024;
            this.displayHeight = 768;

            // use the window object as the view model in this simple example
            this.DataContext = this;

            // initialize the components (controls) of the window
            this.InitializeComponent();
        }


        void OnBulletDebugCheckChange(BulletDebugDraw bdd)
        {
            if (bdd.IsChecked)
                bulletSimulation.DebugDraw |= bdd.debugDrawMode;
            else
            {
                bulletSimulation.DebugDraw &= ~bdd.debugDrawMode;
            }

            foreach (BulletDebugDraw bddc in bulletDebugDraw)
            {
                bddc.SetFromMask(bulletSimulation.DebugDraw);
            }
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
                if (e.Button == System.Windows.Forms.MouseButtons.Left)
                {
                    Matrix4 rotdiff =
                        Matrix4.CreateRotationX(xRot) *
                        Matrix4.CreateRotationY(yRot);
                    this.rotMatrix = this.rotMatrixDn * rotdiff;
                }
                else if (e.Button == System.Windows.Forms.MouseButtons.Right)
                {
                    this.rotMatrix = this.rotMatrixDn * Matrix4.CreateRotationZ(yRot);
                }
                glControl.Invalidate();
            }
        }

        private void GlControl_MouseDown(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            if (currentTool == Tools.Camera)
            {
                mouseDownPt = e.Location;
                this.rotMatrixDn = this.rotMatrix;
                yRotDn = yRot;
                xRotDn = xRot;
            }
            else if (currentTool == Tools.Pick)
            {
                pickPt = e.Location;
                Vector2 ipt = new Vector2((float)pickPt.Value.X / (float)glControl.ClientSize.Width,
                    (float)pickPt.Value.Y / (float)glControl.ClientSize.Height);
                DoPick();
            }
        }
        System.Timers.Timer t = new System.Timers.Timer();
        KinectData.BodyData bodyData;
        long bodyTimeStart = 0;
        long bodyTimeLength = 0;

        KinectBody liveBodies;

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            bulletSimulation = new BulletSimulation();
            pickProgram = GLObjects.Program.FromFiles("Pick.vert", "Pick.frag");
            if (App.DepthFile != null)
                depthVid = new DepthVid();
            if (App.BodyFile != null)
            {
                bodyData = new KinectData.BodyData(App.BodyFile);
                var tr = bodyData.TimeRange;
                bodyTimeStart = tr.Item1;
                bodyTimeLength = tr.Item2 - bodyTimeStart;
            }

            if (scene == null)
                scene = new Scene(pickProgram);

            //bodyViz = new BodyViz(pickProgram);

            if (App.CharacterFile != null)
                character = new CharViz(App.CharacterFile);

            this.projectionMat = Matrix4.CreatePerspectiveFieldOfView(60 * (float)Math.PI / 180.0f, 1, 0.5f, 50.0f);
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

        struct GLPixel
        {
            public byte r;
            public byte g;
            public byte b;
            public byte a;

            public override string ToString()
            {
                return $"{r},{g},{b},{a}";
            }
        }

        GLPixel[] pixels = null;

        bool isPlaying = false;
        long frametime = 0;
        long framerate = 10000000 / 60;
        KinectData.Frame curFrame = null;
        bool live = false;
        private void GlControl_Paint(object sender, System.Windows.Forms.PaintEventArgs e)
        {
            if (live && liveBodies == null)
                liveBodies = new KinectBody();

            if (live)
                curFrame = liveBodies.CurrentFrame;
            else
                curFrame = bodyData?.GetInterpolatedFrame(bodyTimeStart +
                    (frametime % bodyTimeLength));

            if (!scene.IsInitialized)
                scene.Init(bulletSimulation, curFrame);
            else
                scene.SetBodyFrame(curFrame);

            bulletSimulation.Step();

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

            scene.Render(viewProj);
            long timeStamp = 0;
            if (depthVid != null)
                timeStamp = depthVid.Render(viewProj);
            if (jtSelected > 0)
                curFrame.SetJointColor(jtSelected, new Vector3(1, 1, 0));
            //if (curFrame != null && (visibleBits & 1) != 0)
            //    bodyViz.Render(curFrame, viewProj);
            if ((visibleBits & 2) != 0)
                character.Render(curFrame, viewProj);

            if (isPlaying) frametime += framerate;

            bulletSimulation.DrawDebug(viewProj);

            glControl.SwapBuffers();

        }

        object SelectedObject = null;

        void RefreshSelection()
        {
            if (SelectedObject != null)
            {
                if (SelectedObject is KinectData.JointNode)
                {
                    KinectData.JointNode jn = SelectedObject as KinectData.JointNode;
                    SelectionId.Content = jn.jt.ToString();
                }
                else 
                {
                    SelectionId.Content = SelectedObject.ToString();
                }
            }
            else
                SelectionId.Content = "";
        }

        void DoPick()
        {
            GL.Clear(ClearBufferMask.ColorBufferBit);
            GL.Clear(ClearBufferMask.DepthBufferBit);
            GL.Disable(EnableCap.Blend);
            Matrix4 lookTrans = rotMatrix.Inverted() * Matrix4.CreateTranslation(curPos);
            Matrix4 viewProj = viewMat * projectionMat;

            List<object> pickObjects = new List<object>();
            this.viewMat = lookTrans.Inverted();

            GL.UseProgram(pickProgram.ProgramName);
            int idxOffset = 50;
            if (curFrame != null)
            {
                if (bodyViz != null)
                    bodyViz.Pick(curFrame, viewProj, pickObjects, idxOffset);
                scene.Pick(curFrame, viewProj, pickObjects, idxOffset);
            }

            if (pixels == null || pixels.Length != (glControl.Width * glControl.Height))
                pixels = new GLPixel[glControl.Width * glControl.Height];
            GL.ReadBuffer(ReadBufferMode.Back);
            GL.ReadPixels<GLPixel>(0, 0, glControl.Width, glControl.Height, OpenTK.Graphics.ES30.PixelFormat.Rgba, PixelType.UnsignedByte, pixels);
            GLPixel pixel = pixels[(glControl.Height - pickPt.Value.Y) * glControl.Width + pickPt.Value.X];
            int idx = pixel.r | (pixel.g << 8) | (pixel.b << 16);
            idx -= idxOffset;
            if (idx >= 0 && idx < pickObjects.Count)
                SelectedObject = pickObjects[idx];
            else
                SelectedObject = null;
            RefreshSelection();
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


        private void Toolbar_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            this.currentTool = (Tools)Enum.Parse(typeof(Tools), (string)e.AddedItems[0]);
        }

        private void backBtn_Click(object sender, RoutedEventArgs e)
        {
            if (!isPlaying) frametime -= framerate;
            else
                frametime += new TimeSpan(0, 0, 0, 30).Ticks;

        }
        private void playBtn_Click(object sender, RoutedEventArgs e)
        {
            isPlaying = !isPlaying;
            if (depthVid != null) depthVid.isPlaying = !depthVid.isPlaying;

        }

        private void fwdBtn_Click(object sender, RoutedEventArgs e)
        {
            if (!isPlaying) frametime += framerate;
            else frametime -= new TimeSpan(0, 0, 0, 30).Ticks;
        }
    }

    public class BaseNotifier : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        // Create the OnPropertyChanged method to raise the event
        protected void OnPropertyChanged(string name)
        {
            PropertyChangedEventHandler handler = PropertyChanged;
            if (handler != null)
            {
                handler(this, new PropertyChangedEventArgs(name));
            }
        }
    }
    public class BulletDebugDraw : BaseNotifier
    {
        public BulletSharp.DebugDrawModes debugDrawMode;

        public delegate void OnCheckedChangedDel(BulletDebugDraw itm);
        public OnCheckedChangedDel OnCheckedChanged;

        public void SetFromMask(BulletSharp.DebugDrawModes mask)
        {
            this.isChecked = ((mask & debugDrawMode) != 0);
            OnPropertyChanged("IsChecked");
        }

        public string Name { get => debugDrawMode.ToString(); }

        bool isChecked = false;
        public bool IsChecked
        {
            get => isChecked; 
            set
            {
                isChecked = value;
                OnCheckedChanged?.Invoke(this);
            }
        }
    }
}
