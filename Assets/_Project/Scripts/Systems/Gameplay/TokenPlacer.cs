using System;
using System.Collections.Generic;
using Cryptid.Core;
using Cryptid.Systems.Map;
using UnityEngine;
using DG.Tweening;

namespace Cryptid.Systems.Gameplay
{
    /// <summary>
    /// Token types in Cryptid:
    /// - Cube: placed when asking a Question ("Could the cryptid be here?")
    /// - Disc: placed when Searching ("I confirm/deny based on my clue")
    /// </summary>
    public enum TokenType
    {
        Cube = 0,
        Disc = 1
    }

    /// <summary>
    /// Runtime data for a placed token.
    /// </summary>
    public struct TokenInfo
    {
        public TokenType Type;
        public int PlayerIndex;
        public HexCoordinates Coordinates;
        public GameObject Visual;
    }

    /// <summary>
    /// Manages token placement and removal on hex tiles.
    /// 
    /// Responsibilities:
    /// - Spawns 3D token visuals (cube/disc) with player colors
    /// - Animates placement using DOTween (drop-in from above)
    /// - Tracks which tokens are on which tiles
    /// - Supports removal with animation
    /// 
    /// Setup:
    /// 1. Add to a GameObject in the scene
    /// 2. Optionally configure player colors
    /// 3. Call PlaceToken() from game logic or debug input
    /// 
    /// Debug Controls:
    /// - Left-click a tile + press 1-5 to place a Cube for that player
    /// - Left-click a tile + press Shift+1-5 to place a Disc
    /// - Press Delete to remove last token
    /// </summary>
    public class TokenPlacer : MonoBehaviour
    {
        [Header("Token Dimensions")]
        [Tooltip("Size of cube tokens (0.8x original, flattened)")]
        [SerializeField] private Vector3 _cubeSize = new Vector3(0.20f, 0.12f, 0.20f);

        [Tooltip("Disc radius and height (0.8x original, flattened)")]
        [SerializeField] private float _discRadius = 0.16f;
        [SerializeField] private float _discHeight = 0.05f;

        [Header("Player Colors")]
        [SerializeField] private Color[] _playerColors = new Color[]
        {
            new Color(0.2f, 0.5f, 1f),   // Player 1: Blue
            new Color(1f, 0.3f, 0.2f),    // Player 2: Red
            new Color(0.3f, 0.85f, 0.3f), // Player 3: Green
            new Color(1f, 0.75f, 0.1f),   // Player 4: Yellow
            new Color(0.7f, 0.3f, 0.85f), // Player 5: Purple
        };

        [Header("Animation")]
        [Tooltip("Height from which tokens drop in")]
        [SerializeField] private float _dropHeight = 3f;

        [Tooltip("Drop animation duration")]
        [SerializeField] private float _dropDuration = 0.5f;

        [Tooltip("Bounce after landing")]
        [SerializeField] private float _bounceHeight = 0.15f;

        [Tooltip("Scale punch on landing")]
        [SerializeField] private float _landingPunch = 0.2f;

        [Header("Layout")]
        [Tooltip("Vertical offset above the tile surface")]
        [SerializeField] private float _baseYOffset = 0.10f;

        [Tooltip("Stack offset when multiple tokens on same tile")]
        [SerializeField] private float _stackOffset = 0.15f;

        // ---------------------------------------------------------
        // Runtime State
        // ---------------------------------------------------------

        /// <summary> All placed tokens indexed by coordinates. </summary>
        private readonly Dictionary<HexCoordinates, List<TokenInfo>> _tokensByTile = new();

        /// <summary> All tokens in placement order (for undo). </summary>
        private readonly List<TokenInfo> _allTokens = new();

        /// <summary> Container for spawned token objects. </summary>
        private Transform _container;

        // ---------------------------------------------------------
        // Events
        // ---------------------------------------------------------

        /// <summary> Fired when a token is placed. </summary>
        public event Action<TokenInfo> OnTokenPlaced;

        /// <summary> Fired when a token is removed. </summary>
        public event Action<TokenInfo> OnTokenRemoved;

        // ---------------------------------------------------------
        // Lifecycle
        // ---------------------------------------------------------

        private void Awake()
        {
            var containerObj = new GameObject("[Generated] Token Container");
            containerObj.transform.SetParent(transform);
            _container = containerObj.transform;
        }

        // ---------------------------------------------------------
        // Public API
        // ---------------------------------------------------------

        /// <summary>
        /// Places a token on the specified tile with DOTween drop animation.
        /// </summary>
        /// <param name="tile">Target hex tile.</param>
        /// <param name="type">Cube (question) or Disc (search).</param>
        /// <param name="playerIndex">0-based player index.</param>
        /// <returns>The created TokenInfo.</returns>
        public TokenInfo PlaceToken(HexTile tile, TokenType type, int playerIndex)
        {
            return PlaceTokenAt(tile.Coordinates, type, playerIndex);
        }

