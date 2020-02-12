﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenTK.Graphics.ES30;
using OpenTK;
using GLObjects;

namespace kinectwall
{
    class RoomViz
    {
        private Program program;
        private VertexArray vertexArray;

        List<SimObjectMesh> simObjects = new List<SimObjectMesh>();

        public RoomViz()
        {
            program = Program.FromFiles("Room.vert", "Room.frag");
            vertexArray = Cube.MakeCube(program);
        }

        Random r = new Random();

        float RandomNum(float min, float max)
        {
            return (float)r.NextDouble() * (max - min) + min;
        }

        class MeshInfo
        {
            public Vector3 color;
            public Matrix4 meshTransform;
        }
        public void Init(BulletSimulation simulation, KinectData.Frame bodyFrame)
        {
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

                Matrix4 meshTransform = Matrix4.CreateScale(new Vector3(5, 0.1f, 5));
                BulletSharp.TriangleMesh wall = Cube.MakeBulletMesh(meshTransform);

                for (int i = 0; i < 6; ++i)
                {
                    Matrix4 matWorld =
                        rotMats[i] *
                        Matrix4.CreateTranslation(transvecs[i] * 5) *
                        Matrix4.CreateTranslation(new Vector3(0, 2, 5));
                    SimObjectMesh obj = new SimObjectMesh(matWorld, 0.0f, wall);
                    obj.objectInfo = new MeshInfo()
                    {
                        color = wallcolors[i],
                        meshTransform = meshTransform
                    };
                    simObjects.Add(obj);
                }
            }
            /*
            BulletSharp.TriangleMesh smallcubes = Cube.MakeBulletMesh(new Vector3(0.1f, 0.1f, 0.1f));
            for (int i = 0; i < 64; ++i)
            {
                Matrix4 matWorld =
                    Matrix4.CreateTranslation(new Vector3(
                        RandomNum(-5, 5),
                        RandomNum(0, 5),
                        RandomNum(0, 10)));
                SimObjectMesh obj = new SimObjectMesh(matWorld, 1.0f, smallcubes);
                obj.objectInfo = new MeshInfo()
                {
                    color = new Vector3(
                    RandomNum(0.25f, 1),
                    RandomNum(0.25f, 1),
                    RandomNum(0.25f, 1)),
                    scale = new Vector3(0.1f, 0.1f, 0.1f)
                };

                float v = 15;
                obj.Body.LinearVelocity = Utils.FromVector3(
                    new Vector3(RandomNum(-v, v),
                    RandomNum(-v, v),
                    RandomNum(-v, v)));
                simObjects.Add(obj);
            }*/

            Dictionary<KinectData.JointNode, SimObjectMesh>
                jointSims = new Dictionary<KinectData.JointNode, SimObjectMesh>();
            foreach (var body in bodyFrame.bodies)
            {
                if (body != null)
                {
                    body.top.DrawNode((jn) =>
                    {
                        Matrix4 worldMat = jn.WorldMat;
                        Matrix4 meshTransform = Matrix4.CreateTranslation(0, -1, 0) *
                            Matrix4.CreateScale(0.01f, jn.jointLength * 0.5f, 0.01f);
                        BulletSharp.TriangleMesh boneTM = Cube.MakeBulletMesh(meshTransform);
                        
                        SimObjectMesh obj = new SimObjectMesh(worldMat, 0.1f, boneTM);
                        obj.objectInfo = new MeshInfo()
                        {
                            color = new Vector3(
                            RandomNum(0.25f, 1),
                            RandomNum(0.25f, 1),
                            RandomNum(0.25f, 1)),
                            meshTransform = meshTransform
                        };

                        simObjects.Add(obj);
                        jointSims.Add(jn, obj);
                    });
                }
            }

            foreach (var kv in jointSims)
            {
                if (kv.Key.Parent != null)
                {
                    SimObjectMesh soP = jointSims[kv.Key.Parent];                    
                    Constraint c = new Constraint(soP, Vector3.Zero,
                        kv.Value, new Vector3(0,
                            -kv.Key.Parent.jointLength, 0));
                    //simulation.AddConst(c);
                }
            }

            foreach (var so in simObjects)
            {
                simulation.AddObj(so);
            }
        }

        public void Render(Matrix4 viewProj)
        {
            // Select the program for drawing
            GL.UseProgram(program.ProgramName);
            foreach (SimObjectMesh obj in simObjects)
            {
                MeshInfo meshInfo = obj.objectInfo as MeshInfo;
                program.Set3("meshColor", meshInfo.color);

                program.Set3("lightPos", new Vector3(2, 5, 2));
                Matrix4 matWorld = meshInfo.meshTransform *
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
    }
}
