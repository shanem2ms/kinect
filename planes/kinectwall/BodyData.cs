using System;
using System.Collections.Generic;
using System.Linq;
using OpenTK;
using System.IO;
using System.Diagnostics;

namespace KinectData
{
    public class BodyData
    {
        public static JointType[] SpineToHeadJoints = {
            JointType.SpineBase, JointType.SpineMid, JointType.SpineShoulder, JointType.Neck, JointType.Head };

        List<Frame> frames = new List<Frame>();
        List<long> timestamps;

        float headToSpineSize;
        public float HeadToSpineSize => headToSpineSize;

        Dictionary<JointType, List<float>> jointLengths = new Dictionary<JointType, List<float>>();

        public Tuple<long, long> TimeRange => new Tuple<long, long>(frames[0].timeStamp,
            frames.Last().timeStamp);

        public BodyData(string name)
        {
            FileStream fs = new FileStream(name, FileMode.Open, FileAccess.Read);
            byte[] bytes = new byte[fs.Length];
            fs.Read(bytes, 0, bytes.Length);
            int readPtr = 0;

            while (readPtr < bytes.Length)
            {
                Frame frame = new Frame();
                frame.timeStamp = BitConverter.ToInt64(bytes, readPtr);
                readPtr += sizeof(long);
                int nBodies = BitConverter.ToInt32(bytes, readPtr);
                readPtr += sizeof(int);
                frame.bodies = new Dictionary<ulong, Body>();
                for (int i = 0; i < nBodies; ++i)
                {
                    bool isTracked = BitConverter.ToBoolean(bytes, readPtr);
                    readPtr += sizeof(bool);
                    if (isTracked)
                    {
                        Body nb = new Body(this);

                        nb.lean.X = BitConverter.ToSingle(bytes, readPtr);
                        readPtr += sizeof(float);
                        nb.lean.Y = BitConverter.ToSingle(bytes, readPtr);
                        readPtr += sizeof(float);

                        for (int boneIdx = 0; boneIdx < 25; ++boneIdx)
                        {
                            JointType jt = (JointType)BitConverter.ToInt32(bytes, readPtr);
                            readPtr += sizeof(int);

                            TrackingState trackingState = (TrackingState)
                                BitConverter.ToInt32(bytes, readPtr);
                            readPtr += sizeof(int);

                            float[] posvals = new float[3];
                            for (int fidx = 0; fidx < 3; ++fidx)
                            {
                                posvals[fidx] = BitConverter.ToSingle(bytes, readPtr);
                                readPtr += sizeof(float);
                            }

                            float[] rotvals = new float[4];
                            for (int fidx = 0; fidx < 4; ++fidx)
                            {
                                rotvals[fidx] = BitConverter.ToSingle(bytes, readPtr);
                                readPtr += sizeof(float);
                            }

                            nb.joints.Add(jt, new Joint()
                            {
                                Position = new Vector3(posvals[0], posvals[1], posvals[2]),
                                Orientation = new Vector4(rotvals[0], rotvals[1], rotvals[2], rotvals[3]),
                                TrackingState = trackingState
                            });
                        }
                        nb.top = JointNode.MakeBodyDef();
                        nb.top.SetJoints(nb.joints);
                        nb.GetJointNodes();
                        nb.top.GetJointLengths(jointLengths);
                        frame.bodies.Add(0, nb);
                    }
                }
                frames.Add(frame);
            }

            List<float> sums = new List<float>(jointLengths[SpineToHeadJoints[0]]);
            JointType[] jts = SpineToHeadJoints.Skip(1).ToArray();
            foreach (JointType jt in jts)
            {
                List<float> vals = jointLengths[jt];
                for (int i = 0; i < sums.Count; ++i)
                    sums[i] += vals[i];
            }

            {
                List<float> jl = sums;
                jl.Sort();
                int clipnum = jl.Count() / 5;
                List<float> vals = jl.GetRange(clipnum, jl.Count() - clipnum * 2);
                float avg = vals.Average();
                this.headToSpineSize = avg;
            }

            frames.Sort();
            timestamps = frames.Select(f => f.timeStamp).ToList();

            Vector3 prevLeftHand = Vector3.Zero;
            long prevLeftHandTime = 0;
            Vector3 prevRightHand = Vector3.Zero;
            long prevRightHandTime = 0;
            double msPerTicks = 1.0 / TimeSpan.FromMilliseconds(1).Ticks;
            List<string> leftStr = new List<string>();
            for (int fIdx = 0; fIdx < frames.Count; ++fIdx)
            {
                Frame f = frames[fIdx];
                Body b = f.bodies.FirstOrDefault().Value;
                if (b == null)
                    continue;
                {
                    Joint j = b.joints[JointType.HandLeft];
                    if (j.TrackingState == TrackingState.Tracked ||
                        j.TrackingState == TrackingState.Inferred)
                    {
                        double ms = (f.timeStamp - prevLeftHandTime) * msPerTicks;
                        double leftToLeft = (j.Position - prevLeftHand).Length / ms * 1000;
                        
                        ms = (f.timeStamp - prevRightHandTime) * msPerTicks;
                        double leftToRight = (j.Position - prevRightHand).Length / ms * 1000;
                        if (leftToLeft > leftToRight)
                            leftStr.Add($"{fIdx} L2L {leftToLeft} L2R {leftToRight}");
                    }
                    else
                    {
                        leftStr.Add($"{fIdx} Left {j.TrackingState}");
                    }
                }
                {
                    Joint j = b.joints[JointType.HandRight];
                    if (j.TrackingState == TrackingState.Tracked ||
                        j.TrackingState == TrackingState.Inferred)
                    {
                        double ms = (f.timeStamp - prevRightHandTime) * msPerTicks;
                        double rightToRight = (j.Position - prevRightHand).Length / ms * 1000;
                        ms = (f.timeStamp - prevLeftHandTime) * msPerTicks;
                        double rightToLeft = (j.Position - prevLeftHand).Length / ms * 1000;
                        if (rightToRight > rightToLeft)
                            leftStr.Add($"{fIdx} R2R {rightToRight} R2L {rightToLeft}");
                    }
                    else
                    {
                        leftStr.Add($"{fIdx} Right {j.TrackingState}");
                    }
                }
                {
                    Joint j = b.joints[JointType.HandLeft];
                    if (j.TrackingState == TrackingState.Tracked)
                    {
                        prevLeftHand = j.Position;
                        prevLeftHandTime = f.timeStamp;
                    }
                }
                {
                    Joint j = b.joints[JointType.HandRight];
                    if (j.TrackingState == TrackingState.Tracked)
                    {
                        prevRightHand = j.Position;
                        prevRightHandTime = f.timeStamp;
                    }
                }
            }

            string lstr = string.Join("\n", leftStr.ToArray());
            System.Diagnostics.Debug.WriteLine(lstr);

        }

