using System;
using OpenTK.Graphics.ES30;
using OpenTK;
using System.Collections.Generic;
using System.Runtime.InteropServices;

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
                LocationTexCoord0 = GL.GetAttribLocation(ProgramName, "aTexCoord0");
                LocationTexCoord1 = GL.GetAttribLocation(ProgramName, "aTexCoord1");
                LocationTexCoord2 = GL.GetAttribLocation(ProgramName, "aTexCoord2");
                LocationNormals = GL.GetAttribLocation(ProgramName, "aNormal");
            }
        }

        public readonly int ProgramName;
        public readonly int LocationMVP;
        public readonly int LocationPosition;
        public readonly int LocationTexCoord0;
        public readonly int LocationTexCoord1;
        public readonly int LocationTexCoord2;
        public readonly int LocationNormals;
        private Dictionary<string, int> shaderOffsets =
            new Dictionary<string, int>();

        public void Dispose()
        {
            GL.DeleteProgram(ProgramName);
        }

        public int GetLoc(string name)
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

        public void SetMat4(string name, ref Matrix4 value)
        {
            GL.UniformMatrix4(GetLoc(name), false, ref value);
        }
    }

    public abstract class BufferBase : IDisposable
    {
        public int BufferName;

        public void Dispose()
        {
            GL.DeleteBuffer(BufferName);
        }

        public abstract void Update(object vectors);
    }

    public class Buffer<T> : BufferBase where T : struct
    {

        public static int SizeOf<S>() where S : struct
        {
            return Marshal.SizeOf(default(S));
        }

        public Buffer(T []vectors)
        {
            // Generate a buffer name: buffer does not exists yet
            BufferName = GL.GenBuffer();
            // First bind create the buffer, determining its type
            GL.BindBuffer(BufferTarget.ArrayBuffer, BufferName);
            // Set buffer information, 'buffer' is pinned automatically
            GL.BufferData(BufferTarget.ArrayBuffer, (int)(SizeOf<T>() * vectors.Length), vectors, BufferUsageHint.StaticDraw);
        }

        public override void Update(object vecs)
        {
            T[] vectors = vecs as T[];
            // First bind create the buffer, determining its type
            GL.BindBuffer(BufferTarget.ArrayBuffer, BufferName);
            // Set buffer information, 'buffer' is pinned automatically
            GL.BufferData(BufferTarget.ArrayBuffer, (int)(SizeOf<T>() * vectors.Length), vectors, BufferUsageHint.StaticDraw);
        }
    }
    
    public class Texture : IDisposable
    {
        public readonly int TextureName;

        protected Texture()
        {
            TextureName = GL.GenTexture();
        }

        public void Dispose()
        {
            GL.DeleteTexture(TextureName);
        }
    }

    public class TextureFloat : Texture
    {
        public TextureFloat()
        {
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

        public void BindToIndex(int idx)
        {
            GL.ActiveTexture(TextureUnit.Texture0 + idx);
            GL.BindTexture(TextureTarget.Texture2D, TextureName);
        }

    }

    public class TextureRgba : Texture
    {
        public TextureRgba()
        {
        }

        public void Create(int width, int height)
        {
            LoadData(width, height, IntPtr.Zero);
        }

        public void LoadData(int width, int height, IntPtr data)
        {
            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, TextureName);

            GL.TexImage2D(TextureTarget2d.Texture2D, 0, TextureComponentCount.Rgba8,
                width, height, 0, PixelFormat.Rgba, PixelType.UnsignedByte,
                data);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToBorder);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToBorder);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureBorderColor, new float[] { 0, 0, 0 });
        }

        public void BindToIndex(int idx)
        {
            GL.ActiveTexture(TextureUnit.Texture0 + idx);
            GL.BindTexture(TextureTarget.Texture2D, TextureName);
        }
    }

    public class DepthBuffer : IDisposable
    {
        public DepthBuffer()
        {
            RenderBufferName = GL.GenRenderbuffer();
        }

        public void Create(int width, int height)
        {
            GL.BindRenderbuffer(RenderbufferTarget.Renderbuffer, RenderBufferName);
            GL.RenderbufferStorage(RenderbufferTarget.Renderbuffer, RenderbufferInternalFormat.DepthComponent32f,
                width, height);
        }

        public readonly int RenderBufferName;
        public void Bind()
        {
            GL.BindRenderbuffer(RenderbufferTarget.Renderbuffer, RenderBufferName);
        }

        public void Dispose()
        {
            GL.DeleteRenderbuffer(RenderBufferName);
        }

    }

    public class FrameBuffer
    {
        public readonly int FrameBufferName;
        public FrameBuffer()
        {
            FrameBufferName = GL.GenFramebuffer();
        }

        public void Create(Texture[] textures, DepthBuffer db)
        {
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, FrameBufferName);
            GL.FramebufferRenderbuffer(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthAttachment,
                RenderbufferTarget.Renderbuffer, db.RenderBufferName);

            DrawBufferMode[] drawBuffers = new DrawBufferMode[textures.Length];
            for (int i = 0; i < textures.Length; ++i)
            {
                GL.FramebufferTexture2D(FramebufferTarget.Framebuffer,
                    FramebufferAttachment.ColorAttachment0 + i, TextureTarget2d.Texture2D, textures[i].TextureName, 0);
                drawBuffers[i] = DrawBufferMode.ColorAttachment0 + i;
            }

            GL.DrawBuffers(textures.Length, drawBuffers);
            FramebufferErrorCode errorCode = GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer);
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

    struct Vec4i
    {
        public int X;
        public int Y;
        public int Z;
        public int W;
    }
    /// <summary>
    /// Vertex array abstraction.
    /// </summary>
    class VertexArray : IDisposable
    {
        public VertexArray(Program program, Vector3[] positions, ushort[] elems, Vector3[] texCoords,
            Vector3[] normals) :
            this(program, positions, Array.ConvertAll(elems, e => (uint)e), texCoords, null, null,
                normals)
        {

        }

        public VertexArray(Program program, Vector3[] positions, uint[] elems, Vector3[] texCoords0,
            Vector3[] normals) :
            this(program, positions, elems, texCoords0, null, null, normals)
        {

        }

        public VertexArray(Program program, Vector3[] positions, uint[] elems, Vector3[] texCoords0,
            Vec4i[]texCoords1, Vector4[]texCoords2, Vector3[] normals)
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
            _BufferPosition = new GLObjects.Buffer<Vector3>(positions);
            if (texCoords0 != null)
            {
                _BufferTexCoords0 = new GLObjects.Buffer<Vector3>(texCoords0);
                stride += 3;
            }
            if (texCoords1 != null)
            {
                _BufferTexCoords1 = new GLObjects.Buffer<Vec4i>(texCoords1);
                stride += 4;
            }
            if (texCoords2 != null)
            {
                _BufferTexCoords2 = new GLObjects.Buffer<Vector4>(texCoords2);
                stride += 4;
            }
            if (normals != null)
            {
                _BufferNormal = new GLObjects.Buffer<Vector3>(normals);
                stride += 3;
            }
            if (elems != null)
            {
                elementCount = elems.Length;
                _BufferElems = new GLObjects.Buffer<uint>(elems);
                GL.BindBuffer(BufferTarget.ElementArrayBuffer, _BufferElems.BufferName);
            }

            // Select the buffer object
            GL.BindBuffer(BufferTarget.ArrayBuffer, _BufferPosition.BufferName);
            GL.VertexAttribPointer((int)program.LocationPosition, 3, VertexAttribPointerType.Float, false, 0, IntPtr.Zero);
            GL.EnableVertexAttribArray((int)program.LocationPosition);

            if (texCoords0 != null && program.LocationTexCoord0 >= 0)
            {
                GL.BindBuffer(BufferTarget.ArrayBuffer, _BufferTexCoords0.BufferName);
                GL.VertexAttribPointer((int)program.LocationTexCoord0, 3, VertexAttribPointerType.Float, false, 0, IntPtr.Zero);
                GL.EnableVertexAttribArray((int)program.LocationTexCoord0);
            }

            if (texCoords1 != null && program.LocationTexCoord1 >= 0)
            {
                GL.BindBuffer(BufferTarget.ArrayBuffer, _BufferTexCoords1.BufferName);
                GL.VertexAttribIPointer((int)program.LocationTexCoord1, 4, VertexAttribIntegerType.Int, 0, IntPtr.Zero);
                GL.EnableVertexAttribArray((int)program.LocationTexCoord1);
            }

            if (texCoords2 != null && program.LocationTexCoord2 >= 0)
            {
                GL.BindBuffer(BufferTarget.ArrayBuffer, _BufferTexCoords2.BufferName);
                GL.VertexAttribPointer((int)program.LocationTexCoord2, 4, VertexAttribPointerType.Float, false, 0, IntPtr.Zero);
                GL.EnableVertexAttribArray((int)program.LocationTexCoord2);
            }

            if (normals != null && program.LocationNormals >= 0)
            {
                GL.BindBuffer(BufferTarget.ArrayBuffer, _BufferNormal.BufferName);
                GL.VertexAttribPointer((int)program.LocationNormals, 3, VertexAttribPointerType.Float, false, 0, IntPtr.Zero);
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
            _BufferTexCoords0.Update(texcoords);
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
            _BufferWireframeElems = new GLObjects.Buffer<uint>(wireframeElems.ToArray());
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, _BufferWireframeElems.BufferName);
        }

        public void Draw(int offset, int count)
        {
            GL.BindVertexArray(ArrayName);
            if (_BufferElems != null)
                GL.DrawElements(PrimitiveType.Triangles, count, DrawElementsType.UnsignedInt, (IntPtr)(offset * 4));
            else
                GL.DrawArrays(PrimitiveType.Points, 0, vertexCount);
        }

        public void Draw()
        {
            Draw(0, elementCount);
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

        private readonly GLObjects.BufferBase _BufferPosition;
        private readonly GLObjects.BufferBase _BufferNormal;
        private readonly GLObjects.BufferBase _BufferElems;

        private readonly GLObjects.BufferBase _BufferTexCoords0;
        private readonly GLObjects.BufferBase _BufferTexCoords1;
        private readonly GLObjects.BufferBase _BufferTexCoords2;
        private GLObjects.Buffer<uint> _BufferWireframeElems;
        private readonly Program _Program;

        public Program Program { get { return _Program; } }
        uint[] ElementArray = null;

        public void Dispose()
        {
            GL.DeleteVertexArray(ArrayName);

            _BufferPosition.Dispose();
            _BufferTexCoords0.Dispose();
            _BufferTexCoords1.Dispose();
            _BufferTexCoords2.Dispose();
            _BufferNormal.Dispose();
            _BufferElems.Dispose();
        }
    }
}
