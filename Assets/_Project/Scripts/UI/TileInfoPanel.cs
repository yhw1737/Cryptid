using Cryptid.Data;
using Cryptid.Systems.Map;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Cryptid.UI
{
    /// <summary>
    /// Displays detailed info for the currently hovered/selected hex tile.
    /// Shows terrain type, structure, animal territory, and coordinates.
    /// Positioned at bottom-left of the screen, above the Clue panel.
    ///
    /// Listens to TileInteractionSystem.OnTileHovered for updates.
    /// </summary>
    public class TileInfoPanel : MonoBehaviour
    {
        // ---------------------------------------------------------
        // UI References (created in Build)
        // ---------------------------------------------------------

        private RectTransform _root;
        private TextMeshProUGUI _titleText;
        private TextMeshProUGUI _detailText;

        // ---------------------------------------------------------
        // Build
        // ---------------------------------------------------------

        /// <summary>
        /// Constructs the panel UI. Called by GameUIManager.
        /// </summary>
        public void Build(RectTransform root)
        {
            _root = root;

            // Position: bottom-left, above clue panel
            _root.anchorMin = new Vector2(0f, 0.12f);
            _root.anchorMax = new Vector2(0.22f, 0.30f);
            _root.offsetMin = new Vector2(10f, 0f);
            _root.offsetMax = new Vector2(0f, 0f);

            // Title row
            _titleText = UIFactory.CreateTMP(
                _root, "TileInfoTitle", "Tile Info",
                fontSize: 20,
                align: TextAlignmentOptions.TopLeft,
                color: UIFactory.Accent);
            var titleRT = _titleText.GetComponent<RectTransform>();
            titleRT.anchorMin = new Vector2(0f, 0.65f);
            titleRT.anchorMax = new Vector2(1f, 1f);
            titleRT.offsetMin = new Vector2(10f, 0f);
            titleRT.offsetMax = new Vector2(-10f, -5f);

            // Detail text
            _detailText = UIFactory.CreateTMP(
                _root, "TileInfoDetail", "Hover over a tile...",
                fontSize: 16,
                align: TextAlignmentOptions.TopLeft,
                color: Color.white);
            var detailRT = _detailText.GetComponent<RectTransform>();
            detailRT.anchorMin = new Vector2(0f, 0f);
            detailRT.anchorMax = new Vector2(1f, 0.65f);
            detailRT.offsetMin = new Vector2(10f, 5f);
            detailRT.offsetMax = new Vector2(-10f, 0f);

            _detailText.textWrappingMode = TextWrappingModes.Normal;

            gameObject.SetActive(false);
        }

        // ---------------------------------------------------------
        // Public API
        // ---------------------------------------------------------

        /// <summary>
        /// Updates the panel to show info for the given tile.
        /// Pass null to show "no tile" state.
        /// </summary>
        public void ShowTileInfo(HexTile tile)
        {
            if (tile == null)
            {
                _titleText.text = "Tile Info";
                _detailText.text = "Hover over a tile...";
                return;
            }

            var data = tile.TileData;
            var coords = data.Coordinates;
            var offset = coords.ToOffset();

            // Title: coordinates
            _titleText.text = $"Tile ({offset.x}, {offset.y})";

            // Detail lines
            string terrain = GetTerrainDisplay(data.Terrain);
            string structure = data.Structure != StructureType.None
                ? $"\nStructure: <color=#FF6666>{data.Structure}</color>"
                : "\nStructure: <color=#888>None</color>";
            string animal = data.Animal != AnimalType.None
                ? $"\nAnimal: <color=#FFAA00>{data.Animal}</color>"
                : "\nAnimal: <color=#888>None</color>";
            string cubeCoords = $"\nCube: {coords}";

            _detailText.text = $"Terrain: {terrain}{structure}{animal}{cubeCoords}";
        }

        // ---------------------------------------------------------
        // Helpers
        // ---------------------------------------------------------

        private string GetTerrainDisplay(TerrainType terrain)
        {
            string hex = terrain switch
            {
                TerrainType.Desert   => "#F5D67A",
                TerrainType.Forest   => "#22A22A",
                TerrainType.Water    => "#3370E6",
                TerrainType.Swamp    => "#5A6B38",
                TerrainType.Mountain => "#9E9A96",
                _                    => "#FFFFFF"
            };
            return $"<color={hex}>{terrain}</color>";
        }
    }
}
