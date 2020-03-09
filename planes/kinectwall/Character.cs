using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using System.Linq;
using ai = Assimp;
using aic = Assimp.Configs;
using System.IO;
using OpenTK.Graphics.ES30;
using OpenTK;
using GLObjects;
using System.Diagnostics;
using BodyData;
using System.Drawing;
using System.Drawing.Imaging;


/// <summary>
///  Export from Blender
///     DAE format
///         Global Orientation
///            Z Forward
///            -Y Up
///         Apply Global Orientation Checked   
/// </summary>
namespace Character
{
    public class Key : IComparable<Key>
    {
        public double time;
        public object val;

        public int CompareTo(Key other)
        {
            return time.CompareTo(other.time);
        }
    }

    public class Node : BodyData.SceneNode
    {
        public Node parent = null;
        public ObservableCollection<SceneNode> children;
        public JointTransform BindTransform { get; set; } = JointTransform.Identity;
        public JointType? KinectJoint { get; set; }
        public Vector3 color;
        public List<Key>[] keys = new List<Key>[3];
        public bool SetFromBody { get; set;} = false;

        public override Matrix4 WorldMatrix => WorldTransform;

        public override ObservableCollection<SceneNode> Nodes => children;

        public Matrix4 Transform => BindTransform.M4;

        public Node(string _n) : base(_n)
        {
        }

        protected override void OnRender(RenderData renderData)
        {
            if (renderData.passIdx == 1)
            {
                Program program = renderData.ActiveProgram;

                if (this.KinectJoint != null)
                {
                    GL.Disable(EnableCap.DepthTest);
                    Matrix4 matWorld =
                                                Matrix4.CreateScale(
                                                    new Vector3(0.01f, 0.01f, 0.01f)) *
                                                    this.WorldTransform;
                    Matrix4 matWorldViewProj = matWorld * renderData.viewProj;
                    program.SetMat4("uWorld", ref matWorld);

                    if (renderData.isPick)
                    {
                        program.Set4("pickColor", new Vector4((renderData.pickIdx & 0xFF) / 255.0f,
                            ((renderData.pickIdx >> 8) & 0xFF) / 255.0f,
                            ((renderData.pickIdx >> 16) & 0xFF) / 255.0f,
                            1));
                        renderData.pickObjects.Add(this);
                        renderData.pickIdx++;

                    }
                    else
                    {
                        program.Set3("meshColor", this.color);
                        program.Set1("ambient", this.IsSelected ? 1.0f : 0.3f);
                        program.Set3("lightPos", new Vector3(2, 5, 2));
                        Matrix4 matWorldInvT = matWorld.Inverted();
                        matWorldInvT.Transpose();
                        program.SetMat4("uWorldInvTranspose", ref matWorldInvT);
                    }

                    program.SetMat4("uMVP", ref matWorldViewProj);
                    // Use the vertex array
                    renderData.ActiveVA.Draw();
                    GL.Enable(EnableCap.DepthTest);
                }
            }
            base.OnRender(renderData);
        }

        public void OutputNodeDbg(int level)
        {
            Debug.WriteLine(new string(' ', level * 2) + $"KJ={KinectJoint} {name} bt = {BindTransform}");
            if (this.children != null)
            {
                foreach (Node cn in this.children)
                {
                    cn.OutputNodeDbg(level + 1);
                }
            }
        }

        public Matrix4 WorldTransform
        {
            get
            {
                if (parent == null)
                    return Transform;
                else
                    return Transform * parent.WorldTransform;
            }
        }

        object Interp(object l, object r, double lerpd)
        {
            float lerp = (float)lerpd;
            if (l is Vector3)
            {
                Vector3 vl = (Vector3)l;
                Vector3 vr = (Vector3)r;
                return vl * (1 - lerp) + vr * lerp;
            }
            else
            {
                Quaternion vl = (Quaternion)l;
                Quaternion vr = (Quaternion)r;
                return vl * (1 - lerp) + vr * lerp;
            }
        }