        /// <summary>
        /// Places a token at raw coordinates.
        /// </summary>
        public TokenInfo PlaceTokenAt(HexCoordinates coords, TokenType type, int playerIndex)
        {
            Vector3 worldPos = HexMetrics.HexToWorldPosition(coords);

            // Calculate stack position
            int stackIndex = GetTokenCountAt(coords);
            float yPos = _baseYOffset + stackIndex * _stackOffset;
            Vector3 targetPos = worldPos + Vector3.up * yPos;

            // Create visual
            GameObject tokenObj = CreateTokenVisual(type, playerIndex);
            tokenObj.transform.SetParent(_container);

            // Start position (above, for drop animation)
            Vector3 startPos = targetPos + Vector3.up * _dropHeight;
            tokenObj.transform.position = startPos;
            tokenObj.transform.localScale = Vector3.zero;

            // Build token info
            var info = new TokenInfo
            {
                Type = type,
                PlayerIndex = playerIndex,
                Coordinates = coords,
                Visual = tokenObj
            };

            // Register
            if (!_tokensByTile.ContainsKey(coords))
                _tokensByTile[coords] = new List<TokenInfo>();
            _tokensByTile[coords].Add(info);
            _allTokens.Add(info);

            // Animate placement with DOTween
            AnimatePlacement(tokenObj, targetPos);

            OnTokenPlaced?.Invoke(info);

            string typeName = type == TokenType.Cube ? "Cube" : "Disc";
            Debug.Log($"[TokenPlacer] Player {playerIndex + 1} placed {typeName} at {coords}" +
                     $" (stack: {stackIndex + 1})");

            return info;
        }

        /// <summary>
        /// Removes the most recently placed token with animation.
        /// </summary>
        public void RemoveLastToken()
        {
            if (_allTokens.Count == 0)
            {
                Debug.LogWarning("[TokenPlacer] No tokens to remove.");
                return;
            }

            var token = _allTokens[^1];
            RemoveToken(token);
        }

        /// <summary>
        /// Removes a specific token with animation.
        /// </summary>
        public void RemoveToken(TokenInfo token)
        {
            if (token.Visual == null) return;

            _allTokens.Remove(token);
            if (_tokensByTile.TryGetValue(token.Coordinates, out var list))
                list.RemoveAll(t => t.Visual == token.Visual);

            // Animate removal
            AnimateRemoval(token.Visual);

            OnTokenRemoved?.Invoke(token);

            Debug.Log($"[TokenPlacer] Removed token at {token.Coordinates}.");
        }

        /// <summary>
        /// Removes all tokens from a specific tile.
        /// </summary>
        public void ClearTokensAt(HexCoordinates coords)
        {
            if (!_tokensByTile.TryGetValue(coords, out var list)) return;

            foreach (var token in list)
            {
                if (token.Visual != null)
                    AnimateRemoval(token.Visual);
                _allTokens.Remove(token);
            }

            list.Clear();
        }

        /// <summary>
        /// Removes all discs from a specific tile, keeping cubes intact.
        /// Used when a search fails: cubes (denial) stay, discs (confirmation) are removed.
        /// </summary>
        public void RemoveDiscsAt(HexCoordinates coords)
        {
            if (!_tokensByTile.TryGetValue(coords, out var list)) return;

            var discs = list.FindAll(t => t.Type == TokenType.Disc);
            foreach (var disc in discs)
            {
                if (disc.Visual != null)
                    AnimateRemoval(disc.Visual);
                _allTokens.Remove(disc);
                list.Remove(disc);
            }

            Debug.Log($"[TokenPlacer] Removed {discs.Count} disc(s) from {coords}.");
        }

        /// <summary>
        /// Removes all tokens from the board.
        /// </summary>
        [ContextMenu("Clear All Tokens")]
        public void ClearAllTokens()
        {
            foreach (var token in _allTokens)
            {
                if (token.Visual != null)
                    Destroy(token.Visual);
            }

            _allTokens.Clear();
            _tokensByTile.Clear();
            Debug.Log("[TokenPlacer] All tokens cleared.");
        }

        /// <summary>
        /// Returns how many tokens are currently on the given tile.
        /// </summary>
        public int GetTokenCountAt(HexCoordinates coords)
        {
            return _tokensByTile.TryGetValue(coords, out var list) ? list.Count : 0;
        }

        /// <summary>
        /// Returns all tokens placed on the board.
        /// </summary>
        public IReadOnlyList<TokenInfo> AllTokens => _allTokens;

        // ---------------------------------------------------------
        // Tile Validation (Spec 5.3.A)
        // ---------------------------------------------------------

        /// <summary>
        /// Returns true if any cube exists on this tile.
        /// Rule 1 (Cube Blocker): tiles with cubes are permanently dead.
        /// No tokens can be placed and no interactions allowed.
        /// </summary>
        public bool HasAnyCube(HexCoordinates coords)
        {
            if (!_tokensByTile.TryGetValue(coords, out var list)) return false;
            foreach (var token in list)
            {
                if (token.Type == TokenType.Cube) return true;
            }
            return false;
        }