        public Frame GetInterpolatedFrame(long timestamp)
        {
            int idx = timestamps.BinarySearch(timestamp);
            if (idx >= 0f)
                return frames[idx];
            else
            {
                int rightFrameIdx = ~idx;
                if (rightFrameIdx >= frames.Count)
                    return frames.Last();
                int leftFrameIdx = rightFrameIdx - 1;
                if (leftFrameIdx < 0f)
                    return frames[0];

                Frame leftFrame = frames[leftFrameIdx];
                Frame rightFrame = frames[rightFrameIdx];
                float interpVal = (float)(timestamp - leftFrame.timeStamp) / (rightFrame.timeStamp -
                    leftFrame.timeStamp);
                return new Frame(leftFrame, rightFrame, interpVal);
            }
        }
    }



    public class JointNode
    {
        public JointType jt;
        public Vector3 color;
        public float jointLength;
        public Matrix4 localMat;
        public float boneThickness = 1.0f;
        public float jointThickness = 2.0f;

        JointNode parent;
        public JointNode[] children;
        public Matrix4 WorldMat => localMat * ((parent != null) ? parent.WorldMat : Matrix4.Identity);

        public JointNode Parent => parent;

        public Vector3 OriginalWsPos;
        public Quaternion OrigWsOrientation;
        public Matrix3 origRotMat;
        public TrackingState Tracked;

        public void DebugDebugInfo(int level)
        {
            Quaternion q = localMat.ExtractRotation();
            Vector4 rotaxisang = q.ToAxisAngle();
            rotaxisang.W *= 180.0f / (float)Math.PI;
            Debug.WriteLine(new string(' ', level * 2) + $"{jt} - rot={rotaxisang} trn={localMat.ExtractTranslation()}");
            if (children != null)
            {
                foreach (JointNode cn in children)
                {
                    cn.DebugDebugInfo(level + 1);
                }
            }
        }


        public void SetJointColor(JointType jt, Vector3 color)
        {
            if (this.jt == jt) this.color = color;
            else this.color = new Vector3(0.1f, 0.1f, 0.1f);
            if (children != null)
            {
                foreach (JointNode cn in children)
                {
                    cn.SetJointColor(jt, color);
                }
            }

        }

        public void GetJointLengths(Dictionary<JointType, List<float>> jointLengths)
        {
            List<float> jl;
            if (!jointLengths.TryGetValue(jt, out jl))
            {
                jl = new List<float>();
                jointLengths.Add(jt, jl);
            }
            jl.Add(jointLength);

            if (this.children != null)
            {
                foreach (JointNode cn in this.children)
                {
                    cn.GetJointLengths(jointLengths);
                }
            }
        }


        Quaternion FromToVector(Vector3 v1, Vector3 v2)
        {
            Vector3 v1n = v1.Normalized();
            Vector3 v2n = v2.Normalized();
            Quaternion q = new Quaternion(Vector3.Cross(v1n, v2n),
                1.0f + Vector3.Dot(v1n, v2n));
            return q.Normalized();
        }