        HashSet<JointType> useRotations = new HashSet<JointType>()
            {
                JointType.KneeLeft,
                JointType.FootLeft,
                JointType.AnkleLeft,
            };
        public void SetBody(Body b, Matrix4 matWorld)
        {
            if (this.SetFromBody && KinectJoint.HasValue)
            {
                JointNode jn = b.jointNodes[KinectJoint.Value];
                this.BindTransform.rot = jn.LocalTransform.rot;
            }

            if (children != null)
            {
                foreach (Node cn in this.children)
                {
                    cn.SetBody(b, matWorld);
                }
            }
        }
    }

    class Character : BodyData.SceneNode
    {
        public class Bone
        {
            public int meshIdx;
            public JointTransform offsetMat;
            public Node node;
            public int idx;
        }

        public class Texture
        {
            public string filePath;
            public TextureRgba glTexture;
        }

        public class Material
        {
            public Texture diffTex;
        }

        public class Weight
        {
            public Bone bone;
            public float weight;
        }

        public class Vertex
        {
            public Vector3 pos;
            public Vector3 normal;
            public Vector3 texcoord;
            public List<Weight> weights = new List<Weight>();
        }

        public class Mesh
        {
            public int offset;
            public int count;
            public Node node;
            public int materialIdx = -1;
            public List<Bone> bones;
            public Vector3 min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            public Vector3 max = new Vector3(float.MinValue, float.MinValue, float.MinValue);

            static float[] StructArrayToFloatArray<T>(T[] _array) where T : struct
            {
                int tSize = Marshal.SizeOf(typeof(T)) * _array.Length;
                IntPtr arrPtr = Marshal.AllocHGlobal(tSize);
                long LongPtr = arrPtr.ToInt64(); // Must work both on x86 and x64
                for (int idx = 0; idx < _array.Length; idx++)
                {
                    IntPtr ptr = new IntPtr(LongPtr);
                    Marshal.StructureToPtr(_array[idx], ptr, false);
                    LongPtr += Marshal.SizeOf(typeof(T));
                }

                int uSize = Marshal.SizeOf(typeof(float));
                float[] outVals = new float[tSize / uSize];
                Marshal.Copy((IntPtr)arrPtr, outVals, 0, outVals.Length);
                Marshal.FreeHGlobal(arrPtr);
                return outVals;
            }
            int boneMatrixLoc = -1;

            public void Render(Material[] materials, Matrix4[] mats, RenderData renderData,
                Program program,
                VertexArray vertexArray)
            {
                program.Use(renderData.isPick ? 1 : 0);
                renderData.ActiveProgram = program;
                renderData.ActiveVA = vertexArray;

                float[] flvals = StructArrayToFloatArray<Matrix4>(mats);

                if (boneMatrixLoc < 0)
                    boneMatrixLoc = program.GetLoc("boneMatrices");
                List<Matrix4> matList = new List<Matrix4>();

                GL.UniformMatrix4(program.GetLoc("gBones"), flvals.Length / 16, false, flvals);
                program.Set1("gUseBones", 1);

                /*
                Vector3[] boneColors = this.allBones.Select(b => b.node.color).ToArray();
                float[] fvColors = new float[boneColors.Length * 3];

                for (int bIdx = 0; bIdx < boneColors.Length; ++bIdx)
                {
                    fvColors[bIdx * 3] = boneColors[bIdx].X;
                    fvColors[bIdx * 3 + 1] = boneColors[bIdx].Y;
                    fvColors[bIdx * 3 + 2] = boneColors[bIdx].Z;
                }
                GL.Uniform3(this.program.GetLoc("gBonesColor"), fvColors.Length / 3, fvColors);*/
                program.Set1("diffuseMap", 0);

                Matrix4 matWorldViewProj =
                    this.node.WorldTransform * renderData.viewProj;

                if (renderData.isPick)
                {
                    program.Set4("pickColor", new Vector4((renderData.pickIdx & 0xFF) / 255.0f,
                        ((renderData.pickIdx >> 8) & 0xFF) / 255.0f,
                        ((renderData.pickIdx >> 16) & 0xFF) / 255.0f,
                        1));
                    renderData.pickObjects.Add(node);
                    renderData.pickIdx++;
                }
                else
                {
                    if (this.materialIdx >= 0)
                    {
                        Character.Material mat = materials[this.materialIdx];
                        if (mat.diffTex != null) mat.diffTex.glTexture.BindToIndex(0);
                    }
                }
                program.SetMat4("uMVP", ref renderData.viewProj);
                vertexArray.Draw(this.offset, this.count);
            }

        }

