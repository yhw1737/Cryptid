using UnityEngine;

namespace Cryptid.Core
{
    /// <summary>
    /// Generates a flat-top hexagonal mesh at runtime.
    /// Creates a single hex tile with proper UVs and normals.
    /// 
    /// The mesh lies on the XZ plane (Y = 0) and uses HexMetrics.OuterRadius
    /// for sizing consistency with the rest of the grid system.
    /// </summary>
    public static class HexMeshGenerator
    {
        /// <summary>
        /// Creates a flat hexagonal mesh (7 vertices: center + 6 corners).
        /// Flat-top orientation, lying on the XZ plane.
        /// </summary>
        public static Mesh CreateFlatHexMesh()
        {
            var mesh = new Mesh { name = "HexTile" };

            float outer = HexMetrics.OuterRadius;

            // 7 vertices: center + 6 corners
            var vertices = new Vector3[7];
            vertices[0] = Vector3.zero; // Center

            for (int i = 0; i < 6; i++)
            {
                float angleDeg = 60f * i;
                float angleRad = Mathf.Deg2Rad * angleDeg;
                vertices[i + 1] = new Vector3(
                    outer * Mathf.Cos(angleRad),
                    0f,
                    outer * Mathf.Sin(angleRad)
                );
            }

            // 6 triangles (18 indices), all facing up (Y+)
            var triangles = new int[18];
            for (int i = 0; i < 6; i++)
            {
                triangles[i * 3] = 0;                     // Center
                triangles[i * 3 + 1] = i + 1;             // Current corner
                triangles[i * 3 + 2] = (i < 5) ? i + 2 : 1; // Next corner (wrap)
            }

            // UVs: map hex to 0-1 range based on position
            var uvs = new Vector2[7];
            uvs[0] = new Vector2(0.5f, 0.5f); // Center
            for (int i = 0; i < 6; i++)
            {
                uvs[i + 1] = new Vector2(
                    (vertices[i + 1].x / outer + 1f) * 0.5f,
                    (vertices[i + 1].z / outer + 1f) * 0.5f
                );
            }

            // All normals face up
            var normals = new Vector3[7];
            for (int i = 0; i < 7; i++)
            {
                normals[i] = Vector3.up;
            }

            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.uv = uvs;
            mesh.normals = normals;

            return mesh;
        }

        /// <summary>
        /// Creates a hexagonal prism mesh with configurable height.
        /// The top face is at Y = height, bottom face at Y = 0.
        /// Includes top, bottom, and side faces with proper normals.
        /// </summary>
        public static Mesh CreateHexPrismMesh(float height = 0.1f)
        {
            var mesh = new Mesh { name = "HexTilePrism" };

            float outer = HexMetrics.OuterRadius;

            // Calculate corner positions
            var topCorners = new Vector3[6];
            var bottomCorners = new Vector3[6];

            for (int i = 0; i < 6; i++)
            {
                float angleDeg = 60f * i;
                float angleRad = Mathf.Deg2Rad * angleDeg;
                float x = outer * Mathf.Cos(angleRad);
                float z = outer * Mathf.Sin(angleRad);

                topCorners[i] = new Vector3(x, height, z);
                bottomCorners[i] = new Vector3(x, 0f, z);
            }

            // Vertices: top (7) + bottom (7) + sides (6 quads * 4 = 24) = 38
            // Top: center + 6 corners
            // Bottom: center + 6 corners
            // Sides: each quad has 4 unique vertices for flat shading

            var vertices = new Vector3[38];
            var normals = new Vector3[38];
            var uvs = new Vector2[38];

            // --- Top face (indices 0-6) ---
            vertices[0] = new Vector3(0f, height, 0f); // Top center
            normals[0] = Vector3.up;
            uvs[0] = new Vector2(0.5f, 0.5f);

            for (int i = 0; i < 6; i++)
            {
                vertices[i + 1] = topCorners[i];
                normals[i + 1] = Vector3.up;
                uvs[i + 1] = new Vector2(
                    (topCorners[i].x / outer + 1f) * 0.5f,
                    (topCorners[i].z / outer + 1f) * 0.5f);
            }

            // --- Bottom face (indices 7-13) ---
            vertices[7] = new Vector3(0f, 0f, 0f); // Bottom center
            normals[7] = Vector3.down;
            uvs[7] = new Vector2(0.5f, 0.5f);

            for (int i = 0; i < 6; i++)
            {
                vertices[i + 8] = bottomCorners[i];
                normals[i + 8] = Vector3.down;
                uvs[i + 8] = new Vector2(
                    (bottomCorners[i].x / outer + 1f) * 0.5f,
                    (bottomCorners[i].z / outer + 1f) * 0.5f);
            }

            // --- Side faces (indices 14-37, 4 vertices per quad) ---
            int sideStart = 14;
            for (int i = 0; i < 6; i++)
            {
                int next = (i + 1) % 6;
                int baseIdx = sideStart + i * 4;

                // Quad: topCorners[i], topCorners[next], bottomCorners[next], bottomCorners[i]
                vertices[baseIdx] = topCorners[i];
                vertices[baseIdx + 1] = topCorners[next];
                vertices[baseIdx + 2] = bottomCorners[next];
                vertices[baseIdx + 3] = bottomCorners[i];

                // Side normal: perpendicular to the edge, pointing outward
                Vector3 edge = topCorners[next] - topCorners[i];
                Vector3 sideNormal = Vector3.Cross(edge, Vector3.up).normalized;

                normals[baseIdx] = sideNormal;
                normals[baseIdx + 1] = sideNormal;
                normals[baseIdx + 2] = sideNormal;
                normals[baseIdx + 3] = sideNormal;

                // UVs for side (simple mapping)
                uvs[baseIdx] = new Vector2(0f, 1f);
                uvs[baseIdx + 1] = new Vector2(1f, 1f);
                uvs[baseIdx + 2] = new Vector2(1f, 0f);
                uvs[baseIdx + 3] = new Vector2(0f, 0f);
            }

            // --- Triangles ---
            // Top: 6 triangles
            // Bottom: 6 triangles (reversed winding)
            // Sides: 6 quads = 12 triangles
            var triangles = new int[(6 + 6 + 12) * 3];
            int tri = 0;

            // Top face
            for (int i = 0; i < 6; i++)
            {
                triangles[tri++] = 0;
                triangles[tri++] = i + 1;
                triangles[tri++] = (i < 5) ? i + 2 : 1;
            }

            // Bottom face (reversed winding for downward-facing normal)
            for (int i = 0; i < 6; i++)
            {
                triangles[tri++] = 7;
                triangles[tri++] = (i < 5) ? i + 9 : 8;
                triangles[tri++] = i + 8;
            }

            // Side faces (two triangles per quad)
            for (int i = 0; i < 6; i++)
            {
                int baseIdx = sideStart + i * 4;

                triangles[tri++] = baseIdx;
                triangles[tri++] = baseIdx + 1;
                triangles[tri++] = baseIdx + 2;

                triangles[tri++] = baseIdx;
                triangles[tri++] = baseIdx + 2;
                triangles[tri++] = baseIdx + 3;
            }

            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.uv = uvs;
            mesh.normals = normals;

            return mesh;
        }
    }
}