        Vector3[] rotvecs = new Vector3[3] { Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ };
        float[] rotDeg = new float[] { 0f, (float)Math.PI / 2.0f, (float)Math.PI, (float)Math.PI + (float)Math.PI / 2.0f };
        static float CapVal(float val)
        {
            if (Math.Abs(val) < 0.01f)
                return 0;
            if (Math.Abs(val - 1f) < 0.01f)
                return 1;
            if (Math.Abs(val + 1f) < 0.01f)
                return -1;
            return val;
        }

        public void SetJoints(Dictionary<JointType, Joint> jointPositions)
        {
            SetJointsRec(jointPositions, Matrix3.Identity, Matrix4.Identity);
            SetJointLengths(Matrix4.Identity);

            Dictionary<JointType, JointNode> allNodes = new Dictionary<JointType, JointNode>();
            GetAllJointNodes(allNodes);

        }

        void SetJointsRec(Dictionary<JointType, Joint> jointPositions, Matrix3 parentRot, Matrix4 parentWorldMat)
        {
            Matrix4 wInv = parentWorldMat.Inverted();

            Joint joint = jointPositions[jt];
            this.OrigWsOrientation = new Quaternion(joint.Orientation.X, joint.Orientation.Y, joint.Orientation.Z, joint.Orientation.W);
            this.OriginalWsPos = joint.Position;
            this.origRotMat = Matrix3.CreateFromQuaternion(this.OrigWsOrientation);
            this.Tracked = joint.TrackingState;
            Vector3 position = Vector3.TransformPosition(
                joint.Position, wInv);

            if (OrigWsOrientation.LengthSquared == 0)
            {
                Vector3 jointDir = position.Normalized();
                Vector3 xDir = Vector3.Cross(jointDir, Vector3.UnitZ);
                Vector3 zDir = Vector3.Cross(xDir, jointDir);
                Matrix3 matrix3 = new Matrix3(xDir, jointDir, zDir);
                this.localMat = new Matrix4(matrix3) *
                    Matrix4.CreateTranslation(position);
            }
            else
            {
                Matrix3 localRot =
                    this.origRotMat * parentRot.Inverted();
                this.localMat = new Matrix4(localRot) *
                    Matrix4.CreateTranslation(position);
            }

            Matrix4 worldMat = localMat * parentWorldMat;

            if (this.children != null)
            {
                foreach (JointNode cn in this.children)
                {
                    cn.SetJointsRec(jointPositions, this.origRotMat, worldMat);
                }
            }
        }

        public void GetAllJointNodes(Dictionary<JointType, JointNode> jointNodes)
        {
            jointNodes.Add(this.jt, this);
            if (children != null)
            {
                foreach (JointNode cn in children)
                {
                    cn.GetAllJointNodes(jointNodes);
                }
            }
        }

        public void SetJointLengths(Matrix4 parentWorldMat)
        {
            Vector3 parentWp = Vector3.TransformPosition(Vector3.Zero, parentWorldMat);
            Matrix4 worldMat = localMat * parentWorldMat;

            if (parent != null)
            {
                Vector3 connectionPos =
                    Vector3.TransformPosition(parentWp, worldMat.Inverted());
                jointLength = connectionPos.Length;
            }

            if (this.children != null)
            {
                foreach (JointNode cn in this.children)
                {
                    cn.SetJointLengths(worldMat);
                }
            }
        }

        void SetParents(JointNode parent)
        {
            this.parent = parent;
            if (children != null)
            {
                foreach (JointNode cn in children)
                {
                    cn.SetParents(this);
                }
            }
        }

        public void DrawNode(Action<JointNode> drawCallback)
        {
            drawCallback(this);
            if (this.children != null)
            {
                foreach (JointNode cn in children)
                {
                    cn.DrawNode(drawCallback);
                }
            }
        }


        static float Lerp(float l, float r, float i)
        {
            return l * (1 - i) + r * i;
        }

        static Quaternion Lerp(Quaternion l, Quaternion r, float i)
        {
            return (l * (1 - i) + r * i).Normalized();
        }

        static Vector3 Lerp(Vector3 l, Vector3 r, float i)
        {
            return l * (1 - i) + r * i;
        }

        static Matrix3 Lerp(Matrix3 l, Matrix3 r, float i)
        {
            Quaternion qi = Lerp(l.ExtractRotation(), r.ExtractRotation(), i);
            return Matrix3.CreateFromQuaternion(qi);
        }
        static Matrix4 Lerp(Matrix4 l, Matrix4 r, float i)
        {
            Quaternion qi = Lerp(l.ExtractRotation(), r.ExtractRotation(), i);
            Vector3 pi = Lerp(l.ExtractTranslation(), r.ExtractTranslation(), i);
            return Matrix4.CreateFromQuaternion(qi) *
                Matrix4.CreateTranslation(pi);
        }

        public JointNode() { }

