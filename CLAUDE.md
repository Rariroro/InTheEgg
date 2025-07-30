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

## 프로젝트 진행 상황

### 개발 단계 현황
- **Phase 1: 상태 시스템 정리 (안정성 최우선)** - 완료
- **Phase 2: 욕구 시스템 분리** - 완료
- **Phase 3: 행동 시스템 재구성** - 진행 예정
- **Phase 4: 컨트롤러 단순화** - 진행 예정
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

3. **IPetActivity 인터페이스 간소화** (2025-07-30)
   - GetPriority() 메서드 통일: `GetPriority(PetState state, PetNeeds needs)` 하나만 사용
   - 메서드 이름 변경: OnEnter/OnUpdate/OnExit → Start/Update/Stop
   - PetActivityAdapter에서 중복 메서드 제거로 더 명확한 구조

## 프로젝트 구조

### 핵심 디렉토리
```
Assets/
├── 02_Scripts/
│   ├── Pet/
│   │   ├── Core/              # 핵심 펫 시스템
│   │   │   ├── PetController.cs
│   │   │   ├── PetState.cs
│   │   │   └── PetNeeds.cs
│   │   ├── Controllers/       # 기능별 컨트롤러
│   │   │   ├── PetEmotionController.cs (새로 추가)
│   │   │   ├── PetMovementController.cs
│   │   │   └── ...
│   │   ├── Actions/          # AI 행동 정의
│   │   └── Interaction/      # 펫 간 상호작용
│   └── UI/                   # UI 관련 스크립트
└── Scenes/
    ├── PetChoice.unity
    └── PetVillage.unity
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
- **Action Pattern**: IPetAction 인터페이스 구현
- **자율적 의사결정**: 욕구, 환경, 상태에 따른 동적 행동 선택

#### 3. 상호작용 시스템
- **BasePetInteraction**: 모든 상호작용의 기본 클래스
- **다양한 상호작용**: 추격전, 경주, 놀이 등
- **상태 자동 관리**: 상호작용 시작/종료 시 상태 자동 전환

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
    
    public override bool CanStart(PetState state, PetNeeds needs) {
        // State 속성 사용
        if (state.IsHolding) return false;
        // 시작 가능 조건 체크
        return true;
    }
    
    public override float GetPriority(PetState state, PetNeeds needs) {
        // 우선순위 계산 로직
        return 1.0f;
    }
    
    public override void Start() {
        // 활동 시작 시 실행
    }
    
    public override void Update() {
        // 매 프레임 실행
    }
    
    public override void Stop() {
        // 활동 종료 시 실행
    }
}
```

### 주의사항
1. **직접 속성 접근 금지**: `pet.isHolding` (X) → `pet.State.IsHolding` (O)
2. **PetController 수정 자제**: 기능 추가 시 별도 컨트롤러 생성 고려
3. **상호작용 로직**: Interactions 폴더의 기존 구현은 수정하지 않음

## 테스트 및 디버깅

### 일반 명령어
```bash
# Unity 에디터 실행 (Mac)
open -n -a "Unity Hub"

# 프로젝트 빌드 (Unity CLI 필요)
Unity -batchmode -quit -projectPath . -buildTarget StandaloneOSX
```

### 디버깅 팁
1. **상태 확인**: PetState의 CurrentStatus 로그 출력
2. **AI 디버깅**: UpdateAI() 메서드에 브레이크포인트 설정
3. **상호작용 디버깅**: BasePetInteraction의 로그 확인

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

## 참고 문서
- PET_SYSTEM_REFACTORING_PLAN.md: 전체 리팩토링 계획
- Unity 공식 문서: https://docs.unity3d.com/

---

*이 문서는 Claude Code와의 효율적인 협업을 위해 작성되었습니다.*