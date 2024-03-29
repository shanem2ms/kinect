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
using Ookii.Dialogs.Wpf;
using wf = System.Windows.Forms;

namespace kinectwall
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        Matrix4 projectionMat;
        Matrix4 viewMat = Matrix4.Identity;
        //Character.Character character;
        private GLObjects.Program pickProgram;
        BulletSimulation bulletSimulation;
        Scene.SceneNode selectedObject;
        public Scene.SceneNode SelectedObject { get => selectedObject; set { selectedObject = value; selectionBox.SelectedObject = value; } }
        Scene.TransformTool CurrentTransformTool = Scene.TransformTool.None;

        public Scene.SceneNode SceneRoot { get => sceneRoot; }
        Scene.Container sceneRoot = new Scene.Container("root");
        Scene.SelectionBox selectionBox = new Scene.SelectionBox();
        string clipBoard;

        public enum Tools
        {
            Scale,
            Rotate,
            Translate
        };

        Tools currentTool = Tools.Translate;
        public string[] ToolNames { get => Enum.GetNames(typeof(Tools)); }
        public string SelectedTool { get => currentTool.ToString();
            set { this.currentTool = (Tools)Enum.Parse(typeof(Tools), value);
                this.selectionBox.BaseTool = this.currentTool == Tools.Translate ? Scene.TransformTool.Move :
                    (this.currentTool == Tools.Scale ? Scene.TransformTool.Scale : Scene.TransformTool.Rotate);
            }
        }

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
                dmodes.Select(dm => new BulletDebugDraw()
                {
                    debugDrawMode = dm,
                    OnCheckedChanged = OnBulletDebugCheckChange
                }).ToArray();
            // use the window object as the view model in this simple example
            this.DataContext = this;

            // initialize the components (controls) of the window
            this.InitializeComponent();

            App.OnWriteMsg = WriteMsg;
            this.glControl.MouseWheel += GlControl_MouseWheel;
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

        Vector3 mouseDownPivot;
        System.Drawing.Point? mouseDownPt;
        System.Drawing.Point? pickPt;
        float xRot = 0.0f;
        float xRotDn;

        float yRot = 0;
        float yRotDn;

        Matrix4 rotMatrix = Matrix4.CreateRotationY((float)Math.PI);
        Matrix4 rotMatrixDn;
        Vector3 curPosDn;
        private void GlControl_MouseUp(object sender, wf.MouseEventArgs e)
        {
            mouseDownPt = null;
            if (this.CurrentTransformTool != Scene.TransformTool.None)
                selectionBox.EndTransform(true);
            this.CurrentTransformTool = Scene.TransformTool.None;
        }

        private void GlControl_MouseWheel(object sender, wf.MouseEventArgs e)
        {
            double multiplier = Math.Pow(2, -e.Delta / (120.0 * 4));
            mouseDownPivot = SelectedObject != null ? SelectedObject.WorldMatrix.ExtractTranslation() :
                Vector3.Zero;
            Vector3 distFromPivot = this.curPos - mouseDownPivot;
            distFromPivot *= (float)multiplier;
            curPos = mouseDownPivot + distFromPivot;
        }

        Vector2 ScreenToViewport(System.Drawing.Point pt)
        {
            return new Vector2(((float)pt.X / (float)glControl.Width) * 2 - 1.0f,
                             1.0f - ((float)pt.Y / (float)glControl.Height) * 2);
        }

        private void GlControl_MouseMove(object sender, wf.MouseEventArgs e)
        {
            System.Drawing.Point curPt = e.Location;
            if (mouseDownPt != null)
            {
                if (e.Button == wf.MouseButtons.Middle)
                {
                    if ((Keyboard.Modifiers & ModifierKeys.Shift) != 0)
                    {
                        float xOffset = (float)(curPt.X - mouseDownPt.Value.X) * 0.001f;
                        float yOffset = (float)(curPt.Y - mouseDownPt.Value.Y) * 0.001f;

                        Matrix4 viewInv = this.viewMat.Inverted();
                        Vector3 zd = Vector3.TransformNormal(Vector3.UnitZ, viewInv).Normalized();
                        Vector3 xd = Vector3.TransformNormal(Vector3.UnitX, viewInv).Normalized();
                        Vector3 yd = Vector3.TransformNormal(Vector3.UnitY, viewInv).Normalized();

                        Vector3 m = new Vector3(-xOffset, yOffset, 0);
                        curPos = curPosDn + (m.X * xd + m.Y * yd +
                            m.Z * zd);
                    }
                    else
                    {
                        xRot = (float)(curPt.X - mouseDownPt.Value.X) * -0.002f;
                        yRot = (float)(curPt.Y - mouseDownPt.Value.Y) * 0.002f;

                        float distFromPivot = (curPosDn - mouseDownPivot).Length;
                        if (distFromPivot == 0)
                        {
                            mouseDownPivot = curPosDn + 5 * Vector3.UnitZ;
                            distFromPivot = (curPosDn - mouseDownPivot).Length;
                        }

                        Vector3 zDir = (curPosDn - mouseDownPivot).Normalized();
                        Vector3 yDirFrm = Vector3.TransformVector(Vector3.UnitY,
                            this.rotMatrixDn);
                        Vector3 xDir = Vector3.Cross(zDir, yDirFrm);
                        Vector3 yDir = Vector3.Cross(xDir, zDir);
                        zDir = Quaternion.FromAxisAngle(yDir, xRot) *
                            Quaternion.FromAxisAngle(xDir, yRot) * zDir;
                        xDir = Vector3.Cross(zDir, yDir);
                        yDir = Vector3.Cross(xDir, zDir);
                        xDir.Normalize();
                        yDir.Normalize();
                        zDir.Normalize();
                        Matrix3 mt = new Matrix3(-xDir, yDir, zDir);

                        this.rotMatrix = new Matrix4(mt);
                        this.curPos = mouseDownPivot + distFromPivot * zDir;
                    }
                }
                else if (e.Button == wf.MouseButtons.Left &&
                        CurrentTransformTool != Scene.TransformTool.None)
                {
                    Matrix4 viewProj = viewMat * projectionMat;

                    this.selectionBox.Transform(
                        ScreenToViewport(curPt),
                            viewProj);
                }
                glControl.Invalidate();
            }
        }

        private void GlControl_MouseDown(object sender, wf.MouseEventArgs e)
        {
            if (e.Button == wf.MouseButtons.Middle)
            {
                mouseDownPivot = SelectedObject != null ? SelectedObject.WorldMatrix.ExtractTranslation() :
                    Vector3.Zero;

                mouseDownPt = e.Location;
                this.rotMatrixDn = this.rotMatrix;
                this.curPosDn = this.curPos;
                yRotDn = yRot;
                xRotDn = xRot;
            }
            else if (e.Button == wf.MouseButtons.Right ||
                e.Button == wf.MouseButtons.Left)
            {
                mouseDownPt = e.Location;
                pickPt = e.Location;
                Vector2 ipt = new Vector2((float)pickPt.Value.X / (float)glControl.ClientSize.Width,
                    (float)pickPt.Value.Y / (float)glControl.ClientSize.Height);
                DoPick(e.Button == wf.MouseButtons.Right);
            }
        }
        System.Timers.Timer t = new System.Timers.Timer();
        long bodyTimeStart = 0;
        long bodyTimeLength = 0;

        KinectBody liveBodies;
        public bool IsLive { get; set; }
        public bool IsRecording
        {
            get => liveBodies != null ? liveBodies.IsRecording : false;
            set { if (liveBodies != null) liveBodies.IsRecording = value; }
        }


        Scene.Body activeBody;
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            OnPropertyChanged("SceneRoot");
            GLObjects.Registry.LoadAllPrograms();
            bulletSimulation = new BulletSimulation();
            pickProgram = GLObjects.Registry.Programs["pick"];
            if (App.DepthFile != null)
                depthVid = new DepthVid();
            //this.character = new Character.Character(App.CharacterFile);
            //this.SceneRoot.Nodes.Add(this.character);               
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
        Scene.JointType jtSelected = 0;
        Vector3 movement = Vector3.Zero;

        protected override void OnKeyDown(KeyEventArgs e)
        {
            //Quaternion qy = new Quaternion(Vector3.UnitY, yRot);

            switch (e.Key)
            {
                case Key.Up:
                    movement.Z = -1;
                    break;
                case Key.Left:
                    movement.X = -1;
                    break;
                case Key.Down:
                    movement.Z = 1;
                    break;
                case Key.Right:
                    movement.X = 1;
                    break;
                case Key.PageUp:
                    movement.Y = 1;
                    break;
                case Key.PageDown:
                    movement.Y = -1;
                    break;
                case Key.NumPad1:
                    jtSelected = (Scene.JointType)(((int)jtSelected + 1) % 24);
                    System.Diagnostics.Debug.WriteLine($"{jtSelected}");
                    break;
                case Key.R:
                    SelectedTool = Tools.Rotate.ToString();
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("SelectedTool"));
                    break;
                case Key.G:
                    SelectedTool = Tools.Translate.ToString();
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("SelectedTool"));
                    break;
                case Key.S:
                    SelectedTool = Tools.Scale.ToString();
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("SelectedTool"));
                    break;

            }
            base.OnKeyDown(e);
        }

        protected override void OnKeyUp(KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Up:
                case Key.Down:
                    movement.Z = 0;
                    break;
                case Key.Left:
                case Key.Right:
                    movement.X = 0;
                    break;
                case Key.PageUp:
                case Key.PageDown:
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
        int frameIdx;
        private void GlControl_Paint(object sender, wf.PaintEventArgs e)
        {
            if (activeBody != null)
                activeBody.FrameIdx = this.frameIdx % activeBody.NumFrames;
            //this.character.SetBody(activeBody);

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

            bulletSimulation.Step();

            GL.Clear(ClearBufferMask.ColorBufferBit);
            GL.Clear(ClearBufferMask.DepthBufferBit);
            GL.Enable(EnableCap.Blend);
            GL.Enable(EnableCap.DepthTest);

            //GL.Enable(EnableCap.CullFace);
            //GL.CullFace(CullFaceMode.Front);
            GL.BlendFunc(BlendingFactorSrc.One, BlendingFactorDest.OneMinusSrcAlpha);

            Matrix4 lookTrans = this.rotMatrix * Matrix4.CreateTranslation(curPos);
            this.viewMat = lookTrans.Inverted();

            Matrix4 viewProj = viewMat * projectionMat;

            Scene.SceneNode.RenderData rData =
                new Scene.SceneNode.RenderData();

            rData.isPick = false;
            rData.viewProj = viewProj;
            rData.passIdx = 0;
            sceneRoot.Render(rData);
            rData.passIdx = 1;
            sceneRoot.Render(rData);

            selectionBox.Render(rData);

            //scene.Render(viewProj);
            long timeStamp = 0;
            if (depthVid != null)
                timeStamp = depthVid.Render(viewProj);

            bulletSimulation.DrawDebug(viewProj);

            glControl.SwapBuffers();
            if (isPlaying)
                frameIdx++;
        }

        KinectBody.TrackedBody CurrentBody;
        public Scene.JointLimits[] JointLimits { get => CurrentBody?.JLimits; }

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

        void DoPick(bool fullObjectPicking)
        {
            GL.Clear(ClearBufferMask.ColorBufferBit);
            GL.Clear(ClearBufferMask.DepthBufferBit);
            GL.Disable(EnableCap.Blend);
            Matrix4 lookTrans = rotMatrix.Inverted() * Matrix4.CreateTranslation(curPos);
            Matrix4 viewProj = viewMat * projectionMat;

            int idxOffset = 50;
            Scene.SceneNode.RenderData rData =
                new Scene.SceneNode.RenderData();

            rData.pickObjects = new List<Scene.PickItem>();
            rData.isPick = true;
            rData.viewProj = viewProj;
            rData.pickIdx = idxOffset;
            rData.passIdx = 0;
            sceneRoot.Render(rData);
            rData.passIdx = 1;
            sceneRoot.Render(rData);
            selectionBox.Render(rData);

            if (pixels == null || pixels.Length != (glControl.Width * glControl.Height))
                pixels = new GLPixel[glControl.Width * glControl.Height];
            GL.ReadBuffer(ReadBufferMode.Back);
            GL.ReadPixels<GLPixel>(0, 0, glControl.Width, glControl.Height, OpenTK.Graphics.ES30.PixelFormat.Rgba, PixelType.UnsignedByte, pixels);
            GLPixel pixel = pixels[(glControl.Height - pickPt.Value.Y) * glControl.Width + pickPt.Value.X];
            int idx = pixel.r | (pixel.g << 8) | (pixel.b << 16);
            idx -= idxOffset;

            if (fullObjectPicking)
                if (SelectedObject != null) SelectedObject.IsSelected = false;

            if (idx >= 0 && idx < rData.pickObjects.Count)
            {
                Scene.PickItem pickItem = rData.pickObjects[idx];
                if (fullObjectPicking && pickItem.node != null)
                {
                    SelectedObject = rData.pickObjects[idx].node;
                    if (SelectedObject != null) SelectedObject.IsSelected = true;
                }
                else
                {
                    this.CurrentTransformTool = pickItem.tool;
                    if (pickItem.tool != Scene.TransformTool.None)
                    {
                        selectionBox.BeginTransform(
                            CurrentTransformTool,
                            ScreenToViewport(this.mouseDownPt.Value),
                            viewMat * projectionMat);
                    }
                }
            }
            else
            {
                SelectedObject = null;
            }

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

        private void backBtn_Click(object sender, RoutedEventArgs e)
        {
            if (!isPlaying) frameIdx--;

        }
        private void playBtn_Click(object sender, RoutedEventArgs e)
        {
            isPlaying = !isPlaying;
            if (depthVid != null) depthVid.isPlaying = !depthVid.isPlaying;

        }

        private void fwdBtn_Click(object sender, RoutedEventArgs e)
        {
            if (!isPlaying) frameIdx++;
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

        private void SceneTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (SelectedObject != null) SelectedObject.IsSelected = false;
            if (e.NewValue != null)
            {
                this.SelectedObject = e.NewValue as Scene.SceneNode;
                this.SelectedObject.IsSelected = true;
            }
        }

        private void AddCharacter_Click(object sender, RoutedEventArgs e)
        {
            VistaOpenFileDialog ofd = new VistaOpenFileDialog();
            ofd.DefaultExt = ".dae"; // Default file extension
            ofd.Filter = "Collada file (.dae)|*.dae"; // Filter files by extension
            if (ofd.ShowDialog() == true)
            {
                Character.Character character = new Character.Character(ofd.FileName);
                this.sceneRoot.Children.Add(character);
            }
        }

        private void RefBody_Click(object sender, RoutedEventArgs e)
        {
            Scene.Body body = Scene.BodyData.ReferenceBody();
            sceneRoot.Children.Add(body);
        }

        private void LoadBody_Click(object sender, RoutedEventArgs e)
        {
            VistaOpenFileDialog ofd = new VistaOpenFileDialog();
            ofd.DefaultExt = ".out"; // Default file extension
            ofd.Filter = "Body file (.out)|*.out"; // Filter files by extension
            if (ofd.ShowDialog() == true)
            {
                Scene.Body bd = new Scene.Body(ofd.FileName);
                this.sceneRoot.Children.Add(bd);
            }
        }

        private void LoadScene_Click(object sender, RoutedEventArgs e)
        {
            VistaOpenFileDialog ofd = new VistaOpenFileDialog();
            ofd.DefaultExt = ".scene";
            ofd.Filter = "Scene (*.scene)|*.scene";
            if (ofd.ShowDialog() == true)
            {
                StreamReader sr = new StreamReader(ofd.FileName);
                string json = sr.ReadToEnd();
                JsonSerializerSettings settings = new JsonSerializerSettings()
                {
                    TypeNameHandling = TypeNameHandling.All
                };
                Scene.Container root = JsonConvert.DeserializeObject<Scene.Container>(json, settings);
                this.sceneRoot = root;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("SceneRoot"));
            }
        }

        private void SaveScene_Click(object sender, RoutedEventArgs e)
        {
            VistaSaveFileDialog sfd = new VistaSaveFileDialog();
            sfd.DefaultExt = ".scene";
            sfd.Filter = "Scene (*.scene)|*.scene";
            if (sfd.ShowDialog() == true)
            {
                JsonSerializerSettings settings = new JsonSerializerSettings()
                {
                    TypeNameHandling = TypeNameHandling.All
                };

                string json = JsonConvert.SerializeObject(this.sceneRoot, Formatting.Indented, settings);
                StreamWriter sw = new StreamWriter(sfd.FileName);
                sw.Write(json);
                sw.Close();
            }
        }

        static int addIdx = 0;
        private void AddMeshCube_Click(object sender, RoutedEventArgs e)
        {
            Scene.Mesh cubeMesh = new Scene.Mesh($"Cube{addIdx++}");
            this.sceneRoot.Children.Add(cubeMesh);
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("SceneRoot"));
        }

        private void CtrCam_Click(object sender, RoutedEventArgs e)
        {
            
        }

        private void Copy_Click(object sender, RoutedEventArgs e)
        {
            this.clipBoard = JsonConvert.SerializeObject(this.SelectedObject);
        }
        private void Cut_Click(object sender, RoutedEventArgs e)
        {

        }
        private void Paste_Click(object sender, RoutedEventArgs e)
        {
            if (this.clipBoard == null)
                return;
            Scene.SceneNode sn = 
                JsonConvert.DeserializeObject<Scene.SceneNode>
                    (this.clipBoard);
            //this.SelectedObject
        }
        private void Delete_Click(object sender, RoutedEventArgs e)
        {

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
