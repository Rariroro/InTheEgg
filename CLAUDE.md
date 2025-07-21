# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

# InTheEgg - AI 협업 가이드
- 한글로 소통

## 프로젝트 개요
Unity 기반의 펫 시뮬레이션 게임으로, 다양한 동물 펫들이 AI를 통해 자율적으로 행동하고 상호작용하는 프로젝트입니다.

### 게임 플로우
1. **PetChoice 씬**: 플레이어가 펫, 환경, 아이템을 선택하는 초기 설정 화면
2. **PetVillage 씬**: 선택한 펫들이 AI로 자율 행동하며 생활하는 메인 게임 씬

### 주요 특징
- 다중 펫 선택 시스템 (토글 기반 UI)
- 환경 커스터마이징 (다양한 환경 오브젝트 선택 가능)
- 아이템 시스템 (펫들이 상호작용할 수 있는 아이템)
- 식성 시스템 (DietaryFlags - 육식, 초식, 잡식)
- 특수 행동 시스템 (나무 오르기, 물 속 행동 등)

## 기술 스택
- **Unity Version**: 6000.0.49f1
- **Render Pipeline**: URP (Universal Render Pipeline)
- **주요 패키지**:
  - AI Navigation (com.unity.ai.navigation)
  - Unity Test Framework (com.unity.test-framework)
  - Cursor IDE Integration (com.boxqkrtm.ide.cursor)
  - Unity Toon Shader
- **주요 시스템**:
  - NavMesh 기반 AI 네비게이션
  - 행동 기반 AI 시스템 (Action-based AI)
  - 감정 표현 시스템
  - 펫 간 상호작용 시스템

## 프로젝트 구조
```
Assets/
├── 01_Scenes/          # 게임 씬 파일들
│   ├── PetChoice.unity      # 펫/환경/아이템 선택 씬
│   └── PetVillge.unity      # 메인 게임플레이 씬
├── 02_Scripts/         # 모든 C# 스크립트
│   ├── Pet/           # 펫 관련 핵심 스크립트
│   │   ├── PetController.cs        # 중앙 컨트롤러
│   │   ├── Actions/               # IPetAction 구현체들
│   │   ├── Controllers/           # 각 기능별 컨트롤러
│   │   └── Interactions/          # 펫 상호작용 구현체
│   ├── Manager/       # 게임 매니저들
│   ├── UI/            # UI 관련 스크립트
│   ├── Environment/   # 환경 상호작용
│   └── Editor/        # Unity Editor 확장
├── 03_Prefabs/        # 프리팹 파일들
├── 04_Arts/           # 아트 리소스
└── 08_ThirdParty/     # 서드파티 에셋
```

## 핵심 아키텍처

### 1. 컴포넌트 기반 펫 시스템
`PetController`가 중앙 제어 역할을 하며, 각 기능은 별도 컨트롤러로 분리:
- `PetMovementController`: 이동 제어
- `PetAnimationController`: 애니메이션 제어
- `PetInteractionController`: 타 펫과의 상호작용
- `PetFeedingController`: 먹이 행동
- `PetSleepingController`: 수면 행동
- `PetTreeClimbingController`: 나무 오르기
- `PetWaterBehaviorController`: 물 속 행동

### 2. Action 기반 AI 시스템
- **인터페이스**: `IPetAction`
- **우선순위 시스템**: `GetPriority()` 메서드로 동적 우선순위 결정
- **주요 Action들**:
  - 기본 행동: `WanderAction`, `EatAction`, `SleepAction`
  - 상호작용: `InteractWithPetAction`, `PlayWithItemAction`
  - 특수 행동: `ClimbTreeAction`, `BeeEscapeAction`, `GatherAction`

### 3. 상호작용 시스템
- **기본 클래스**: `BasePetInteraction`
- **일반 상호작용**: `HeadbuttInteraction`, `RaceInteraction`, `FightInteraction`
- **특수 상호작용**: `CamelAlpacaSpitFightInteraction`, `SlothKoalaRaceInteraction`

## 개발 명령어

### Unity Editor 작업
```bash
# Unity Hub에서 프로젝트 열기
open -a "Unity Hub"

# 특정 씬 열기 (Unity Editor 내에서)
File > Open Scene > Assets/01_Scenes/PetChoice.unity
```

### 빌드 명령어
```bash
# iOS 빌드 (BuildScript.cs가 필요)
/Applications/Unity/Hub/Editor/6000.0.49f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -quit \
  -projectPath /Users/rariroro/Documents/Unity/InTheEgg \
  -buildTarget iOS \
  -executeMethod BuildScript.PerformiOSBuild

# Android 빌드
/Applications/Unity/Hub/Editor/6000.0.49f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -quit \
  -projectPath /Users/rariroro/Documents/Unity/InTheEgg \
  -buildTarget Android \
  -executeMethod BuildScript.PerformAndroidBuild
```

