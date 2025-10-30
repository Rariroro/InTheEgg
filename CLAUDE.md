# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

# InTheEgg - AI 협업 가이드
- 한글로 소통
- **중요: 무조건 동의하지 말고, 실제 최적의 방법을 고려해서 대답하기**
- **사용자 제안이 최선이 아니면 더 나은 대안 제시하기**
- **기술적으로 잘못된 부분은 명확히 지적하기**

## 프로젝트 개요
Unity 기반의 펫 시뮬레이션 게임으로, 다양한 동물 펫들이 AI를 통해 자율적으로 행동하고 상호작용하는 프로젝트입니다. Unity 6000.0 버전을 사용하며 Universal Render Pipeline(URP)로 구성되어 있습니다.

### 게임 플로우
1. **PetChoice 씬**: 플레이어가 펫, 환경, 아이템을 선택하는 초기 설정 화면
2. **PetVillage 씬**: 선택한 펫들이 AI로 자율 행동하며 생활하는 메인 게임 씬

### 주요 특징
- 다중 펫 선택 시스템 (토글 기반 UI)
- 레전드 펫 시스템 (드래곤 11종, 유니콘 10종)
- 환경 커스터마이징 (다양한 환경 오브젝트 선택 가능)
- 아이템 시스템 (펫들이 상호작용할 수 있는 아이템)
- 식성 시스템 (DietaryFlags - 복수 선택 가능한 Flags 열거형)
- 특수 행동 시스템 (나무 오르기, 물 속 행동, 보물 찾기 등)

## 프로젝트 진행 상황

### 개발 단계 현황
- **Phase 1: 상태 시스템 정리 (안정성 최우선)** - 완료
- **Phase 2: 욕구 시스템 분리** - 완료
- **Phase 3: 행동 시스템 재구성** - 완료 (IPetActivity 인터페이스 도입, PetActivityAdapter 구현)
- **Phase 4: 컨트롤러 단순화** - 진행 중 (PetEmotionController 등 분리 완료)
- **Phase 5: 이벤트 시스템 도입** - 진행 예정

### 최근 리팩토링 내용 (Phase 3)
1. **PetController 책임 분리**
   - PetEmotionController 생성: 감정 표현 관련 로직 분리
   - 구식 속성 제거: 직접 속성 접근 대신 State 속성 사용
   - 코드 라인 수: 852줄 → 더 깔끔한 구조

2. **상태 접근 패턴 통일**
   - 이전: `pet.isHolding`, `pet.isSelected`
   - 이후: `pet.State.IsHolding`, `pet.State.IsSelected`
   - 모든 Activity와 Controller 클래스에 적용

3. **IPetActivity 인터페이스 간소화**
   - GetPriority() 메서드 통일: `GetPriority(PetState state, PetNeeds needs)` 하나만 사용
   - 메서드 이름 변경: OnEnter/OnUpdate/OnExit → Start/Update/Stop
   - PetActivityAdapter에서 중복 메서드 제거로 더 명확한 구조
   - IsInterruptible, IsComplete 속성 추가로 활동 중단 및 완료 상태 관리

## 프로젝트 구조

