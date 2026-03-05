using System.Collections.Generic;

namespace Cryptid.Core
{
    /// <summary>
    /// Simple static localization system supporting Korean (KR) and English (EN).
    /// All game text is accessed via keys. Use <see cref="Get"/> for static strings
    /// and <see cref="Format"/> for strings with arguments.
    ///
    /// Language can be switched at runtime via <see cref="CurrentLanguage"/>.
    /// Defaults to Korean.
    /// </summary>
    public static class L
    {
        public enum Language { KR, EN }

        private static Language _current = Language.KR;

        /// <summary>Gets or sets the current language. Fires <see cref="OnLanguageChanged"/> on change.</summary>
        public static Language CurrentLanguage
        {
            get => _current;
            set
            {
                if (_current == value) return;
                _current = value;
                OnLanguageChanged?.Invoke(value);
            }
        }

        /// <summary>Fired when the language changes.</summary>
        public static event System.Action<Language> OnLanguageChanged;

        /// <summary>Returns the localized string for the given key.</summary>
        public static string Get(string key)
        {
            var dict = _current == Language.KR ? _kr : _en;
            return dict.TryGetValue(key, out var val) ? val : $"[{key}]";
        }

        /// <summary>Returns the localized string formatted with arguments.</summary>
        public static string Format(string key, params object[] args)
        {
            var template = Get(key);
            try { return string.Format(template, args); }
            catch { return template; }
        }

        // =============================================================
        // Korean Strings
        // =============================================================

