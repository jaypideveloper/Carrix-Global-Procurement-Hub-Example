using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ProcurementHub.Globe
{
    /// <summary>Procedural meshes for the network globe. All meshes carry vertex colours and normals for ProcurementHub/Unlit.</summary>
    public static class GlobeMeshes
    {
        public static Vector3 Dir(float lat, float lon)
        {
            float la = lat * Mathf.Deg2Rad, lo = lon * Mathf.Deg2Rad;
            return new Vector3(Mathf.Cos(la) * Mathf.Sin(lo), Mathf.Sin(la), -Mathf.Cos(la) * Mathf.Cos(lo));
        }

        public static Mesh Sphere(float radius, int segments, int rings, Color color)
        {
            var verts = new List<Vector3>();
            var normals = new List<Vector3>();
            var colors = new List<Color>();
            var tris = new List<int>();
            for (int r = 0; r <= rings; r++)
            {
                float lat = -90f + 180f * r / rings;
                for (int s = 0; s <= segments; s++)
                {
                    float lon = -180f + 360f * s / segments;
                    var d = Dir(lat, lon);
                    verts.Add(d * radius);
                    normals.Add(d);
                    colors.Add(color);
                }
            }
            int stride = segments + 1;
            for (int r = 0; r < rings; r++)
            {
                for (int s = 0; s < segments; s++)
                {
                    int a = r * stride + s, b = a + 1, c = a + stride, d = c + 1;
                    tris.Add(a); tris.Add(c); tris.Add(b);
                    tris.Add(b); tris.Add(c); tris.Add(d);
                }
            }
            var mesh = new Mesh { name = "GlobeSphere", indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.SetColors(colors);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            // Keep winding facing outward regardless of the handedness of Dir().
            EnsureOutward(mesh);
            return mesh;
        }

        static void EnsureOutward(Mesh mesh)
        {
            var v = mesh.vertices;
            var t = mesh.triangles;
            for (int i = 0; i < t.Length; i += 3)
            {
                var n = Vector3.Cross(v[t[i + 1]] - v[t[i]], v[t[i + 2]] - v[t[i]]);
                if (n.sqrMagnitude < 1e-12f) continue;
                if (Vector3.Dot(n, v[t[i]]) < 0)
                {
                    // Flip every triangle once and stop - winding is uniform across the grid.
                    for (int k = 0; k < t.Length; k += 3) { int tmp = t[k + 1]; t[k + 1] = t[k + 2]; t[k + 2] = tmp; }
                    mesh.triangles = t;
                }
                return;
            }
        }

        /// <summary>Hexagonal dots on a Fibonacci lattice wherever <see cref="WorldGeo.IsLand"/> is true.</summary>
        public static Mesh LandDots(int samples, float radius, float dotSize, Color color)
        {
            var verts = new List<Vector3>();
            var normals = new List<Vector3>();
            var colors = new List<Color>();
            var tris = new List<int>();
            float golden = Mathf.PI * (3f - Mathf.Sqrt(5f));
            var rnd = new System.Random(7);
            for (int i = 0; i < samples; i++)
            {
                float y = 1f - (i + 0.5f) / samples * 2f;
                float r = Mathf.Sqrt(1f - y * y);
                float theta = golden * i;
                var p = new Vector3(Mathf.Cos(theta) * r, y, Mathf.Sin(theta) * r);
                float lat = Mathf.Asin(p.y) * Mathf.Rad2Deg;
                // Invert Dir(): x = cos(lat) sin(lon), z = -cos(lat) cos(lon)
                float lon = Mathf.Atan2(p.x, -p.z) * Mathf.Rad2Deg;
                if (!WorldGeo.IsLand(lon, lat)) continue;

                var n = p.normalized;
                var tangent = Vector3.Cross(n, Vector3.up);
                if (tangent.sqrMagnitude < 1e-4f) tangent = Vector3.Cross(n, Vector3.right);
                tangent.Normalize();
                var bitangent = Vector3.Cross(n, tangent);
                var center = n * radius;
                float shade = 0.82f + 0.18f * (float)rnd.NextDouble();
                var c = new Color(color.r * shade, color.g * shade, color.b * shade, color.a);

                int baseIndex = verts.Count;
                verts.Add(center); normals.Add(n); colors.Add(c);
                for (int k = 0; k < 6; k++)
                {
                    float a = k * Mathf.PI / 3f;
                    verts.Add(center + (tangent * Mathf.Cos(a) + bitangent * Mathf.Sin(a)) * dotSize);
                    normals.Add(n);
                    colors.Add(c);
                }
                for (int k = 0; k < 6; k++)
                {
                    tris.Add(baseIndex);
                    tris.Add(baseIndex + 1 + k);
                    tris.Add(baseIndex + 1 + (k + 1) % 6);
                }
            }
            var mesh = new Mesh { name = "LandDots", indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.SetColors(colors);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>Latitude/longitude grid as a line-topology mesh.</summary>
        public static Mesh Graticule(float radius, Color color)
        {
            var verts = new List<Vector3>();
            var normals = new List<Vector3>();
            var colors = new List<Color>();
            var idx = new List<int>();
            void Seg(Vector3 a, Vector3 b)
            {
                idx.Add(verts.Count); verts.Add(a * radius); normals.Add(a); colors.Add(color);
                idx.Add(verts.Count); verts.Add(b * radius); normals.Add(b); colors.Add(color);
            }
            for (int lon = -180; lon < 180; lon += 30)
                for (int lat = -80; lat < 80; lat += 4)
                    Seg(Dir(lat, lon), Dir(lat + 4, lon));
            for (int lat = -60; lat <= 60; lat += 30)
                for (int lon = -180; lon < 180; lon += 4)
                    Seg(Dir(lat, lon), Dir(lat, lon + 4));
            var mesh = new Mesh { name = "Graticule" };
            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.SetColors(colors);
            mesh.SetIndices(idx, MeshTopology.Lines, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>Unit-height square column standing on the XZ plane (grows along +Y). Faces are shaded for depth.</summary>
        public static Mesh Column()
        {
            var verts = new List<Vector3>();
            var normals = new List<Vector3>();
            var colors = new List<Color>();
            var tris = new List<int>();
            void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 n, float shade)
            {
                int i = verts.Count;
                verts.Add(a); verts.Add(b); verts.Add(c); verts.Add(d);
                for (int k = 0; k < 4; k++) { normals.Add(n); colors.Add(new Color(shade, shade, shade, 1f)); }
                tris.Add(i); tris.Add(i + 1); tris.Add(i + 2);
                tris.Add(i); tris.Add(i + 2); tris.Add(i + 3);
            }
            float h = 0.5f;
            var p000 = new Vector3(-h, 0, -h); var p100 = new Vector3(h, 0, -h); var p101 = new Vector3(h, 0, h); var p001 = new Vector3(-h, 0, h);
            var p010 = new Vector3(-h, 1, -h); var p110 = new Vector3(h, 1, -h); var p111 = new Vector3(h, 1, h); var p011 = new Vector3(-h, 1, h);
            Quad(p010, p011, p111, p110, Vector3.up, 1.0f);
            Quad(p000, p010, p110, p100, Vector3.back, 0.78f);
            Quad(p100, p110, p111, p101, Vector3.right, 0.64f);
            Quad(p101, p111, p011, p001, Vector3.forward, 0.78f);
            Quad(p001, p011, p010, p000, Vector3.left, 0.64f);
            var mesh = new Mesh { name = "PinColumn" };
            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.SetColors(colors);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>Flat ring in the XZ plane, facing +Y.</summary>
        public static Mesh Ring(float inner, float outer, int segments)
        {
            var verts = new List<Vector3>();
            var normals = new List<Vector3>();
            var colors = new List<Color>();
            var tris = new List<int>();
            for (int i = 0; i <= segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                var d = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                verts.Add(d * inner); verts.Add(d * outer);
                normals.Add(Vector3.up); normals.Add(Vector3.up);
                colors.Add(Color.white); colors.Add(new Color(1, 1, 1, 0.55f));
            }
            for (int i = 0; i < segments; i++)
            {
                int a = i * 2;
                tris.Add(a); tris.Add(a + 1); tris.Add(a + 3);
                tris.Add(a); tris.Add(a + 3); tris.Add(a + 2);
            }
            var mesh = new Mesh { name = "Ring" };
            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.SetColors(colors);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>Flat filled hexagon facing -Z (camera-facing billboard).</summary>
        public static Mesh Dot()
        {
            var verts = new List<Vector3> { Vector3.zero };
            var normals = new List<Vector3> { Vector3.back };
            var colors = new List<Color> { Color.white };
            var tris = new List<int>();
            for (int k = 0; k < 12; k++)
            {
                float a = k * Mathf.PI / 6f;
                verts.Add(new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0));
                normals.Add(Vector3.back);
                colors.Add(new Color(1, 1, 1, 0.0f));
            }
            for (int k = 0; k < 12; k++) { tris.Add(0); tris.Add(1 + (k + 1) % 12); tris.Add(1 + k); }
            var mesh = new Mesh { name = "Dot" };
            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.SetColors(colors);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