### 핵심 디렉토리
```
Assets/
├── 01_Scenes/                # 게임 씬 파일들
│   ├── PetChoice.unity       # 펫/환경/아이템 선택 씬
│   └── PetVillge.unity       # 메인 게임플레이 씬
├── 02_Scripts/
│   ├── Pet/
│   │   ├── Core/             # 핵심 펫 시스템
│   │   │   ├── PetState.cs
│   │   │   ├── PetNeeds.cs
│   │   │   ├── PetAI.cs
│   │   │   ├── PetTraits.cs
│   │   │   └── IPetActivity.cs
│   │   ├── PetController.cs  # 메인 펫 컨트롤러 (partial class)
│   │   ├── Controllers/      # 기능별 컨트롤러
│   │   │   ├── PetEmotionController.cs
│   │   │   ├── PetMovementController.cs
│   │   │   ├── PetAnimationController.cs
│   │   │   ├── PetFeedingController.cs
│   │   │   ├── PetTreeClimbingController.cs
│   │   │   └── PetWaterBehaviorController.cs
│   │   ├── Activities/       # AI 활동 구현체들
│   │   │   ├── Basic/        # 기본 활동 (Wander, ClimbTree, Diving)
│   │   │   ├── Needs/        # 욕구 관련 (Eat, Sleep, Exhausted)
│   │   │   ├── Social/       # 사회적 활동 (Interact, Gather, TreasureHunt, TreasureFound)
│   │   │   ├── Environment/  # 환경 활동 (Butterfly, EnvironmentGather)
│   │   │   └── Emergency/    # 긴급 활동 (BeeEscape)
│   │   ├── Interaction/      # 펫 간 상호작용
│   │   └── Data/             # 펫 데이터 정의
│   ├── LegendaryPet/         # 레전드 펫 시스템
│   │   ├── LegendaryPetController.cs
│   │   ├── LegendaryPetAI.cs
│   │   ├── LegendaryPetManager.cs
│   │   └── LegendaryPetSelectionManager.cs
│   ├── Manager/              # 게임 매니저들
│   │   ├── TreasureHuntManager.cs
│   │   ├── EmotionManager.cs
│   │   └── PetManager.cs
│   └── UI/                   # UI 관련 스크립트
├── 03_Prefabs/               # 프리팹들
│   ├── Pet/                  # 펫 프리팹 (50+ 종류)
│   ├── Environments/         # 환경 프리팹
│   └── Food/                 # 음식 프리팹
└── 09_GameDatas/
    └── PetDataDatabase.asset # 모든 펫 데이터 중앙 저장소
```

### 주요 시스템 설명

#### 1. 상태 시스템 (PetState)
```csharp
public enum PetStatus {
    Idle,           // 기본 상태
    PlayerControl,  // 플레이어가 제어 중
    Interacting,    // 다른 펫과 상호작용
    Environmental,  // 환경과 상호작용
    Emergency,      // 긴급 상태
    Gathering       // 모이기 명령 수행 중
}
```

#### 2. AI 행동 시스템
- **우선순위 기반**: 0.5초마다 모든 가능한 행동의 우선순위 계산
- **Activity Pattern**: IPetActivity 인터페이스 구현 (기존 IPetAction에서 변경)
- **자율적 의사결정**: 욕구, 환경, 상태에 따른 동적 행동 선택
- **중단 불가능한 활동**: IsInterruptible=false인 활동은 우선순위 50 미만일 때 중단 불가
- **활동 완료 감지**: IsComplete=true가 되면 자동으로 활동 종료 후 새 활동 선택

#### 3. 상호작용 시스템
- **BasePetInteraction**: 모든 펫 간 상호작용의 기본 클래스
- **다양한 상호작용**: 추격전, 경주, 놀이, 싸움, 함께 걷기, 함께 자기 등
- **상태 자동 관리**: 상호작용 시작/종료 시 PetStatus 자동 전환

#### 4. 레전드 펫 시스템
- **LegendaryPetManager**: 레전드 펫 생성/관리 싱글톤 매니저
- **드래곤 11종**: Amber, Blossom, Cloud, Ocean, Peach, Snow, Spring, Star, Storm, Sunset, Volcano
- **유니콘 10종**: Dream, Mint, Night, Prism, Pure, Rose, Shadow, Sky, Terra, Twin
- **5단계 스폰 시스템**: Gift 생성 → 펫 등장 → 비행 경로 → 착륙 → 선물 오픈
- **특별 연출**: 최초 등장 효과, 불꽃놀이, 축하 파티클 등

#### 5. 보물 찾기 시스템
- **TreasureHuntManager**: 보물 찾기 이벤트 관리
- **TreasureHuntActivity**: 보물 탐색 활동 (우선순위 15)
- **TreasureFoundActivity**: 보물 발견 후 이동 (우선순위 20)
- **treasureHoldPoint**: 펫이 보물을 물고 갈 위치 (Transform)

