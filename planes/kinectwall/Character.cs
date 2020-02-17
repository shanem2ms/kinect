using System;
using System.Collections.Generic;
using System.Linq;
using ai = Assimp;
using aic = Assimp.Configs;
using System.IO;
using OpenTK.Graphics.ES30;
using OpenTK;
using GLObjects;
using System.Diagnostics;
using KinectData;
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
namespace kinectwall
{
    struct JointTransform
    {
        public Vector3 pos;
        public Vector3 scl;
        public Quaternion rot;

        public static JointTransform Identity = new JointTransform()
        {
            pos = Vector3.Zero,
            scl = Vector3.One,
            rot = Quaternion.Identity
        };

        public Matrix4 Value
        {
            get => Matrix4.CreateScale(scl) *
                    Matrix4.CreateFromQuaternion(rot) *
                    Matrix4.CreateTranslation(pos);
            set
            {
                Vector3 vscl = value.ExtractScale();
                if (vscl.X != 1 && Math.Abs(vscl.X - 1) < 1e-3)
                    vscl.X = 1;
                if (vscl.Y != 1 && Math.Abs(vscl.Y - 1) < 1e-3)
                    vscl.Y = 1;
                if (vscl.Z != 1 && Math.Abs(vscl.Z - 1) < 1e-3)
                    vscl.Z = 1;
                this.scl = vscl;

                Quaternion vrot = value.ExtractRotation();
                if (vrot.X != 0 && Math.Abs(vrot.X) < 1e-3)
                    vrot.X = 0;
                if (vrot.Y != 0 && Math.Abs(vrot.Y) < 1e-3)
                    vrot.Y = 0;
                if (vrot.Z != 0 && Math.Abs(vrot.Z) < 1e-3)
                    vrot.Z = 0;
                if (vrot.W != 0 && Math.Abs(vrot.W) < 1e-3)
                    vrot.W = 0;
                if (vrot.W != 1 && Math.Abs(1 - vrot.W) < 1e-3)
                    vrot.W = 1;
                rot = vrot;

                Vector3 vpos = value.ExtractTranslation();
                if (vpos.X != 0 && Math.Abs(vpos.X) < 1e-3)
                    vpos.X = 0;
                if (vpos.Y != 0 && Math.Abs(vpos.Y) < 1e-3)
                    vpos.Y = 0;
                if (vpos.Z != 0 && Math.Abs(vpos.Z) < 1e-3)
                    vpos.Z = 0;
                this.pos = vpos;
            }
        }

        public override string ToString()
        {
            string outstr = "";
            if (pos != Vector3.Zero) outstr += "T: " + pos + " ";
            if (scl != Vector3.One) outstr += "S: " + scl + " ";
            if (rot != Quaternion.Identity)
            {
                Vector4 axisang = rot.ToAxisAngle();
                axisang.W *= 180.0f / (float)Math.PI;
                outstr += "R: " + axisang;
            }
            return outstr;
        }
    }

    class Character
    {
        public class Bone
        {
            public int meshIdx;
            public JointTransform offsetMat;
            public Node node;
            public int idx;
        }

        public class Key : IComparable<Key>
        {
            public double time;
            public object val;

            public int CompareTo(Key other)
            {
                return time.CompareTo(other.time);
            }
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

        public class Node
        {
            public Node parent = null;
            public string name;
            public List<Node> children;
            public JointTransform bindTransform;
            JointTransform overrideTransform;
            bool useOverride = false;
            public JointType? kinectJoint;
            public Vector3 color;
            public List<Key>[] keys = new List<Key>[3];

            public Matrix4 Transform => useOverride ? overrideTransform.Value : 
                bindTransform.Value;

