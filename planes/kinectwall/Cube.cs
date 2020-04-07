using GLObjects;
using OpenTK;
using System;
using bs = BulletSharp;


namespace kinectwall
{
    class Cube
    {
        public static bs.TriangleMesh MakeBulletMesh(Vector3 scale)
        {
            bs.TriangleMesh bulletMesh = new bs.TriangleMesh();
            for (int i = 0; i < _Cube.Length; i += 3)
            {
                bulletMesh.AddTriangle(Utils.FromVector3(_Cube[i] * scale),
                    Utils.FromVector3(_Cube[i + 1] * scale),
                    Utils.FromVector3(_Cube[i + 1] * scale));
            }

            return bulletMesh;
        }

        public static VertexArray MakeCube(Program program)
        {
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


            Vector3[] nrmCoords = new Vector3[_Cube.Length];
            for (int i = 0; i < 6; ++i)
            {
                Vector3 d1 = _Cube[i * 6 + 1] - _Cube[i * 6];
                Vector3 d2 = _Cube[i * 6 + 2] - _Cube[i * 6 + 1];
                Vector3 nrm = Vector3.Cross(d1, d2).Normalized();
                for (int nIdx = 0; nIdx < 6; ++nIdx)
                {
                    nrmCoords[i * 6 + nIdx] = nrm;
                }
            }

            for (int i = 0; i < indices.Length; ++i)
            {
                indices[i] = (ushort)i;
                Vector3 xdir = xdirs[i / 12];
                Vector3 ydir = ydirs[i / 12];
                int sideIdx = i / 6;
                texCoords[i] = new Vector3(Vector3.Dot(_Cube[i], xdir),
                    Vector3.Dot(_Cube[i], ydir), (float)sideIdx / 6.0f);
            }

            return new VertexArray(program, _Cube, indices, texCoords, nrmCoords);
        }

        static Vector3[] _Octahedron
         = new Vector3[] {
            new Vector3(1, 0, 0),
            new Vector3(0, -1, 0),
            new Vector3(-1, 0, 0),
            new Vector3(0, 1, 0),
            new Vector3(0, 0, 1),
            new Vector3(0, 0, -1) };

        static uint[] _OctaIndices =
        {
            4, 0, 1,
            4, 1, 2,
            4, 2, 3,
            4, 3, 0,
            5, 1, 0,
            5, 2, 1,
            5, 3, 2,
            5, 0, 3
        };

        public static VertexArray MakeOctahedron(Program program)
        {
            Vector3[] texCoords = new Vector3[_Octahedron.Length];
            Vector3[] nrmCoords = new Vector3[_Octahedron.Length];
            for (int idx = 0; idx < _Octahedron.Length; ++idx)
            {
                texCoords[idx] = _Octahedron[idx];
                nrmCoords[idx] = _Octahedron[idx].Normalized();
            }

            return new VertexArray(program, _Octahedron, _OctaIndices, texCoords, nrmCoords);
        }

        private static readonly Vector3[] _Cube = new Vector3[] {
            new Vector3(-1.0f, -1.0f, -1.0f),  // 0 
            new Vector3(1.0f, -1.0f, -1.0f),  // 1
            new Vector3(1.0f, 1.0f, -1.0f),  // 2

            new Vector3(-1.0f, -1.0f, -1.0f),  // 0 
            new Vector3(1.0f, 1.0f, -1.0f),  // 2
            new Vector3(-1.0f, 1.0f, -1.0f),  // 3

            new Vector3(-1.0f, -1.0f, 1.0f),  // 4
            new Vector3(1.0f, -1.0f, 1.0f),  // 5
            new Vector3(1.0f, 1.0f, 1.0f),  // 6

            new Vector3(-1.0f, -1.0f, 1.0f),  // 4
            new Vector3(1.0f, 1.0f, 1.0f),  // 6
            new Vector3(-1.0f, 1.0f, 1.0f),  // 7

            new Vector3(-1.0f, -1.0f, -1.0f),  // 0 
            new Vector3(1.0f, -1.0f, -1.0f),  // 1
            new Vector3(1.0f, -1.0f, 1.0f),  // 5

            new Vector3(-1.0f, -1.0f, -1.0f),  // 0 
            new Vector3(1.0f, -1.0f, 1.0f),  // 5
            new Vector3(-1.0f, -1.0f, 1.0f),  // 4

            new Vector3(1.0f, 1.0f, -1.0f),  // 2
            new Vector3(-1.0f, 1.0f, -1.0f),  // 3
            new Vector3(-1.0f, 1.0f, 1.0f),  // 7

            new Vector3(1.0f, 1.0f, -1.0f),  // 2
            new Vector3(-1.0f, 1.0f, 1.0f),  // 7
            new Vector3(1.0f, 1.0f, 1.0f),  // 6

            new Vector3(-1.0f, -1.0f, -1.0f),  // 0 
            new Vector3(-1.0f, 1.0f, -1.0f),  // 3
            new Vector3(-1.0f, 1.0f, 1.0f),  // 7

            new Vector3(-1.0f, -1.0f, -1.0f),  // 0 
            new Vector3(-1.0f, 1.0f, 1.0f),  // 7
            new Vector3(-1.0f, -1.0f, 1.0f),  // 4

            new Vector3(1.0f, -1.0f, -1.0f),  // 1
            new Vector3(1.0f, 1.0f, -1.0f),  // 2
            new Vector3(1.0f, 1.0f, 1.0f),  // 6

            new Vector3(1.0f, -1.0f, -1.0f),  // 1
            new Vector3(1.0f, 1.0f, 1.0f),  // 6
            new Vector3(1.0f, -1.0f, 1.0f),  // 5
        };

