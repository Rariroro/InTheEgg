# 펫 시스템 리팩토링 계획

## 📋 목차
1. [현재 시스템 분석](#현재-시스템-분석)
2. [주요 문제점](#주요-문제점)
3. [새로운 아키텍처 설계](#새로운-아키텍처-설계)
4. [단계별 리팩토링 계획](#단계별-리팩토링-계획)
5. [설계 검증](#설계-검증)

---

## 현재 시스템 분석

### 전체 아키텍처
- **우선순위 기반 상태머신**: AI가 0.5초마다 모든 가능한 행동의 우선순위를 계산하여 최적 행동 선택
- **컴포넌트 시스템**: PetController가 중앙에서 각 기능별 컨트롤러(7개)를 관리
- **Action Pattern**: 모든 행동이 IPetAction 인터페이스를 구현하여 일관된 생명주기 관리

### 핵심 작동 흐름
```
1. Awake() → 초기화
   - NavMeshAgent, Animator 설정
   - 모든 컨트롤러 AddComponent로 동적 추가
   - 행동 리스트 초기화 (InitializeActions)
   - NavMesh 위치 확인

2. Update() → 매 프레임 실행
   - 플레이어 입력 처리 (isHolding 체크)
   - 욕구 업데이트 (배고픔, 졸림, 친밀도)
   - AI 의사결정 (0.5초마다)
   - 현재 행동 실행
   - 애니메이션 업데이트

3. UpdateAI() → AI 의사결정
   - 모든 Action의 GetPriority() 호출
   - 최고 우선순위 Action 선택
   - 행동 전환 (OnExit → OnEnter)
```

### 우선순위 시스템
- **50.0f**: ExhaustedAction (탈진 상태)
- **20.0f**: GatherAction (모이기 명령)
- **15.0f**: EnvironmentGatherAction
- **10.0f**: InteractWithPetAction
- **5.0f**: SelectedAction
- **0.2~2.0f**: EatAction, SleepAction (욕구 기반)
- **0.1f**: WanderAction (기본 행동)

---

## 주요 문제점

### 1. 책임 분리 문제
- **PetController가 너무 많은 책임**: 852줄의 거대한 클래스
  - AI 의사결정
  - 상태 관리 (40개 이상의 public 변수)
  - 욕구 시스템
  - 감정 표현
  - 회전 처리
  - 다양한 플래그 관리

### 2. 상태 관리의 복잡성
```csharp
// 서로 간섭하는 복잡한 플래그들
isHolding, isSelected, isInteracting, isGathering, 
isClimbingTree, isInWater, isExhausted, isActionLocked,
isGatheringAnimationOverride, isAnimationLocked...
```
- 플래그들이 서로 영향을 주며 예측 불가능한 상태 생성
- 어떤 플래그가 우선순위가 높은지 불명확

### 3. Action과 Controller의 경계 모호
- **Action**: AI 의사결정 로직
- **Controller**: 실제 동작 수행
- 하지만 Action이 Controller를 직접 호출하여 책임이 섞임

### 4. 의존성 문제
- 모든 컴포넌트가 PetController에 강하게 결합
- Controller들이 서로를 알고 있어야 함
- 순환 참조 위험

### 5. 우선순위 시스템의 한계
- 하드코딩된 우선순위 값
- 복잡한 상황에서 예측 어려움
- 디버깅 힘듦

---

## 새로운 아키텍처 설계

### 핵심 설계 원칙
1. **단일 책임 원칙**: 각 클래스는 하나의 명확한 책임만
2. **의존성 역전**: 인터페이스를 통한 느슨한 결합
3. **명확한 상태 머신**: 예측 가능한 상태 전환
4. **이벤트 기반 통신**: 직접 참조 최소화

### 새로운 구조
```
PetBrain (중앙 의사결정)
    ├─ PetState (상태 관리)
    ├─ PetNeeds (욕구 시스템)
    └─ PetBehavior (행동 실행)
          ├─ Movement
          ├─ Animation
          ├─ Interaction
          └─ Environment
```

### 주요 컴포넌트 재설계

#### 1. PetBrain (기존 PetController 대체)
```csharp
// 오직 의사결정만 담당
public class PetBrain : MonoBehaviour {
    private PetState state;
    private PetNeeds needs;
    private PetBehavior behavior;
    private IPetActivity currentActivity;
    private List<IPetActivity> availableActivities;
}
```

#### 2. PetState (명확한 상태 관리)
```csharp
public enum PetStatus {
    Idle,           // 기본 상태
    PlayerControl,  // 플레이어가 제어 중
    Interacting,    // 다른 펫과 상호작용
    Environmental,  // 환경과 상호작용
    Emergency       // 긴급 상태 (탈진, 벌 공격 등)
}

public class PetState {
    public PetStatus CurrentStatus { get; private set; }
    public bool CanChangeActivity => CurrentStatus == PetStatus.Idle;
}
```

#### 3. PetActivity (Action 대체)
```csharp
// 더 직관적인 이름
public interface IPetActivity {
    string Name { get; }
    bool CanStart(PetState state, PetNeeds needs);
    void Start();
    void Update();
    void Stop();
    bool IsComplete { get; }
}
```

#### 4. PetBehavior (실제 동작 수행)
```csharp
public class PetBehavior : MonoBehaviour {
    private PetMovement movement;      // 이동만 담당
    private PetAnimator animator;      // 애니메이션만 담당
    private PetSensor sensor;          // 주변 감지만 담당
    private PetEffects effects;        // 시각 효과만 담당
}
```

---

## 단계별 리팩토링 계획

### Phase 1: 상태 시스템 정리 (안정성 최우선) ✅ 완료
**목표**: 복잡한 플래그들을 명확한 상태 머신으로 전환

```csharp
// Before: 복잡한 플래그 체크
if (!isHolding && !isActionLocked && !isInteracting && !isGathering) {
    // 행동 가능
}

// After: 명확한 상태 체크
if (petState.CanChangeActivity) {
    // 행동 가능
}
```

**완료된 작업**:
1. ✅ PetState 클래스 생성 (`Assets/02_Scripts/Pet/Core/PetState.cs`)
2. ✅ PetStatus enum 정의 (Idle, PlayerControl, Interacting, Environmental, Emergency, Gathering)
3. ✅ 상태 전환 규칙 명확화 (CanTransition 메서드)
4. ✅ PetController에 PetState 통합 및 SyncStateWithFlags 메서드 구현
5. ✅ 기존 플래그와 새 상태 시스템 병행 사용 (호환성 유지)

**Phase 1 구현 예시**:
- `SelectedAction.GetPriority()`: 새로운 상태 체크와 기존 플래그 체크 병행
- `PetController.UpdateAI()`: PlayerControl 상태 체크 추가
- `PetController.BeginInteraction()`: 상태 시스템에 즉시 반영

### Phase 2: 욕구 시스템 분리
**목표**: PetController에서 욕구 관리 로직 분리

```csharp
public class PetNeeds : MonoBehaviour {
    [SerializeField] private float hunger;
    [SerializeField] private float sleepiness;
    [SerializeField] private float affection;
    
    public event Action<NeedType, float> OnNeedChanged;
    
    public bool IsHungry => hunger > 70f;
    public bool IsSleepy => sleepiness > 70f;
}
```

### Phase 3: 행동 시스템 재구성
**목표**: Action을 Activity로 리네이밍하고 책임 명확화

```csharp
// 기존 Action들을 Activity로 변환
public class WanderActivity : IPetActivity {
    private PetMovement movement;
    
    public bool CanStart(PetState state, PetNeeds needs) {
        return state.CurrentStatus == PetStatus.Idle;
    }
}
```

### Phase 4: 컨트롤러 단순화
**목표**: 각 컨트롤러가 하나의 기능만 담당

```
PetMovement     - 이동만
PetAnimator     - 애니메이션만
PetSensor       - 감지만
PetEffects      - 이펙트만
PetInteractor   - 상호작용 입력만
```

### Phase 5: 이벤트 시스템 도입
**목표**: 컴포넌트 간 직접 참조 제거

```csharp
public class PetEventBus {
    public event Action<PetEvent> OnPetEvent;
    
    public void Publish(PetEvent petEvent) {
        OnPetEvent?.Invoke(petEvent);
    }
}
```

---

## 설계 검증

### 장점
1. **명확한 책임 분리**: 각 컴포넌트가 하나의 역할만 수행
2. **예측 가능한 상태**: 상태 머신으로 복잡한 플래그 제거
3. **확장 용이**: 새로운 Activity 추가가 간단
4. **디버깅 용이**: 각 시스템이 독립적으로 작동

### 잠재적 문제와 해결책

1. **성능 고려사항**
   - 문제: 이벤트 시스템 오버헤드
   - 해결: 중요한 통신은 직접 참조 유지

2. **기존 코드와의 호환성**
   - 문제: 대규모 변경으로 인한 버그 위험
   - 해결: 단계별 마이그레이션, 기존 인터페이스 유지

3. **복잡한 상호작용 처리**
   - 문제: 여러 펫이 동시에 상호작용
   - 해결: InteractionManager를 통한 중앙 관리

### 최종 구조 다이어그램
```
PetBrain (의사결정 센터)
    │
    ├─ PetState (상태 관리)
    │   ├─ CurrentStatus
    │   ├─ CanChangeActivity
    │   └─ StateTransitionRules
    │
    ├─ PetNeeds (욕구 시스템)
    │   ├─ Hunger
    │   ├─ Sleepiness
    │   └─ Affection
    │
    └─ PetBehavior (행동 실행)
        ├─ PetMovement (이동)
        ├─ PetAnimator (애니메이션)
        ├─ PetSensor (감지)
        ├─ PetEffects (시각효과)
        └─ PetInteractor (입력처리)

Activities (행동 정의)
    ├─ BasicActivities (기본)
    │   ├─ WanderActivity
    │   ├─ EatActivity
    │   └─ SleepActivity
    │
    ├─ InteractionActivities (상호작용)
    │   ├─ PlayWithPetActivity
    │   └─ FollowPlayerActivity
    │
    └─ EnvironmentActivities (환경)
        ├─ ClimbTreeActivity
        └─ SwimActivity
```

---

## 중요 참고사항

### 펫 상호작용 처리
- **상태 시스템**: 펫끼리 상호작용하는 트리거는 새로운 상태 시스템으로 관리
- **상호작용 로직**: `Interactions` 폴더 내의 상세 구현은 **수정하지 않음**
  - `BasePetInteraction` 클래스와 하위 구현체들은 그대로 유지
  - 상태 전환 부분만 새로운 시스템에 연결

### 핵심 개선사항 요약

1. **단순화된 의사결정**
   ```csharp
   // PetBrain의 핵심 로직
   void Update() {
       if (state.CanChangeActivity) {
           var bestActivity = SelectBestActivity();
           if (bestActivity != currentActivity) {
               ChangeActivity(bestActivity);
           }
       }
       currentActivity?.Update();
   }
   ```

2. **명확한 상태 전환**
   ```csharp
   // 상태는 오직 한 곳에서만 관리
   public void SetStatus(PetStatus newStatus) {
       if (CanTransition(CurrentStatus, newStatus)) {
           CurrentStatus = newStatus;
           OnStatusChanged?.Invoke(newStatus);
       }
   }
   ```

3. **느슨한 결합**
   - 모든 컴포넌트는 인터페이스를 통해 통신
   - 직접 참조 최소화
   - 이벤트를 통한 비동기 통신

이 설계는 **유지보수가 쉽고**, **이해하기 쉬우며**, **확장 가능한** 구조를 제공합니다.