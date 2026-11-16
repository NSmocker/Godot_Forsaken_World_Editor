using System;

namespace SilverEcho.ForsakenWorld
{
    /// <summary>
    /// TileMode scroll and TexRow×TexCol sheet animation for ID 100/101
    /// (create_id_100.gd fragment UV block). Pure C#.
    /// Returns an optional unit-UV axis exchange followed by uv * scale + offset.
    /// </summary>
    public static class GfxSpriteUvEval
    {
        public static GfxSpriteUvState Evaluate(GfxSpriteParams p, float currentTimeMs)
        {
            if (p == null)
                return GfxSpriteUvState.Identity;

            float scaleU;
            float scaleV;
            float offsetU;
            float offsetV;

            if (p.TileMode)
            {
                var cols = Math.Max(1f, p.TexCol);
                var rows = Math.Max(1f, p.TexRow);
                var tSec = currentTimeMs / 1000f;
                scaleU = cols;
                scaleV = rows;
                offsetU = tSec * p.TexSpeed1;
                offsetV = tSec * p.TexSpeed2;
            }
            else if (p.TexCol > 1 || p.TexRow > 1)
            {
                var cols = (float)Math.Max(1, p.TexCol);
                var rows = (float)Math.Max(1, p.TexRow);
                var total = cols * rows;
                var interval = Math.Max(1f, p.TexInterval);
                var frame = GfxMath.Fmod(
                    (float)Math.Floor(currentTimeMs / interval), total);
                var frameX = GfxMath.Fmod(frame, cols);
                var frameY = (float)Math.Floor(frame / cols);
                scaleU = 1f / cols;
                scaleV = 1f / rows;
                offsetU = frameX / cols;
                offsetV = frameY / rows;
            }
            else
            {
                scaleU = 1f;
                scaleV = 1f;
                offsetU = 0f;
                offsetV = 0f;
            }

            // Godot: UVExchg, then UReverse, then VReverse on unit UV before tile/sheet.
            // Exchanging axes is an off-diagonal transform; scale/offset alone
            // cannot represent it. The shader swaps unit UV before this map.

            if (p.UReverse)
            {
                offsetU = scaleU + offsetU;
                scaleU = -scaleU;
            }

            if (p.VReverse)
            {
                offsetV = scaleV + offsetV;
                scaleV = -scaleV;
            }

            return new GfxSpriteUvState
            {
                Exchange = p.UvExchange,
                ScaleU = scaleU,
                ScaleV = scaleV,
                OffsetU = offsetU,
                OffsetV = offsetV
            };
        }
    }
}