            public void OutputNodeDbg(int level)
            {
                Debug.WriteLine(new string(' ', level * 2) + $"KJ={kinectJoint} {name} bt = {bindTransform}");

                /*
                Matrix4 wt = WorldTransform;
                Vector3 wpos = Vector3.TransformPosition(Vector3.Zero, WorldTransform);
                Vector3 xdir = Vector3.TransformVector(Vector3.UnitX, WorldTransform);
                Vector3 ydir = Vector3.TransformVector(Vector3.UnitY, WorldTransform);
                Vector3 zdir = Vector3.TransformVector(Vector3.UnitZ, WorldTransform);
                Debug.WriteLine(new string(' ', level * 2) + $"---- WT Pos={wpos}  Rot=[x{xdir} y{ydir} z{zdir}]");*/
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

            void SetValue(int i, object val)
            { 
                if (i == 0) overrideTransform.scl = (Vector3)val;
                else if (i == 1) overrideTransform.rot = (Quaternion)val;
                else if (i == 2) overrideTransform.pos = (Vector3)val;
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

            public void SetAnimationTime(double time)
            {
                SetAnimationTimeRec(time);
            }

            void SetAnimationTimeRec(double time)
            {
                useOverride = false;
                for (int i = 0; i < 3; ++i)
                {
                    if (keys[i] != null)
                    {
                        if (useOverride == false)
                        {
                            overrideTransform = this.bindTransform;
                            useOverride = true;
                        }
                        List<Key> lkeys = keys[i];
                        int index = lkeys.BinarySearch(new Key() { time = time });
                        if (index >= 0)
                        {
                            SetValue(i, lkeys[index].val);
                        }
                        else
                        {
                            index = ~index;
                            if (index == 0)
                                SetValue(i, lkeys[index].val);
                            else if (index == lkeys.Count)
                                SetValue(i, lkeys[index - 1].val);
                            else
                            {
                                Key leftKey = lkeys[index - 1];
                                Key rightKey = lkeys[index];
                                SetValue(i, Interp(leftKey.val, rightKey.val,
                                    (time - leftKey.time) / (rightKey.time - leftKey.time)));
                            }
                        }

                    }
                }

                if (this.children != null)
                {
                    foreach (Node cn in children)
                    {
                        cn.SetAnimationTimeRec(time);
                    }
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
                return;
                if (kinectJoint.HasValue)
                {
                    JointNode jn = b.jointNodes[kinectJoint.Value];
                    Quaternion q = jn.localMat.ExtractRotation();
                    PoseData.Joint pj = PoseData.JointsIdx[(int)jn.jt];
                    Quaternion dfp = q * pj.rot.Inverted();

                    useOverride = true;
                    this.overrideTransform = this.bindTransform;
                    this.overrideTransform.rot = this.bindTransform.rot * dfp;
                    this.color = jn.color;
                }
                else
                    useOverride = false;

                if (children != null)
                {
                    foreach (Node cn in this.children)
                    {
                        cn.SetBody(b, matWorld);
                    }
                }
            }
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
        }

        public List<Vertex> vertices = new List<Vertex>();
        public List<uint> elems = new List<uint>();
        public List<Mesh> meshes = new List<Mesh>();
        public VertexArray vertexArray;
        public Node Root;
        public Bone[] allBones;
        public float headToSpineSize;
        public double duration;
        string loadPath;
        public Material[] materials;
        public Vector3 footPos;

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
                Debug.WriteLine($"idx ({idx}): node={mesh.node.name}");
                Vector3 size = mesh.max - mesh.min;
                Debug.WriteLine($"MeshSize = {size}");

                Debug.WriteLine("  --- Bones ----");
                int bidx = 0;
                Matrix4 matw = mesh.node.WorldTransform;
                foreach (Bone b in mesh.bones)
                {
                    Matrix4 m1 = matw * b.node.WorldTransform.Inverted();
                    JointTransform j1 = new JointTransform();
                    j1.Value = m1;
                    Debug.WriteLine($"   idx={bidx++} name={b.node.name} mat={b.offsetMat} calculated={j1}");
                }
            }
        }

