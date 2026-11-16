using System;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SilverEcho.ForsakenWorld;

// C02 (glm-trails-work.md Finding 4): Lightning 150 UVExchg ordering.
// Unity side  : production GfxLightningEval.Build (EmitPair UVs, GfxLightningEval.cs:748-757 /
//               EmitPairEx :791-800, sheet via EvaluateSheet :172-210) — swap-then-sheet.
// Godot side  : exact constant model of the generated shader — strip cell_uv
//               (create_id_150.gd:311-312), base spritesheet/tile block
//               (create_id_140.gd:343-351), then 150 hook _gen_uv_post
//               (create_id_150.gd:315-318) swaps AFTER the sheet — sheet-then-swap.
// Pure CPU, no scene, no rendering. Run() writes unity-lightning-uv.json next to the case files.
public static class UnityLightningUvAudit
{
    const string OutDir = "B:/Git/Godot_Forsaken_World_Editor/.validation/gfx-parity/";
    const float ParityTol = 1e-4f;     // float-noise budget for parity cases
    const float DivergenceMin = 0.05f; // minimum meaningful UV channel error

    // Godot reference mapping (constant form of the generated GLSL).
    // tMs = v_current_time in ms (element clock; see clock note in the work md).
    static float[] GodotUv(GfxLightningParams p, float ratio, float side, float tMs)
    {
        var cellU = p.UReverse ? 1f - ratio : ratio;                 // create_id_150.gd:311
        var cellV = (side < 0.5f) != p.VReverse ? 1f : 0f;           // create_id_150.gd:312
        var fCols = Math.Max(1f, p.TexCol);
        var fRows = Math.Max(1f, p.TexRow);
        float u, v;
        if (p.TileMode)                                              // create_id_140.gd:348-351
        {
            var t = tMs / 1000f;
            u = cellU * fCols + t * p.TexSpeed1;
            v = cellV * fRows + t * p.TexSpeed2;
        }
        else                                                         // create_id_140.gd:343-347
        {
            var fInterval = Math.Max(1f, p.TexInterval);
            var frame = GfxMath.Fmod((float)Math.Floor(tMs / fInterval), fCols * fRows);
            u = (GfxMath.Fmod(frame, fCols) + cellU) / fCols;
            v = ((float)Math.Floor(frame / fCols) + cellV) / fRows;
        }
        // create_id_150.gd:315-318 — UVExchg runs after the sheet block.
        return p.UvExchange ? new[] { v, u } : new[] { u, v };
    }

    // Shared straight-lightning fixture: Segs=2, LightNum=1 -> 3 pairs (ratios 0, .5, 1), sides 0/1.
    static GfxLightningParams F(int cols, int rows, bool tile, bool exchg, float s1, float s2) =>
        new GfxLightningParams
        {
            SystemId = "150",
            TexFile = "audit_tex.png",
            Segs = 2,
            LightNum = 1,
            Interval = 100,
            Width = 0.5f,
            Alpha = 1f,
            RandomSeed = 7,
            TexCol = cols,
            TexRow = rows,
            TileMode = tile,
            UvExchange = exchg,
            TexSpeed1 = s1,
            TexSpeed2 = s2
        };

    static float MaxDiff(float[] a, float[] b)
    {
        var m = 0f;
        for (var i = 0; i < Math.Min(a.Length, b.Length); i++)
            m = Math.Max(m, Math.Abs(a[i] - b[i]));
        return m;
    }

    static JObject RunCase(string name, string expectation, GfxLightningParams p, float tMs)
    {
        var mesh = GfxLightningEval.Build(p, null, tMs, 0f, 1f, null); // production evaluator
        var pairs = mesh.PairCount;
        var verts = new JArray();
        var maxAbs = 0f;
        for (var i = 0; i < pairs; i++)
        {
            var ratio = pairs > 1 ? i / (float)(pairs - 1) : 0f;
            for (var s = 0; s < 2; s++)
            {
                var idx = i * 2 + s;
                var godot = GodotUv(p, ratio, s, tMs);
                var unity = new[] { mesh.Uvs[idx * 2], mesh.Uvs[idx * 2 + 1] };
                var dx = godot[0] - unity[0];
                var dy = godot[1] - unity[1];
                maxAbs = Math.Max(maxAbs, Math.Max(Math.Abs(dx), Math.Abs(dy)));
                verts.Add(new JObject
                {
                    ["pair"] = i,
                    ["side"] = s,
                    ["ratio"] = ratio,
                    ["godot"] = new JArray { godot[0], godot[1] },
                    ["unity"] = new JArray { unity[0], unity[1] },
                    ["diff"] = new JArray { dx, dy }
                });
            }
        }
        var pass = expectation == "parity"
            ? maxAbs <= ParityTol
            : maxAbs >= DivergenceMin;
        return new JObject
        {
            ["name"] = name,
            ["expectation"] = expectation, // "parity" or "divergence"
            ["elementTimeMs"] = tMs,
            ["texCol"] = p.TexCol,
            ["texRow"] = p.TexRow,
            ["tileMode"] = p.TileMode,
            ["texSpeed1"] = p.TexSpeed1,
            ["texSpeed2"] = p.TexSpeed2,
            ["uvExchg"] = p.UvExchange,
            ["uReverse"] = p.UReverse,
            ["vReverse"] = p.VReverse,
            ["pairs"] = pairs,
            ["godotFormula"] = p.UvExchange
                ? (p.TileMode
                    ? "x = v*R + (t/1000)*s2 ; y = u*C + (t/1000)*s1  [swap AFTER sheet]"
                    : "x = (frameY+v)/R ; y = (frameX+u)/C  [swap AFTER sheet]")
                : (p.TileMode
                    ? "x = u*C + (t/1000)*s1 ; y = v*R + (t/1000)*s2"
                    : "x = (frameX+u)/C ; y = (frameY+v)/R"),
            ["unityFormula"] = p.UvExchange
                ? (p.TileMode
                    ? "x = v*C + (t/1000)*s1 ; y = u*R + (t/1000)*s2  [swap BEFORE sheet]"
                    : "x = v/C + frameX/C ; y = u/R + frameY/R  [swap BEFORE sheet]")
                : (p.TileMode
                    ? "x = u*C + (t/1000)*s1 ; y = v*R + (t/1000)*s2"
                    : "x = u/C + frameX/C ; y = v/R + frameY/R"),
            ["verts"] = verts,
            ["maxAbsDiff"] = maxAbs,
            ["pass"] = pass
        };
    }