        private static readonly Dictionary<string, string> _kr = new()
        {
            // ── Title / Mode Selection ──
            ["title_cryptid"]       = "크립티드",
            ["choose_game_mode"]    = "게임 모드 선택",
            ["local_game"]          = "로컬 게임",
            ["host_game"]           = "호스트 게임",
            ["join_game"]           = "참가하기",

            // ── Join Panel ──
            ["connect"]             = "접속",
            ["cancel"]              = "취소",
            ["connecting_to"]       = "{0}:{1} 에 접속 중...",
            ["disconnected"]        = "호스트와 연결이 끊겼습니다.",
            ["enter_text"]          = "텍스트 입력...",

            // ── Lobby ──
            ["lobby"]               = "로비",
            ["lobby_host"]          = "로비 (호스트)",
            ["players_count"]       = "플레이어: {0} / 5",
            ["your_nickname"]       = "닉네임:",
            ["not_ready"]           = "✗ 준비 안됨",
            ["ready"]               = "✓ 준비 완료",
            ["start_game"]          = "게임 시작",
            ["start_game_need"]     = "게임 시작 ({0}/3 최소)",
            ["host_suffix"]         = " (호스트)",
            ["copy"]                = "복사",
            ["player_default"]      = "플레이어 {0}",
            ["player_connected"]    = "{0} 접속",
            ["player_disconnected"] = "{0} 연결 끊김",

            // ── Turn Indicator ──
            ["turn_n"]              = "턴 {0}",
            ["player_turn"]         = "{0}의 턴",
            ["phase_choose"]        = "행동 선택",
            ["phase_select_tile"]   = "타일 선택",
            ["phase_waiting"]       = "응답 대기 중...",
            ["phase_search"]           = "타일 탐색",
            ["phase_search_animating"] = "탐색 검증 중...",
            ["phase_penalty"]       = "패널티 큐브 배치",
            ["phase_turn_end"]      = "턴 종료...",

            // ── Action Panel ──
            ["choose_action"]       = "행동을 선택하세요",
            ["click_tile_question"] = "질문할 타일을 클릭하세요",
            ["click_tile_search"]   = "크립티드를 탐색할 타일을 클릭하세요",
            ["place_penalty_msg"]   = "틀렸습니다! 단서와 일치하지 않는 타일에 큐브를 놓으세요",
            ["btn_question"]        = "질문",
            ["btn_search"]          = "탐색",

            // ── Player Select ──
            ["ask_which_player"]    = "어떤 플레이어에게 질문할까요?",

            // ── Clue Panel ──
            ["your_clue"]           = "나의 단서",
            ["player_clue"]         = "플레이어 {0}의 단서",

            // ── Game Over ──
            ["game_over"]           = "게임 종료",
            ["player_wins"]         = "플레이어 {0} 승리!",
            ["play_again"]          = "다시 하기",
            ["cryptid_location"]    = "크립티드는 {0}에 있었습니다",

            // ── Game Log ──
            ["game_log"]            = "게임 로그",
            ["chat_placeholder"]    = "Enter 를 눌러 채팅...",
            ["log_game_started"]    = "=== 게임 시작 ===",
            ["log_net_started"]     = "=== 네트워크 게임 시작 ===",
            ["log_turn_header"]     = "--- 턴 {0}: {1} ---",
            ["log_asks"]            = "{0}이(가) {1}에게 질문: \"{2}\"",
            ["log_responds_yes"]    = "{0} 응답: 예 (디스크)",
            ["log_responds_no"]     = "{0} 응답: 아니오 (큐브)",
            ["log_must_place_cube"] = "  → {0}은(는) 일치하지 않는 타일에 큐브를 놓아야 합니다",
            ["log_search_placing"]  = "{0}이(가) {1} 탐색 — 디스크 배치 중...",
            ["log_verify_yes"]      = "  {0} 확인: 예 (디스크)",
            ["log_verify_no"]       = "  {0} 확인: 아니오 (큐브)",
            ["log_search_success"]  = "  탐색 성공! {0} 승리!",
            ["log_search_fail"]     = "  탐색 실패!",
            ["log_penalty_placed"]  = "  패널티: {0}이(가) {1}에 큐브 배치",

            // ── Tile Info ──
            ["tile_info"]           = "타일 정보",
            ["hover_tile"]          = "타일 위로 마우스를 올리세요...",
            ["tile_coords"]         = "타일 ({0}, {1})",
            ["label_terrain"]       = "지형:",
            ["label_structure"]     = "구조물:",
            ["label_animal"]        = "동물:",
            ["label_cube"]          = "큐브:",
            ["none"]                = "없음",

            // ── Terrain ──
            ["terrain_desert"]      = "사막",
            ["terrain_forest"]      = "숲",
            ["terrain_water"]       = "물",
            ["terrain_swamp"]       = "늪",
            ["terrain_mountain"]    = "산",

            // ── Structure ──
            ["structure_standing_stone"]   = "선돌",
            ["structure_abandoned_shack"]  = "폐허 오두막",

            // ── Animal ──
            ["animal_tiger"]        = "호랑이",
            ["animal_deer"]         = "사슴",

            // ── Warnings / Errors ──
            ["warn_tile_has_cube"]     = "  ⚠ 해당 타일에 큐브가 있어 차단되었습니다.",
            ["warn_already_token"]     = "  ⚠ 이미 해당 타일에 토큰이 있습니다.",
            ["warn_matches_clue"]      = "해당 타일은 당신의 단서와 일치합니다! 다른 타일을 선택하세요.",
            ["error_title"]            = "오류",
            ["map_generation_failed"]  = "맵 생성 실패\n\n100번 시도 후에도 유효한 맵을 생성할 수 없습니다.\n게임을 재시작하여 다시 시도하세요.",
            ["connection_failed"]      = "Steam 연결 실패\n\nSteam이 실행 중이고 유효한 룸 코드를 입력했는지 확인하세요.",
            ["restart"]                = "재시작",

            // ── Timer ──
            ["timer_remaining"]        = "남은 시간: {0}초",
            ["timer_expired"]          = "시간 초과!",
            ["timer_auto_penalty"]     = "  ⌛ 시간 초과 — 자동으로 페널티 큐브를 배치합니다.",

            // ── Settings ──
            ["settings"]              = "설정",
            ["settings_language"]      = "언어",
            ["settings_resolution"]    = "해상도",
            ["settings_audio"]         = "음량",
            ["tab_main"]               = "메인",
            ["tab_audio"]              = "음향",
            ["tab_display"]            = "디스플레이",
            ["settings_master_volume"] = "\U0001f50a 마스터 음량 (전체)",
            ["settings_bgm_volume"]    = "\U0001f3b5 배경음 음량 (BGM)",
            ["settings_sfx_volume"]    = "\U0001f514 효과음 음량 (SFX/UI)",
            ["settings_input_device"]  = "입력 장치",
            ["settings_output_device"] = "출력 장치",
            ["scan_devices"]           = "장치 스캔",
            ["default_device"]         = "기본 장치",
            ["no_devices"]             = "장치 없음",
            ["vivox_not_ready"]        = "Vivox 미연결",
            ["enter_steam_id"]         = "Steam ID 입력",
            ["invalid_steam_id"]       = "유효하지 않은 Steam ID",
            ["connecting_steam"]       = "Steam {0} 연결 중...",
            ["settings_perspective"]   = "카메라 시점",
            ["settings_ortho"]         = "직교",
            ["settings_persp"]         = "원근",
            ["settings_apply"]         = "적용",
            ["settings_back"]          = "뒤로",
            ["game_description"]       = "크립티드는 추론 기반 보드 게임입니다.\n각 플레이어는 미지의 생물에 대한 단서를 하나 보유하고 있으며,\n질문과 탐색을 통해 크립티드의 위치를 추론하세요!\n3~5인 멀티플레이어를 지원합니다.",

            // ── Disconnect ──
            ["player_disconnected"]    = "{0} 연결 끊김",
            ["player_skipped"]         = "{0} 건너뜀 (연결 끊김)",
        };

