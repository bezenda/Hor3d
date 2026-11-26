using UnityEngine;

namespace AshenWoods.EditorTools
{
    /// <summary>
    /// The house dressing's surface helpers, shared with the menu scene builder: boxes whose
    /// UVs are in metres (so textures tile evenly at any size) and Poly Haven surface materials.
    /// </summary>
    public static partial class HouseBuilder
    {
        /// <summary>Unit cube mesh with UVs in metres for a box of <paramref name="size"/> (sub-mesh 0 = top).</summary>
        internal static Mesh MetricBoxMesh(Vector3 size) => BoxMesh(size);

        /// <summary>URP Lit over a Poly Haven texture set, tiled every <paramref name="tileMeters"/>.</summary>
        internal static Material PolyHavenSurface(string textureId, float tileMeters, float smoothness, Color tint)
            => SurfaceMaterial(textureId, tileMeters, smoothness, tint);
    }
}
