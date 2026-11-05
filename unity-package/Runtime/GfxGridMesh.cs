using System;
using System.Collections.Generic;
using UnityEngine;

namespace SilverEcho.ForsakenWorld
{
    /// <summary>
    /// Builds the ID 210/211 grid mesh (create_id_210._build_mesh):
    /// w*h shared verts, UV = (col,row)/(w-1,h-1), two tris per cell (0,1,2)/(1,3,2).
    /// </summary>
    public static class GfxGridMesh
    {
        public sealed class BuildResult
        {
            public Mesh Mesh;
            public int VertCount;
            public int TriCount;
        }

        public static BuildResult Build(GfxGridParams p, float[] positions, float[] colors)
        {
            var result = new BuildResult();
            if (p == null || p.W < 2 || p.H < 2)
            {
                result.Mesh = new Mesh { name = "GridDecal_empty" };
                return result;
            }

            var w = p.W;
            var h = p.H;
            var n = w * h;
            var verts = new Vector3[n];
            var cols = new Color[n];
            var uvs = new Vector2[n];
            var denomU = Math.Max(1, w - 1);
            var denomV = Math.Max(1, h - 1);
            for (var r = 0; r < h; r++)
            {
                for (var c = 0; c < w; c++)
                {
                    var i = r * w + c;
                    var po = i * 3;
                    var co = i * 4;
                    var px = positions != null && po + 2 < positions.Length ? positions[po] : 0f;
                    var py = positions != null && po + 2 < positions.Length ? positions[po + 1] : 0f;
                    var pz = positions != null && po + 2 < positions.Length ? positions[po + 2] : 0f;
                    verts[i] = new Vector3(px, py, pz);
                    if (colors != null && co + 3 < colors.Length)
                        cols[i] = new Color(colors[co], colors[co + 1], colors[co + 2], colors[co + 3]);
                    else
                        cols[i] = Color.white;

                    var u = (float)c / denomU;
                    var v = (float)r / denomV;
                    if (p.UReverse) u = 1f - u;
                    if (p.VReverse) v = 1f - v;
                    if (p.UVExchg)
                    {
                        var tmp = u;
                        u = v;
                        v = tmp;
                    }
                    uvs[i] = new Vector2(u, v);
                }
            }

            var indices = new List<int>((h - 1) * (w - 1) * 6);
            for (var r = 0; r < h - 1; r++)
            {
                for (var c = 0; c < w - 1; c++)
                {
                    var i1 = r * w + c;
                    var i3 = i1 + w;
                    // Godot: [i1, i1+1, i3, i1+1, i3+1, i3]
                    indices.Add(i1);
                    indices.Add(i1 + 1);
                    indices.Add(i3);
                    indices.Add(i1 + 1);
                    indices.Add(i3 + 1);
                    indices.Add(i3);
                }
            }

            var mesh = new Mesh { name = p.Is2D ? "GridDecal2D_GFX" : "GridDecal_GFX" };
            mesh.vertices = verts;
            mesh.colors = cols;
            mesh.uv = uvs;
            mesh.triangles = indices.ToArray();
            result.Mesh = mesh;
            result.VertCount = n;
            result.TriCount = indices.Count / 3;
            return result;
        }
    }
}