        public List<Vertex> vertices = new List<Vertex>();
        public List<uint> elems = new List<uint>();
        public List<Mesh> meshes = new List<Mesh>();
        public Node Root;
        public Bone[] allBones;
        public float headToSpineSize;
        public double duration;
        string loadPath;
        public Material[] materials;
        public Vector3 footPos;
        public VertexArray vertexArray;
        public Program program;
        public Program bonePrgm;
        public VertexArray cubeVA;

        ObservableCollection<SceneNode> nodes = new ObservableCollection<SceneNode>();
        public override ObservableCollection<SceneNode> Nodes => nodes;

        public Character(string path) :
            base(Path.GetFileName(path))
        {
            Load(path);
        }

        void GetAllBones(List<Bone> bones)
        {
            foreach (Mesh m in meshes)
            {
                bones.AddRange(m.bones);
            }
        }

        static JointType? GetKinectJoint(string name)
        {
            int uidx = name.IndexOf('_');
            if (uidx < 0)
                return null;
            name = name.Substring(uidx);
            name = name.Replace("_", "");
            JointType jt;
            if (Enum.TryParse(name, out jt))
                return jt;
            return null;
        }

        public void OutputDebug()
        {
            Debug.WriteLine("--- Nodes ----");
            this.Root.OutputNodeDbg(0);

            Debug.WriteLine("--- Meshes ----");
            int idx = 0;
            foreach (Character.Mesh mesh in this.meshes)
            {
                Debug.WriteLine($"idx ({idx}): node={mesh.node.Name}");
                Vector3 size = mesh.max - mesh.min;
                Debug.WriteLine($"MeshSize = {size}");

                Debug.WriteLine("  --- Bones ----");
                int bidx = 0;
                Matrix4 matw = mesh.node.WorldTransform;
                foreach (Bone b in mesh.bones)
                {
                    Matrix4 m1 = matw * b.node.WorldTransform.Inverted();
                    JointTransform j1 = new JointTransform();
                    j1.M4 = m1;
                    Debug.WriteLine($"   idx={bidx++} name={b.node.Name} mat={b.offsetMat} calculated={j1}");
                }
            }
        }

