using System.Collections.Generic;
using System.Linq;
using UnityEngine.Experimental.Rendering.LWRP.LibTessDotNet;

namespace UnityEngine.Experimental.Rendering.LWRP
{
    internal static class LightUtility
    {
        public static bool CheckForChange<T>(T a, ref T b)
        {
            bool changed = !Equals(a,b);
            b = a;
            return changed;
        }

        public static Bounds CalculateBoundingSphere(ref Vector3[] vertices)
        {
            Bounds localBounds = new Bounds();

            Vector3 minimum = new Vector3(float.MaxValue, float.MaxValue);
            Vector3 maximum = new Vector3(float.MinValue, float.MinValue);
            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 vertex = vertices[i];
                minimum.x = vertex.x < minimum.x ? vertex.x : minimum.x;
                minimum.y = vertex.y < minimum.y ? vertex.y : minimum.y;
                maximum.x = vertex.x > maximum.x ? vertex.x : maximum.x;
                maximum.y = vertex.y > maximum.y ? vertex.y : maximum.y;
            }

            localBounds.max = maximum;
            localBounds.min = minimum;

            return localBounds;
        }

        // Takes in a mesh that
        public static Bounds GenerateParametricMesh(ref Mesh mesh, float radius, float angle, int sides)
        {
            if (mesh == null)
                mesh = new Mesh();

            float angleOffset = Mathf.PI / 2.0f + Mathf.Deg2Rad * angle;
            if (sides < 3)
            {
                radius = 0.70710678118654752440084436210485f * radius;
                sides = 4;
            }

            if(sides == 4)
            {
                angleOffset = Mathf.PI / 4.0f + Mathf.Deg2Rad * angle;
            }

            // Return a shape with radius = 1
            Vector3[] vertices;
            int[] triangles;
            Color[] colors;

            int centerIndex;
            vertices = new Vector3[1 + 2 * sides];
            colors = new Color[1 + 2 * sides];
            triangles = new int[3 * 3 * sides];
            centerIndex = 2 * sides;

            // Color will contain r,g = x,y extrusion direction, a = alpha. b is unused at the moment. The inner shape should not be extruded
            Color color = new Color(0, 0, 0, 1);
            vertices[centerIndex] = Vector3.zero;
            colors[centerIndex] = color;
            float radiansPerSide = 2 * Mathf.PI / sides;
            
            for (int i = 0; i < sides; i++)
            {
                float endAngle = (i + 1) * radiansPerSide;

                Vector3 extrudeDir = new Vector3(Mathf.Cos(endAngle + angleOffset), Mathf.Sin(endAngle + angleOffset), 0);
                Vector3 endPoint = radius * extrudeDir;

                int vertexIndex;
                vertexIndex = (2 * i + 2) % (2 * sides);

                vertices[vertexIndex] = endPoint; // This is the extruded endpoint
                vertices[vertexIndex + 1] = endPoint;

                colors[vertexIndex] = new Color(extrudeDir.x, extrudeDir.y, 0, 0);
                colors[vertexIndex + 1] = color;

                // Triangle 1 (Tip)
                int triangleIndex = 9 * i;
                triangles[triangleIndex] = vertexIndex + 1;
                triangles[triangleIndex + 1] = 2 * i + 1;
                triangles[triangleIndex + 2] = centerIndex;

                // Triangle 2 (Upper Top Left)
                triangles[triangleIndex + 3] = vertexIndex;
                triangles[triangleIndex + 4] = 2 * i;
                triangles[triangleIndex + 5] = 2 * i + 1;

                // Triangle 2 (Bottom Top Left)
                triangles[triangleIndex + 6] = vertexIndex + 1;
                triangles[triangleIndex + 7] = vertexIndex;
                triangles[triangleIndex + 8] = 2 * i + 1;
            }

            mesh.Clear();
            mesh.vertices = vertices;
            mesh.colors = colors;
            mesh.triangles = triangles;

            return CalculateBoundingSphere(ref vertices);
        }

