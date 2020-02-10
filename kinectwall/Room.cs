using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenTK.Graphics.ES30;
using OpenTK;
using GLObjects;
using Microsoft.Kinect;

namespace kinectwall
{
    class RoomViz
    {
        private Program program;
        private VertexArray vertexArray;

        public RoomViz()
        {
            program = Program.FromFiles("Room.vert", "Room.frag");
            ushort[] indices = new ushort[_Cube.Length];
            Vector3[] texCoords = new Vector3[_Cube.Length];
            Vector3[] normals = new Vector3[3]
            {
                Vector3.UnitZ,
                Vector3.UnitY,
                Vector3.UnitX
            };
            Vector3[] xdirs = new Vector3[3]
            {
                Vector3.UnitX,
                Vector3.UnitX,
                Vector3.UnitZ
            };
            Vector3[] ydirs = new Vector3[3]
            {
                Vector3.UnitY,
                Vector3.UnitZ,
                Vector3.UnitY
            };

            for (int i = 0; i < indices.Length; ++i)
            {
                indices[i] = (ushort)i;
                Vector3 xdir = xdirs[i / 12];
                Vector3 ydir = ydirs[i / 12];
                int sideIdx = i / 6;
                texCoords[i] = new Vector3(Vector3.Dot(_Cube[i], xdir),
                    Vector3.Dot(_Cube[i], ydir), (float)sideIdx / 6.0f);
            }
            List<Vector3> cuberev = new List<Vector3>(_Cube);
            cuberev.Reverse();
            vertexArray = new VertexArray(program, _Cube, indices, texCoords, null);
        }

        public void Render(Matrix4 viewProj)
        {
            // Select the program for drawing
            GL.UseProgram(program.ProgramName);


            Matrix4 matWorldViewProj = Matrix4.CreateTranslation(new Vector3(-0.5f, -0.5f, 0)) *
                Matrix4.CreateScale(10) *
                Matrix4.CreateTranslation(new Vector3(0, 2, 0)) *
                viewProj;
            // Compute the model-view-projection on CPU
            // Set uniform state
            GL.UniformMatrix4(program.LocationMVP, false, ref matWorldViewProj);
            // Use the vertex array
            vertexArray.Draw();
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
    }
}
