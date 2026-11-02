using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Freelancer.EditorTools
{
    /// <summary>
    /// Generates flat-shaded low-poly meshes (saved as assets) for the low-poly art style.
    /// Same sizes as Unity's primitives: capsule/cylinder are 2 tall with radius 0.5, sphere has radius 0.5.
    /// </summary>
    static class LowPolyMeshes
    {
        public const string Dir = SetupUtil.Root + "/Art/Meshes";

        public static Mesh Capsule => Get("LP_Capsule", () => Lathe(CapsuleProfile(3), 8));
        public static Mesh Sphere => Get("LP_Sphere", () => Lathe(SphereProfile(5), 9));
        public static Mesh Cylinder => Get("LP_Cylinder", () => Lathe(new List<Vector2>
            { new(0, 1), new(0.5f, 1), new(0.5f, -1), new(0, -1) }, 10));

        /// <summary>A render-only object using a low-poly mesh.</summary>
        public static GameObject Visual(Mesh mesh, string name, Transform parent, Vector3 localPos,
            Vector3 localScale, Material mat, Quaternion? localRot = null)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = localRot ?? Quaternion.identity;
            go.transform.localScale = localScale;
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }

        static readonly Dictionary<string, Mesh> s_Built = new();

        static Mesh Get(string name, Func<Mesh> build)
        {
            if (s_Built.TryGetValue(name, out var cached) && cached != null) return cached;

            SetupUtil.EnsureFolder(Dir);
            string path = $"{Dir}/{name}.asset";
            var fresh = build();
            fresh.name = name;
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(fresh, path);
                existing = fresh;
            }
            else
            {
                EditorUtility.CopySerialized(fresh, existing);
                UnityEngine.Object.DestroyImmediate(fresh);
                EditorUtility.SetDirty(existing);
            }
            s_Built[name] = existing;
            return existing;
        }

        // Profiles are (radius, y) pairs from top to bottom; radius 0 means a pole.
        static List<Vector2> CapsuleProfile(int ringsPerCap)
        {
            var p = new List<Vector2> { new(0, 1) };
            for (int i = 1; i <= ringsPerCap; i++)
            {
                float a = Mathf.PI / 2f * (1f - i / (float)ringsPerCap);
                p.Add(new Vector2(0.5f * Mathf.Cos(a), 0.5f + 0.5f * Mathf.Sin(a)));
            }
            for (int i = 0; i < ringsPerCap; i++)
            {
                float a = -Mathf.PI / 2f * (i / (float)ringsPerCap);
                p.Add(new Vector2(0.5f * Mathf.Cos(a), -0.5f + 0.5f * Mathf.Sin(a)));
            }
            p.Add(new Vector2(0, -1));
            return p;
        }

        static List<Vector2> SphereProfile(int rings)
        {
            var p = new List<Vector2>();
            for (int i = 0; i <= rings; i++)
            {
                float a = Mathf.PI * i / rings;
                p.Add(new Vector2(0.5f * Mathf.Sin(a), 0.5f * Mathf.Cos(a)));
            }
            return p;
        }

        /// <summary>Spin a profile around the Y axis and build a flat-shaded mesh from it.</summary>
        static Mesh Lathe(List<Vector2> profile, int sides)
        {
            var verts = new List<Vector3>();
            var tris = new List<int>();

            Vector3 Point(Vector2 pr, int side)
            {
                float a = side * Mathf.PI * 2f / sides;
                return new Vector3(pr.x * Mathf.Cos(a), pr.y, pr.x * Mathf.Sin(a));
            }

            void Tri(Vector3 a, Vector3 b, Vector3 c)
            {
                // Make every face point outward (away from the centre). All shapes are convex.
                Vector3 n = Vector3.Cross(b - a, c - a);
                Vector3 centroid = (a + b + c) / 3f;
                if (Vector3.Dot(n, centroid) < 0) (b, c) = (c, b);
                int i = verts.Count;
                verts.Add(a); verts.Add(b); verts.Add(c);
                tris.Add(i); tris.Add(i + 1); tris.Add(i + 2);
            }

            for (int j = 0; j < profile.Count - 1; j++)
            {
                Vector2 top = profile[j], bottom = profile[j + 1];
                for (int k = 0; k < sides; k++)
                {
                    Vector3 v00 = Point(top, k), v01 = Point(top, k + 1);
                    Vector3 v10 = Point(bottom, k), v11 = Point(bottom, k + 1);
                    if (top.x > 1e-5f) Tri(v00, v01, v11);
                    if (bottom.x > 1e-5f) Tri(v00, v11, v10);
                }
            }

            var mesh = new Mesh();
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