        // =============================================================
        // English Strings
        // =============================================================

        private static readonly Dictionary<string, string> _en = new()
        {
            // ── Title / Mode Selection ──
            ["title_cryptid"]       = "CRYPTID",
            ["choose_game_mode"]    = "Choose Game Mode",
            ["local_game"]          = "Local Game",
            ["host_game"]           = "Host Game",
            ["join_game"]           = "Join Game",

            // ── Join Panel ──
            ["connect"]             = "Connect",
            ["cancel"]              = "Cancel",
            ["connecting_to"]       = "Connecting to {0}:{1}...",
            ["disconnected"]        = "Disconnected from host.",
            ["enter_text"]          = "Enter text...",

            // ── Lobby ──
            ["lobby"]               = "LOBBY",
            ["lobby_host"]          = "LOBBY (Host)",
            ["players_count"]       = "Players: {0} / 5",
            ["your_nickname"]       = "Your Nickname:",
            ["not_ready"]           = "✗ Not Ready",
            ["ready"]               = "✓ Ready",
            ["start_game"]          = "Start Game",
            ["start_game_need"]     = "Start Game ({0}/3 min)",
            ["host_suffix"]         = " (Host)",
            ["copy"]                = "Copy",
            ["player_default"]      = "Player {0}",
            ["player_connected"]    = "{0} connected",
            ["player_disconnected"] = "{0} disconnected",

            // ── Turn Indicator ──
            ["turn_n"]              = "Turn {0}",
            ["player_turn"]         = "{0}'s Turn",
            ["phase_choose"]        = "Choose Action",
            ["phase_select_tile"]   = "Select a Tile",
            ["phase_waiting"]       = "Waiting for Response...",
            ["phase_search"]           = "Search a Tile",
            ["phase_search_animating"] = "Verifying Search...",
            ["phase_penalty"]       = "Place Penalty Cube",
            ["phase_turn_end"]      = "Turn Ending...",

            // ── Action Panel ──
            ["choose_action"]       = "Choose your action",
            ["click_tile_question"] = "Click a tile to ask about",
            ["click_tile_search"]   = "Click a tile to search for the Cryptid",
            ["place_penalty_msg"]   = "Wrong! Place a cube on a tile your clue does NOT match",
            ["btn_question"]        = "Question",
            ["btn_search"]          = "Search",

            // ── Player Select ──
            ["ask_which_player"]    = "Ask which player?",

            // ── Clue Panel ──
            ["your_clue"]           = "YOUR CLUE",
            ["player_clue"]         = "PLAYER {0}'S CLUE",

            // ── Game Over ──
            ["game_over"]           = "GAME OVER",
            ["player_wins"]         = "Player {0} Wins!",
            ["play_again"]          = "Play Again",
            ["cryptid_location"]    = "The Cryptid was at {0}",

            // ── Game Log ──
            ["game_log"]            = "GAME LOG",
            ["chat_placeholder"]    = "Press Enter to chat...",
            ["log_game_started"]    = "=== Game Started ===",
            ["log_net_started"]     = "=== Network Game Started ===",
            ["log_turn_header"]     = "--- Turn {0}: {1} ---",
            ["log_asks"]            = "{0} asks {1}: \"{2}\"",
            ["log_responds_yes"]    = "{0} responds: YES (disc)",
            ["log_responds_no"]     = "{0} responds: NO (cube)",
            ["log_must_place_cube"] = "  → {0} must place a cube on a non-matching tile",
            ["log_search_placing"]  = "{0} searches {1} — placing disc...",
            ["log_verify_yes"]      = "  {0} verifies: YES (disc)",
            ["log_verify_no"]       = "  {0} verifies: NO (cube)",
            ["log_search_success"]  = "  Search SUCCESS! {0} wins!",
            ["log_search_fail"]     = "  Search FAILED!",
            ["log_penalty_placed"]  = "  Penalty: {0} places cube at {1}",

            // ── Tile Info ──
            ["tile_info"]           = "Tile Info",
            ["hover_tile"]          = "Hover over a tile...",
            ["tile_coords"]         = "Tile ({0}, {1})",
            ["label_terrain"]       = "Terrain:",
            ["label_structure"]     = "Structure:",
            ["label_animal"]        = "Animal:",
            ["label_cube"]          = "Cube:",
            ["none"]                = "None",

            // ── Terrain ──
            ["terrain_desert"]      = "Desert",
            ["terrain_forest"]      = "Forest",
            ["terrain_water"]       = "Water",
            ["terrain_swamp"]       = "Swamp",
            ["terrain_mountain"]    = "Mountain",

            // ── Structure ──
            ["structure_standing_stone"]   = "Standing Stone",
            ["structure_abandoned_shack"]  = "Abandoned Shack",

            // ── Animal ──
            ["animal_tiger"]        = "Tiger",
            ["animal_deer"]         = "Deer",

            // ── Warnings / Errors ──
            ["warn_tile_has_cube"]     = "  ⚠ That tile has a cube — permanently blocked.",
            ["warn_already_token"]     = "  ⚠ You already have a token on that tile.",
            ["warn_matches_clue"]      = "That tile matches your clue! Choose a different tile.",
            ["error_title"]            = "ERROR",
            ["map_generation_failed"]  = "Map Generation Failed\n\nUnable to generate a valid map after 100 attempts.\nPlease restart the game to try again.",
            ["connection_failed"]      = "Steam Connection Failed\n\nPlease ensure Steam is running and you have entered a valid room code.",
            ["restart"]                = "Restart",

            // ── Timer ──
            ["timer_remaining"]        = "Time left: {0}s",
            ["timer_expired"]          = "Time expired!",
            ["timer_auto_penalty"]     = "  ⌛ Time expired — auto-placed penalty cube.",

            // ── Settings ──
            ["settings"]              = "Settings",
            ["settings_language"]      = "Language",
            ["settings_resolution"]    = "Resolution",
            ["settings_audio"]         = "Audio Volume",
            ["tab_main"]               = "Main",
            ["tab_audio"]              = "Audio",
            ["tab_display"]            = "Display",
            ["settings_master_volume"] = "\U0001f50a Master Volume (All)",
            ["settings_bgm_volume"]    = "\U0001f3b5 BGM Volume (Music)",
            ["settings_sfx_volume"]    = "\U0001f514 SFX Volume (Effects)",
            ["settings_input_device"]  = "Input Device",
            ["settings_output_device"] = "Output Device",
            ["scan_devices"]           = "Scan Devices",
            ["default_device"]         = "Default Device",
            ["no_devices"]             = "No Devices",
            ["vivox_not_ready"]        = "Vivox Not Ready",
            ["enter_steam_id"]         = "Enter Steam ID",
            ["invalid_steam_id"]       = "Invalid Steam ID",
            ["connecting_steam"]       = "Connecting to Steam {0}...",
            ["settings_perspective"]   = "Camera Perspective",
            ["settings_ortho"]         = "Orthographic",
            ["settings_persp"]         = "Perspective",
            ["settings_apply"]         = "Apply",
            ["settings_back"]          = "Back",
            ["game_description"]       = "Cryptid is a deduction-based board game.\nEach player holds one clue about an unknown creature.\nUse questions and searches to deduce the Cryptid's location!\nSupports 3-5 players in multiplayer.",

            // ── Disconnect ──
            ["player_disconnected"]    = "{0} disconnected",
            ["player_skipped"]         = "{0} skipped (disconnected)",
        };