        static Vector3[] _Torus = new Vector3[]
        {
new Vector3(0.946977f, 0.392250f, 0.000000f),
new Vector3(1.012500f, 0.000000f, 0.021651f),
new Vector3(1.025000f, 0.000000f, 0.000000f),
new Vector3(0.912331f, 0.377900f, 0.021651f),
new Vector3(0.987500f, 0.000000f, 0.021651f),
new Vector3(0.975000f, 0.000000f, 0.000000f),
new Vector3(0.912331f, 0.377900f, -0.021651f),
new Vector3(0.987500f, 0.000000f, -0.021651f),
new Vector3(1.012500f, 0.000000f, -0.021651f),
new Vector3(0.724784f, 0.724784f, 0.000000f),
new Vector3(0.935428f, 0.387467f, 0.021651f),
new Vector3(0.698268f, 0.698268f, 0.021651f),
new Vector3(0.900783f, 0.373116f, 0.000000f),
new Vector3(0.689429f, 0.689429f, 0.000000f),
new Vector3(0.698268f, 0.698268f, -0.021651f),
new Vector3(0.935428f, 0.387467f, -0.021651f),
new Vector3(0.387467f, 0.935428f, 0.021651f),
new Vector3(0.715946f, 0.715946f, 0.021651f),
new Vector3(0.373117f, 0.900782f, 0.000000f),
new Vector3(0.387467f, 0.935428f, -0.021651f),
new Vector3(0.715946f, 0.715946f, -0.021651f),
new Vector3(0.000000f, 1.025000f, 0.000000f),
new Vector3(0.392251f, 0.946976f, 0.000000f),
new Vector3(0.000000f, 1.012500f, 0.021651f),
new Vector3(0.377900f, 0.912331f, 0.021651f),
new Vector3(0.000000f, 0.987500f, 0.021651f),
new Vector3(0.000000f, 0.987500f, -0.021651f),
new Vector3(0.377900f, 0.912331f, -0.021651f),
new Vector3(0.000000f, 1.012500f, -0.021651f),
new Vector3(-0.392251f, 0.946976f, 0.000000f),
new Vector3(-0.377900f, 0.912331f, 0.021651f),
new Vector3(0.000000f, 0.975000f, 0.000000f),
new Vector3(-0.377900f, 0.912331f, -0.021651f),
new Vector3(-0.387467f, 0.935428f, -0.021651f),
new Vector3(-0.724785f, 0.724784f, 0.000000f),
new Vector3(-0.387467f, 0.935428f, 0.021651f),
new Vector3(-0.698268f, 0.698268f, 0.021651f),
new Vector3(-0.373116f, 0.900783f, 0.000000f),
new Vector3(-0.698268f, 0.698268f, -0.021651f),
new Vector3(-0.935428f, 0.387467f, 0.021651f),
new Vector3(-0.715946f, 0.715945f, 0.021651f),
new Vector3(-0.900783f, 0.373116f, 0.000000f),
new Vector3(-0.689429f, 0.689429f, 0.000000f),
new Vector3(-0.935428f, 0.387467f, -0.021651f),
new Vector3(-0.715946f, 0.715945f, -0.021651f),
new Vector3(-1.025000f, 0.000000f, 0.000000f),
new Vector3(-0.946976f, 0.392251f, 0.000000f),
new Vector3(-1.012500f, 0.000000f, 0.021651f),
new Vector3(-0.912331f, 0.377900f, 0.021651f),
new Vector3(-0.987500f, 0.000000f, 0.021651f),
new Vector3(-0.975000f, 0.000000f, 0.000000f),
new Vector3(-0.912331f, 0.377900f, -0.021651f),
new Vector3(-0.987500f, 0.000000f, -0.021651f),
new Vector3(-1.012500f, 0.000000f, -0.021651f),
new Vector3(-0.946977f, -0.392250f, 0.000000f),
new Vector3(-0.912331f, -0.377900f, 0.021651f),
new Vector3(-0.900783f, -0.373116f, 0.000000f),
new Vector3(-0.912331f, -0.377900f, -0.021651f),
new Vector3(-0.935428f, -0.387467f, -0.021651f),
new Vector3(-0.724785f, -0.724784f, 0.000000f),
new Vector3(-0.935428f, -0.387467f, 0.021651f),
new Vector3(-0.698268f, -0.698268f, 0.021651f),
new Vector3(-0.689429f, -0.689429f, 0.000000f),
new Vector3(-0.698268f, -0.698268f, -0.021651f),
new Vector3(-0.387467f, -0.935428f, 0.021651f),
new Vector3(-0.715946f, -0.715945f, 0.021651f),
new Vector3(-0.373116f, -0.900783f, 0.000000f),
new Vector3(-0.387467f, -0.935428f, -0.021651f),
new Vector3(-0.715946f, -0.715945f, -0.021651f),
new Vector3(0.000000f, -1.025000f, 0.000000f),
new Vector3(-0.392251f, -0.946976f, 0.000000f),
new Vector3(0.000000f, -1.012500f, 0.021651f),
new Vector3(-0.377900f, -0.912331f, 0.021651f),
new Vector3(0.000000f, -0.975000f, 0.000000f),
new Vector3(-0.377900f, -0.912331f, -0.021651f),
new Vector3(0.000000f, -1.012500f, -0.021651f),
new Vector3(0.392251f, -0.946976f, 0.000000f),
new Vector3(0.377900f, -0.912331f, 0.021651f),
new Vector3(0.000000f, -0.987500f, 0.021651f),
new Vector3(0.373117f, -0.900782f, 0.000000f),
new Vector3(0.000000f, -0.987500f, -0.021651f),
new Vector3(0.377900f, -0.912331f, -0.021651f),
new Vector3(0.387467f, -0.935428f, -0.021651f),
new Vector3(0.724784f, -0.724785f, 0.000000f),
new Vector3(0.387467f, -0.935428f, 0.021651f),
new Vector3(0.698268f, -0.698268f, 0.021651f),
new Vector3(0.698268f, -0.698268f, -0.021651f),
new Vector3(0.715945f, -0.715946f, -0.021651f),
new Vector3(0.935428f, -0.387467f, 0.021651f),
new Vector3(0.715945f, -0.715946f, 0.021651f),
new Vector3(0.900782f, -0.373117f, 0.000000f),
new Vector3(0.689429f, -0.689430f, 0.000000f),
new Vector3(0.935428f, -0.387467f, -0.021651f),
new Vector3(0.946976f, -0.392251f, 0.000000f),
new Vector3(0.912331f, -0.377900f, 0.021651f),
new Vector3(0.912331f, -0.377900f, -0.021651f)
        };

