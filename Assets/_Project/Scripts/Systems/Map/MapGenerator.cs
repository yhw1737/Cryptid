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
        /// Spawns enriched hex tile visuals with:
        ///   1. Hex prism base (terrain-colored)
        ///   2. Terrain decorations from MapDecorationDatabase (trees, rocks, etc.)
        ///   3. Structure models (StandingStone from asset, AbandonedShack placeholder)
        ///   4. Animal territory markers (colored capsules with labels)
        /// Water tiles are slightly lowered for visual depth.
        /// </summary>
        private void SpawnDebugCubes()
        {
            MapDecorationDatabase.Load();

            Transform container = GetOrCreateContainer();
            var rng = new System.Random(_mapSeed >= 0 ? _mapSeed + 999 : System.Environment.TickCount);

            // Shared hex meshes (water is thinner + lowered)
            Mesh hexMesh = HexMeshGenerator.CreateHexPrismMesh(0.1f);
            Mesh waterMesh = HexMeshGenerator.CreateHexPrismMesh(0.04f);

            // Shared URP material template
            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");

            int decoCount = 0;

            foreach (var kvp in WorldMap)
            {
                Vector3 worldPos = HexMetrics.HexToWorldPosition(kvp.Key);
                WorldTile tile = kvp.Value;

                bool isWater = tile.Terrain == TerrainType.Water;

                // ── 1. Hex base ──────────────────────────────────
                var hexObj = new GameObject($"HexTile_{kvp.Key}_{tile.Terrain}");
                hexObj.transform.position = isWater
                    ? worldPos + Vector3.down * 0.05f
                    : worldPos;
                hexObj.transform.SetParent(container);

                var meshFilter = hexObj.AddComponent<MeshFilter>();
                meshFilter.sharedMesh = isWater ? waterMesh : hexMesh;

                var meshRenderer = hexObj.AddComponent<MeshRenderer>();
                var mat = new Material(urpLit);
                mat.color = TileVisualConfig.GetTerrainDebugColor(tile.Terrain);

                // Water gets semi-transparency
                if (isWater)
                {
                    mat.SetFloat("_Surface", 1); // Transparent
                    mat.SetFloat("_Blend", 0);
                    mat.SetOverrideTag("RenderType", "Transparent");
                    mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    mat.SetInt("_ZWrite", 0);
                    mat.DisableKeyword("_ALPHATEST_ON");
                    mat.EnableKeyword("_ALPHABLEND_ON");
                    mat.renderQueue = 3000;
                    var c = mat.color;
                    c.a = 0.75f;
                    mat.color = c;
                }

                meshRenderer.material = mat;

                // Collider for raycasting
                var meshCollider = hexObj.AddComponent<MeshCollider>();
                meshCollider.sharedMesh = isWater ? waterMesh : hexMesh;

                // HexTile component for interaction
                var hexTile = hexObj.AddComponent<HexTile>();
                hexTile.Initialize(tile);

                // ── 2. Terrain decorations ───────────────────────
                if (!isWater)
                {
                    int decoAmount = GetDecorationCount(tile.Terrain, rng);
                    for (int i = 0; i < decoAmount; i++)
                    {
                        GameObject decoPrefab = MapDecorationDatabase.GetRandomDecoration(tile.Terrain, rng);
                        if (decoPrefab != null)
                        {
                            // Random position within hex (inner radius)
                            Vector2 offset = RandomPointInHex(rng, HexMetrics.InnerRadius * 0.6f);
                            Vector3 decoPos = worldPos + new Vector3(offset.x, 0.1f, offset.y);
                            float yRot = (float)(rng.NextDouble() * 360.0);
                            float scale = 0.15f + (float)(rng.NextDouble() * 0.1);

                            GameObject deco = Instantiate(decoPrefab, decoPos,
                                Quaternion.Euler(0f, yRot, 0f), hexObj.transform);
                            deco.transform.localScale = Vector3.one * scale;
                            deco.name = $"Deco_{decoPrefab.name}";

                            // Remove colliders on decoration so hover works on hex base
                            foreach (var col in deco.GetComponentsInChildren<Collider>())
                                Destroy(col);

                            // Fix materials: asset packs use Built-in shader → URP pink
                            EnsureURPMaterials(deco, urpLit);

                            decoCount++;
                        }
                    }
                }

                // ── 3. Structure ──────────────────────────────────
                if (tile.Structure != StructureType.None)
                {
                    SpawnStructure(tile.Structure, worldPos, hexObj.transform, urpLit);
                }

                // ── 4. Animal territory marker ───────────────────
                if (tile.Animal != AnimalType.None)
                {
                    SpawnAnimalMarker(tile.Animal, worldPos, hexObj.transform, urpLit);
                }
            }

            Debug.Log($"[MapGenerator] Spawned {WorldMap.Count} hex tiles " +
                      $"with {decoCount} decorations (enriched mode).");
        }

        // ---------------------------------------------------------
        // Decoration Helpers
        // ---------------------------------------------------------

        /// <summary>
        /// Returns how many decoration objects to spawn per terrain type.
        /// Forest gets the most, desert the least.
        /// </summary>
        private int GetDecorationCount(TerrainType terrain, System.Random rng)
        {
            return terrain switch
            {
                TerrainType.Forest   => 1 + rng.Next(3),   // 1-3
                TerrainType.Swamp    => 1 + rng.Next(2),   // 1-2
                TerrainType.Mountain => 1 + rng.Next(2),   // 1-2
                TerrainType.Desert   => rng.Next(2),        // 0-1
                _                    => 0
            };
        }

        /// <summary>
        /// Returns a random 2D point within a hexagon of given radius.
        /// Uses rejection sampling on the hex boundary.
        /// </summary>
        private Vector2 RandomPointInHex(System.Random rng, float radius)
        {
            // Simple approach: random in circle, good enough for hex
            float angle = (float)(rng.NextDouble() * Mathf.PI * 2f);
            float r = radius * Mathf.Sqrt((float)rng.NextDouble());
            return new Vector2(r * Mathf.Cos(angle), r * Mathf.Sin(angle));
        }

        /// <summary>
        /// Spawns a 3D structure model on a tile.
        /// StandingStone uses asset from MapDecorationDatabase.
        /// AbandonedShack uses a procedural box+roof placeholder.
        /// </summary>
        private void SpawnStructure(StructureType type, Vector3 worldPos,
                                    Transform parent, Shader urpLit)
        {
            if (type == StructureType.StandingStone)
            {
                SpawnStandingStone(worldPos, parent, urpLit);
            }
            else if (type == StructureType.AbandonedShack)
            {
                SpawnAbandonedShack(worldPos, parent, urpLit);
            }
        }

        private void SpawnStandingStone(Vector3 worldPos, Transform parent, Shader urpLit)
        {
            GameObject prefab = MapDecorationDatabase.GetStandingStonePrefab();
            if (prefab != null)
            {
                Vector3 pos = worldPos + Vector3.up * 0.1f;
                GameObject stone = Instantiate(prefab, pos, Quaternion.identity, parent);
                stone.transform.localScale = Vector3.one * 0.3f;
                stone.name = "Structure_StandingStone";

                // Remove colliders so hover is not blocked
                foreach (var col in stone.GetComponentsInChildren<Collider>())
                    Destroy(col);

                EnsureURPMaterials(stone, urpLit);
            }
            else
            {
                // Fallback: tall thin cylinder
                var pillar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                pillar.transform.position = worldPos + Vector3.up * 0.35f;
                pillar.transform.localScale = new Vector3(0.12f, 0.35f, 0.12f);
                pillar.transform.SetParent(parent);
                pillar.name = "Structure_StandingStone_Fallback";

                Destroy(pillar.GetComponent<Collider>());

                var r = pillar.GetComponent<Renderer>();
                var m = new Material(urpLit) { color = new Color(0.5f, 0.52f, 0.48f) };
                r.material = m;
            }
        }

        private void SpawnAbandonedShack(Vector3 worldPos, Transform parent, Shader urpLit)
        {
            var shackRoot = new GameObject("Structure_AbandonedShack");
            shackRoot.transform.position = worldPos;
            shackRoot.transform.SetParent(parent);

            Material woodMat = new Material(urpLit)
            {
                color = new Color(0.45f, 0.30f, 0.18f) // Dark brown wood
            };
            Material roofMat = new Material(urpLit)
            {
                color = new Color(0.35f, 0.22f, 0.12f) // Darker brown roof
            };

            // Walls (box)
            var walls = GameObject.CreatePrimitive(PrimitiveType.Cube);
            walls.transform.SetParent(shackRoot.transform);
            walls.transform.localPosition = Vector3.up * 0.25f;
            walls.transform.localScale = new Vector3(0.35f, 0.25f, 0.3f);
            walls.name = "Walls";
            Destroy(walls.GetComponent<Collider>());
            walls.GetComponent<Renderer>().material = woodMat;

            // Roof (rotated cube as triangle-ish roof)
            var roof = GameObject.CreatePrimitive(PrimitiveType.Cube);
            roof.transform.SetParent(shackRoot.transform);
            roof.transform.localPosition = Vector3.up * 0.45f;
            roof.transform.localScale = new Vector3(0.4f, 0.15f, 0.35f);
            roof.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            roof.name = "Roof";
            Destroy(roof.GetComponent<Collider>());
            roof.GetComponent<Renderer>().material = roofMat;
        }

        /// <summary>
        /// Spawns a colored capsule as an animal territory marker.
        /// Tiger = orange-striped, Wolf = dark gray.
        /// </summary>
        private void SpawnAnimalMarker(AnimalType animal, Vector3 worldPos,
                                       Transform parent, Shader urpLit)
        {
            // Position at tile edge so it doesn't overlap structure
            Vector3 markerPos = worldPos + new Vector3(0.3f, 0.2f, 0.3f);

            var marker = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            marker.transform.position = markerPos;
            marker.transform.localScale = new Vector3(0.12f, 0.15f, 0.12f);
            marker.transform.SetParent(parent);
            marker.name = $"Animal_{animal}";

            // Remove collider so hover is not blocked
            Destroy(marker.GetComponent<Collider>());

            Color animalColor = animal switch
            {
                AnimalType.Tiger => new Color(0.95f, 0.55f, 0.10f), // Orange
                AnimalType.Wolf  => new Color(0.45f, 0.45f, 0.50f), // Dark gray
                _                => Color.magenta
            };

            var r = marker.GetComponent<Renderer>();
            var m = new Material(urpLit) { color = animalColor };
            r.material = m;
        }

        /// <summary>
        /// Converts all materials on a GameObject hierarchy from Built-in Standard
        /// to URP/Lit, preserving the original base color and texture.
        /// Fixes the purple/magenta rendering caused by shader incompatibility.
        /// </summary>
        private void EnsureURPMaterials(GameObject obj, Shader urpLit)
        {
            foreach (var renderer in obj.GetComponentsInChildren<Renderer>())
            {
                var materials = renderer.materials;
                bool changed = false;

                for (int i = 0; i < materials.Length; i++)
                {
                    var mat = materials[i];
                    if (mat == null) continue;

                    // Skip if already using URP shader
                    if (mat.shader != null && mat.shader.name.Contains("Universal Render Pipeline"))
                        continue;

                    // Extract original color and texture before replacing shader
                    Color originalColor = Color.white;
                    Texture mainTex = null;

                    if (mat.HasProperty("_Color"))
                        originalColor = mat.color;
                    else if (mat.HasProperty("_BaseColor"))
                        originalColor = mat.GetColor("_BaseColor");

                    if (mat.HasProperty("_MainTex"))
                        mainTex = mat.mainTexture;
                    else if (mat.HasProperty("_BaseMap"))
                        mainTex = mat.GetTexture("_BaseMap");

                    // Create new URP material
                    var newMat = new Material(urpLit);
                    newMat.color = originalColor;
                    if (mainTex != null)
                        newMat.mainTexture = mainTex;

                    materials[i] = newMat;
                    changed = true;
                }

                if (changed)
                    renderer.materials = materials;
            }
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