        public void Load(string path, Program program)
        {
            ai.AssimpContext importer = new ai.AssimpContext();
            importer.SetConfig(new aic.NormalSmoothingAngleConfig(66.0f));

            ai.Scene model = importer.ImportFile(path, ai.PostProcessPreset.TargetRealTimeMaximumQuality);
            this.loadPath = Path.GetDirectoryName(path);

            Dictionary<string, Character.Node> nodeDict = new Dictionary<string, Character.Node>();
            this.Root = BuildModelNodes(nodeDict, model.RootNode);

            BuildMaterials(model);
            BuildMeshes(model, model.RootNode, nodeDict, 0);

            List<Bone> allBones = new List<Bone>();
            GetAllBones(allBones);
            this.allBones = allBones.ToArray();
            int boneIdx = 0;

            Bone leftFoot = this.allBones.FirstOrDefault(b => b.node.kinectJoint.HasValue &&
                b.node.kinectJoint.Value == JointType.FootLeft);
            Bone rightFoot = this.allBones.FirstOrDefault(b => b.node.kinectJoint.HasValue &&
                b.node.kinectJoint.Value == JointType.FootRight);
            
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
                nodeDict.Values.Where(n => n.kinectJoint.HasValue).
                    Select(n => new KeyValuePair<KinectData.JointType, Node>(
                        n.kinectJoint.Value, n));
            foreach (var kv in keyvals)
                kNodes.Add(kv.Key, kv.Value);

            this.headToSpineSize = 0;
            for (int idx = 1; idx < BodyData.SpineToHeadJoints.Length; ++idx)
            {
                Vector3 pos0 = kNodes[BodyData.SpineToHeadJoints[idx - 1]].WorldTransform.ExtractTranslation();
                Vector3 pos1 = kNodes[BodyData.SpineToHeadJoints[idx]].WorldTransform.ExtractTranslation();
                this.headToSpineSize += (pos1 - pos0).Length;
            }
            //OutputDebug();
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

        double LoadAnimations(ai.Scene model, Dictionary<string, Character.Node> nodeDict)
        {
            double maxTime = 0;
            foreach (ai.Animation anim in model.Animations)
            {
                foreach (ai.NodeAnimationChannel aic in anim.NodeAnimationChannels)
                {
                    Character.Node node = nodeDict[aic.NodeName];
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

        static Vector3 GetJointColor(KinectData.JointType? kjjoint)
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

        private Node BuildModelNodes(Dictionary<string, Character.Node> nodeDict, ai.Node node)
        {
            Node n = new Node();
            n.name = node.Name;
            n.bindTransform.Value = FromMatrix(node.Transform);
            n.kinectJoint = GetKinectJoint(n.name);
            n.color = GetJointColor(n.kinectJoint);

            if (node.HasChildren)
                n.children = new List<Character.Node>();
            for (int i = 0; i < node.ChildCount; i++)
            {
                Character.Node mn = BuildModelNodes(nodeDict, node.Children[i]);
                n.children.Add(mn);
                mn.parent = n;
            }

            nodeDict.Add(n.name, n);
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
                        vtx.texcoord = FromVector(mesh.TextureCoordinateChannels[0][i]);
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
                            jt.Value = mat;
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
            Root.bindTransform.pos = body.top.localMat.ExtractTranslation();
            Vector3 bleftFoot = body.jointNodes[JointType.FootLeft].WorldMat.ExtractTranslation();
            Vector3 brightFoot = body.jointNodes[JointType.FootRight].WorldMat.ExtractTranslation();
            Vector3 bfootPos = (bleftFoot + brightFoot) * 0.5f;


            Bone cleftFoot = this.allBones.FirstOrDefault(b => b.node.kinectJoint.HasValue &&
                b.node.kinectJoint.Value == JointType.FootLeft);
            Bone crightFoot = this.allBones.FirstOrDefault(b => b.node.kinectJoint.HasValue &&
                b.node.kinectJoint.Value == JointType.FootRight);
            Vector3 leftFootPos = cleftFoot.node.WorldTransform.ExtractTranslation();
            Vector3 rightFootPos = crightFoot.node.WorldTransform.ExtractTranslation();
            Vector3 cfootPos = (leftFootPos + rightFootPos) * 0.5f;

            Vector3 bpos = Root.bindTransform.pos;
            bpos.Y += (bfootPos.Y - cfootPos.Y);
            Root.bindTransform.pos = bpos;

            //float bodyToCharRatio = body.bodyData.HeadToSpineSize / this.headToSpineSize;
            //Root.bindTransform.scl = new Vector3(bodyToCharRatio, bodyToCharRatio, bodyToCharRatio);
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