        public void Load(string path)
        {
            this.program = Program.FromFiles("Character.vert", "Character.frag");
            this.bonePrgm = Program.FromFiles("Main.vert", "Main.frag");
            this.cubeVA = kinectwall.Cube.MakeCube(program);

            ai.AssimpContext importer = new ai.AssimpContext();
            importer.SetConfig(new aic.NormalSmoothingAngleConfig(66.0f));

            ai.Scene model = importer.ImportFile(path, ai.PostProcessPreset.TargetRealTimeMaximumQuality);
            this.loadPath = Path.GetDirectoryName(path);

            Dictionary<string, Node> nodeDict = new Dictionary<string, Node>();
            this.Root = BuildModelNodes(nodeDict, model.RootNode);
            this.Root.BindTransform = JointTransform.Identity;
            this.nodes.Add(this.Root);

            BuildMaterials(model);
            BuildMeshes(model, model.RootNode, nodeDict, 0);

            List<Bone> allBones = new List<Bone>();
            GetAllBones(allBones);
            this.allBones = allBones.ToArray();
            int boneIdx = 0;

            Bone leftFoot = this.allBones.FirstOrDefault(b => b.node.KinectJoint.HasValue &&
                b.node.KinectJoint.Value == JointType.FootLeft);
            Bone rightFoot = this.allBones.FirstOrDefault(b => b.node.KinectJoint.HasValue &&
                b.node.KinectJoint.Value == JointType.FootRight);

            Vector3 leftFootPos = leftFoot.node.WorldTransform.ExtractTranslation();
            Vector3 rightFootPos = rightFoot.node.WorldTransform.ExtractTranslation();
            this.footPos = (leftFootPos + rightFootPos) * 0.5f;

            foreach (Bone b in allBones)
            {
                b.idx = boneIdx++;
            }

            Matrix4[] boneMatrices = allBones.Select(b => b.node.WorldTransform).ToArray();

            Vector3[] pts = vertices.Select(v => v.pos).ToArray();
            Vector3[] nrm = vertices.Select(v => v.normal).ToArray();
            Vector3[] txc = vertices.Select(v => v.texcoord).ToArray();
            Vec4i[] bones = vertices.Select(v =>
                new Vec4i()
                {
                    X = v.weights.Count > 0 ? v.weights[0].bone.idx : -1,
                    Y = v.weights.Count > 1 ? v.weights[1].bone.idx : -1,
                    Z = v.weights.Count > 2 ? v.weights[2].bone.idx : -1,
                    W = v.weights.Count > 3 ? v.weights[3].bone.idx : -1
                }).ToArray();
            Vector4[] weights = vertices.Select(v =>
                new Vector4()
                {
                    X = v.weights.Count > 0 ? v.weights[0].weight : 0,
                    Y = v.weights.Count > 1 ? v.weights[1].weight : 0,
                    Z = v.weights.Count > 2 ? v.weights[2].weight : 0,
                    W = v.weights.Count > 3 ? v.weights[3].weight : 0
                }).ToArray();
            this.vertexArray = new VertexArray(program, pts, this.elems.ToArray(), txc, bones, weights, nrm);

            this.duration = LoadAnimations(model, nodeDict);

            Dictionary<JointType, Node> kNodes = new Dictionary<JointType, Node>();
            var keyvals =
                nodeDict.Values.Where(n => n.KinectJoint.HasValue).
                    Select(n => new KeyValuePair<BodyData.JointType, Node>(
                        n.KinectJoint.Value, n));
            foreach (var kv in keyvals)
                kNodes.Add(kv.Key, kv.Value);

            this.headToSpineSize = 0;
            for (int idx = 1; idx < BodyData.BodyData.SpineToHeadJoints.Length; ++idx)
            {
                Vector3 pos0 = kNodes[BodyData.BodyData.SpineToHeadJoints[idx - 1]].WorldTransform.ExtractTranslation();
                Vector3 pos1 = kNodes[BodyData.BodyData.SpineToHeadJoints[idx]].WorldTransform.ExtractTranslation();
                this.headToSpineSize += (pos1 - pos0).Length;
            }
            //OutputDebug();
        }

        protected override void OnRender(RenderData renderData)
        {
            if (renderData.passIdx == 0)
            {
                Matrix4[] mats = this.allBones.Select(b => (
                    b.offsetMat.M4 *
                    b.node.WorldTransform *
                    this.meshes[b.meshIdx].node.WorldTransform.Inverted())).ToArray();

                foreach (Mesh m in this.meshes)
                {
                    m.Render(this.materials, mats, renderData, this.program,
                        this.vertexArray);
                }
            }
            bonePrgm.Use(renderData.isPick ? 1 : 0);
            renderData.ActiveProgram = bonePrgm;
            renderData.ActiveVA = cubeVA;
            base.OnRender(renderData);
        }

        void BuildMaterials(ai.Scene model)
        {
            Dictionary<string, Texture> textures = new Dictionary<string, Texture>();
            List<Material> mats = new List<Material>();
            foreach (ai.Material mat in model.Materials)
            {
                foreach (ai.TextureSlot slot in mat.GetAllMaterialTextures())
                {
                    Texture tex;
                    if (!textures.TryGetValue(slot.FilePath, out tex))
                    {
                        tex = new Texture()
                        {
                            filePath = slot.FilePath,
                            glTexture = LoadTexture(Path.Combine(loadPath, slot.FilePath))
                        };
                        textures.Add(slot.FilePath, tex);
                    }
                    mats.Add(new Material() { diffTex = tex });
                }
            }
            this.materials = mats.ToArray();
        }