    public static object Run()
    {
        var cases = new JArray();
        // 1x1 controls: identity sheet — swap commutes, UVExchg alone must be harmless.
        cases.Add(RunCase("control_1x1_exchg_off", "parity", F(1, 1, false, false, 0f, 0f), 0f));
        cases.Add(RunCase("control_1x1_exchg_on", "parity", F(1, 1, false, true, 0f, 0f), 0f));
        // Asymmetric spritesheet (2 cols x 3 rows), frame 0 (t=0): parity off, divergence on.
        cases.Add(RunCase("sheet_2x3_exchg_off", "parity", F(2, 3, false, false, 0f, 0f), 0f));
        cases.Add(RunCase("sheet_2x3_exchg_on", "divergence", F(2, 3, false, true, 0f, 0f), 0f));
        // Tile/scroll: C=2, R=3, s1=0.5, s2=-0.25, t=1000ms: parity off, divergence on.
        cases.Add(RunCase("tile_scroll_exchg_off", "parity", F(2, 3, true, false, 0.5f, -0.25f), 1000f));
        cases.Add(RunCase("tile_scroll_exchg_on", "divergence", F(2, 3, true, true, 0.5f, -0.25f), 1000f));

        // UV-only finding: UVExchg must not move positions (same fixture, sheet branch).
        var posOff = GfxLightningEval.Build(F(2, 3, false, false, 0f, 0f), null, 0f, 0f, 1f, null);
        var posOn = GfxLightningEval.Build(F(2, 3, false, true, 0f, 0f), null, 0f, 0f, 1f, null);
        var posDiff = MaxDiff(posOff.Positions, posOn.Positions);

        var parityCases = 0; var parityPassed = 0;
        var divCases = 0; var divConfirmed = 0;
        foreach (var c in cases)
        {
            if ((string)c["expectation"] == "parity")
            {
                parityCases++;
                if ((bool)c["pass"]) parityPassed++;
            }
            else
            {
                divCases++;
                if ((bool)c["pass"]) divConfirmed++;
            }
        }
        var allPass = parityPassed == parityCases && divConfirmed == divCases && posDiff <= 1e-6f;

        var result = new JObject
        {
            ["audit"] = "C02 lightning 150 UVExchg order (Unity vs Godot)",
            ["unity"] = "production GfxLightningEval.Build (pure C# evaluator)",
            ["godot"] = "constant model of create_id_140.gd:343-352 + create_id_150.gd:311-318",
            ["clockMapping"] = "v_current_time ~= elementTimeMs = elapsed + ElementStartTime (ms); TexInterval in ms on both sides; tile speeds per second on both sides",
            ["cases"] = cases,
            ["positionsMaxAbsDiff"] = posDiff,
            ["positionsInvariantToUvExchg"] = posDiff <= 1e-6f,
            ["summary"] = new JObject
            {
                ["parityCases"] = parityCases,
                ["parityPassed"] = parityPassed,
                ["divergenceCases"] = divCases,
                ["divergenceConfirmed"] = divConfirmed,
                ["allAssertionsPass"] = allPass
            }
        };
        File.WriteAllText(OutDir + "unity-lightning-uv.json", result.ToString(Formatting.Indented));
        return new
        {
            cases = cases.Count,
            parityPassed,
            divergenceConfirmed = divConfirmed,
            positionsInvariant = posDiff <= 1e-6f,
            allAssertionsPass = allPass,
            output = OutDir + "unity-lightning-uv.json"
        };
    }
}
