using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenTK.Graphics.ES30;
using OpenTK;
using GLObjects;

namespace kinectwall
{
    class Scene
    {
        private Program program;
        private Program pickProgram;
        private VertexArray vertexArray;
        private VertexArray vertexArrayPick;
        public bool IsInitialized = false;

        List<SimObjectMesh> simObjects = new List<SimObjectMesh>();
        Dictionary<KinectData.JointType, Constraint> bodyDraggers
            = new Dictionary<KinectData.JointType, Constraint>();

        public Scene(Program _pickProgram)
        {
            program = Program.FromFiles("Main.vert", "Main.frag");
            pickProgram = _pickProgram;
            vertexArray = Cube.MakeCube(program);
            vertexArrayPick = Cube.MakeCube(pickProgram);
        }

        Random r = new Random();

        float RandomNum(float min, float max)
        {
            return (float)r.NextDouble() * (max - min) + min;
        }

        class MeshInfo
        {
            public Vector3 color;
            public Vector3 scale;
            public string name;

            public override string ToString()
            {
                return name;
            }
        }

        class ConstraintDef
        {
            public KinectData.JointType jt;
            public bool isTwoBodies = true;
            public SimObjectMesh node1;
            public Vector3 localPivot1;
            public SimObjectMesh node2;
            public Vector3 localPivot2;
        }

        Quaternion FromToVector(Vector3 v1, Vector3 v2)
        {
            Vector3 v1n = v1.Normalized();
            Vector3 v2n = v2.Normalized();
            Quaternion q = new Quaternion(Vector3.Cross(v1n, v2n),
                1.0f + Vector3.Dot(v1n, v2n));
            return q.Normalized();
        }

        bool firstDbgDraw = true;

        void DebugDrawLine(ref Matrix4 viewProj, Vector3 from, Vector3 to, Vector3 color)
        {
            if (firstDbgDraw)
            {
                GL.UseProgram(program.ProgramName);
                program.Set3("lightPos", new Vector3(2, 5, 2));
            }

            program.Set1("ambient", 1.0f);
            program.Set3("meshColor", color);
            Vector3 offset = (from + to) * 0.5f;
            Vector3 dir = (to - from);
            float len = (to - from).Length;
            dir /= len;

            Quaternion q = FromToVector(Vector3.UnitZ, dir);
            
            Matrix4 matWorld = Matrix4.CreateScale(0.001f, 0.001f, len / 2) *
                Matrix4.CreateFromQuaternion(q) *
                    Matrix4.CreateTranslation(offset);
            Matrix4 matWorldViewProj = matWorld * viewProj;
            GL.UniformMatrix4(program.LocationMVP, false, ref matWorldViewProj);

            program.SetMat4("uWorld", ref matWorld);
            Matrix4 matWorldInvT = matWorld.Inverted();
            matWorldInvT.Transpose();
            program.SetMat4("uWorldInvTranspose", ref matWorldInvT);

            vertexArray.Draw();
            firstDbgDraw = false;
        }