## 개발 가이드라인

### 코드 컨벤션
1. **상태 접근**: 항상 `pet.State.속성명` 패턴 사용
2. **감정 표현**: `pet.ShowEmotion()` 메서드 사용 (PetEmotionController가 처리)
3. **null 체크**: 펫 관련 작업 시 항상 null 체크 수행
4. **코루틴 사용**: 시간이 걸리는 작업은 코루틴으로 구현

### 새로운 Activity 추가 시
```csharp
public class NewActivity : PetActivityAdapter {
    public override string Name => "NewActivity";

    // 중단 불가능한 활동으로 만들려면 false로 설정 (기본값: true)
    public override bool IsInterruptible => true;

    // 활동이 완료되었는지 체크 (기본값: false)
    public override bool IsComplete => isComplete;
    private bool isComplete = false;

    public NewActivity(PetController petController) : base(petController) { }

    public override bool CanStart(PetState state, PetNeeds needs) {
        // State 속성 사용
        if (state.IsHolding) return false;
        // 시작 가능 조건 체크
        return true;
    }

    public override float GetPriority(PetState state, PetNeeds needs) {
        // 우선순위 계산 로직
        // 긴급: 50+, 높음: 20-49, 중간: 10-19, 낮음: 1-9
        return 1.0f;
    }

    public override void Start() {
        // 활동 시작 시 실행
        isComplete = false;
    }

    public override void Update() {
        // 매 프레임 실행

        // 작업 완료 시
        if (/* 완료 조건 */) {
            isComplete = true;
        }
    }

    public override void Stop() {
        // 활동 종료 시 실행 (정리 작업)
    }
}
```

### 주의사항
1. **직접 속성 접근 금지**: `pet.isHolding` (X) → `pet.State.IsHolding` (O)
2. **PetController는 partial class**: 분리된 파일에서 확장 가능
3. **컨트롤러 단일 책임**: 기능 추가 시 별도 컨트롤러 생성 고려
4. **상호작용 로직**: Interactions 폴더의 기존 구현은 수정하지 않음
5. **Activity 등록**: 새로운 Activity는 PetAI.RegisterActivities()에 등록 필요

## 개발 명령어

### Unity 에디터 실행
```bash
# Mac에서 Unity Hub 실행
open -n -a "Unity Hub"

# Unity 프로젝트 직접 열기 (Unity 6000.0 필요)
Unity -projectPath /Users/rariroro/Documents/Unity/InTheEgg
```

### 빌드 명령어
```bash
# Mac 빌드
Unity -batchmode -quit -projectPath . -buildTarget StandaloneOSX

# iOS 빌드
Unity -batchmode -quit -projectPath . -buildTarget iOS

# Android 빌드
Unity -batchmode -quit -projectPath . -buildTarget Android
```

### 디버깅 팁
1. **상태 확인**: PetState의 CurrentStatus 로그 출력
2. **AI 디버깅**: PetAI.UpdateAI() 메서드에 브레이크포인트 설정
3. **상호작용 디버깅**: BasePetInteraction의 로그 확인
4. **Activity 디버깅**: IPetActivity 구현체의 CanStart, GetPriority 메서드 확인

## 향후 계획

### Phase 3: 행동 시스템 재구성
- Action → Activity 리네이밍
- 책임 명확화
- 새로운 인터페이스 설계

### Phase 4: 컨트롤러 단순화
- 각 컨트롤러가 하나의 기능만 담당
- PetMovement, PetAnimator, PetSensor 등으로 분리

### Phase 5: 이벤트 시스템
- 컴포넌트 간 직접 참조 제거
- 이벤트 버스 패턴 도입

## 핵심 아키텍처 패턴

