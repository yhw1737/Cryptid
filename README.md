# 🔍 Cryptid — 디지털 크립티드

> **크립티드**는 3D 헥사곤 맵 위에서 플레이어들이 각자의 비밀 단서를 조합해 단 하나의 "크립티드 서식지"를 추리하는 **턴제 멀티플레이어 추리 보드게임**입니다.

---

## 📌 프로젝트 개요

| 항목 | 내용 |
|---|---|
| **장르** | 3D 멀티플레이어 추리 보드게임 (턴제) |
| **엔진** | Unity 6 (6000.2.7f1) / URP |
| **플랫폼** | PC (Steam) |
| **플레이어** | 2~5인 (로컬 / 온라인) |
| **네트워크** | Netcode for GameObjects + Facepunch.Steamworks P2P |

---

## 🎮 게임 방법

1. 게임이 시작되면 **12×9 헥사곤 맵**이 절차적으로 생성됩니다.
2. 각 플레이어는 크립티드가 숨어있는 타일을 정의하는 **비밀 단서**를 하나씩 받습니다.
   - 예: *"크립티드는 숲 지형 또는 물 지형 옆에 있다"*
3. 자신의 턴에 두 가지 행동 중 하나를 선택합니다.
   - **질문 (Question):** 다른 플레이어에게 특정 타일이 자신의 단서와 일치하는지 묻습니다. 아니라면 그 플레이어는 일치하지 않는 타일에 **큐브**(오답 마커)를 놓습니다.
   - **탐색 (Search):** 정답이라고 확신하는 타일을 지목합니다. 모든 플레이어가 디스크를 놓으면 성공, 한 명이라도 큐브를 놓으면 실패하고 패널티를 받습니다.
4. 가장 먼저 크립티드의 위치를 정확히 밝히는 플레이어가 승리합니다.

---

## ✨ 주요 기능

- **절차적 맵 생성** — Perlin Noise 기반 지형(사막·숲·물·늪·산), 구조물(오두막·선돌), 동물(호랑이·사슴) 자동 배치
- **논리 클루 엔진** — 맵을 역산하여 모든 플레이어의 단서 난이도가 균형 잡힌 시나리오를 자동 생성
- **Steam P2P 멀티플레이** — 룸 코드 공유 방식으로 추가 서버 없이 친구와 플레이
- **턴 타이머** — 제한 시간 초과 시 자동 패널티 큐브 배치
- **로컬 멀티플레이** — 한 PC에서 2~5명 동시 플레이 지원 (핫시트 방식)
- **다국어 지원** — 한국어 / English 실시간 전환

---

## 🏗️ 기술 스택

| 분류 | 라이브러리 / 패키지 |
|---|---|
| **네트워크** | Netcode for GameObjects 2.7.0, Facepunch.Steamworks |
| **UI** | TextMeshPro, DOTween (HOTween v2) |
| **비동기** | UniTask |
| **직렬화** | Newtonsoft.Json |
| **입력** | Unity Input System |
| **렌더링** | Universal Render Pipeline (URP) 17.x |
| **테스트** | ParrelSync (멀티인스턴스 테스트) |

---

## 🗂️ 프로젝트 구조

```
Assets/_Project/
├── Scripts/
│   ├── Core/          # GameBootstrapper, GameService, FSM, Localization
│   ├── Data/          # ScriptableObjects (MapConfig, ClueDefinitions)
│   ├── Network/       # ConnectionManager, FacepunchTransport, NetworkGameManager
│   ├── Systems/
│   │   ├── Map/       # HexGrid, ProceduralMapBuilder, HexTile
│   │   ├── Clue/      # ClueEngine, LogicSolver
│   │   └── Turn/      # TurnManager, TurnTimer, PlayerAction
│   └── UI/            # GameUIManager, Panels (GameOver, TileInfo, Log...)
├── Prefabs/           # Map Tiles, Player Tokens, Structures
└── ScriptableObjects/ # MapConfigs, ClueDefs
```

---

## 🚀 빌드 & 실행

### 요구 환경

- **Unity** 6000.2.7f1 이상
- **Steam** 설치 및 실행 (멀티플레이 기능 사용 시)

### 로컬 실행

1. 저장소 클론 후 Unity Hub에서 프로젝트 열기
2. `Assets/Scenes/` 에서 메인 씬 실행
3. **로컬 게임** 선택 → 플레이어 수 지정 후 시작

### 온라인 멀티플레이

1. **호스트** 가 "호스트 게임" 선택 → 표시되는 룸 코드 공유
2. **클라이언트** 가 "참가하기" 선택 → 룸 코드 입력 후 접속
3. 모든 플레이어 준비 완료 후 호스트가 게임 시작

---

## 🔖 브랜치 전략

| 브랜치 | 용도 |
|---|---|
| `main` | 안정 릴리즈 |
| `develop` | 통합 개발 |
| `Feat/*` | 기능 개발 |
| `Fix/*` | 버그 수정 |
| `Docs/*` | 문서 업데이트 |

---

## 📄 라이선스

이 프로젝트는 개인 학습 및 포트폴리오 목적으로 제작되었습니다.  
사용된 에셋의 라이선스는 각 에셋 제공사의 정책을 따릅니다.
