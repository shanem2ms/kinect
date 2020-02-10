using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenTK.Graphics.ES30;
using OpenTK;
using GLObjects;
using Microsoft.Kinect;
using System.IO;

namespace kinectwall
{
    class BodyViz
    {
        private Program program;
        private VertexArray vertexArray;

        /// <summary>
        /// definition of bones
        /// </summary>
        private List<Tuple<JointType, JointType>> bones;

        public BodyViz()
        {
            program = Program.FromFiles("Body.vert", "Body.frag");
            ushort[] indices = new ushort[_Cube.Length];
            Vector3[] texCoords = new Vector3[_Cube.Length];
            for (int i = 0; i < indices.Length; ++i)
            {
                indices[i] = (ushort)i;
                int side = i / 6;
                texCoords[i] = new Vector3((float)(side % 2), (float)(side / 3), 1);
            }
            vertexArray = new VertexArray(program, _Cube, indices, texCoords, null);
            InitBones();
        }

        void InitBones()
        {
            // a bone defined as a line between two joints
            this.bones = new List<Tuple<JointType, JointType>>();

            // Torso
            this.bones.Add(new Tuple<JointType, JointType>(JointType.Head, JointType.Neck));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.Neck, JointType.SpineShoulder));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.SpineShoulder, JointType.SpineMid));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.SpineMid, JointType.SpineBase));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.SpineShoulder, JointType.ShoulderRight));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.SpineShoulder, JointType.ShoulderLeft));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.SpineBase, JointType.HipRight));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.SpineBase, JointType.HipLeft));

            // Right Arm
            this.bones.Add(new Tuple<JointType, JointType>(JointType.ShoulderRight, JointType.ElbowRight));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.ElbowRight, JointType.WristRight));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.WristRight, JointType.HandRight));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.HandRight, JointType.HandTipRight));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.WristRight, JointType.ThumbRight));

            // Left Arm
            this.bones.Add(new Tuple<JointType, JointType>(JointType.ShoulderLeft, JointType.ElbowLeft));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.ElbowLeft, JointType.WristLeft));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.WristLeft, JointType.HandLeft));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.HandLeft, JointType.HandTipLeft));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.WristLeft, JointType.ThumbLeft));

            // Right Leg
            this.bones.Add(new Tuple<JointType, JointType>(JointType.HipRight, JointType.KneeRight));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.KneeRight, JointType.AnkleRight));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.AnkleRight, JointType.FootRight));

            // Left Leg
            this.bones.Add(new Tuple<JointType, JointType>(JointType.HipLeft, JointType.KneeLeft));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.KneeLeft, JointType.AnkleLeft));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.AnkleLeft, JointType.FootLeft));

        }

        FileStream fs = null;

        public void Render(Matrix4 viewProj, Body[] bodies, TimeSpan frameTime)
        {
            // Select the program for drawing
            GL.UseProgram(program.ProgramName);
            if (bodies == null)
                return;

            if (App.Recording)
            {
                if (fs == null) fs = new FileStream("body.out", FileMode.Create, FileAccess.Write);
                byte[] bytes = BitConverter.GetBytes(frameTime.Ticks);
                fs.Write(bytes, 0, bytes.Length);
                bytes = BitConverter.GetBytes(bodies.Count());
                fs.Write(bytes, 0, bytes.Length);
                foreach (Body body in bodies)
                {
                    bytes = BitConverter.GetBytes(body.IsTracked);
                    fs.Write(bytes, 0, bytes.Length);
                    if (body.IsTracked)
                    {
                        IReadOnlyDictionary<JointType, Joint> joints = body.Joints;
                        foreach (var joint in body.Joints)
                        {
                            bytes = BitConverter.GetBytes((int)joint.Key);
                            fs.Write(bytes, 0, bytes.Length);

                            bytes = BitConverter.GetBytes(joint.Value.Position.X);
                            fs.Write(bytes, 0, bytes.Length);
                            bytes = BitConverter.GetBytes(joint.Value.Position.Y);
                            fs.Write(bytes, 0, bytes.Length);
                            bytes = BitConverter.GetBytes(joint.Value.Position.Z);
                            fs.Write(bytes, 0, bytes.Length);
                        }
                        foreach (var jointOrient in body.JointOrientations)
                        {
                            bytes = BitConverter.GetBytes((int)jointOrient.Key);
                            fs.Write(bytes, 0, bytes.Length);

                            bytes = BitConverter.GetBytes(jointOrient.Value.Orientation.X);
                            fs.Write(bytes, 0, bytes.Length);
                            bytes = BitConverter.GetBytes(jointOrient.Value.Orientation.Y);
                            fs.Write(bytes, 0, bytes.Length);
                            bytes = BitConverter.GetBytes(jointOrient.Value.Orientation.Z);
                            fs.Write(bytes, 0, bytes.Length);
                            bytes = BitConverter.GetBytes(jointOrient.Value.Orientation.W);
                            fs.Write(bytes, 0, bytes.Length);
                        }
                    }
                }
            }


            foreach (Body body in bodies)
            {
                if (body.IsTracked)
                {
                    IReadOnlyDictionary<JointType, Joint> joints = body.Joints;
                    foreach (var bone in bones)
                    {
                        Joint j1 = joints[bone.Item1];
                        Joint j2 = joints[bone.Item2];

                        Vector3 jointPos1 = new Vector3(j1.Position.X,
                            j1.Position.Y,
                            j1.Position.Z);
                        Vector3 jointPos2 = new Vector3(j2.Position.X,
                            j2.Position.Y,
                            j2.Position.Z);

                        Vector3 boneDX = Vector3.Normalize(jointPos2 - jointPos1);
                        Vector3 boneDY = Vector3.Cross(Vector3.UnitZ, boneDX);
                        Vector3 boneDZ = Vector3.Cross(Vector3.UnitY, boneDX);
                        Matrix4 rotate = new Matrix4(new OpenTK.Vector4(boneDX, 1),
                            new OpenTK.Vector4(boneDY, 1),
                            new OpenTK.Vector4(boneDZ, 1),
                            new OpenTK.Vector4(0, 0, 0, 1));
                        Vector3 bonePos = Vector3.Multiply((jointPos1 + jointPos2), 0.5f);

                        Matrix4 jointMat = Matrix4.CreateScale(new Vector3((jointPos2 - jointPos1).Length * 1.0f, 0.05f, 0.05f)) *
                           rotate *
                           Matrix4.CreateTranslation(bonePos);

                        Matrix4 matWorldViewProj = jointMat * viewProj;
                        // Compute the model-view-projection on CPU
                        // Set uniform state
                        GL.UniformMatrix4(program.LocationMVP, false, ref matWorldViewProj);
                        // Use the vertex array
                        vertexArray.Draw();
                    }
                    /*
                    foreach (var kv in joints)
                    {
                        Vector3 jointPos = new Vector3(kv.Value.Position.X,
                            kv.Value.Position.Y,
                            kv.Value.Position.Z);
                        Matrix4 jointMat = Matrix4.CreateScale(0.025f) *
                            Matrix4.CreateTranslation(jointPos);

                        Matrix4 matWorldViewProj = jointMat * viewProj;
                        // Compute the model-view-projection on CPU
                        // Set uniform state
                        GL.UniformMatrix4(program.LocationMVP, false, ref matWorldViewProj);
                        // Use the vertex array
                        vertexArray.Draw();
                    }*/
                }
            }
        }


        private static readonly Vector3[] _Cube = new Vector3[] {
            new Vector3(0.0f, 0.0f, 0.0f),  // 0 
            new Vector3(1.0f, 0.0f, 0.0f),  // 1
            new Vector3(1.0f, 1.0f, 0.0f),  // 2

            new Vector3(0.0f, 0.0f, 0.0f),  // 0 
            new Vector3(1.0f, 1.0f, 0.0f),  // 2
            new Vector3(0.0f, 1.0f, 0.0f),  // 3

            new Vector3(0.0f, 0.0f, 1.0f),  // 4
            new Vector3(1.0f, 0.0f, 1.0f),  // 5
            new Vector3(1.0f, 1.0f, 1.0f),  // 6

            new Vector3(0.0f, 0.0f, 1.0f),  // 4
            new Vector3(1.0f, 1.0f, 1.0f),  // 6
            new Vector3(0.0f, 1.0f, 1.0f),  // 7

            new Vector3(0.0f, 0.0f, 0.0f),  // 0 
            new Vector3(1.0f, 0.0f, 0.0f),  // 1
            new Vector3(1.0f, 0.0f, 1.0f),  // 5

            new Vector3(0.0f, 0.0f, 0.0f),  // 0 
            new Vector3(1.0f, 0.0f, 1.0f),  // 5
            new Vector3(0.0f, 0.0f, 1.0f),  // 4

            new Vector3(1.0f, 1.0f, 0.0f),  // 2
            new Vector3(0.0f, 1.0f, 0.0f),  // 3
            new Vector3(0.0f, 1.0f, 1.0f),  // 7

            new Vector3(1.0f, 1.0f, 0.0f),  // 2
            new Vector3(0.0f, 1.0f, 1.0f),  // 7
            new Vector3(1.0f, 1.0f, 1.0f),  // 6

            new Vector3(0.0f, 0.0f, 0.0f),  // 0 
            new Vector3(0.0f, 1.0f, 0.0f),  // 3
            new Vector3(0.0f, 1.0f, 1.0f),  // 7

            new Vector3(0.0f, 0.0f, 0.0f),  // 0 
            new Vector3(0.0f, 1.0f, 1.0f),  // 7
            new Vector3(0.0f, 0.0f, 1.0f),  // 4

            new Vector3(1.0f, 0.0f, 0.0f),  // 1
            new Vector3(1.0f, 1.0f, 0.0f),  // 2
            new Vector3(1.0f, 1.0f, 1.0f),  // 6

            new Vector3(1.0f, 0.0f, 0.0f),  // 1
            new Vector3(1.0f, 1.0f, 1.0f),  // 6
            new Vector3(1.0f, 0.0f, 1.0f),  // 5
        };

        private static readonly ushort[] _CubeIndices = new ushort[]
        {
            0, 1, 2,
            0, 2, 3,
            4, 5, 6,
            4, 6, 7,

            0, 1, 5,
            0, 5, 4,
            2, 3, 7,
            2, 7, 6,

            0, 3, 7,
            0, 7, 4,
            1, 2, 6,
            2, 6, 5
        };
    }
}