        /// <summary>
        /// Returns true if the specified player already has any token on this tile.
        /// Rule 2 (No Self-Stacking): can't place or interact where you already have a token.
        /// </summary>
        public bool HasPlayerToken(HexCoordinates coords, int playerIndex)
        {
            if (!_tokensByTile.TryGetValue(coords, out var list)) return false;
            foreach (var token in list)
            {
                if (token.PlayerIndex == playerIndex) return true;
            }
            return false;
        }

        /// <summary>
        /// Returns true if the specified player has a cube specifically on this tile.
        /// Used for search/penalty validation where discs should not block.
        /// </summary>
        public bool HasPlayerCube(HexCoordinates coords, int playerIndex)
        {
            if (!_tokensByTile.TryGetValue(coords, out var list)) return false;
            foreach (var token in list)
            {
                if (token.PlayerIndex == playerIndex && token.Type == TokenType.Cube) return true;
            }
            return false;
        }

        /// <summary>
        /// Validates all 3 tile interaction rules from spec 5.3.A:
        ///   1. Cube Blocker: no cubes on tile
        ///   2. No Self-Stacking: player has no tokens on tile
        ///   3. Discs are Stackable: other players' discs are OK (implicitly allowed)
        /// </summary>
        /// <returns>True if the tile is valid for this player to interact with.</returns>
        public bool CanInteract(HexCoordinates coords, int playerIndex)
        {
            return !HasAnyCube(coords) && !HasPlayerToken(coords, playerIndex);
        }

        // ---------------------------------------------------------
        // Visual Creation
        // ---------------------------------------------------------

        /// <summary>
        /// Creates the 3D mesh for a token (cube or disc).
        /// </summary>
        private GameObject CreateTokenVisual(TokenType type, int playerIndex)
        {
            Color color = playerIndex < _playerColors.Length
                ? _playerColors[playerIndex]
                : Color.white;

            GameObject obj;

            if (type == TokenType.Cube)
            {
                obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
                obj.transform.localScale = _cubeSize;
                obj.name = $"Token_Cube_P{playerIndex + 1}";
            }
            else // Disc
            {
                obj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                obj.transform.localScale = new Vector3(
                    _discRadius * 2f,
                    _discHeight,
                    _discRadius * 2f);
                obj.name = $"Token_Disc_P{playerIndex + 1}";
            }

            // Apply player color via URP Lit material
            var renderer = obj.GetComponent<Renderer>();
            if (renderer != null)
            {
                var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                mat.color = color;
                renderer.material = mat;
            }

            // Remove default collider (tokens shouldn't block raycasts)
            var collider = obj.GetComponent<Collider>();
            if (collider != null)
                Destroy(collider);

            return obj;
        }

        // ---------------------------------------------------------
        // DOTween Animations
        // ---------------------------------------------------------

        /// <summary>
        /// Drop-in animation: scale up + fall from above + bounce + landing punch.
        /// </summary>
        private void AnimatePlacement(GameObject tokenObj, Vector3 targetPos)
        {
            Transform t = tokenObj.transform;

            // Create a sequence for coordinated animation
            Sequence seq = DOTween.Sequence();

            // Scale in (pop)
            seq.Append(t.DOScale(Vector3.one * 1f, _dropDuration * 0.3f)
                .From(Vector3.zero)
                .SetEase(Ease.OutBack));

            // Drop down
            seq.Join(t.DOMove(targetPos, _dropDuration)
                .SetEase(Ease.InQuad));

            // Bounce on landing
            seq.Append(t.DOMove(targetPos + Vector3.up * _bounceHeight, _dropDuration * 0.25f)
                .SetEase(Ease.OutQuad));

            seq.Append(t.DOMove(targetPos, _dropDuration * 0.2f)
                .SetEase(Ease.InQuad));

            // Scale punch on landing
            seq.Join(t.DOPunchScale(Vector3.one * _landingPunch, _dropDuration * 0.3f, 1, 0.5f));

            seq.SetLink(tokenObj); // Auto-kill when object is destroyed
        }

        /// <summary>
        /// Removal animation: shrink + float up + destroy.
        /// </summary>
        private void AnimateRemoval(GameObject tokenObj)
        {
            if (tokenObj == null) return;

            Transform t = tokenObj.transform;

            Sequence seq = DOTween.Sequence();

            // Float up
            seq.Append(t.DOMove(t.position + Vector3.up * 1.5f, 0.4f)
                .SetEase(Ease.InBack));

            // Shrink
            seq.Join(t.DOScale(Vector3.zero, 0.35f)
                .SetEase(Ease.InBack));

            // Destroy when done
            seq.OnComplete(() =>
            {
                if (tokenObj != null) Destroy(tokenObj);
            });

            seq.SetLink(tokenObj);
        }
    }
}