### Activity 시스템 (행동 시스템)
- **IPetActivity 인터페이스**: 모든 펫 행동의 기본 계약
  - `bool CanStart(PetState, PetNeeds)`: 활동 시작 가능 여부
  - `float GetPriority(PetState, PetNeeds)`: 우선순위 계산 (긴급 50+, 높음 20-49, 중간 10-19, 낮음 1-9)
  - `Start/Update/Stop`: 활동 생명주기 메서드
  - `bool IsInterruptible`: 중단 가능 여부 (false면 우선순위 50 미만에서는 중단 불가)
  - `bool IsComplete`: 활동 완료 여부 (true가 되면 자동 종료)
- **PetActivityAdapter**: Activity 구현을 위한 추상 클래스 (생성자에 PetController 필요)
- **우선순위 기반 선택**: 0.5초마다 모든 활동의 우선순위를 계산하여 최적 행동 선택
- **상태와 욕구 분리**: PetState (상태)와 PetNeeds (욕구)를 독립적으로 관리
- **PetAI.RegisterActivities()**: 모든 Activity를 등록하는 메서드 (새 Activity 추가 시 수정 필요)

### 컨트롤러 패턴
- **PetController**: 메인 컨트롤러 (partial class로 확장 가능)
  - PetProfile, MovementSettings 관리
  - PetState, PetNeeds 참조 제공
  - 모든 하위 컨트롤러 통합
- **단일 책임 원칙**: 각 컨트롤러는 하나의 기능만 담당
  - PetMovementController: 이동 및 NavMeshAgent 제어
  - PetAnimationController: 애니메이션 상태 관리
  - PetEmotionController: 감정 표현 및 이모티콘 표시
  - PetFeedingController: 먹이 탐색 및 섭취
  - PetSleepingController: 수면 관리
  - PetTreeClimbingController: 나무 오르기 행동
  - PetWaterBehaviorController: 물 속 행동 및 깊이 제어

### 상호작용 시스템
- **BasePetInteraction**: 모든 펫 간 상호작용의 기본 클래스
- **다양한 상호작용 유형**: Chase, Fight, Race, Walk Together 등
- **자동 상태 관리**: 상호작용 시작/종료 시 PetStatus 자동 전환

## 중요한 타입 별칭 및 호환성
```csharp
// 리팩토링 과정에서 유지되는 타입 별칭
using PetAIProperties = PetTraits;  // 기존 코드 호환성 유지
```

## 데이터 관리
- **PetData**: ScriptableObject 기반 펫 데이터 정의
- **PetDataDatabase**: 모든 펫 데이터를 중앙 관리 (Assets/09_GameDatas/PetDataDatabase.asset)
- **PetProfile**: 런타임 펫 프로필 관리 (이름, 생일, 성격, 식성, 서식지 등)
- **PetTraits**: 펫의 성격과 특성 정의
  - `Personality`: Shy, Brave, Lazy, Playful
  - `DietaryFlags`: Flags 열거형으로 복수 선택 가능 (SeedsAndGrains, FruitsAndVegetables, Grass, Honey, Meat, Fish)
  - `Habitat`: Water, Forest, Field, Fence, Tree
  - `Size`: Small, Medium, Large (CapsuleCollider radius 기준)

## 씬 전환 및 데이터 유지
- **PetSelectionManager**: PetChoice 씬에서 선택한 일반 펫 정보 관리 (싱글톤)
- **LegendaryPetSelectionManager**: 레전드 펫 선택 정보 관리 (싱글톤)
- **EnvironmentSelectionManager**: 환경 선택 정보 관리 (싱글톤)
- **ItemSelectionManager**: 아이템 선택 정보 관리 (싱글톤)
- 씬 전환 시 DontDestroyOnLoad로 선택 정보 유지

## 참고 문서
- Unity 공식 문서: https://docs.unity3d.com/6000.0/
- URP 문서: https://docs.unity3d.com/Packages/com.unity.render-pipelines.universal@latest

---

*이 문서는 Claude Code와의 효율적인 협업을 위해 작성되었습니다.*