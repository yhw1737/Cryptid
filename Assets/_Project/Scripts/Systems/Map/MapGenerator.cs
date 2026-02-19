using System.Collections.Generic;
using Cryptid.Core;
using Cryptid.Data;
using UnityEngine;

namespace Cryptid.Systems.Map
{
    /// <summary>
    /// Assembles the world map from either:
    ///   A) MapPieceData (legacy) via MapAssemblyConfig
    ///   B) Procedural generation via ProceduralMapConfig (Spec 5.1)
    /// Also handles spawning visual prefabs via TileVisualConfig.
    /// </summary>
    public class MapGenerator : MonoBehaviour
    {
        [Header("Generation Mode")]
        [Tooltip("true = procedural (Spec 5.1), false = legacy MapPiece assembly")]
        [SerializeField] private bool _useProcedural = true;

        [Header("Procedural Config")]
        [SerializeField] private ProceduralMapConfig _proceduralConfig;

        [Tooltip("Random seed for procedural generation (-1 = random)")]
        [SerializeField] private int _mapSeed = -1;

        [Header("Legacy (MapPiece) Config")]
        [SerializeField] private MapAssemblyConfig _assemblyConfig;

        [Header("Visuals")]
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
        /// If _useProcedural is true, delegates to ProceduralMapBuilder instead.
        /// </summary>
        [ContextMenu("Generate Map (Data Only)")]
        public void GenerateMap()
        {
            WorldMap.Clear();

            if (_useProcedural)
            {
                GenerateProceduralMap();
                return;
            }

            GenerateLegacyMap();
        }

        /// <summary>
        /// Procedural map generation using Perlin Noise (Spec 5.1.B).
        /// </summary>
        private void GenerateProceduralMap()
        {
            if (_proceduralConfig == null)
            {
                Debug.LogError("[MapGenerator] No ProceduralMapConfig assigned! " +
                              "Create one via Assets > Create > Cryptid > Procedural Map Config.");
                return;
            }

            var result = ProceduralMapBuilder.Build(_proceduralConfig, _mapSeed);
            if (result == null)
            {
                Debug.LogError("[MapGenerator] Procedural map generation failed!");
                return;
            }

            foreach (var kvp in result)
                WorldMap[kvp.Key] = kvp.Value;

            LogTerrainStats();
        }

        /// <summary>
        /// Logs terrain/structure/animal distribution for debug verification.
        /// </summary>
        private void LogTerrainStats()
        {
            var terrainCounts = new Dictionary<TerrainType, int>();
            int structures = 0;
            int animals = 0;

            foreach (var tile in WorldMap.Values)
            {
                if (!terrainCounts.ContainsKey(tile.Terrain))
                    terrainCounts[tile.Terrain] = 0;
                terrainCounts[tile.Terrain]++;

                if (tile.Structure != StructureType.None) structures++;
                if (tile.Animal != AnimalType.None) animals++;
            }

            string stats = $"[MapGenerator] Procedural map: {WorldMap.Count} tiles | ";
            foreach (var kvp in terrainCounts)
                stats += $"{kvp.Key}:{kvp.Value} ";
            stats += $"| Structures:{structures} Animals:{animals}";
            Debug.Log(stats);
        }

        /// <summary>
        /// Legacy map assembly from MapPieceData ScriptableObjects.
        /// </summary>
        private void GenerateLegacyMap()
        {
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
        /// Spawns colored hex tiles when no TileVisualConfig is available.
        /// Uses procedural hex mesh with terrain-based coloring.
        /// Structures are shown as small spheres on top.
        /// </summary>
        private void SpawnDebugCubes()
        {
            Transform container = GetOrCreateContainer();

            // Generate hex mesh once and reuse for all tiles
            Mesh hexMesh = HexMeshGenerator.CreateHexPrismMesh(0.1f);

            foreach (var kvp in WorldMap)
            {
                Vector3 worldPos = HexMetrics.HexToWorldPosition(kvp.Key);
                WorldTile tile = kvp.Value;

                // Create hex tile object with mesh
                var hexObj = new GameObject($"HexTile_{kvp.Key}_{tile.Terrain}");
                hexObj.transform.position = worldPos;
                hexObj.transform.SetParent(container);

                var meshFilter = hexObj.AddComponent<MeshFilter>();
                meshFilter.sharedMesh = hexMesh;

                var meshRenderer = hexObj.AddComponent<MeshRenderer>();
                var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                mat.color = TileVisualConfig.GetTerrainDebugColor(tile.Terrain);
                meshRenderer.material = mat;

                // Add collider for future raycasting
                var meshCollider = hexObj.AddComponent<MeshCollider>();
                meshCollider.sharedMesh = hexMesh;

                // Add HexTile component for interaction
                var hexTile = hexObj.AddComponent<HexTile>();
                hexTile.Initialize(tile);

                // Add small sphere for structures
                if (tile.Structure != StructureType.None)
                {
                    GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    marker.transform.position = worldPos + Vector3.up * 0.25f;
                    marker.transform.localScale = Vector3.one * 0.3f;
                    marker.transform.SetParent(hexObj.transform);
                    marker.name = $"Structure_{tile.Structure}";

                    var markerRenderer = marker.GetComponent<Renderer>();
                    if (markerRenderer != null)
                    {
                        var markerMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                        markerMat.color = Color.red;
                        markerRenderer.material = markerMat;
                    }
                }
            }

            Debug.Log($"[MapGenerator] Spawned {WorldMap.Count} hex tiles (debug mode).");
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
