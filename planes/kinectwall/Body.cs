using OpenTK.Graphics.ES30;
using OpenTK;
using GLObjects;
using KinectData;
using System;

namespace kinectwall
{
    class BodyViz
    {
        private Program program;
        private VertexArray vertexArray;

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
        }

        public void Render(KinectData.Frame frame, Matrix4 viewProj, long timestamp)
        {
            // Select the program for drawing
            GL.UseProgram(program.ProgramName);
            foreach (Body body in frame.bodies)
            {
                if (body != null)
                {
                    foreach (var kv in body.joints)
                    {
                        Vector3 pos = kv.Value.Position;
                        Quaternion rot = new Quaternion(kv.Value.Orientation.X,
                            kv.Value.Orientation.Y,
                            kv.Value.Orientation.Z,
                            kv.Value.Orientation.W);
                        Matrix4 worldMat = Matrix4.CreateFromQuaternion(rot) * 
                            Matrix4.CreateTranslation(pos);

                        program.Set1("opacity", 1.0f);

                        Matrix4 matWorldViewProj = matWorldViewProj =
                            Matrix4.CreateTranslation(0, -0.5f, -0.5f) *
                            Matrix4.CreateScale(0.06f, 0.01f, 0.01f) *
                            Matrix4.CreateTranslation(0, 0, 0) *
                            worldMat * viewProj;
                        program.Set3("meshColor", new Vector3(1,0,0));
                        GL.UniformMatrix4(program.LocationMVP, false, ref matWorldViewProj);
                        vertexArray.Draw();

                        matWorldViewProj = matWorldViewProj =
                                Matrix4.CreateTranslation(-0.5f, 0, -0.5f) *
                                Matrix4.CreateScale(0.01f, 0.06f, 0.01f) *
                                Matrix4.CreateTranslation(0, 0, 0) *
                                worldMat * viewProj;
                        program.Set3("meshColor", new Vector3(0, 1, 0));
                        GL.UniformMatrix4(program.LocationMVP, false, ref matWorldViewProj);
                        vertexArray.Draw();

                        matWorldViewProj = matWorldViewProj =
                                Matrix4.CreateTranslation(-0.5f, -0.5f, 0) *
                                Matrix4.CreateScale(0.01f, 0.01f, 0.06f) *
                                Matrix4.CreateTranslation(0, 0, 0) *
                                worldMat * viewProj;
                        program.Set3("meshColor", new Vector3(0, 0, 1));
                        GL.UniformMatrix4(program.LocationMVP, false, ref matWorldViewProj);
                        vertexArray.Draw();

                        Vector3 color = new Vector3(0.5f, 1.0f, 0.5f);
                        if (kv.Value.TrackingState == TrackingState.Inferred)
                            color = new Vector3(1.0f, 0.5f, 0.5f);
                        else if (kv.Value.TrackingState == TrackingState.NotTracked)
                            color = new Vector3(1.0f, 0, 0);

                        matWorldViewProj = matWorldViewProj =
                            Matrix4.CreateTranslation(-0.5f, -1, -0.5f) *
                            Matrix4.CreateScale(0.01f * 2, 0.1f, 0.01f * 2) *
                            worldMat * viewProj;
                        program.Set3("meshColor", color);
                        GL.UniformMatrix4(program.LocationMVP, false, ref matWorldViewProj);
                        vertexArray.Draw();
                    }
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