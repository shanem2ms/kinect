using System;
using OpenTK.Graphics.ES30;
using OpenTK;
using System.Collections.Generic;

namespace GLObjects
{
    // Note: abstractions for drawing using programmable pipeline.

    /// <summary>
    /// Shader object abstraction.
    /// </summary>
    public class Object : IDisposable
    {
        public Object(ShaderType shaderType, string source)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            // Create
            ShaderName = GL.CreateShader(shaderType);
            // Submit source code
            GL.ShaderSource(ShaderName, source);
            // Compile
            GL.CompileShader(ShaderName);
            // Check compilation status
            int compiled;

            GL.GetShader(ShaderName, ShaderParameter.CompileStatus, out compiled);
            if (compiled != 0)
                return;

            // Throw exception on compilation errors
            const int logMaxLength = 1024;

            string infolog = null;
            int infologLength;

            GL.GetShaderInfoLog(ShaderName, logMaxLength, out infologLength, out infolog);

            throw new InvalidOperationException($"unable to compile shader: {infolog}");
        }

        public readonly int ShaderName;

        public void Dispose()
        {
            GL.DeleteShader(ShaderName);
        }
    }

    /// <summary>
    /// Program abstraction.
    /// </summary>
    public class Program : IDisposable
    {
        public static Program FromFiles(string vtxPath, string pixPath)
        {
            return new Program(
                System.IO.File.ReadAllText("Shaders/" + vtxPath),
                System.IO.File.ReadAllText("Shaders/" + pixPath));
        }
        public Program(string vertexSource, string fragmentSource)
        {
            // Create vertex and frament shaders
            // Note: they can be disposed after linking to program; resources are freed when deleting the program
            using (Object vObject = new Object(ShaderType.VertexShader, vertexSource))
            using (Object fObject = new Object(ShaderType.FragmentShader, fragmentSource))
            {
                // Create program
                ProgramName = GL.CreateProgram();
                // Attach shaders
                GL.AttachShader(ProgramName, vObject.ShaderName);
                GL.AttachShader(ProgramName, fObject.ShaderName);
                // Link program
                GL.LinkProgram(ProgramName);

                // Check linkage status
                int linked;
                GL.GetProgram(ProgramName, GetProgramParameterName.LinkStatus, out linked);

                if (linked == 0)
                {
                    const int logMaxLength = 1024;
                    string infolog;
                    int infologLength;

                    GL.GetProgramInfoLog(ProgramName, 1024, out infologLength, out infolog);

                    throw new InvalidOperationException($"unable to link program: {infolog}");
                }

                // Get uniform locations
                LocationMVP = GL.GetUniformLocation(ProgramName, "uMVP");

                // Get attributes locations
                if ((LocationPosition = GL.GetAttribLocation(ProgramName, "aPosition")) < 0)
                    throw new InvalidOperationException("no attribute aPosition");
                LocationTexCoords = GL.GetAttribLocation(ProgramName, "aTexCoord");
                LocationNormals = GL.GetAttribLocation(ProgramName, "aNormal");
            }
        }

        public readonly int ProgramName;
        public readonly int LocationMVP;
        public readonly int LocationPosition;
        public readonly int LocationTexCoords;
        public readonly int LocationNormals;
        private Dictionary<string, int> shaderOffsets =
            new Dictionary<string, int>();

        public void Dispose()
        {
            GL.DeleteProgram(ProgramName);
        }

        private int GetLoc(string name)
        {
            int loc;
            if (!shaderOffsets.TryGetValue(name, out loc))
            {
                loc = GL.GetUniformLocation(ProgramName, name);
                shaderOffsets.Add(name, loc);
            }
            return loc;
        }

        public void Set1(string name, float value)
        {
            GL.Uniform1(GetLoc(name), value);
        }

        public void Set1(string name, int value)
        {
            GL.Uniform1(GetLoc(name), value);
        }

        public void Set2(string name, Vector2 value)
        {
            GL.Uniform2(GetLoc(name), value);
        }

        public void Set3(string name, Vector3 value)
        {
            GL.Uniform3(GetLoc(name), value);
        }

        public void Set4(string name, Vector4 value)
        {
            GL.Uniform4(GetLoc(name), value);
        }
    }

    /// <summary>
    /// Buffer abstraction.
    /// </summary>
    public class Buffer : IDisposable
    {
        public Buffer(Vector3[] vectors)
        {
            // Generate a buffer name: buffer does not exists yet
            BufferName = GL.GenBuffer();
            // First bind create the buffer, determining its type
            GL.BindBuffer(BufferTarget.ArrayBuffer, BufferName);
            // Set buffer information, 'buffer' is pinned automatically
            GL.BufferData(BufferTarget.ArrayBuffer, (int)(12 * vectors.Length), vectors, BufferUsageHint.StaticDraw);

        }
        public Buffer(float[] buffer)
        {
            // Generate a buffer name: buffer does not exists yet
            BufferName = GL.GenBuffer();
            // First bind create the buffer, determining its type
            GL.BindBuffer(BufferTarget.ArrayBuffer, BufferName);
            // Set buffer information, 'buffer' is pinned automatically
            GL.BufferData(BufferTarget.ArrayBuffer, (int)(4 * buffer.Length), buffer, BufferUsageHint.StaticDraw);
        }

        public Buffer(ushort[] buffer)
        {
            // Generate a buffer name: buffer does not exists yet
            BufferName = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, BufferName);
            // Set buffer information, 'buffer' is pinned automatically
            GL.BufferData(BufferTarget.ElementArrayBuffer, (int)(2 * buffer.Length), buffer, BufferUsageHint.StaticDraw);
        }

        public Buffer(uint[] buffer)
        {
            // Generate a buffer name: buffer does not exists yet
            BufferName = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, BufferName);
            // Set buffer information, 'buffer' is pinned automatically
            GL.BufferData(BufferTarget.ElementArrayBuffer, (int)(4 * buffer.Length), buffer, BufferUsageHint.StaticDraw);
        }

        public void Update(Vector3[] vectors)
        {
            // First bind create the buffer, determining its type
            GL.BindBuffer(BufferTarget.ArrayBuffer, BufferName);
            // Set buffer information, 'buffer' is pinned automatically
            GL.BufferData(BufferTarget.ArrayBuffer, (int)(12 * vectors.Length), vectors, BufferUsageHint.StaticDraw);

        }

        public void Update(float[] buffer)
        {
            // First bind create the buffer, determining its type
            GL.BindBuffer(BufferTarget.ArrayBuffer, BufferName);
            // Set buffer information, 'buffer' is pinned automatically
            GL.BufferData(BufferTarget.ArrayBuffer, (int)(4 * buffer.Length), buffer, BufferUsageHint.StaticDraw);
        }

        public void Update(ushort[] buffer)
        {
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, BufferName);
            // Set buffer information, 'buffer' is pinned automatically
            GL.BufferData(BufferTarget.ElementArrayBuffer, (int)(2 * buffer.Length), buffer, BufferUsageHint.StaticDraw);
        }

        public void Update(uint[] buffer)
        {
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, BufferName);
            // Set buffer information, 'buffer' is pinned automatically
            GL.BufferData(BufferTarget.ElementArrayBuffer, (int)(4 * buffer.Length), buffer, BufferUsageHint.StaticDraw);
        }

        public readonly int BufferName;

        public void Dispose()
        {
            GL.DeleteBuffer(BufferName);
        }
    }


    public class TextureFloat : IDisposable
    {
        public TextureFloat()
        {
            TextureName = GL.GenTexture();
        }
        public void LoadDepthFrame(int width, int height, byte []data)
        {
            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, TextureName);

            GL.TexImage2D(TextureTarget2d.Texture2D, 0, TextureComponentCount.R8,
                width, height, 0, PixelFormat.Red, PixelType.Float,
                data);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToBorder);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToBorder);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureBorderColor, new float[] { 0, 0, 0 });
        }

        public readonly int TextureName;
        public void BindToIndex(int idx)
        {
            GL.ActiveTexture(TextureUnit.Texture0 + idx);
            GL.BindTexture(TextureTarget.Texture2D, TextureName);
        }

        public void Dispose()
        {
            GL.DeleteTexture(TextureName);
        }
    }

    class TextureYUV : IDisposable
    {
        public TextureYUV()
        {
            TextureNameY = GL.GenTexture();
            TextureNameUV = GL.GenTexture();
        }

        public delegate void OnGlErrorDel();
        public void LoadImageFrame(int width, int height, byte[] data)
        {
            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, TextureNameY);
            int ySize = height * width;
            byte[] yData = new byte[ySize];
            System.Buffer.BlockCopy(data, 0, yData, 0, ySize);

            GL.TexImage2D(TextureTarget2d.Texture2D, 0, TextureComponentCount.R8,
                width, height, 0, PixelFormat.Red, PixelType.UnsignedByte,
                yData);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToBorder);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToBorder);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureBorderColor, new float[] { 0, 0, 0 });

            GL.ActiveTexture(TextureUnit.Texture1);
            GL.BindTexture(TextureTarget.Texture2D, TextureNameUV);

            int uvWidth = (width / 2);
            int uvHeight = (height / 2);
            int uvSize = uvHeight * uvWidth * 2;
            byte[] uvData = new byte[uvSize];
            System.Buffer.BlockCopy(data, ySize, uvData, 0, uvSize);
            GL.TexImage2D(TextureTarget2d.Texture2D, 0, TextureComponentCount.Rg8,
                uvWidth, uvHeight, 0, PixelFormat.Rg, PixelType.UnsignedByte,
                uvData);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
        }

        public void BindToIndex(int idx0, int idx1)
        {
            GL.ActiveTexture(TextureUnit.Texture0 + idx0);
            GL.BindTexture(TextureTarget.Texture2D, TextureNameY);
            GL.ActiveTexture(TextureUnit.Texture0 + idx1);
            GL.BindTexture(TextureTarget.Texture2D, TextureNameUV);
        }

        public readonly int TextureNameY;
        public readonly int TextureNameUV;

        public void Dispose()
        {
            GL.DeleteTexture(TextureNameY);
        }
    }

    /// <summary>
    /// Vertex array abstraction.
    /// </summary>
    class VertexArray : IDisposable
    {
        public VertexArray(Program program, Vector3[] positions, ushort[] elems, Vector3[] texCoords,
            Vector3[] normals) :
            this(program, positions, Array.ConvertAll(elems, e => (uint)e), texCoords,
                normals)
        {

        }
        public VertexArray(Program program, Vector3[] positions, uint[] elems, Vector3[] texCoords,
            Vector3[] normals)
        {

            this._Program = program;

            this.ElementArray = elems;
            // Generate VAO name
            ArrayName = GL.GenVertexArray();
            // First bind create the VAO
            GL.BindVertexArray(ArrayName);

            vertexCount = positions.Length;
            int stride = 3;
            // Allocate buffers referenced by this vertex array
            _BufferPosition = new GLObjects.Buffer(positions);
            if (texCoords != null)
            {
                _BufferTexCoords = new GLObjects.Buffer(texCoords);
                stride += 3;
            }
            if (normals != null)
            {
                _BufferNormal = new GLObjects.Buffer(normals);
                stride += 3;
            }
            if (elems != null)
            {
                elementCount = elems.Length;
                _BufferElems = new GLObjects.Buffer(elems);
                GL.BindBuffer(BufferTarget.ElementArrayBuffer, _BufferElems.BufferName);
            }

            stride = 0;
            // Select the buffer object
            GL.BindBuffer(BufferTarget.ArrayBuffer, _BufferPosition.BufferName);
            GL.VertexAttribPointer((int)program.LocationPosition, 3, VertexAttribPointerType.Float, false, stride * sizeof(float), IntPtr.Zero);
            GL.EnableVertexAttribArray((int)program.LocationPosition);

            if (texCoords != null && program.LocationTexCoords >= 0)
            {
                GL.BindBuffer(BufferTarget.ArrayBuffer, _BufferTexCoords.BufferName);
                GL.VertexAttribPointer((int)program.LocationTexCoords, 3, VertexAttribPointerType.Float, false, stride * sizeof(float), IntPtr.Zero);
                GL.EnableVertexAttribArray((int)program.LocationTexCoords);
            }

            if (normals != null && program.LocationNormals >= 0)
            {
                GL.BindBuffer(BufferTarget.ArrayBuffer, _BufferNormal.BufferName);
                GL.VertexAttribPointer((int)program.LocationNormals, 3, VertexAttribPointerType.Float, false, stride * sizeof(float), IntPtr.Zero);
                GL.EnableVertexAttribArray((int)program.LocationNormals);
            }
        }

        public void UpdatePositions(Vector3[] positions)
        {
            _BufferPosition.Update(positions);
        }

        public void UpdateNormals(Vector3[] normals)
        {
            _BufferNormal.Update(normals);
        }

        public void UpdateTexCoords(Vector3[] texcoords)
        {
            _BufferTexCoords.Update(texcoords);
        }

        void BuildWireframeElems()
        {
            List<uint> wireframeElems = new List<uint>();
            for (int idx = 0; idx < ElementArray.Length; idx += 3)
            {
                wireframeElems.Add(ElementArray[idx]);
                wireframeElems.Add(ElementArray[idx + 1]);
                wireframeElems.Add(ElementArray[idx + 1]);
                wireframeElems.Add(ElementArray[idx + 2]);
                wireframeElems.Add(ElementArray[idx + 2]);
                wireframeElems.Add(ElementArray[idx]);
            }

            elementCountWireframe = wireframeElems.Count;
            _BufferWireframeElems = new GLObjects.Buffer(wireframeElems.ToArray());
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, _BufferWireframeElems.BufferName);
        }

        public void Draw(int count)
        {
            GL.BindVertexArray(ArrayName);
            if (_BufferElems != null)
                GL.DrawElements(PrimitiveType.Triangles, count, DrawElementsType.UnsignedInt, IntPtr.Zero);
            else
                GL.DrawArrays(PrimitiveType.Points, 0, vertexCount);
        }

        public void Draw()
        {
            Draw(elementCount);
        }

        public void DrawWireframe()
        {
            GL.BindVertexArray(ArrayName);
            if (_BufferWireframeElems == null)
                BuildWireframeElems();
            GL.DrawElements(PrimitiveType.Lines, elementCountWireframe, DrawElementsType.UnsignedInt, IntPtr.Zero);
        }

        private int elementCount = 0;
        private int elementCountWireframe = 0;
        private int vertexCount = 0;

        public readonly int ArrayName;

        private readonly GLObjects.Buffer _BufferPosition;
        private readonly GLObjects.Buffer _BufferTexCoords;
        private readonly GLObjects.Buffer _BufferNormal;
        private readonly GLObjects.Buffer _BufferElems;
        private GLObjects.Buffer _BufferWireframeElems;
        private readonly Program _Program;

        public Program Program { get { return _Program; } }
        uint[] ElementArray = null;

        public void Dispose()
        {
            GL.DeleteVertexArray(ArrayName);

            _BufferPosition.Dispose();
            _BufferTexCoords.Dispose();
            _BufferNormal.Dispose();
            _BufferElems.Dispose();
        }
    }
}
