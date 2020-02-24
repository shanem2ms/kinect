using OpenTK.Graphics.ES30;
using OpenTK;
using GLObjects;
using KinectData;
using System.Collections.Generic;

namespace kinectwall
{
    class BodyViz
    {
        private Program program;
        private Program pickProgram;
        private VertexArray vertexArray;

        private VertexArray pickVA;

        public BodyViz(Program pickProgram)
        {
            program = Program.FromFiles("Body.vert", "Body.frag");
            vertexArray = Cube.MakeCube(program);
            pickVA = Cube.MakeCube(pickProgram);
            this.pickProgram = pickProgram;
        }

        public void Pick(KinectData.Frame frame, Matrix4 viewProj,
            List<object> pickObjects, int offset)
        {
            int idx = offset;
            foreach (Body body in frame.bodies.Values)
            {
                body.top.OnSceneNode<JointNode>((jn) =>
                {
                    Matrix4 worldMat = jn.WorldMat;
                    Matrix4 matWorldViewProj = matWorldViewProj =
                        Matrix4.CreateTranslation(0, 1, 0) *
                        Matrix4.CreateScale(0.01f, jn.JointLength * 0.5f, 0.01f) *
                        worldMat * viewProj;
                    pickProgram.Set4("pickColor", new Vector4((idx & 0xFF) / 255.0f, 
                        ((idx >> 8) & 0xFF) / 255.0f,
                        ((idx >> 16) & 0xFF) / 255.0f, 
                        1));
                    GL.UniformMatrix4(pickProgram.LocationMVP, false, ref matWorldViewProj);
                    vertexArray.Draw();
                    pickObjects.Add(jn);
                    idx++;
                });
            }
        }

        public void Render(KinectData.Frame frame, Matrix4 viewProj)
        {
            // Select the program for drawing
            GL.UseProgram(program.ProgramName);
            foreach (Body body in frame.bodies.Values)
            {
                body.top.OnSceneNode<JointNode>((jn) =>
                {
                    Matrix4 worldMat = jn.WorldMat;
                    program.Set1("opacity", 1.0f);

                    Matrix4 matWorldViewProj = 
                        Matrix4.CreateTranslation(1, 0, 0) *
                        Matrix4.CreateScale(0.05f, 0.004f, 0.004f) *
                        Matrix4.CreateTranslation(0, 0, 0) *
                        worldMat * viewProj;
                    program.Set3("meshColor", new Vector3(1, 0, 0));
                    GL.UniformMatrix4(program.LocationMVP, false, ref matWorldViewProj);
                    vertexArray.Draw();

                    matWorldViewProj = matWorldViewProj =
                            Matrix4.CreateTranslation(0, 1, 0) *
                            Matrix4.CreateScale(0.004f, 0.06f, 0.004f) *
                            Matrix4.CreateTranslation(0, 0, 0) *
                            worldMat * viewProj;
                    program.Set3("meshColor", new Vector3(0, 1, 0));
                    GL.UniformMatrix4(program.LocationMVP, false, ref matWorldViewProj);
                    vertexArray.Draw();

                    matWorldViewProj = matWorldViewProj =
                            Matrix4.CreateTranslation(0, 0, 1) *
                            Matrix4.CreateScale(0.004f, 0.004f, 0.06f) *
                            Matrix4.CreateTranslation(0, 0, 0) *
                            worldMat * viewProj;
                    program.Set3("meshColor", new Vector3(0, 0, 1));
                    GL.UniformMatrix4(program.LocationMVP, false, ref matWorldViewProj);
                    vertexArray.Draw();

                    Vector3 color = new Vector3(0.5f, 1.0f, 0.5f);
                    if (jn.Tracked == TrackingState.Inferred)
                        color = new Vector3(1.0f, 0.5f, 0.5f);
                    else if (jn.Tracked == TrackingState.NotTracked)
                        color = new Vector3(1.0f, 0, 0);

                    matWorldViewProj = matWorldViewProj =
                        Matrix4.CreateTranslation(0, -1, 0) *
                        Matrix4.CreateScale(0.01f, jn.JointLength * 0.5f, 0.01f) *
                        worldMat * viewProj;
                    program.Set3("meshColor", color);
                    GL.UniformMatrix4(program.LocationMVP, false, ref matWorldViewProj);
                    vertexArray.Draw();
                });
            }
        }
    }

}