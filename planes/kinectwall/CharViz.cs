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
        Character model;
        Vector3 scale;

        public int boneSelIdx = 0;
        public CharViz(Character _model)
        {
            model = _model;
        }

        public int matrixMode = 0;

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
        {
            model.program.Use(0);
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
                boneMatrixLoc = model.program.GetLoc("boneMatrices");
            List<Matrix4> matList = new List<Matrix4>();

            if (frame != null)
            {
                Body body = frame.bodies.FirstOrDefault().Value;
                if (body != null) model.SetBody(body);
            }


            GL.UniformMatrix4(model.program.GetLoc("gBones"), flvals.Length / 16, false, flvals);
            model.program.Set1("gUseBones", 1);
            Vector3[] boneColors = model.allBones.Select(b => b.node.color).ToArray();
            float[] fvColors = new float[boneColors.Length * 3];

            for (int bIdx = 0; bIdx < boneColors.Length; ++bIdx)
            {
                fvColors[bIdx * 3] = boneColors[bIdx].X;
                fvColors[bIdx * 3 + 1] = boneColors[bIdx].Y;
                fvColors[bIdx * 3 + 2] = boneColors[bIdx].Z;
            }
            GL.Uniform3(model.program.GetLoc("gBonesColor"), fvColors.Length / 3, fvColors);
            model.program.Set1("diffuseMap", 0);

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
                model.program.SetMat4("uMVP", ref viewProj);
                this.model.vertexArray.Draw(mesh.offset, mesh.count);
            }

            animTime += 0.01;
        }
    }


}