        public JointNode(JointNode left, JointNode right, float interpVal)
        {
            boneThickness = Lerp(right.boneThickness, left.boneThickness, interpVal);
            jointThickness = Lerp(right.jointThickness, left.jointThickness, interpVal);
            localMat = Lerp(left.localMat, right.localMat, interpVal);
            jointLength = Lerp(left.jointLength, right.jointLength, interpVal);
            color = Lerp(left.color, right.color, interpVal);
            jt = left.jt;
            OrigWsOrientation = Lerp(left.OrigWsOrientation, right.OrigWsOrientation, interpVal);
            OriginalWsPos = Lerp(left.OriginalWsPos, right.OriginalWsPos, interpVal);
            origRotMat = Lerp(left.origRotMat, right.origRotMat, interpVal);
            Tracked = left.Tracked;

            children = new JointNode[left.children.Length];
            for (int idx = 0; idx < children.Length; ++idx)
            {
                children[idx] = new JointNode(left.children[idx], right.children[idx], interpVal);
                children[idx].parent = this;
            }
        }

        public static JointNode MakeBodyDef()
        {
            JointNode jn = new JointNode()
            {
                jt = JointType.SpineBase,
                boneThickness = 15f,
                color = new Vector3(0.0f, 1.0f, 0.0f),
                children = new JointNode[] {
                    new JointNode()
                    {
                        jt = JointType.SpineMid,
                        boneThickness = 15f,
                        color = new Vector3(0.6f, 0.6f, 0.6f),
                        children = new JointNode[]
                        {
                            new JointNode()
                            {
                                jt = JointType.SpineShoulder,
                                boneThickness = 15f,
                                color = new Vector3(0.7f, 0.7f, 0.7f),
                                children = new JointNode[]
                                {
                                    new JointNode()
                                    {
                                        jt = JointType.Neck,
                                        boneThickness = 10f,
                                        color = new Vector3(0.8f, 0.8f, 0.8f),
                                        children = new JointNode[]
                                        {
                                            new JointNode()
                                            {
                                                boneThickness = 8f,
                                                jt = JointType.Head,
                                                color = new Vector3(0.9f, 0.9f, 0.9f),
                                                children = new JointNode[]
                                                {
                                                }
                                            },
                                            new JointNode()
                                            {
                                                jt = JointType.ShoulderLeft,
                                                boneThickness = 4f,
                                                color = new Vector3(0.9f, 0.9f, 0f),
                                                children = new JointNode[]
                                                {
                                                    new JointNode()
                                                    {
                                                        jt = JointType.ElbowLeft,
                                                        boneThickness = 4f,
                                                        color = new Vector3(0.9f, 0.5f, 0f),
                                                        children = new JointNode[]
                                                        {
                                                            new JointNode()
                                                            {
                                                                color = new Vector3(0.9f, 0.25f, 0f),
                                                                boneThickness = 4f,
                                                                jt = JointType.WristLeft,
                                                                children = new JointNode[]
                                                                {
                                                                    new JointNode()
                                                                    {
                                                                        jt = JointType.HandLeft,
                                                                        boneThickness = 7f,
                                                                        color = new Vector3(0.9f, 0.05f, 0f),
                                                                        children = new JointNode[]
                                                                        {
                                                                        }
                                                                    }
                                                                }
                                                            }
                                                        }
                                                    }
                                                }
                                            },
                                            new JointNode()
                                            {
                                                jt = JointType.ShoulderRight,
                                                boneThickness = 4f,
                                                color = new Vector3(0f, 0.9f, 0.9f),
                                                children = new JointNode[]
                                                {
                                                    new JointNode()
                                                    {
                                                        jt = JointType.ElbowRight,
                                                        boneThickness = 4f,
                                                        color = new Vector3(0f, 0.9f, 0.5f),
                                                        children = new JointNode[]
                                                        {
                                                            new JointNode()
                                                            {
                                                                jt = JointType.WristRight,
                                                                boneThickness = 4f,
                                                                color = new Vector3(0f, 0.9f, 0.25f),
                                                                children = new JointNode[]
                                                                {
                                                                    new JointNode()
                                                                    {
                                                                        jt = JointType.HandRight,
                                                                        boneThickness = 7f,
                                                                        color = new Vector3(0f, 0.9f, 0.05f),
                                                                        children = new JointNode[]
                                                                        {
                                                                        }
                                                                    }
                                                                }
                                                            }
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                    }

                                }
                            }
                        }
                    },
                    new JointNode()
                    {
                        jt = JointType.HipLeft,
                        boneThickness = 10f,
                        color = new Vector3(0.9f, 0f, 0.9f),
                        children = new JointNode[]
                        {
                            new JointNode()
                            {
                                jt = JointType.KneeLeft,
                                boneThickness = 5f,
                                color = new Vector3(0.9f, 0f, 0.65f),
                                children = new JointNode[]
                                {
                                    new JointNode()
                                    {
                                        jt = JointType.AnkleLeft,
                                        boneThickness = 5f,
                                        color = new Vector3(0.9f, 0f, 0.45f),
                                        children = new JointNode[]
                                        {
                                            new JointNode()
                                            {
                                                jt = JointType.FootLeft,
                                                boneThickness = 5f,
                                                color = new Vector3(0.9f, 0f, 0.25f),
                                                children = new JointNode[]
                                                {
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    },
                    new JointNode()
                    {
                        jt = JointType.HipRight,
                        boneThickness = 10f,
                        color = new Vector3(0.5f, 0.9f, 0.9f),
                        children = new JointNode[]
                        {
                            new JointNode()
                            {
                                jt = JointType.KneeRight,
                                boneThickness = 5f,
                                color = new Vector3(0.5f, 0.9f, 0.65f),
                                children = new JointNode[]
                                {
                                    new JointNode()
                                    {
                                        jt = JointType.AnkleRight,
                                        boneThickness = 5f,
                                        color = new Vector3(0.5f, 0.9f, 0.45f),
                                        children = new JointNode[]
                                        {
                                            new JointNode()
                                            {
                                                jt = JointType.FootRight,
                                                boneThickness = 5f,
                                                color = new Vector3(0.5f, 0.9f, 0.25f),
                                                children = new JointNode[]
                                                {
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    },
                }
            };
            jn.SetParents(null);
            return jn;
        }
    }
    //
    // Summary:
    //     Specifies the state of tracking a body or body's attribute.
    public enum TrackingState
    {
        //
        // Summary:
        //     The joint data is not tracked and no data is known about this joint.
        NotTracked = 0,
        //
        // Summary:
        //     The joint data is inferred and confidence in the position data is lower than
        //     if it were Tracked.
        Inferred = 1,
        //
        // Summary:
        //     The joint data is being tracked and the data can be trusted.
        Tracked = 2
    }


    //
    // Summary:
    //     The types of joints of a Body.
    public enum JointType
    {
        //
        // Summary:
        //     Base of the spine.
        SpineBase = 0,
        //
        // Summary:
        //     Middle of the spine.
        SpineMid = 1,
        //
        // Summary:
        //     Neck.
        Neck = 2,
        //
        // Summary:
        //     Head.
        Head = 3,
        //
        // Summary:
        //     Left shoulder.
        ShoulderLeft = 4,
        //
        // Summary:
        //     Left elbow.
        ElbowLeft = 5,
        //
        // Summary:
        //     Left wrist.
        WristLeft = 6,
        //
        // Summary:
        //     Left hand.
        HandLeft = 7,
        //
        // Summary:
        //     Right shoulder.
        ShoulderRight = 8,
        //
        // Summary:
        //     Right elbow.
        ElbowRight = 9,
        //
        // Summary:
        //     Right wrist.
        WristRight = 10,
        //
        // Summary:
        //     Right hand.
        HandRight = 11,
        //
        // Summary:
        //     Left hip.
        HipLeft = 12,
        //
        // Summary:
        //     Left knee.
        KneeLeft = 13,
        //
        // Summary:
        //     Left ankle.
        AnkleLeft = 14,
        //
        // Summary:
        //     Left foot.
        FootLeft = 15,
        //
        // Summary:
        //     Right hip.
        HipRight = 16,
        //
        // Summary:
        //     Right knee.
        KneeRight = 17,
        //
        // Summary:
        //     Right ankle.
        AnkleRight = 18,
        //
        // Summary:
        //     Right foot.
        FootRight = 19,
        //
        // Summary:
        //     Between the shoulders on the spine.
        SpineShoulder = 20,
        //
        // Summary:
        //     Tip of the left hand.
        HandTipLeft = 21,
        //
        // Summary:
        //     Left thumb.
        ThumbLeft = 22,
        //
        // Summary:
        //     Tip of the right hand.
        HandTipRight = 23,
        //
        // Summary:
        //     Right thumb.
        ThumbRight = 24
    }

    public class Joint
    {
        public Vector3 Position;
        public Vector4 Orientation;
        public TrackingState TrackingState;
    }

    public class Body
    {
        public Dictionary<JointType, Joint> joints =
            new Dictionary<JointType, Joint>();

        public Dictionary<JointType, JointNode> jointNodes =
                new Dictionary<JointType, JointNode>();


        public BodyData bodyData;
        public JointNode top = null;
        public Vector2 lean;
        public FaceMesh face;

        public Body(BodyData bd) { bodyData = bd; }

        public Body(Body left, Body right, float interpVal)
        {
            bodyData = left.bodyData;
            joints = left.joints;
            top = new JointNode(left.top, right.top, interpVal);
            top.SetJointLengths(Matrix4.Identity);
            GetJointNodes();
        }

        public void SetJointColor(JointType jt, Vector3 color)
        {
            top.SetJointColor(jt, color);
        }
        public void DumpDebugInfo()
        {
            top.DebugDebugInfo(0);
        }

        public void GetJointNodes()
        {
            top.GetAllJointNodes(this.jointNodes);
        }
    }

    public class FaceMesh
    {
        public uint[] indices = null;
        public Vector3[] pos;
    }


    public class Frame : IComparable<Frame>
    {
        public Dictionary<ulong, Body> bodies;
        public long timeStamp;
        public int CompareTo(Frame other)
        {
            return timeStamp.CompareTo(other.timeStamp);
        }

        public Frame()
        {

        }


        public void DumpDebugInfo()
        {
            foreach (var b in bodies.Values)
                if (b != null) b.DumpDebugInfo();
        }

        public void SetJointColor(JointType jt, Vector3 color)
        {
            foreach (Body b in bodies.Values)
            {
                b.SetJointColor(jt, color);
            }
        }

        public Frame(Frame left, Frame right, float interpVal)
        {
            this.bodies = new Dictionary<ulong, Body>();
            foreach (var kb in left.bodies)
            {
                Body rightBody;
                if (right.bodies.TryGetValue(kb.Key, out rightBody))
                {
                    this.bodies.Add(kb.Key,
                        new Body(kb.Value, rightBody, interpVal));
                }
            }
        }
    }

    public class JointLimits : kinectwall.BaseNotifier
    {
        private static Vector3 QToEuler(Quaternion q)
        {
            double eX = Math.Atan2(-2 * (q.Y * q.Z - q.W * q.X), q.W * q.W - q.X * q.X - q.Y * q.Y + q.Z * q.Z);
            double eY = Math.Asin(2 * (q.X * q.Z + q.W * q.Y));
            double eZ = Math.Atan2(-2 * (q.X * q.Y - q.W * q.Z), q.W * q.W + q.X * q.X - q.Y * q.Y - q.Z * q.Z);
            return new Vector3((float)eX, (float)eY, (float)eZ);
        }

        public static JointLimits []Build()
        {
            JointLimits[] limits = new JointLimits[(int)JointType.ThumbRight + 1];
            for (int i = 0; i < limits.Length; ++i)
                limits[i] = new JointLimits((JointType)i);
            return limits;
        }

        public KinectData.JointType jt { get; }
        Vector3 minVals = new Vector3(1, 1, 1);
        public Vector3 MinVals => minVals;
        Vector3 maxVals = new Vector3(-1, -1, -1);
        public Vector3 MaxVals => maxVals;
        const float PiInv = 1.0f / (float)Math.PI;

        public Vector3 Range { get
            {
                return new Vector3(
                    Math.Max(0, maxVals.X - minVals.X),
                    Math.Max(0, maxVals.Y - minVals.Y),
                    Math.Max(0, maxVals.Z - minVals.Z));
            }
        }

        JointLimits(JointType _jt)
        {
            jt = _jt;
        }
        public void ApplyQuaternion(Quaternion q)
        {
            Vector3 v = QToEuler(q) * PiInv;
            bool minupdated = false,
                maxupdated = false;
            if (v.X < minVals.X)
            {
                minVals.X = v.X;
                minupdated = true;
            }
            if (v.X > maxVals.X)
            {
                maxVals.X = v.X;
                maxupdated = true;
            }
            if (v.Y < minVals.Y)
            {
                minVals.Y = v.Y;
                minupdated = true;
            }
            if (v.Y > maxVals.Y)
            {
                maxVals.Y = v.Y;
                maxupdated = true;
            }
            if (v.Z < minVals.Z)
            {
                minVals.Z = v.Z;
                minupdated = true;
            }
            if (v.Z > maxVals.Z)
            {
                maxVals.Z = v.Z;
                maxupdated = true;
            }

            if (minupdated)
                OnPropertyChanged("MinVals");
            if (maxupdated)
                OnPropertyChanged("MaxVals");
            if (minupdated || maxupdated)
                OnPropertyChanged("Range");
        }

        public override string ToString()
        {
            return $"min: {minVals}  max: {maxVals}";
        }
    }

    public static class JointConstraints
    {
        const float eps = 1e-5f;

        public class Limit
        {            
            public Vector3 upper;
            public Vector3 lower;
        }

        public static Limit[] Limits = new Limit[]
        {
//        SpineBase = 0,
            new Limit() { upper = new Vector3(eps, eps, eps), lower = new Vector3(-eps, -eps, -eps) },
//        SpineMid = 1,
            new Limit() { upper = new Vector3(eps, eps, eps), lower = new Vector3(-eps, -eps, -eps) },
//        Neck = 2,
            new Limit() { upper = new Vector3(eps, eps, eps), lower = new Vector3(-eps, -eps, -eps) },
//        Head = 3,
            new Limit() { upper = new Vector3(eps, eps, eps), lower = new Vector3(-eps, -eps, -eps) },
//        ShoulderLeft = 4,
            new Limit() { upper = new Vector3(1, 1, 1), lower = new Vector3(-1, -1, -1) },
//        ElbowLeft = 5,
            new Limit() { upper = new Vector3(1, eps, eps), lower = new Vector3(-1, -eps, -eps) },
//        WristLeft = 6,
            new Limit() { upper = new Vector3(eps, eps, eps), lower = new Vector3(-eps, -eps, -eps) },
//        HandLeft = 7,
            new Limit() { upper = new Vector3(eps, eps, eps), lower = new Vector3(-eps, -eps, -eps) },
//        ShoulderRight = 8,
            new Limit() { upper = new Vector3(1, 1, 1), lower = new Vector3(-1, -1, -1) },
//        ElbowRight = 9,
            new Limit() { upper = new Vector3(1, eps, eps), lower = new Vector3(-1, -eps, -eps) },
//        WristRight = 10,
            new Limit() { upper = new Vector3(eps, eps, eps), lower = new Vector3(-eps, -eps, -eps) },
//        HandRight = 11,
            new Limit() { upper = new Vector3(eps, eps, eps), lower = new Vector3(-eps, -eps, -eps) },
//        HipLeft = 12,
            new Limit() { upper = new Vector3(1, 1, 1), lower = new Vector3(-1, -1, -1) },
//        KneeLeft = 13,
            new Limit() { upper = new Vector3(1, eps, eps), lower = new Vector3(-1, -eps, -eps) },
//        AnkleLeft = 14,
            new Limit() { upper = new Vector3(eps, eps, eps), lower = new Vector3(-eps, -eps, -eps) },
//        FootLeft = 15,
            new Limit() { upper = new Vector3(eps, eps, eps), lower = new Vector3(-eps, -eps, -eps) },
//        HipRight = 16,
            new Limit() { upper = new Vector3(1, 1, 1), lower = new Vector3(-1, -1, -1) },
//        KneeRight = 17,
            new Limit() { upper = new Vector3(1, eps, eps), lower = new Vector3(-1, -eps, -eps) },
//        AnkleRight = 18,
            new Limit() { upper = new Vector3(eps, eps, eps), lower = new Vector3(-eps, -eps, -eps) },
//        FootRight = 19,
            new Limit() { upper = new Vector3(eps, eps, eps), lower = new Vector3(-eps, -eps, -eps) },
//        SpineShoulder = 20,
            new Limit() { upper = new Vector3(eps, eps, eps), lower = new Vector3(-eps, -eps, -eps) },
//        HandTipLeft = 21,
            new Limit() { upper = new Vector3(eps, eps, eps), lower = new Vector3(-eps, -eps, -eps) },
//        ThumbLeft = 22,
            new Limit() { upper = new Vector3(eps, eps, eps), lower = new Vector3(-eps, -eps, -eps) },
//        HandTipRight = 23,
            new Limit() { upper = new Vector3(eps, eps, eps), lower = new Vector3(-eps, -eps, -eps) },
//        ThumbRight = 24
        };
    }
    public static class PoseData
    {
        public class Joint
        {
            public Joint(JointType _jt, Vector4 _rot, Vector3 _trn)
            {
                jt = _jt;
                rot = Quaternion.FromAxisAngle(new Vector3(_rot), _rot.W * (float)Math.PI / 180.0f);
                trn = _trn;
            }
            public JointType jt;
            public Quaternion rot;
            public Vector3 trn;
        }

        static Joint[] joints = new Joint[] {
            new Joint(JointType.SpineBase, new Vector4(0.9942161f, 0.1074039f, 0f, 9.116547f), new Vector3(0.04066718f, -0.376448f, 2.359505f)),
            new Joint(JointType.SpineMid, new Vector4(-0.9998327f, -0.01829153f, -9.333396E-10f, 93.76741f), new Vector3(-0.005831068f, 0.3187318f, -0.02099164f)),
            new Joint(JointType.SpineShoulder, new Vector4(-0.9997728f, -0.01975309f, 0f, 1.834985f), new Vector3(-0.0001477433f, 0.0074778f, 0.2334016f)),
            new Joint(JointType.Neck, new Vector4(-0.9998381f, -0.01881689f, 0f, 2.381824f), new Vector3(-5.953809E-05f, 0.003163565f, 0.0760673f)),
            new Joint(JointType.Head, new Vector4(-0.4896227f, 0.8719401f, 0f, 3.293739f), new Vector3(0.008494768f, 0.004770085f, 0.1692971f)),
            new Joint(JointType.ShoulderLeft, new Vector4(0.09970951f, -0.9950166f, -1.564339E-08f, 107.7204f), new Vector3(-0.2043049f, -0.02047318f, -0.06560931f)),
            new Joint(JointType.ElbowLeft, new Vector4(0.1150492f, 0.9933622f, -1.442467E-09f, 18.83373f), new Vector3(0.08088996f, -0.009368523f, 0.2387395f)),
            new Joint(JointType.WristLeft, new Vector4(-0.5498477f, 0.8352633f, 1.261903E-09f, 5.293272f), new Vector3(0.01890495f, 0.0124472f, 0.2442859f)),
            new Joint(JointType.HandLeft, new Vector4(0.04536316f, 0.9989712f, 0f, 11.94175f), new Vector3(0.02336239f, -0.001072822f, 0.1105974f)),
            new Joint(JointType.ShoulderRight, new Vector4(0.10637f, 0.9943267f, 8.19519E-09f, 114.6131f), new Vector3(0.1859358f, -0.01989083f, -0.08566561f)),
            new Joint(JointType.ElbowRight, new Vector4(-0.12358f, -0.9923341f, -1.436953E-09f, 18.90872f), new Vector3(-0.07522178f, 0.009367723f, 0.2212914f)),
            new Joint(JointType.WristRight, new Vector4(-0.4798411f, -0.8773544f, -2.017027E-09f, 13.348f), new Vector3(-0.05448611f, 0.02979944f, 0.2617323f)),
            new Joint(JointType.HandRight, new Vector4(-0.1330791f, -0.9911101f, -5.404217E-10f, 6.18323f), new Vector3(-0.01084462f, 0.001449653f, 0.1009915f)),
            new Joint(JointType.HipLeft, new Vector4(0.01261382f, -0.9999205f, 0f, 116.9627f), new Vector3(-0.08589271f, -0.001083517f, -0.0436977f)),
            new Joint(JointType.KneeLeft, new Vector4(0.9927685f, 0.1200447f, 0f, 83.63462f), new Vector3(0.04354432f, -0.3601108f, 0.04046511f)),
            new Joint(JointType.AnkleLeft, new Vector4(0.3396894f, 0.9405374f, 3.142197E-09f, 17.24103f), new Vector3(0.1082723f, -0.03910416f, 0.370943f)),
            new Joint(JointType.FootLeft, new Vector4(-0.2767107f, -0.9609533f, 0f, 93.09793f), new Vector3(-0.1520643f, 0.04378759f, -0.008564442f)),
            new Joint(JointType.HipRight, new Vector4(-0.0126296f, 0.9999203f, -1.983853E-09f, 110.1319f), new Vector3(0.08590463f, 0.001085032f, -0.03149338f)),
            new Joint(JointType.KneeRight, new Vector4(0.9804152f, -0.1969417f, 2.240472E-08f, 86.05919f), new Vector3(-0.07307392f, -0.3637771f, 0.0255608f)),
            new Joint(JointType.AnkleRight, new Vector4(0.3143786f, -0.9492956f, 1.418607E-09f, 9.446458f), new Vector3(-0.06327723f, -0.02095552f, 0.4006202f)),
            new Joint(JointType.FootRight, new Vector4(-0.1347214f, 0.9908835f, -1.491198E-08f, 92.18292f), new Vector3(0.1568054f, 0.02131936f, -0.006032104f)),
        };


        public static Joint[] JointsIdx = null;
        static PoseData()
        {
            JointsIdx = new Joint[24];
            foreach (Joint j in joints)
            {
                JointsIdx[(int)j.jt] = j;
            }
        }

        public static Vector3[] JointVals = new Vector3[]
        {
        new Vector3(0, 0, 0),
        //SpineBase = 0,
        new Vector3(0, 0, 0),
        //SpineMid = 1,
        new Vector3(0, -1, 0),
        //Neck = 1,
        new Vector3(0, -1, 0),
        //Head = 1,
        new Vector3(-1, -0.5f, 0),
        //ShoulderLeft = 1,
        new Vector3(-1, -0.5f, 0),
        //ElbowLeft = 1,
        new Vector3(-1, -0.5f, 0),
        //WristLeft = 1,
        new Vector3(-1, -0.5f, 0),
        //HandLeft = 1,
        new Vector3(1, -0.5f, 0),
        //ShoulderRight = 1,
        new Vector3(1, -0.5f, 0),
        //ElbowRight = 1,
        new Vector3(1, -0.5f, 0),
        //WristRight = 1,
        new Vector3(1, -0.5f, 0),
        //HandRight = 1,
        new Vector3(-1, 0.5f, 0),
        //HipLeft = 1,
        new Vector3(-1, 0.5f, 0),
        //KneeLeft = 1,
        new Vector3(-1, 1, 0),
        //AnkleLeft = 1,
        new Vector3(-1, 1, 0),
        //FootLeft = 1,
        new Vector3(1, 0.5f, 0),
        //HipRight = 1,
        new Vector3(1, 0.5f, 0),
        //KneeRight = 1,
        new Vector3(1, 1, 0),
        //AnkleRight = 1,
        new Vector3(1, 1, 0),
        //FootRight = 1,        
        new Vector3(0, 0, 0),
        //SpineShoulder = 1,
        new Vector3(-1, -0.5f, 0),
        //HandTipLeft = 1,
        new Vector3(-1, -0.5f, 0),
        //ThumbLeft = 1,
        new Vector3(1, -0.5f, 0),
        //HandTipRight = 1,
        new Vector3(1, -0.5f, 0)
            //ThumbRight = 1,
        };


    }
}