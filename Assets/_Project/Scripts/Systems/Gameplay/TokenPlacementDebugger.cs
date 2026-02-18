using Cryptid.Systems.Map;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Cryptid.Systems.Gameplay
{
    /// <summary>
    /// Debug controller for testing token placement in Play Mode.
    /// 
    /// Integrates with TileInteractionSystem to detect selected tiles,
    /// then listens for number keys to place tokens.
    /// 
    /// Controls:
    ///   Click a tile to select it, then:
    ///   - 1-5: Place a Cube for Player 1-5
    ///   - Shift + 1-5: Place a Disc for Player 1-5
    ///   - Delete/Backspace: Remove last placed token
    ///   - X: Clear all tokens
    /// 
    /// Setup:
    ///   1. Add this component to a GameObject
    ///   2. Assign TokenPlacer and TileInteractionSystem references
    ///   3. Enter Play Mode and test
    /// </summary>
    public class TokenPlacementDebugger : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private TokenPlacer _tokenPlacer;
        [SerializeField] private TileInteractionSystem _tileInteraction;

        // Current selected tile (from TileInteractionSystem)
        private HexTile _selectedTile;

        private void OnEnable()
        {
            if (_tileInteraction != null)
                _tileInteraction.OnTileSelected += HandleTileSelected;
        }

        private void OnDisable()
        {
            if (_tileInteraction != null)
                _tileInteraction.OnTileSelected -= HandleTileSelected;
        }

        private void HandleTileSelected(HexTile tile)
        {
            _selectedTile = tile;
        }

        private void Update()
        {
            if (_tokenPlacer == null) return;

            var kb = Keyboard.current;
            if (kb == null) return;

            // Delete / Backspace → remove last token
            if (kb.deleteKey.wasPressedThisFrame || kb.backspaceKey.wasPressedThisFrame)
            {
                _tokenPlacer.RemoveLastToken();
                return;
            }

            // X → clear all
            if (kb.xKey.wasPressedThisFrame)
            {
                _tokenPlacer.ClearAllTokens();
                return;
            }

            // Need a selected tile for placement
            if (_selectedTile == null) return;

            // Check number keys 1-5
            bool shift = kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed;

            for (int i = 0; i < 5; i++)
            {
                Key numKey = Key.Digit1 + i;
                if (kb[numKey].wasPressedThisFrame)
                {
                    TokenType type = shift ? TokenType.Disc : TokenType.Cube;
                    _tokenPlacer.PlaceToken(_selectedTile, type, i);
                    return;
                }
            }
        }
    }
}