        // =============================================================
        // Helpers for Terrain/Structure/Animal display names
        // =============================================================

        /// <summary>Returns the localized display name for a terrain type.</summary>
        public static string TerrainName(Data.TerrainType t) => t switch
        {
            Data.TerrainType.Desert   => Get("terrain_desert"),
            Data.TerrainType.Forest   => Get("terrain_forest"),
            Data.TerrainType.Water    => Get("terrain_water"),
            Data.TerrainType.Swamp    => Get("terrain_swamp"),
            Data.TerrainType.Mountain => Get("terrain_mountain"),
            _ => t.ToString()
        };

        /// <summary>Returns the localized display name for a structure type.</summary>
        public static string StructureName(Data.StructureType s) => s switch
        {
            Data.StructureType.None           => Get("none"),
            Data.StructureType.StandingStone  => Get("structure_standing_stone"),
            Data.StructureType.AbandonedShack => Get("structure_abandoned_shack"),
            _ => s.ToString()
        };

        /// <summary>Returns the localized display name for an animal type.</summary>
        public static string AnimalName(Data.AnimalType a) => a switch
        {
            Data.AnimalType.None  => Get("none"),
            Data.AnimalType.Tiger => Get("animal_tiger"),
            Data.AnimalType.Deer  => Get("animal_deer"),
            _ => a.ToString()
        };

        /// <summary>Returns a short player name like "P1" for log formatting.</summary>
        public static string PlayerShort(int playerIndex) => $"P{playerIndex + 1}";
    }
}
