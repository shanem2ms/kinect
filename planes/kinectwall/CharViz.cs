using System;
using System.Collections.Generic;
using System.Linq;
using ai = Assimp;
using aic = Assimp.Configs;
using System.IO;
using OpenTK.Graphics.ES30;
using OpenTK;
using GLObjects;
using System.Runtime.InteropServices;
using System.Diagnostics;
using KinectData;
using System.Drawing;
using System.Drawing.Imaging;

namespace kinectwall
{
    class CharViz
    {
        private Program program;
        string path;
        Character model;
        Vector3 scale;

        public int boneSelIdx = 0;
        public CharViz(string _path)
        {
            path = _path;
            Init();
        }

        public int matrixMode = 0;
        public void Init()
        {
            program = Program.FromFiles("Character.vert", "Character.frag");
            this.model = new Character();
            this.model.Load(path, program);

        }

        int boneMatrixLoc = -1;

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

        double animTime = 0;
        public void Render(Frame frame, Matrix4 viewProj)
        {/*
            GL.UseProgram(boneProgram.ProgramName);

            Character.Node []nodes = model.allBones.GroupBy(b => b.node).Select(g => g.Key).ToArray();
            for (int idx = 0; idx < nodes.Length; ++idx)
            {
                Matrix4 matWorldViewProj =
                    Matrix4.CreateTranslation(1, 0, 0) *
                    Matrix4.CreateScale(0.02f, 0.005f, 0.005f) *
                    nodes[idx].WorldTransform * viewProj;
                GL.UniformMatrix4(boneProgram.LocationMVP, false, ref matWorldViewProj);
                boneProgram.Set3("nodeColor", new Vector3(1, 0.2f, 0.2f));
                boneVA.Draw();
                matWorldViewProj =
                    Matrix4.CreateTranslation(0, 1, 0) *
                    Matrix4.CreateScale(0.005f, 0.02f, 0.005f) *
                    nodes[idx].WorldTransform * viewProj;
                GL.UniformMatrix4(boneProgram.LocationMVP, false, ref matWorldViewProj);
                boneProgram.Set3("nodeColor", new Vector3(0.2f, 1, 0.2f));
                boneVA.Draw();
                matWorldViewProj =
                    Matrix4.CreateTranslation(0, 0, 1) *
                    Matrix4.CreateScale(0.005f, 0.005f, 0.02f) *
                    nodes[idx].WorldTransform * viewProj;
                GL.UniformMatrix4(boneProgram.LocationMVP, false, ref matWorldViewProj);
                boneProgram.Set3("nodeColor", new Vector3(0.2f, 0.2f, 1));
                boneVA.Draw();
            }
            */

            GL.UseProgram(program.ProgramName);
            float len = scale.Length;
            Matrix4[] mats = model.allBones.Select(b => (                
                b.offsetMat.M4 *
                b.node.WorldTransform *
                model.meshes[b.meshIdx].node.WorldTransform.Inverted())).ToArray();
            float[] flvals = StructArrayToFloatArray<Matrix4>(mats);

            if (animTime > model.duration)
                animTime -= model.duration;
            //model.Root.SetAnimationTime(animTime);

            if (boneMatrixLoc < 0)
                boneMatrixLoc = program.GetLoc("boneMatrices");
            List<Matrix4> matList = new List<Matrix4>();

            if (frame != null)
            {
                Body body = frame.bodies.FirstOrDefault().Value;
                if (body != null) model.SetBody(body);
            }


            GL.UniformMatrix4(program.GetLoc("gBones"), flvals.Length / 16, false, flvals);
            program.Set1("gUseBones", 1);
            Vector3[] boneColors = model.allBones.Select(b => b.node.color).ToArray();
            float[] fvColors = new float[boneColors.Length * 3];

            for (int bIdx = 0; bIdx < boneColors.Length; ++bIdx)
            {
                fvColors[bIdx * 3] = boneColors[bIdx].X;
                fvColors[bIdx * 3 + 1] = boneColors[bIdx].Y;
                fvColors[bIdx * 3 + 2] = boneColors[bIdx].Z;
            }
            GL.Uniform3(program.GetLoc("gBonesColor"), fvColors.Length / 3, fvColors);
            program.Set1("diffuseMap", 0);

            // Use the vertex array
            foreach (Character.Mesh mesh in this.model.meshes)
            {
                int mm = matrixMode % 3;
                Matrix4 matWorldViewProj =
                    mesh.node.WorldTransform * viewProj;
                if (mesh.materialIdx >= 0)
                {
                    Character.Material mat = this.model.materials[mesh.materialIdx];
                    if (mat.diffTex != null) mat.diffTex.glTexture.BindToIndex(0);
                }
                GL.UniformMatrix4(program.LocationMVP, false, ref matWorldViewProj);
                this.model.vertexArray.Draw(mesh.offset, mesh.count);
            }

            animTime += 0.01;
        }
    }


}
