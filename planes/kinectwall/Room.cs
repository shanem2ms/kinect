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
    class RoomViz
    {
        private Program program;
        private VertexArray vertexArray;

        public RoomViz()
        {
            program = Program.FromFiles("Room.vert", "Room.frag");
            vertexArray = Cube.MakeCube(program);
        }

        public void Render(Matrix4 viewProj)
        {
            // Select the program for drawing
            GL.UseProgram(program.ProgramName);


            Matrix4 matWorldViewProj = Matrix4.CreateTranslation(new Vector3(0, 0, 1)) *
                Matrix4.CreateScale(5) *
                Matrix4.CreateTranslation(new Vector3(0, 2, 0)) *
                viewProj;
            // Compute the model-view-projection on CPU
            // Set uniform state
            GL.UniformMatrix4(program.LocationMVP, false, ref matWorldViewProj);
            // Use the vertex array
            vertexArray.Draw();
        }
    }
}