        public void Init(BulletSimulation simulation, KinectData.Frame bodyFrame)
        {
            simulation.DebugDrawLine = DebugDrawLine;
            {
                Vector3[] transvecs = new Vector3[6]
                {
                Vector3.UnitX,
                Vector3.UnitX * -1,
                Vector3.UnitY,
                Vector3.UnitY * -1,
                Vector3.UnitZ,
                Vector3.UnitZ * -1
                };

                Matrix4[] rotMats = new Matrix4[6]
                {
                Matrix4.CreateFromAxisAngle(Vector3.UnitZ, (float)Math.PI * 0.5f),
                Matrix4.CreateFromAxisAngle(Vector3.UnitZ, -(float)Math.PI * 0.5f),
                Matrix4.Identity,
                Matrix4.CreateFromAxisAngle(Vector3.UnitX, (float)Math.PI),
                Matrix4.CreateFromAxisAngle(Vector3.UnitX, (float)Math.PI * 0.5f),
                Matrix4.CreateFromAxisAngle(Vector3.UnitX, -(float)Math.PI * 0.5f)
                };

                Vector3[] wallcolors = new Vector3[6] {
                new Vector3(0, 1, 1),
                new Vector3(0, 1, 1),
                new Vector3(1, 1, 0),
                new Vector3(1, 1, 0),
                new Vector3(1, 0, 1),
                new Vector3(1, 0, 1) };

                for (int idx = 0; idx < wallcolors.Length; ++idx)
                {
                    wallcolors[idx] *= 0.6f;
                }

                Vector3 meshScale = new Vector3(5, 0.1f, 5);
                BulletSharp.TriangleMesh wall = Cube.MakeBulletMesh(meshScale);

                for (int i = 0; i < 6; ++i)
                {
                    Matrix4 matWorld =
                        rotMats[i] *
                        Matrix4.CreateTranslation(transvecs[i] * 5) *
                        Matrix4.CreateTranslation(new Vector3(0, 3.5f, 5));
                    SimObjectMesh obj = new SimObjectMesh(matWorld, 0.0f, wall);
                    obj.objectInfo = new MeshInfo()
                    {
                        color = wallcolors[i],
                        scale = meshScale,
                        name = $"wall{i}"
                    };
                    simObjects.Add(obj);
                }
            }

            Dictionary<KinectData.JointNode, SimObjectMesh>
                simDict = new Dictionary<KinectData.JointNode, SimObjectMesh>();

            List<ConstraintDef> constraints = new List<ConstraintDef>();
            foreach (var body in bodyFrame.bodies)
            {
                if (body != null)
                {
                    body.top.DrawNode((jn) =>
                    {
                        Matrix4 worldMat =
                            Matrix4.CreateTranslation(0, -jn.jointLength * 0.5f, 0) *
                            jn.WorldMat;
                        Vector3 meshScale = new Vector3(0.01f, jn.jointLength * 0.5f, 0.01f);
                        BulletSharp.TriangleMesh boneTM = Cube.MakeBulletMesh(meshScale);

                        SimObjectMesh obj = new SimObjectMesh(worldMat, 0.2f, boneTM);
                        obj.CollisionGroup = 64;
                        Vector3 vecjoint = KinectData.PoseData.JointVals[(int)jn.jt];

                        Vector3 lrColor = new Vector3(0.5f, 0.5f, 0.5f);
                        if (vecjoint.X > 0)
                            lrColor = new Vector3(0, 1, 0);
                        else if (vecjoint.X < 0)
                            lrColor = new Vector3(1, 0, 0);

                        obj.objectInfo = new MeshInfo()
                        {
                            color = lrColor,
                            scale = meshScale,
                            name = jn.jt.ToString()
                        };

                        simObjects.Add(obj);
                        simDict.Add(jn, obj);

                        if (jn.Parent != null)
                        {
                            SimObjectMesh parentObj = simDict[jn.Parent];
                            Vector3 localPivot = new Vector3(0, -jn.jointLength * 0.5f, 0);
                            Vector3 worldPivot = Vector3.TransformPosition(localPivot, obj.WorldMatrix);
                            Vector3 parentLocalPivot = Vector3.TransformPosition(worldPivot,
                                parentObj.WorldMatrix.Inverted());
                            constraints.Add(new ConstraintDef()
                            {
                                node1 = obj,
                                localPivot1 = localPivot,
                                node2 = parentObj,
                                localPivot2 = parentLocalPivot
                            });
                        }


                        /*if (jn.jt == KinectData.JointType.HandLeft ||
                            jn.jt == KinectData.JointType.HandRight ||
                            jn.jt == KinectData.JointType.FootLeft ||
                            jn.jt == KinectData.JointType.FootRight ||
                            jn.jt == KinectData.JointType.Head)*/
                        {
                            constraints.Add(new ConstraintDef()
                            {
                                isTwoBodies = false,
                                jt = jn.jt,
                                node1 = obj,
                                localPivot1 = Vector3.Zero
                            });
                        }

                    });
                }
            }

            foreach (var so in simObjects)
            {
                simulation.AddObj(so);
            }
            
            foreach (var con in constraints)
            {
                if (con.isTwoBodies)
                {
                    simulation.AddConst(new Constraint(
                        con.node1, con.localPivot1,
                        con.node2, con.localPivot2));
                }
                else
                {
                    Constraint c = new Constraint(
                        con.node1, con.localPivot1);
                    bodyDraggers.Add(con.jt, c);
                    simulation.AddConst(c);
                }
            }


            IsInitialized = true;
        }

        public void SetBodyFrame(KinectData.Frame bodyFrame)
        {
            foreach (var body in bodyFrame.bodies)
            {
                if (body != null)
                {
                    body.top.DrawNode((jn) =>
                    {
                        Constraint constraint;
                        if (bodyDraggers.TryGetValue(jn.jt, out constraint))
                        {
                            Matrix4 worldMat =
                                Matrix4.CreateTranslation(0, -jn.jointLength * 0.5f, 0) *
                                jn.WorldMat;
                            if (jn.Tracked == KinectData.TrackingState.Tracked)
                            {
                                constraint.Enabled = true;
                                constraint.UpdateWsPos(worldMat.ExtractTranslation());
                            }
                            else
                                constraint.Enabled = false;
                        }                    

                    });
                }
            }

        }

        public void Render(Matrix4 viewProj)
        {
            firstDbgDraw = true;
            // Select the program for drawing
            GL.UseProgram(program.ProgramName);

            program.Set1("ambient", 0.3f);
            foreach (SimObjectMesh obj in simObjects)
            {
                MeshInfo meshInfo = obj.objectInfo as MeshInfo;
                program.Set3("meshColor", meshInfo.color);

                program.Set3("lightPos", new Vector3(2, 5, 2));
                Matrix4 matWorld = Matrix4.CreateScale(meshInfo.scale) *
                    obj.WorldMatrix;
                Matrix4 matWorldViewProj = matWorld * viewProj;
                program.SetMat4("uWorld", ref matWorld);
                Matrix4 matWorldInvT = matWorld.Inverted();
                matWorldInvT.Transpose();
                program.SetMat4("uWorldInvTranspose", ref matWorldInvT);

                GL.UniformMatrix4(program.LocationMVP, false, ref matWorldViewProj);
                // Use the vertex array
                vertexArray.Draw();
            }
        }

        public void Pick(KinectData.Frame frame, Matrix4 viewProj,
            List<object> pickObjects, int offset)
        {
            int idx = offset;
            foreach (SimObjectMesh obj in simObjects)
            {
                MeshInfo meshInfo = obj.objectInfo as MeshInfo;
                Matrix4 matWorld = Matrix4.CreateScale(meshInfo.scale) *
                    obj.WorldMatrix;
                Matrix4 matWorldViewProj = matWorld * viewProj;
                pickProgram.Set4("pickColor", new Vector4((idx & 0xFF) / 255.0f,
                    ((idx >> 8) & 0xFF) / 255.0f,
                    ((idx >> 16) & 0xFF) / 255.0f,
                    1));
                GL.UniformMatrix4(pickProgram.LocationMVP, false, ref matWorldViewProj);
                vertexArray.Draw();
                pickObjects.Add(obj);
                idx++;
            }
        }

    }
}