        static uint[] _TorusIndices =
        {
0, 1, 2,
1, 3, 4,
3, 5, 4,
5, 6, 7,
6, 8, 7,
8, 0, 2,
9, 10, 0,
10, 11, 3,
11, 12, 3,
13, 6, 12,
14, 15, 6,
15, 9, 0,
9, 16, 17,
16, 11, 17,
11, 18, 13,
18, 14, 13,
14, 19, 20,
19, 9, 20,
21, 16, 22,
23, 24, 16,
25, 18, 24,
18, 26, 27,
27, 28, 19,
28, 22, 19,
29, 23, 21,
23, 30, 25,
30, 31, 25,
31, 32, 26,
32, 28, 26,
33, 21, 28,
34, 35, 29,
35, 36, 30,
36, 37, 30,
37, 38, 32,
38, 33, 32,
33, 34, 29,
34, 39, 40,
39, 36, 40,
36, 41, 42,
41, 38, 42,
38, 43, 44,
43, 34, 44,
45, 39, 46,
47, 48, 39,
49, 41, 48,
50, 51, 41,
52, 43, 51,
53, 46, 43,
54, 47, 45,
47, 55, 49,
49, 56, 50,
56, 52, 50,
57, 53, 52,
58, 45, 53,
59, 60, 54,
60, 61, 55,
61, 56, 55,
62, 57, 56,
63, 58, 57,
58, 59, 54,
59, 64, 65,
64, 61, 65,
61, 66, 62,
66, 63, 62,
63, 67, 68,
67, 59, 68,
69, 64, 70,
71, 72, 64,
72, 73, 66,
73, 74, 66,
74, 75, 67,
75, 70, 67,
76, 71, 69,
71, 77, 78,
78, 79, 73,
79, 80, 73,
81, 75, 80,
82, 69, 75,
83, 84, 76,
84, 85, 77,
85, 79, 77,
79, 86, 81,
86, 82, 81,
87, 76, 82,
83, 88, 89,
88, 85, 89,
85, 90, 91,
90, 86, 91,
86, 92, 87,
92, 83, 87,
93, 1, 88,
1, 94, 88,
94, 5, 90,
5, 95, 90,
95, 8, 92,
8, 93, 92,
0, 10, 1,
1, 10, 3,
3, 12, 5,
5, 12, 6,
6, 15, 8,
8, 15, 0,
9, 17, 10,
10, 17, 11,
11, 13, 12,
13, 14, 6,
14, 20, 15,
15, 20, 9,
9, 22, 16,
16, 24, 11,
11, 24, 18,
18, 27, 14,
14, 27, 19,
19, 22, 9,
21, 23, 16,
23, 25, 24,
25, 31, 18,
18, 31, 26,
27, 26, 28,
28, 21, 22,
29, 35, 23,
23, 35, 30,
30, 37, 31,
31, 37, 32,
32, 33, 28,
33, 29, 21,
34, 40, 35,
35, 40, 36,
36, 42, 37,
37, 42, 38,
38, 44, 33,
33, 44, 34,
34, 46, 39,
39, 48, 36,
36, 48, 41,
41, 51, 38,
38, 51, 43,
43, 46, 34,
45, 47, 39,
47, 49, 48,
49, 50, 41,
50, 52, 51,
52, 53, 43,
53, 45, 46,
54, 60, 47,
47, 60, 55,
49, 55, 56,
56, 57, 52,
57, 58, 53,
58, 54, 45,
59, 65, 60,
60, 65, 61,
61, 62, 56,
62, 63, 57,
63, 68, 58,
58, 68, 59,
59, 70, 64,
64, 72, 61,
61, 72, 66,
66, 74, 63,
63, 74, 67,
67, 70, 59,
69, 71, 64,
71, 78, 72,
72, 78, 73,
73, 80, 74,
74, 80, 75,
75, 69, 70,
76, 84, 71,
71, 84, 77,
78, 77, 79,
79, 81, 80,
81, 82, 75,
82, 76, 69,
83, 89, 84,
84, 89, 85,
85, 91, 79,
79, 91, 86,
86, 87, 82,
87, 83, 76,
83, 93, 88,
88, 94, 85,
85, 94, 90,
90, 95, 86,
86, 95, 92,
92, 93, 83,
93, 2, 1,
1, 4, 94,
94, 4, 5,
5, 7, 95,
95, 7, 8,
8, 2, 93
        };

        public static VertexArray MakeTorus(Program program)
        {
            Vector3[] texCoords = new Vector3[_Torus.Length];
            Vector3[] nrmCoords = new Vector3[_Torus.Length];
            for (int idx = 0; idx < _Torus.Length; ++idx)
            {
                texCoords[idx] = _Torus[idx];
                nrmCoords[idx] = _Torus[idx].Normalized();
            }

            return new VertexArray(program, _Torus, _TorusIndices, texCoords, nrmCoords);
        }

    }
}
