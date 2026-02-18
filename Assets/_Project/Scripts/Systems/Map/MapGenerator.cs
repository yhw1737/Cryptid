using System.Collections.Generic;
using Cryptid.Core;
using Cryptid.Data;
using UnityEngine;

namespace Cryptid.Systems.Map
{
    /// <summary>
    /// Assembles MapPieceData into a unified world map.
    /// Reads a MapAssemblyConfig, applies rotation and translation to each piece,
    /// and produces a Dictionary of global HexCoordinates -> WorldTile.
    /// 
    /// Also handles spawning visual prefabs via TileVisualConfig.
    /// </summary>
    public class MapGenerator : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] private MapAssemblyConfig _assemblyConfig;
        [SerializeField] private TileVisualConfig _visualConfig;

        [Header("Generation Options")]
        [Tooltip("Parent transform for spawned tile objects")]
        [SerializeField] private Transform _tileContainer;

        [Tooltip("Spawn visual prefabs on Start")]
        [SerializeField] private bool _spawnOnStart = true;

        // ---------------------------------------------------------
        // Runtime Data
        // ---------------------------------------------------------

        /// <summary>
        /// The assembled world map. Key = global cube coordinates.
        /// Populated after GenerateMap() is called.
        /// </summary>
        public Dictionary<HexCoordinates, WorldTile> WorldMap { get; private set; }
            = new Dictionary<HexCoordinates, WorldTile>();

        // ---------------------------------------------------------
        // Lifecycle
        // ---------------------------------------------------------

        private void Start()
        {
            if (_spawnOnStart)
            {
                GenerateMap();
                SpawnVisuals();
            }
        }

        // ---------------------------------------------------------
        // Map Assembly (Pure Logic)
        // ---------------------------------------------------------

        /// <summary>
        /// Reads all PiecePlacements from the config, transforms local tile
        /// coordinates to global coordinates, and populates the WorldMap dictionary.
        /// </summary>
        [ContextMenu("Generate Map (Data Only)")]
        public void GenerateMap()
        {
            WorldMap.Clear();

            if (_assemblyConfig == null)
            {
                Debug.LogError("[MapGenerator] No MapAssemblyConfig assigned!");
                return;
            }

            foreach (var placement in _assemblyConfig.Placements)
            {
                if (placement.Piece == null)
                {
                    Debug.LogWarning("[MapGenerator] A placement has no MapPieceData assigned. Skipping.");
                    continue;
                }

                AssemblePiece(placement);
            }

            Debug.Log($"[MapGenerator] World map assembled: {WorldMap.Count} tiles from " +
                      $"{_assemblyConfig.PlacementCount} pieces.");
        }

        /// <summary>
        /// Processes a single piece placement:
        /// 1. Convert each tile's local offset to cube coordinates
        /// 2. Apply rotation (N * 60° CW)
        /// 3. Apply translation (offset)
        /// 4. Add to WorldMap dictionary
        /// </summary>
        private void AssemblePiece(PiecePlacement placement)
        {
            HexCoordinates offset = placement.GetOffset();

            foreach (var tileData in placement.Piece.Tiles)
            {
                // Step 1: Local offset -> cube coordinates
                HexCoordinates localHex = HexCoordinates.FromOffset(
                    tileData.LocalOffset.x,
                    tileData.LocalOffset.y);

                // Step 2: Rotate around local origin
                HexCoordinates rotatedHex = localHex.Rotate(placement.RotationSteps);

                // Step 3: Translate to global position
                HexCoordinates globalHex = rotatedHex + offset;

                // Step 4: Add to world map
                if (WorldMap.ContainsKey(globalHex))
                {
                    Debug.LogWarning(
                        $"[MapGenerator] Tile overlap at {globalHex}! " +
                        $"Piece '{placement.Piece.PieceName}' overwrites existing tile.");
                }

                WorldMap[globalHex] = new WorldTile(globalHex, tileData, placement.Piece.PieceId);
            }
        }

        // ---------------------------------------------------------
        // Visual Spawning
        // ---------------------------------------------------------

        /// <summary>
        /// Instantiates prefabs for all tiles in the WorldMap.
        /// Uses TileVisualConfig to resolve terrain/structure -> prefab mappings.
        /// </summary>
        [ContextMenu("Spawn Visuals")]
        public void SpawnVisuals()
        {
            ClearVisuals();

            if (WorldMap.Count == 0)
            {
                Debug.LogWarning("[MapGenerator] WorldMap is empty. Call GenerateMap() first.");
                return;
            }

            if (_visualConfig == null)
            {
                Debug.LogWarning("[MapGenerator] No TileVisualConfig assigned. Spawning debug cubes.");
                SpawnDebugCubes();
                return;
            }

            Transform container = GetOrCreateContainer();

            foreach (var kvp in WorldMap)
            {
                Vector3 worldPos = HexMetrics.HexToWorldPosition(kvp.Key);
                WorldTile tile = kvp.Value;

                // Spawn terrain base
                GameObject terrainPrefab = _visualConfig.GetTerrainPrefab(tile.Terrain);
                if (terrainPrefab != null)
                {
                    GameObject terrainObj = Instantiate(terrainPrefab, worldPos, Quaternion.identity, container);
                    terrainObj.name = $"Tile_{kvp.Key}_{tile.Terrain}";
                }

                // Spawn structure on top
                GameObject structurePrefab = _visualConfig.GetStructurePrefab(tile.Structure);
                if (structurePrefab != null)
                {
                    GameObject structureObj = Instantiate(structurePrefab, worldPos, Quaternion.identity, container);
                    structureObj.name = $"Structure_{kvp.Key}_{tile.Structure}";
                }
            }

            Debug.Log($"[MapGenerator] Spawned visuals for {WorldMap.Count} tiles.");
        }

        /// <summary>
        /// Spawns colored debug cubes when no TileVisualConfig is available.
        /// Each cube is colored by terrain type for quick visual verification.
        /// </summary>
        private void SpawnDebugCubes()
        {
            Transform container = GetOrCreateContainer();

            foreach (var kvp in WorldMap)
            {
                Vector3 worldPos = HexMetrics.HexToWorldPosition(kvp.Key);
                WorldTile tile = kvp.Value;

                // Create a primitive cube as placeholder
                GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cube.transform.position = worldPos;
                cube.transform.localScale = new Vector3(
                    HexMetrics.InnerRadius * 1.6f,
                    0.1f,
                    HexMetrics.OuterRadius * 0.9f);
                cube.transform.SetParent(container);
                cube.name = $"DebugTile_{kvp.Key}_{tile.Terrain}";

                // Color by terrain
                var renderer = cube.GetComponent<Renderer>();
                if (renderer != null)
                {
                    var mat = new Material(renderer.sharedMaterial);
                    mat.color = TileVisualConfig.GetTerrainDebugColor(tile.Terrain);
                    renderer.material = mat;
                }

                // Add small sphere for structures
                if (tile.Structure != StructureType.None)
                {
                    GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    marker.transform.position = worldPos + Vector3.up * 0.2f;
                    marker.transform.localScale = Vector3.one * 0.3f;
                    marker.transform.SetParent(cube.transform);
                    marker.name = $"Structure_{tile.Structure}";

                    var markerRenderer = marker.GetComponent<Renderer>();
                    if (markerRenderer != null)
                    {
                        var markerMat = new Material(markerRenderer.sharedMaterial);
                        markerMat.color = Color.red;
                        markerRenderer.material = markerMat;
                    }
                }
            }

            Debug.Log($"[MapGenerator] Spawned {WorldMap.Count} debug cubes (no TileVisualConfig).");
        }

        /// <summary>
        /// Destroys all spawned tile visuals.
        /// </summary>
        [ContextMenu("Clear Visuals")]
        public void ClearVisuals()
        {
            Transform container = _tileContainer != null ? _tileContainer : transform;

            for (int i = container.childCount - 1; i >= 0; i--)
            {
                if (Application.isPlaying)
                    Destroy(container.GetChild(i).gameObject);
                else
                    DestroyImmediate(container.GetChild(i).gameObject);
            }
        }

        private Transform GetOrCreateContainer()
        {
            if (_tileContainer != null)
                return _tileContainer;

            var containerObj = new GameObject("[Generated] Tile Container");
            containerObj.transform.SetParent(transform);
            _tileContainer = containerObj.transform;
            return _tileContainer;
        }

        // ---------------------------------------------------------
        // Gizmos (Scene View - always visible)
        // ---------------------------------------------------------

        private void OnDrawGizmos()
        {
            // Only draw in editor when we have runtime data
            if (WorldMap == null || WorldMap.Count == 0) return;

            foreach (var kvp in WorldMap)
            {
                Vector3 worldPos = HexMetrics.HexToWorldPosition(kvp.Key);
                Gizmos.color = TileVisualConfig.GetTerrainDebugColor(kvp.Value.Terrain);
                DrawHexOutline(worldPos);
            }
        }

        private void DrawHexOutline(Vector3 center)
        {
            Vector3[] corners = HexMetrics.GetHexCorners(center);
            for (int i = 0; i < 6; i++)
            {
                Gizmos.DrawLine(corners[i], corners[(i + 1) % 6]);
            }
        }
    }
}