        double LoadAnimations(ai.Scene model, Dictionary<string, Node> nodeDict)
        {
            double maxTime = 0;
            foreach (ai.Animation anim in model.Animations)
            {
                foreach (ai.NodeAnimationChannel aic in anim.NodeAnimationChannels)
                {
                    Node node = nodeDict[aic.NodeName];
                    if (aic.ScalingKeyCount > 0)
                        node.keys[0] = new List<Key>();
                    foreach (var vk in aic.ScalingKeys)
                    {
                        node.keys[0].Add(new Key() { time = vk.Time, val = FromVector(vk.Value) });
                        maxTime = Math.Max(maxTime, vk.Time);
                    }

                    if (aic.RotationKeyCount > 0)
                        node.keys[1] = new List<Key>();
                    foreach (var vk in aic.RotationKeys)
                    {
                        node.keys[1].Add(new Key() { time = vk.Time, val = new Quaternion(vk.Value.X, vk.Value.Y, vk.Value.Z, vk.Value.W) });
                        maxTime = Math.Max(maxTime, vk.Time);
                    }
                    if (aic.PositionKeyCount > 0)
                        node.keys[2] = new List<Key>();
                    foreach (var vk in aic.PositionKeys)
                    {
                        node.keys[2].Add(new Key() { time = vk.Time, val = FromVector(vk.Value) });
                        maxTime = Math.Max(maxTime, vk.Time);
                    }

                    //Debug.WriteLine($"A {aic.PositionKeyCount} {aic.RotationKeyCount} {aic.ScalingKeyCount}");
                }
            }

            return maxTime;
        }

        static Vector3 GetJointColor(BodyData.JointType? kjjoint)
        {
            if (kjjoint == null)
                return new Vector3(0, 0, 0);
            switch (kjjoint.Value)
            {
                case JointType.Head:
                case JointType.Neck:
                    return new Vector3(1, 1, 1);
                case JointType.ShoulderLeft:
                case JointType.WristLeft:
                    return new Vector3(1, 1, 0);
                case JointType.ShoulderRight:
                case JointType.WristRight:
                    return new Vector3(0, 1, 1);
                case JointType.KneeLeft:
                case JointType.FootLeft:
                    return new Vector3(1, 0, 0);
                case JointType.KneeRight:
                case JointType.FootRight:
                    return new Vector3(0, 1, 0);
                default:
                    return new Vector3(0.25f, 0.25f, 0.25f);
            }
        }

        private Node BuildModelNodes(Dictionary<string, Node> nodeDict, ai.Node node)
        {
            Node n = new Node(node.Name);
            n.BindTransform.M4 = FromMatrix(node.Transform);
            n.KinectJoint = GetKinectJoint(n.Name);
            n.color = GetJointColor(n.KinectJoint);

            if (node.HasChildren)
                n.children = new ObservableCollection<SceneNode>();
            for (int i = 0; i < node.ChildCount; i++)
            {
                Node mn = BuildModelNodes(nodeDict, node.Children[i]);
                n.children.Add(mn);
                mn.parent = n;
            }

            nodeDict.Add(n.Name, n);
            return n;
        }