### 테스트 실행
```bash
# Unity Test Framework 사용
# Editor 모드: Window > General > Test Runner
# PlayMode 테스트 실행
```

### Git 작업
```bash
# 현재 브랜치 상태 확인
git status

# 변경사항 확인
git diff

# 최근 커밋 로그
git log --oneline -10

# Unity 메타 파일 포함하여 커밋
git add -A
git commit -m "커밋 메시지"
```

## 코드 스타일 가이드

- 우선순위 기반의 상태 머신(Priority-Based State Machine) 사용
- PetController.cs에 모든 행동을 결정하는 중앙 통제 메서드를 고려

### 명명 규칙
- **Classes**: PascalCase (예: `PetController`)
- **Public 변수**: PascalCase (예: `PetType`)
- **Private 변수**: camelCase 또는 _camelCase (예: `_currentAction`)
- **메서드**: PascalCase (예: `UpdateAI()`)
- **상수**: UPPER_SNAKE_CASE (예: `SLEEPY_EMOTION_INTERVAL`)

### 주석 규칙
- 한글 주석 사용
- 복잡한 로직은 반드시 주석 추가
- TODO 주석 형식: `// TODO: 설명`

## 주요 시스템 상세

### 1. Pet AI 시스템
- **중앙 제어**: `PetController.UpdateAI()`에서 모든 행동 결정
- **행동 선택**: 우선순위 기반 (`_currentAction = GetHighestPriorityAction()`)
- **상태 관리**: 각 Action이 자체적으로 상태 관리

### 2. 감정 표현 시스템
- **매니저**: `EmotionManager.cs`
- **감정 타입**: `EmotionType` enum (Happy, Sad, Angry, Sleepy 등)
- **표현 방식**: 
  - 말풍선 (EmotionBubble)
  - 파티클 효과
  - 커스텀 위치 (`emotionOrigin` Transform)

### 3. 식성 시스템
- **DietaryFlags**: Flags 열거형으로 복합 식성 표현 가능
- **음식 타입**: `FoodType` enum
- **호환성 체크**: `PetFeedingController.CanEatFood()`

### 4. 특수 기능
- **나무 오르기**: `treeClimbChance` 확률 기반
- **물 속 행동**: `waterSinkDepth`로 깊이 조절
- **모임 행동**: `GatherAction`으로 그룹 형성

## 디버깅 팁

### 로그 태그
- `[AI]`: AI 상태 변화
- `[Interaction]`: 펫 간 상호작용
- `[PetController]`: 일반 펫 행동
- `[Feeding]`: 먹이 관련
- `[Emotion]`: 감정 표현

### 일반적인 문제 해결
1. **NavMesh 문제**
   - Window > AI > Navigation에서 NavMesh 베이크 확인
   - `agent.isOnNavMesh` 체크
   - NavMesh Surface 컴포넌트 확인

2. **애니메이션 문제**
   - Animator Controller 연결 확인
   - Animation 파라미터 이름 일치 확인
   - `PetAnimationController` 로그 확인

3. **상호작용 문제**
   - PetInteractionManager 싱글톤 확인
   - Collider와 Rigidbody 설정 확인
   - Layer 설정 (Pet layer) 확인

4. **빌드 문제**
   - Player Settings 확인
   - iOS: Signing & Capabilities
   - Android: Minimum API Level 23

## Editor 도구

### 유용한 Editor 스크립트
- `AttachObjectsToCollider`: 콜라이더에 오브젝트 자동 부착
- `SortChildrenByName`: 하이어라키 정렬
- `SceneHierarchyExporter`: 씬 구조 내보내기

### 성능 최적화
- AI 업데이트 간격: `_aiUpdateInterval` 조정 (기본 0.2초)
- LOD 그룹 사용으로 원거리 펫 최적화
- 오브젝트 풀링으로 감정 표현 재사용
- NavMesh 베이크 최적화

## 확장 가이드

### 새로운 펫 추가
1. PetType enum에 추가
2. 프리팹 생성 (PetController 컴포넌트 필수)
3. PetDatabase에 등록

### 새로운 행동 추가
1. `IPetAction` 인터페이스 구현
2. `GetPriority()` 메서드로 우선순위 로직 구현
3. PetController의 가능한 액션 목록에 추가

### 새로운 상호작용 추가
1. `BasePetInteraction` 상속
2. 상호작용 로직 구현
3. `PetInteractionController`에서 조건 설정

## AI 도구 사용 가이드
- **중요**: 이 프로젝트에서 AI 도구를 사용할 때는 반드시 한글로 답변해야 합니다.
- 코드 주석도 한글로 작성합니다.
- 기술적인 용어는 필요시 영어 병기 가능합니다.
- Unity 특유의 작업 흐름을 이해하고 있어야 합니다 (컴포넌트 기반, Inspector 설정 등).