        public static Bounds GenerateSpriteMesh(ref Mesh mesh, Sprite sprite, float scale)
        {
            if (mesh == null)
                mesh = new Mesh();

            if (sprite != null)
            {
                Vector2[] vertices2d = sprite.vertices;
                Vector3[] vertices3d = new Vector3[vertices2d.Length];
                Color[] colors = new Color[vertices2d.Length];
                Vector4[] volumeColor = new Vector4[vertices2d.Length];

                ushort[] triangles2d = sprite.triangles;
                int[] triangles3d = new int[triangles2d.Length];


                Vector3 center = 0.5f * scale * (sprite.bounds.min + sprite.bounds.max);
                for (int vertexIdx = 0; vertexIdx < vertices2d.Length; vertexIdx++)
                {
                    Vector3 pos = new Vector3(vertices2d[vertexIdx].x, vertices2d[vertexIdx].y) - center;
                    vertices3d[vertexIdx] = scale * pos;
                    colors[vertexIdx] = new Color(0,0,0,1);  // This will not have any extrusion available. Alpha will be 1 * the pixel alpha
                }

                for (int triangleIdx = 0; triangleIdx < triangles2d.Length; triangleIdx++)
                {
                    triangles3d[triangleIdx] = (int)triangles2d[triangleIdx];
                }

                mesh.Clear();
                mesh.vertices = vertices3d;
                mesh.uv = sprite.uv;
                mesh.triangles = triangles3d;
                mesh.colors = colors;

                return CalculateBoundingSphere(ref vertices3d);
            }

            return new Bounds(Vector3.zero, Vector3.zero);
        }

        static void UpdateFeatheredShapeLightMesh(ContourVertex[] contourPoints, int contourPointCount, ref List<Vector2> featheredShape, ref List<Vector2> extrusionDir)
        {
            for (int i = 0; i < contourPointCount; ++i)
            {
                int h = (i == 0) ? (contourPointCount - 1) : (i - 1);
                int j = (i + 1) % contourPointCount;

                Vector2 pp = new Vector2(contourPoints[h].Position.X, contourPoints[h].Position.Y);
                Vector2 cp = new Vector2(contourPoints[i].Position.X, contourPoints[i].Position.Y);
                Vector2 np = new Vector2(contourPoints[j].Position.X, contourPoints[j].Position.Y);

                Vector2 cpd = cp - pp;
                Vector2 npd = np - cp;
                if (cpd.magnitude < 0.001f || npd.magnitude < 0.001f)
                    continue;

                Vector2 vl = cpd.normalized;
                Vector2 vr = npd.normalized;

                vl = new Vector2(-vl.y, vl.x);
                vr = new Vector2(-vr.y, vr.x);

                Vector2 va = vl.normalized + vr.normalized;
                Vector2 vn = -va.normalized;


                if (va.magnitude > 0 && vn.magnitude > 0)
                {
                    Vector2 dir = new Vector2(vn.x, vn.y);
                    featheredShape.Add(new Vector2(cp.x, cp.y) + dir);
                    extrusionDir.Add(5*dir);
                }
            }
        }

        static object InterpCustomVertexData(Vec3 position, object[] data, float[] weights)
        {
            return data[0];
        }


        public static void GetFeatheredShape(Vector3[] shapePath, float feathering, ref List<Vector2> featheredShape, ref List<Vector2> extrusionDir)
        {
            int pointCount = shapePath.Length;
            var inputs = new ContourVertex[pointCount];
            for (int i = 0; i < pointCount; ++i)
                inputs[i] = new ContourVertex() { Position = new Vec3() { X = shapePath[i].x, Y = shapePath[i].y }, Data = null };

            UpdateFeatheredShapeLightMesh(inputs, pointCount, ref featheredShape, ref extrusionDir);
        }


        public static void GenerateFeatheredGeometry(Vector3[] shapePath, float falloff, out Vector3[] featheredVertices )
        {

        }
        

