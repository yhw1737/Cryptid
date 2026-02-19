# Console Errors Reference

이 문서는 Unity 콘솔에서 반복적으로 발생한 에러와 그 해결 방법을 기록한다.
코드 작성 시 같은 실수를 반복하지 않기 위한 참고용.

---

## 1. CS0246: 'HexCoordinates' could not be found

- **원인:** `HexCoordinates`는 `Cryptid.Core` 네임스페이스에 정의되어 있음. `using Cryptid.Core;`를 빠뜨리면 발생.
- **해결:** 파일 상단에 `using Cryptid.Core;` 추가.
- **규칙:** `HexCoordinates`, `WorldTile`, `HexTile` 등 Core 타입을 사용할 때는 반드시 `using Cryptid.Core;` 필요.

```
// 프로젝트 주요 네임스페이스 → 타입 매핑
Cryptid.Core         → HexCoordinates, WorldTile, HexTile, GameService, GameStateMachine
Cryptid.Data         → TerrainType, StructureType, AnimalType, TokenType, PuzzleSetup
Cryptid.Systems.Clue → IClue, PuzzleGenerator, ClueBalancer, 각 ClueType 클래스
Cryptid.Systems.Turn → TurnManager, TurnPhase, PlayerAction
Cryptid.Systems.Map  → MapGenerator
Cryptid.UI           → GameUIManager, UIFactory, ActionPanel, TurnIndicatorPanel 등
```

---

## 2. CS0117: 'TextAlignmentOptions' does not contain 'MidlineCenter'

- **원인:** Unity 6의 TextMeshPro에서 `TextAlignmentOptions` enum 값이 변경됨.
- **해결:** `TextAlignmentOptions.MidlineCenter` → `TextAlignmentOptions.Center` 사용.
- **규칙:** Unity 6 TMP에서는 `Center`, `Left`, `Right` 등 간소화된 이름 사용.

---

## 3. CS0618: 'enableWordWrapping' is obsolete

- **원인:** Unity 6 TMP에서 `enableWordWrapping` (bool) 프로퍼티가 deprecated됨.
- **해결:** `tmp.enableWordWrapping = true` → `tmp.textWrappingMode = TextWrappingModes.Normal` 사용.
- **참고:** `TextWrappingModes.NoWrap`으로 비활성화.

---

## 4. CS0414: Private field is assigned but never used

- **원인:** `[SerializeField]`로 선언한 필드를 코드에서 사용하지 않으면 경고 발생.
- **해결 방법 (택 1):**
  1. 실제로 사용하지 않는 필드라면 삭제.
  2. 의도적으로 Inspector 노출용이면 `#pragma warning disable 0414` 사용.
- **사례:**
  - `RTSCameraController._panSpeed`: 실제 pan 로직에서 world-space delta를 사용하므로 필드 불필요 → 삭제.
  - `PuzzleDebugVisualizer._generateKey` 등 `KeyCode` 필드: New Input System(`Keyboard.current`)을 직접 사용하므로 → 삭제.

---

## 5. New Input System vs Legacy Input

- **규칙:** 이 프로젝트는 **New Input System** (`UnityEngine.InputSystem`)만 사용.
- `Input.GetKey()`, `KeyCode` 등 Legacy API 사용 금지.
- 올바른 사용법:
  ```csharp
  using UnityEngine.InputSystem;
  
  var kb = Keyboard.current;
  if (kb.gKey.wasPressedThisFrame) { ... }
  ```
- `[SerializeField] KeyCode _key` 같은 패턴은 사용하지 않는다.
