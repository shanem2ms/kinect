﻿using System;
using System.Linq;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.ComponentModel;
using OpenTK.Graphics.ES30;
using OpenTK;
using System.Windows.Input;
using System.Windows.Media;
using System.IO;
using Newtonsoft.Json;
using System.Reflection;

namespace kinectwall
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
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

        public KinectData.SceneNode SceneRoot { get => sceneRoot; }
        KinectData.Container sceneRoot = new KinectData.Container("root");

        public enum Tools
        {
            Camera,
            Pick
        };

        Tools currentTool = Tools.Pick;
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

            App.OnWriteMsg = WriteMsg;
        }

        void WriteMsg(string msg)
        {
            Dispatcher.BeginInvoke(new Action<string>((str) =>
            {
                this.OutputLog.AppendText(str);
                this.OutputLog.ScrollToEnd();
            }), new object[] { msg });
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
                if (e.Button == System.Windows.Forms.MouseButtons.Left ||
                    e.Button == System.Windows.Forms.MouseButtons.Middle)
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
            if (currentTool == Tools.Camera || 
                e.Button == System.Windows.Forms.MouseButtons.Middle)
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
        public bool IsLive { get; set; }
        public bool IsRecording { get => liveBodies != null ? liveBodies.IsRecording : false;
            set { if (liveBodies != null) liveBodies.IsRecording = value; } }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            KinectData.Body body = KinectData.BodyData.ReferenceBody();
            sceneRoot.Children.Add(body);

            OnPropertyChanged("SceneRoot");
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
        int visibleBits = 3;
        KinectData.JointType jtSelected = 0;
        Vector3 movement = Vector3.Zero;

        protected override void OnKeyDown(KeyEventArgs e)
        {
            //Quaternion qy = new Quaternion(Vector3.UnitY, yRot);

            switch (e.Key)
            {
                case Key.W:
                    movement.Z = -1;
                    break;
                case Key.A:
                    movement.X = -1;
                    break;
                case Key.S:
                    movement.Z = 1;
                    break;
                case Key.D:
                    movement.X = 1;
                    break;
                case Key.Q:
                    movement.Y = 1;
                    break;
                case Key.Z:
                    movement.Y = -1;
                    break;
                case Key.O:
                    framerate *= 2;
                    break;
                case Key.P:
                    framerate /= 2;
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
            base.OnKeyDown(e);
        }

        protected override void OnKeyUp(KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.W:
                case Key.S:
                    movement.Z = 0;
                    break;
                case Key.A:
                case Key.D:
                    movement.X = 0;
                    break;
                case Key.Q:
                case Key.Z:
                    movement.Y = 0;
                    break;
            }
                    base.OnKeyUp(e);
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
        private void GlControl_Paint(object sender, System.Windows.Forms.PaintEventArgs e)
        {
            Matrix4 viewInv = this.viewMat.Inverted();
            Vector3 zd = Vector3.TransformNormal(Vector3.UnitZ, viewInv).Normalized();
            Vector3 xd = Vector3.TransformNormal(Vector3.UnitX, viewInv).Normalized();
            Vector3 yd = Vector3.TransformNormal(Vector3.UnitY, viewInv).Normalized();

            curPos += (movement.X * xd + movement.Y * yd +
                movement.Z * zd) * 0.05f;

            if (IsLive && liveBodies == null)
            {
                liveBodies = new KinectBody();
                liveBodies.OnNewTrackedBody += LiveBodies_OnNewTrackedBody;
            }

            if (IsLive)
                curFrame = liveBodies.CurrentFrame;
            else
                curFrame = bodyData?.GetInterpolatedFrame(bodyTimeStart +
                    (frametime % bodyTimeLength));

            if (!scene.IsInitialized)
                scene.Init(bulletSimulation, sceneRoot);
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

        KinectBody.TrackedBody CurrentBody;
        public KinectData.JointLimits []JointLimits { get => CurrentBody?.JLimits; }

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

        private void LiveBodies_OnNewTrackedBody(object sender, KinectBody.TrackedBody e)
        {
            CurrentBody = e;
            OnPropertyChanged("JointLimits");
        }

        object SelectedObject = null;

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
            //if (bodyViz != null)
            //    bodyViz.Pick(curFrame, viewProj, pickObjects, idxOffset);
            scene.Pick(viewProj, pickObjects, idxOffset);

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

            TreeViewItem tvi = GetTreeViewItem(this.SceneTree, SelectedObject);
            if (tvi != null) tvi.IsSelected = true;
        }

        public class MyVirtualizingStackPanel : VirtualizingStackPanel
        {
            /// <summary>
            /// Publically expose BringIndexIntoView.
            /// </summary>
            public void BringIntoView(int index)
            {

                this.BringIndexIntoView(index);
            }
        }
        /// <summary>
        /// Recursively search for an item in this subtree.
        /// </summary>
        /// <param name="container">
        /// The parent ItemsControl. This can be a TreeView or a TreeViewItem.
        /// </param>
        /// <param name="item">
        /// The item to search for.
        /// </param>
        /// <returns>
        /// The TreeViewItem that contains the specified item.
        /// </returns>
        private TreeViewItem GetTreeViewItem(ItemsControl container, object item)
        {
            if (container != null)
            {
                if (container.DataContext == item)
                {
                    return container as TreeViewItem;
                }

                // Expand the current container
                if (container is TreeViewItem && !((TreeViewItem)container).IsExpanded)
                {
                    container.SetValue(TreeViewItem.IsExpandedProperty, true);
                }

                // Try to generate the ItemsPresenter and the ItemsPanel.
                // by calling ApplyTemplate.  Note that in the 
                // virtualizing case even if the item is marked 
                // expanded we still need to do this step in order to 
                // regenerate the visuals because they may have been virtualized away.

                container.ApplyTemplate();
                ItemsPresenter itemsPresenter =
                    (ItemsPresenter)container.Template.FindName("ItemsHost", container);
                if (itemsPresenter != null)
                {
                    itemsPresenter.ApplyTemplate();
                }
                else
                {
                    // The Tree template has not named the ItemsPresenter, 
                    // so walk the descendents and find the child.
                    itemsPresenter = FindVisualChild<ItemsPresenter>(container);
                    if (itemsPresenter == null)
                    {
                        container.UpdateLayout();

                        itemsPresenter = FindVisualChild<ItemsPresenter>(container);
                    }
                }

                Panel itemsHostPanel = (Panel)VisualTreeHelper.GetChild(itemsPresenter, 0);

                // Ensure that the generator for this panel has been created.
                UIElementCollection children = itemsHostPanel.Children;

                MyVirtualizingStackPanel virtualizingPanel =
                    itemsHostPanel as MyVirtualizingStackPanel;

                for (int i = 0, count = container.Items.Count; i < count; i++)
                {
                    TreeViewItem subContainer;
                    if (virtualizingPanel != null)
                    {
                        // Bring the item into view so 
                        // that the container will be generated.
                        virtualizingPanel.BringIntoView(i);

                        subContainer =
                            (TreeViewItem)container.ItemContainerGenerator.
                            ContainerFromIndex(i);
                    }
                    else
                    {
                        subContainer =
                            (TreeViewItem)container.ItemContainerGenerator.
                            ContainerFromIndex(i);

                        // Bring the item into view to maintain the 
                        // same behavior as with a virtualizing panel.
                        subContainer.BringIntoView();
                    }

                    if (subContainer != null)
                    {
                        // Search the next level for the object.
                        TreeViewItem resultContainer = GetTreeViewItem(subContainer, item);
                        if (resultContainer != null)
                        {
                            return resultContainer;
                        }
                        else
                        {
                            // The object is not under this TreeViewItem
                            // so collapse it.
                            subContainer.IsExpanded = false;
                        }
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Search for an element of a certain type in the visual tree.
        /// </summary>
        /// <typeparam name="T">The type of element to find.</typeparam>
        /// <param name="visual">The parent element.</param>
        /// <returns></returns>
        private T FindVisualChild<T>(Visual visual) where T : Visual
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(visual); i++)
            {
                Visual child = (Visual)VisualTreeHelper.GetChild(visual, i);
                if (child != null)
                {
                    T correctlyTyped = child as T;
                    if (correctlyTyped != null)
                    {
                        return correctlyTyped;
                    }

                    T descendent = FindVisualChild<T>(child);
                    if (descendent != null)
                    {
                        return descendent;
                    }
                }
            }

            return null;
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

        private void Export_JointLimits(object sender, RoutedEventArgs e)
        {
            string jsonLimits = JsonConvert.SerializeObject(CurrentBody.JLimits);
            using (FileStream fs = new FileStream(@"jointlimits.json", FileMode.Create, FileAccess.Write))
            {
                StreamWriter sw = new StreamWriter(fs);
                sw.Write(jsonLimits);
            }
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
