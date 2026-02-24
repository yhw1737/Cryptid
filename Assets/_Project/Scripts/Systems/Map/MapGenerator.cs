using System.Collections.Generic;
using Cryptid.Core;
using Cryptid.Data;
using Cryptid.Network;
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

        /// <summary>
        /// Map of coordinates to spawned HexTile components.
        /// Populated during SpawnVisuals().
        /// </summary>
        private readonly Dictionary<HexCoordinates, HexTile> _hexTileMap = new();

        /// <summary>Returns the HexTile component for the given coordinates, or null.</summary>
        public HexTile GetHexTile(HexCoordinates coords) =>
            _hexTileMap.TryGetValue(coords, out var tile) ? tile : null;

        /// <summary>
        /// Sets the random seed for procedural map generation.
        /// Call before GenerateMap() to produce deterministic maps.
        /// </summary>
        public void SetSeed(int seed) => _mapSeed = seed;

        /// <summary> The seed used for map generation. </summary>
        public int MapSeed => _mapSeed;

        // ---------------------------------------------------------
        // Lifecycle
        // ---------------------------------------------------------

        private void Start()
        {
            // If ConnectionManager exists, defer map generation until mode is selected.
            bool hasConnectionManager =
                FindFirstObjectByType<Cryptid.Network.ConnectionManager>() != null;

            if (_spawnOnStart && !hasConnectionManager)
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

            // Always use enriched hex visuals (terrain heights, colors, bumps, decorations)
            SpawnEnrichedTiles();
        }

        /// <summary>
        /// Spawns enriched hex tile visuals with:
        ///   1. Hex prism base (terrain-colored, with per-terrain height and color noise)
        ///   2. Terrain decorations from MapDecorationDatabase (trees, rocks, etc.)
        ///   3. Procedural terrain bumps (small rocks, mounds) for natural feel
        ///   4. Structure models (StandingStone from asset, AbandonedShack placeholder)
        ///   5. Animal territory markers (colored capsules with labels)
        /// Water tiles are slightly lowered for visual depth.
        /// </summary>
        private void SpawnEnrichedTiles()
        {
            MapDecorationDatabase.Load();

            Transform container = GetOrCreateContainer();
            var rng = new System.Random(_mapSeed >= 0 ? _mapSeed + 999 : System.Environment.TickCount);

            // Pre-generate hex meshes per terrain height
            Mesh waterMesh   = HexMeshGenerator.CreateHexPrismMesh(0.04f);
            Mesh desertMesh  = HexMeshGenerator.CreateHexPrismMesh(0.08f);
            Mesh forestMesh  = HexMeshGenerator.CreateHexPrismMesh(0.12f);
            Mesh swampMesh   = HexMeshGenerator.CreateHexPrismMesh(0.06f);
            Mesh mountainMesh = HexMeshGenerator.CreateHexPrismMesh(0.22f);

            // Shared URP material template
            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");

            int decoCount = 0;

            foreach (var kvp in WorldMap)
            {
                Vector3 worldPos = HexMetrics.HexToWorldPosition(kvp.Key);
                WorldTile tile = kvp.Value;

                bool isWater = tile.Terrain == TerrainType.Water;

                // Per-tile color noise for natural variation
                float noise = Mathf.PerlinNoise(
                    worldPos.x * 0.3f + 1000f,
                    worldPos.z * 0.3f + 1000f);
                float colorVariation = 0.85f + noise * 0.3f; // 0.85 – 1.15

                // ── 1. Hex base with terrain-specific height ─────
                Mesh tileMesh = tile.Terrain switch
                {
                    TerrainType.Water    => waterMesh,
                    TerrainType.Desert   => desertMesh,
                    TerrainType.Forest   => forestMesh,
                    TerrainType.Swamp    => swampMesh,
                    TerrainType.Mountain => mountainMesh,
                    _                    => desertMesh
                };

                // Vertical offset: water sinks, mountains rise
                float yOffset = tile.Terrain switch
                {
                    TerrainType.Water    => -0.06f,
                    TerrainType.Swamp    => -0.02f,
                    TerrainType.Mountain =>  0.08f,
                    _                    =>  0f
                };

                var hexObj = new GameObject($"HexTile_{kvp.Key}_{tile.Terrain}");
                hexObj.transform.position = worldPos + Vector3.up * yOffset;
                hexObj.transform.SetParent(container);

                var meshFilter = hexObj.AddComponent<MeshFilter>();
                meshFilter.sharedMesh = tileMesh;

                var meshRenderer = hexObj.AddComponent<MeshRenderer>();
                Color baseColor = GetNaturalTerrainColor(tile.Terrain);
                var mat = new Material(urpLit);
                mat.color = baseColor * colorVariation;

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
                meshCollider.sharedMesh = tileMesh;

                // HexTile component for interaction
                var hexTile = hexObj.AddComponent<HexTile>();
                hexTile.Initialize(tile);
                _hexTileMap[tile.Coordinates] = hexTile;

                // ── 2. Procedural terrain bumps ──────────────────
                int bumpCount = GetTerrainBumpCount(tile.Terrain, rng);
                float baseY = yOffset + GetTerrainPrismHeight(tile.Terrain);

                for (int i = 0; i < bumpCount; i++)
                {
                    SpawnTerrainBump(tile.Terrain, worldPos, baseY,
                                     hexObj.transform, urpLit, rng, colorVariation);
                    decoCount++;
                }

                // ── 3. Terrain decorations from database ─────────
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
                            Vector3 decoPos = worldPos + new Vector3(offset.x, baseY, offset.y);
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

                // ── 4. Structure ──────────────────────────────────
                if (tile.Structure != StructureType.None)
                {
                    SpawnStructure(tile.Structure,
                                   worldPos + Vector3.up * baseY, hexObj.transform, urpLit);
                }

                // ── 5. Animal territory marker ───────────────────
                if (tile.Animal != AnimalType.None)
                {
                    SpawnAnimalMarker(tile.Animal,
                                      worldPos + Vector3.up * baseY, hexObj.transform, urpLit);
                }
            }

            Debug.Log($"[MapGenerator] Spawned {WorldMap.Count} hex tiles " +
                      $"with {decoCount} decorations (enriched mode).");
        }

        /// <summary>
        /// Returns a more natural / realistic base color for each terrain type.
        /// Uses earth-tone palettes inspired by low-poly nature packs.
        /// </summary>
        private static Color GetNaturalTerrainColor(TerrainType terrain)
        {
            return terrain switch
            {
                // Warm sandy beige with slight orange undertone
                TerrainType.Desert   => new Color(0.82f, 0.72f, 0.50f),
                // Rich green with earthy depth
                TerrainType.Forest   => new Color(0.22f, 0.50f, 0.18f),
                // Deep blue-teal
                TerrainType.Water    => new Color(0.18f, 0.42f, 0.72f),
                // Murky olive-brown
                TerrainType.Swamp    => new Color(0.30f, 0.38f, 0.18f),
                // Rocky gray-brown
                TerrainType.Mountain => new Color(0.50f, 0.48f, 0.44f),
                _                    => Color.magenta
            };
        }

        /// <summary>Returns the hex prism height for each terrain type.</summary>
        private static float GetTerrainPrismHeight(TerrainType terrain)
        {
            return terrain switch
            {
                TerrainType.Water    => 0.04f,
                TerrainType.Desert   => 0.08f,
                TerrainType.Forest   => 0.12f,
                TerrainType.Swamp    => 0.06f,
                TerrainType.Mountain => 0.22f,
                _                    => 0.08f
            };
        }

        /// <summary>
        /// Returns how many procedural terrain bumps to spawn on this tile.
        /// Mountains get the most, water/swamp get none.
        /// </summary>
        private static int GetTerrainBumpCount(TerrainType terrain, System.Random rng)
        {
            return terrain switch
            {
                TerrainType.Mountain => 2 + rng.Next(3),  // 2-4 rock bumps
                TerrainType.Desert   => rng.Next(2),       // 0-1 small rocks
                TerrainType.Forest   => rng.Next(2),       // 0-1 ground mounds
                TerrainType.Swamp    => 0,
                TerrainType.Water    => 0,
                _                    => 0
            };
        }

        /// <summary>
        /// Spawns a small procedural terrain bump (rock or mound) on a hex tile.
        /// Uses primitive shapes with terrain-appropriate colors.
        /// </summary>
        private void SpawnTerrainBump(TerrainType terrain, Vector3 tileWorldPos,
            float baseY, Transform parent, Shader urpLit, System.Random rng,
            float colorVariation)
        {
            Vector2 offset = RandomPointInHex(rng, HexMetrics.InnerRadius * 0.55f);
            float yRot = (float)(rng.NextDouble() * 360.0);

            if (terrain == TerrainType.Mountain)
            {
                // Rocky mound: squashed sphere or scaled cube
                bool useRock = rng.NextDouble() > 0.3;
                var bump = GameObject.CreatePrimitive(
                    useRock ? PrimitiveType.Sphere : PrimitiveType.Cube);
                bump.transform.SetParent(parent);
                bump.transform.position = tileWorldPos +
                    new Vector3(offset.x, baseY, offset.y);

                float sx = 0.08f + (float)(rng.NextDouble() * 0.12f);
                float sy = 0.05f + (float)(rng.NextDouble() * 0.10f);
                float sz = 0.08f + (float)(rng.NextDouble() * 0.12f);
                bump.transform.localScale = new Vector3(sx, sy, sz);
                bump.transform.rotation = Quaternion.Euler(
                    (float)(rng.NextDouble() * 15f),
                    yRot,
                    (float)(rng.NextDouble() * 15f));
                bump.name = "Bump_Rock";

                Destroy(bump.GetComponent<Collider>());

                var r = bump.GetComponent<Renderer>();
                // Vary between gray and brown for rocky appearance
                float grayBrown = (float)rng.NextDouble();
                Color rockColor = Color.Lerp(
                    new Color(0.45f, 0.43f, 0.40f),  // Gray rock
                    new Color(0.50f, 0.40f, 0.30f),  // Brown rock
                    grayBrown) * colorVariation;
                r.material = new Material(urpLit) { color = rockColor };
            }
            else if (terrain == TerrainType.Desert)
            {
                // Small sandy pebble
                var pebble = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                pebble.transform.SetParent(parent);
                pebble.transform.position = tileWorldPos +
                    new Vector3(offset.x, baseY - 0.01f, offset.y);

                float s = 0.04f + (float)(rng.NextDouble() * 0.05f);
                pebble.transform.localScale = new Vector3(s, s * 0.5f, s);
                pebble.name = "Bump_Pebble";

                Destroy(pebble.GetComponent<Collider>());

                var r = pebble.GetComponent<Renderer>();
                r.material = new Material(urpLit)
                {
                    color = new Color(0.75f, 0.65f, 0.45f) * colorVariation
                };
            }
            else if (terrain == TerrainType.Forest)
            {
                // Small earthy mound
                var mound = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                mound.transform.SetParent(parent);
                mound.transform.position = tileWorldPos +
                    new Vector3(offset.x, baseY - 0.02f, offset.y);

                float s = 0.06f + (float)(rng.NextDouble() * 0.06f);
                mound.transform.localScale = new Vector3(s, s * 0.3f, s);
                mound.name = "Bump_Mound";

                Destroy(mound.GetComponent<Collider>());

                var r = mound.GetComponent<Renderer>();
                r.material = new Material(urpLit)
                {
                    color = new Color(0.28f, 0.22f, 0.12f) * colorVariation
                };
            }
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
        /// Spawns an animal model from prefab at the tile edge.
        /// Uses Deer_001 and Tiger_001 prefabs from ithappy Animals_FREE pack.
        /// Falls back to colored capsule if prefab not found.
        /// </summary>
        private void SpawnAnimalMarker(AnimalType animal, Vector3 worldPos,
                                       Transform parent, Shader urpLit)
        {
            // Position at tile edge so it doesn't overlap structure
            Vector3 markerPos = worldPos + new Vector3(0.3f, 0.0f, 0.3f);

            string prefabName = animal switch
            {
                AnimalType.Tiger => "Tiger_001",
                AnimalType.Deer  => "Deer_001",
                _                => null
            };

            // Try loading the prefab from the ithappy Animals_FREE pack
            GameObject prefab = null;
            if (prefabName != null)
            {
                // Try Resources.Load first, then fall back to known path
                prefab = Resources.Load<GameObject>($"Animals/{prefabName}");

                #if UNITY_EDITOR
                if (prefab == null)
                {
                    prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                        $"Assets/ithappy/Animals_FREE/Prefabs/{prefabName}.prefab");
                }
                #endif
            }

            if (prefab != null)
            {
                var marker = Instantiate(prefab, markerPos, Quaternion.identity, parent);
                marker.transform.localScale = Vector3.one * 0.25f;
                marker.name = $"Animal_{animal}";

                // Remove colliders so hover works on hex base
                foreach (var col in marker.GetComponentsInChildren<Collider>())
                    Destroy(col);

                // Fix materials for URP
                EnsureURPMaterials(marker, urpLit);
            }
            else
            {
                // Fallback: colored capsule
                var marker = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                marker.transform.position = markerPos + Vector3.up * 0.2f;
                marker.transform.localScale = new Vector3(0.12f, 0.15f, 0.12f);
                marker.transform.SetParent(parent);
                marker.name = $"Animal_{animal}";

                Destroy(marker.GetComponent<Collider>());

                Color animalColor = animal switch
                {
                    AnimalType.Tiger => new Color(0.95f, 0.55f, 0.10f),
                    AnimalType.Deer  => new Color(0.72f, 0.53f, 0.30f),
                    _                => Color.magenta
                };

                var r = marker.GetComponent<Renderer>();
                var m = new Material(urpLit) { color = animalColor };
                r.material = m;
            }
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
                    bool hasExplicitColor = false;

                    // Try all common color property names
                    if (mat.HasProperty("_BaseColor"))
                    {
                        originalColor = mat.GetColor("_BaseColor");
                        hasExplicitColor = true;
                    }
                    else if (mat.HasProperty("_Color"))
                    {
                        originalColor = mat.color;
                        hasExplicitColor = true;
                    }

                    // Try all common texture property names
                    if (mat.HasProperty("_BaseMap"))
                        mainTex = mat.GetTexture("_BaseMap");
                    else if (mat.HasProperty("_MainTex"))
                        mainTex = mat.mainTexture;

                    // If color is pure white and we have a texture, keep white
                    // (texture * white = texture). If no texture and no explicit
                    // color, use a neutral gray to avoid invisible white objects.
                    if (!hasExplicitColor && mainTex == null)
                        originalColor = new Color(0.6f, 0.6f, 0.6f);

                    // Create new URP material
                    var newMat = new Material(urpLit);
                    newMat.SetColor("_BaseColor", originalColor);
                    if (mainTex != null)
                        newMat.SetTexture("_BaseMap", mainTex);

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
            _hexTileMap.Clear();
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