        private void BuildMeshes(ai.Scene model, ai.Node node,
            Dictionary<string, Node> nodeDict, int level)
        {

            Node nd = nodeDict[node.Name];
            //Debug.WriteLine(new string(' ', level * 2) + nd.name + ": " + nd.transform);
            if (node.HasMeshes)
            {
                foreach (int index in node.MeshIndices)
                {
                    int vtxoffset = this.vertices.Count;

                    Character.Mesh m = new Character.Mesh();
                    m.node = nodeDict[node.Name];
                    m.offset = this.elems.Count;
                    ai.Mesh mesh = model.Meshes[index];

                    m.materialIdx = mesh.MaterialIndex;
                    Matrix4 wmat = m.node.WorldTransform;
                    for (int i = 0; i < mesh.VertexCount; i++)
                    {
                        Vertex vtx = new Vertex();
                        vtx.pos = FromVector(mesh.Vertices[i]);
                        vtx.normal = FromVector(mesh.Normals[i]);
                        vtx.texcoord = i < mesh.TextureCoordinateChannels[0].Count ? FromVector(mesh.TextureCoordinateChannels[0][i])
                            : Vector3.Zero;
                        this.vertices.Add(vtx);

                        Vector3 worldPt = Vector3.TransformPosition(vtx.pos, wmat);
                        m.min = Vector3.ComponentMin(m.min, worldPt);
                        m.max = Vector3.ComponentMax(m.min, worldPt);
                    }

                    for (int i = 0; i < mesh.FaceCount; i++)
                    {
                        ai.Face f = mesh.Faces[i];
                        for (int j = 0; j < 3; ++j)
                        {
                            this.elems.Add((uint)(f.Indices[j] + vtxoffset));
                        }
                    }

                    if (mesh.HasBones)
                    {
                        m.bones = new List<Character.Bone>();
                        foreach (ai.Bone b in mesh.Bones)
                        {
                            Matrix4 mat = FromMatrix(b.OffsetMatrix);
                            JointTransform jt = new JointTransform();
                            jt.M4 = mat;
                            Bone bone = new Bone()
                            {
                                meshIdx = index,
                                node = nodeDict[b.Name],
                                offsetMat = jt
                            };

                            if (b.HasVertexWeights)
                            {
                                for (int vwIdx = 0; vwIdx < b.VertexWeightCount; ++vwIdx)
                                {
                                    ai.VertexWeight vw = b.VertexWeights[vwIdx];
                                    this.vertices[vw.VertexID + vtxoffset].weights
                                        .Add(new Weight() { bone = bone, weight = vw.Weight });
                                }
                            }
                            m.bones.Add(bone);
                        }
                    }

                    m.count = this.elems.Count - m.offset;
                    this.meshes.Add(m);
                }
            }

            for (int i = 0; i < node.ChildCount; i++)
            {
                BuildMeshes(model, node.Children[i], nodeDict, level + 1);
            }
        }


        public void SetBody(Body body)
        {
            Root.SetBody(body, Matrix4.Identity);
        }

        int MaxWeightsPerVertex => this.vertices.Select(v => v.weights.Count).Max();

        private Matrix4 FromMatrix(ai.Matrix4x4 mat)
        {
            Matrix4 m = new Matrix4();
            m.M11 = mat.A1;
            m.M12 = mat.A2;
            m.M13 = mat.A3;
            m.M14 = mat.A4;
            m.M21 = mat.B1;
            m.M22 = mat.B2;
            m.M23 = mat.B3;
            m.M24 = mat.B4;
            m.M31 = mat.C1;
            m.M32 = mat.C2;
            m.M33 = mat.C3;
            m.M34 = mat.C4;
            m.M41 = mat.D1;
            m.M42 = mat.D2;
            m.M43 = mat.D3;
            m.M44 = mat.D4;
            m.Transpose();
            return m;
        }

        private Vector3 FromVector(ai.Vector3D vec)
        {
            Vector3 v;
            v.X = vec.X;
            v.Y = vec.Y;
            v.Z = vec.Z;
            return v;
        }


        static public TextureRgba LoadTexture(string file)
        {
            Bitmap bitmap = new Bitmap(file);

            int tex;
            GL.Hint(HintTarget.PerspectiveCorrectionHint, HintMode.Nicest);

            GL.GenTextures(1, out tex);
            GL.BindTexture(TextureTarget.Texture2D, tex);

            BitmapData data = bitmap.LockBits(new System.Drawing.Rectangle(0, 0, bitmap.Width, bitmap.Height),
                ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);

            TextureRgba glTex = new TextureRgba();
            glTex.LoadData(data.Width, data.Height, data.Scan0);
            bitmap.UnlockBits(data);
            return glTex;
        }
    }
}
