#nullable enable
using UnityEngine;
using UnityEngine.UI;
namespace ModConfigEnhance.Overview;

/// <summary>A filled rectangle with 2 px rounded corners, drawn as a mesh (no sprite).</summary>
internal sealed class RoundedChip : MaskableGraphic
{
    public override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear(); Rect r = rectTransform.rect; float radius = Mathf.Min(2, Mathf.Min(r.width, r.height) * .5f);
        mesh.AddVert(r.center, color, Vector2.zero); int count = 0;
        for (int corner = 0; corner < 4; corner++)
        for (int step = 0; step <= 8; step++)
        {
            float angle = (corner * 90 + step * 90f / 8) * Mathf.Deg2Rad;
            Vector2 center = new(corner == 0 || corner == 3 ? r.xMax - radius : r.xMin + radius, corner < 2 ? r.yMax - radius : r.yMin + radius);
            mesh.AddVert(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius, color, Vector2.zero); count++;
        }
        for (int i = 1; i <= count; i++) mesh.AddTriangle(0, i, i == count ? 1 : i + 1);
    }
}