        public static Bounds GenerateShapeMesh(ref Mesh mesh, Vector3[] shapePath)
        {
            Bounds localBounds;
            Color meshInteriorColor = new Color(0,0,0,1);

            int pointCount = shapePath.Length;
            var inputs = new ContourVertex[pointCount];
            for (int i = 0; i < pointCount; ++i)
                inputs[i] = new ContourVertex() { Position = new Vec3() { X = shapePath[i].x, Y = shapePath[i].y }, Data = meshInteriorColor };

            List<Vector2> feathered = new List<Vector2>();
            List<Vector2> extrusionDir = new List<Vector2>();
            UpdateFeatheredShapeLightMesh(inputs, pointCount, ref feathered, ref extrusionDir);
            int featheredPointCount = feathered.Count + pointCount;

            Tess tessI = new Tess();  // Interior
            Tess tessF = new Tess();  // Feathered Edge

            var inputsI = new ContourVertex[pointCount];
            for (int i = 0; i < pointCount - 1; ++i)
            {
                var inputsF = new ContourVertex[4];
                inputsF[0] = new ContourVertex() { Position = new Vec3() { X = shapePath[i].x, Y = shapePath[i].y }, Data = meshInteriorColor };
                inputsF[1] = new ContourVertex() { Position = new Vec3() { X = feathered[i].x, Y = feathered[i].y }, Data = new Color(extrusionDir[i].x, extrusionDir[i].y, 0, 0) };
                inputsF[2] = new ContourVertex() { Position = new Vec3() { X = feathered[i + 1].x, Y = feathered[i + 1].y }, Data = new Color(extrusionDir[i + 1].x, extrusionDir[i + 1].y, 0, 0) };
                inputsF[3] = new ContourVertex() { Position = new Vec3() { X = shapePath[i + 1].x, Y = shapePath[i + 1].y }, Data = meshInteriorColor };
                tessF.AddContour(inputsF, ContourOrientation.Original);

                inputsI[i] = new ContourVertex() { Position = new Vec3() { X = shapePath[i].x, Y = shapePath[i].y }, Data = meshInteriorColor };
            }
            var inputsL = new ContourVertex[4];
            inputsL[0] = new ContourVertex() { Position = new Vec3() { X = shapePath[pointCount - 1].x, Y = shapePath[pointCount - 1].y }, Data = meshInteriorColor };
            inputsL[1] = new ContourVertex() { Position = new Vec3() { X = feathered[pointCount - 1].x, Y = feathered[pointCount - 1].y }, Data = new Color(extrusionDir[pointCount - 1].x, extrusionDir[pointCount - 1].y, 0, 0) };
            inputsL[2] = new ContourVertex() { Position = new Vec3() { X = feathered[0].x, Y = feathered[0].y }, Data = new Color(extrusionDir[0].x, extrusionDir[0].y, 0, 0) };
            inputsL[3] = new ContourVertex() { Position = new Vec3() { X = shapePath[0].x, Y = shapePath[0].y }, Data = meshInteriorColor };

            inputsI[pointCount - 1] = new ContourVertex() { Position = new Vec3() { X = shapePath[pointCount - 1].x, Y = shapePath[pointCount - 1].y }, Data = meshInteriorColor };
            tessI.AddContour(inputsI, ContourOrientation.Original);
            tessI.Tessellate(WindingRule.EvenOdd, ElementType.Polygons, 3, InterpCustomVertexData);


            tessF.AddContour(inputsL, ContourOrientation.Original);
            tessF.Tessellate(WindingRule.EvenOdd, ElementType.Polygons, 3, InterpCustomVertexData);

            var indicesI = tessI.Elements.Select(i => i).ToArray();
            var verticesI = tessI.Vertices.Select(v => new Vector3(v.Position.X, v.Position.Y, 0)).ToArray();
            var colorsI = tessI.Vertices.Select(v => new Color(((Color)v.Data).r, ((Color)v.Data).g, ((Color)v.Data).b, ((Color)v.Data).a)).ToArray();

            var indicesF = tessF.Elements.Select(i => i + verticesI.Length).ToArray();
            var verticesF = tessF.Vertices.Select(v => new Vector3(v.Position.X, v.Position.Y, 0)).ToArray();
            var colorsF = tessF.Vertices.Select(v => new Color(((Color)v.Data).r, ((Color)v.Data).g, ((Color)v.Data).b, ((Color)v.Data).a)).ToArray();


            List<Vector3> finalVertices = new List<Vector3>();
            List<int> finalIndices = new List<int>();
            List<Color> finalColors = new List<Color>();
            finalVertices.AddRange(verticesI);
            finalVertices.AddRange(verticesF);
            finalIndices.AddRange(indicesI);
            finalIndices.AddRange(indicesF);
            finalColors.AddRange(colorsI);
            finalColors.AddRange(colorsF);

            Vector3[] vertices = finalVertices.ToArray();
            mesh.Clear();
            mesh.vertices = vertices;
            mesh.colors = finalColors.ToArray();
            mesh.SetIndices(finalIndices.ToArray(), MeshTopology.Triangles, 0);

            localBounds = CalculateBoundingSphere(ref vertices);

            return localBounds;
        }
    }
}
