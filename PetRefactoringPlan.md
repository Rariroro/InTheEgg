# 펫 시스템 리팩토링 상세 계획

## 개요
펫 관련 코드의 복잡성을 줄이고 유지보수성을 높이기 위한 단계별 리팩토링 계획입니다.
핵심 원칙: **단순함, 통합, 직관성**

---

## 1단계: 상태 관리 통합 (Phase 4)

### 현재 문제점
```csharp
// PetController에 중복된 상태 관리
public bool isInteracting = false;
public bool isSelected = false;
public bool isHolding = false;
public bool isClimbingTree = false;
public bool isInWater = false;
public bool isExhausted = false;
// ... 등 20개 이상의 개별 플래그

// 동시에 새로운 PetState 시스템도 존재
private PetState petState = new PetState();
```

### 개선 방안

#### 1.1 모든 플래그를 PetState로 이관
```csharp
// 기존 플래그 제거하고 PetState의 프로퍼티로 접근
public class PetController : MonoBehaviour
{
    [SerializeField] private PetState state = new PetState();
    
    // 기존 플래그들을 프로퍼티로 변환 (호환성 유지)
    public bool isInteracting => state.IsInteracting;
    public bool isSelected => state.IsSelected;
    public bool isHolding => state.IsHolding;
    // ...
}
```

#### 1.2 상태 전환 로직 중앙화
```csharp
// 모든 상태 변경을 PetState를 통해서만 수행
public void SetSelected(bool selected)
{
    if (selected)
        state.SetPlayerControl(holding: false, selected: true);
    else if (!state.IsHolding)
        state.TrySetStatus(PetStatus.Idle);
}
```

#### 1.3 단계별 진행
1. **준비**: 기존 코드에서 플래그 직접 수정하는 부분 찾기
2. **이관**: 각 플래그를 PetState 메서드로 변경
3. **테스트**: 각 기능별로 동작 확인
4. **정리**: 중복 플래그 제거

### 예상 결과
- 상태 관리 일원화로 버그 감소
- 상태 전환 규칙이 명확해짐
- 디버깅이 쉬워짐

---

## 2단계: Activity 시스템 단순화 (Phase 5)

### 현재 문제점
```csharp
// 복잡한 인터페이스 구조
IPetActivity → PetActivityAdapter → 각 Activity 클래스
// 10개의 Activity가 각각 별도 파일에 분산
// 총 300줄 이상의 보일러플레이트 코드
```

### 개선 방안

#### 2.1 Activity를 내부 클래스로 통합
```csharp
public partial class PetController : MonoBehaviour
{
    // PetController.Activities.cs 파일에 모든 Activity 통합
    private abstract class BaseActivity
    {
        protected PetController pet;
        public abstract string Name { get; }
        public abstract float Priority { get; }
        public abstract void Execute();
    }
    
    private class WanderActivity : BaseActivity
    {
        public override string Name => "Wander";
        public override float Priority => 0.1f;
        public override void Execute()
        {
            // 간단한 배회 로직
            pet.SetRandomDestination();
        }
    }
    
    private class EatActivity : BaseActivity
    {
        public override string Name => "Eat";
        public override float Priority => pet.hunger > 70f ? 0.9f : 0f;
        public override void Execute()
        {
            // 음식 찾기 로직
            pet.feedingCore.SeekFood();
        }
    }
}
```

#### 2.2 Activity 선택 로직 단순화
```csharp
private void UpdateActivity()
{
    // 가장 높은 우선순위 Activity 선택
    BaseActivity bestActivity = null;
    float maxPriority = 0f;
    
    foreach (var activity in activities)
    {
        if (activity.Priority > maxPriority)
        {
            maxPriority = activity.Priority;
            bestActivity = activity;
        }
    }
    
    currentActivity = bestActivity;
    currentActivity?.Execute();
}
```

#### 2.3 통합 후 구조
```
PetController.cs (메인)
PetController.Activities.cs (모든 Activity)
PetController.Helpers.cs (헬퍼 메서드)
```

### 예상 결과
- 코드 라인 50% 감소
- 한 파일에서 모든 행동 파악 가능
- 새 Activity 추가가 간단해짐

---

## 3단계: 컨트롤러 통합

### 현재 문제점
```csharp
// 8개의 분산된 컨트롤러
PetMovementController
PetAnimationController
PetInteractionController  // 이름이 혼동됨 (플레이어 상호작용)
PetFeedingController
PetSleepingController
PetWaterBehaviorController
PetTreeClimbingController
PetEffects
```

### 개선 방안

#### 3.1 4개의 핵심 컨트롤러로 통합

##### **PetBehaviorCore** (기본 행동)
```csharp
public class PetBehaviorCore : MonoBehaviour
{
    private PetController pet;
    
    // Movement (PetMovementController 통합)
    public void SetDestination(Vector3 position) { }
    public void SetRandomDestination() { }
    public void StopMovement() { }
    
    // Animation (PetAnimationController 통합)
    public void PlayAnimation(AnimationType type) { }
    public void UpdateMovementAnimation() { }
    
    // 기본 상태 업데이트
    public void Update()
    {
        UpdateMovementAnimation();
        HandleRotation();
    }
}
```

##### **PetNeedsCore** (욕구 관리)
```csharp
public class PetNeedsCore : MonoBehaviour
{
    // PetNeeds + FeedingController + SleepingController 통합
    
    [Header("욕구 상태")]
    public float hunger;
    public float sleepiness;
    public float affection;
    
    // 통합된 욕구 처리
    public void UpdateNeeds() { }
    public void HandleFeeding() { }
    public void HandleSleeping() { }
    
    // 간단한 인터페이스
    public bool IsHungry => hunger > 70f;
    public bool IsSleepy => sleepiness > 70f;
}
```

##### **PetEnvironmentCore** (환경 상호작용)
```csharp
public class PetEnvironmentCore : MonoBehaviour
{
    // WaterBehaviorController + TreeClimbingController 통합
    
    public void CheckEnvironment() { }
    public void HandleWaterBehavior() { }
    public void HandleTreeClimbing() { }
    
    // 환경 상태
    public bool IsInSpecialEnvironment => 
        pet.state.IsInWater || pet.state.IsClimbingTree;
}
```

##### **PetPlayerCore** (플레이어 상호작용)
```csharp
public class PetPlayerCore : MonoBehaviour  
{
    // 기존 PetInteractionController를 명확한 이름으로
    
    public void HandlePlayerInput() { }
    public void HandlePicking() { }
    public void HandleSelection() { }
    public void HandleGathering() { }
}
```

#### 3.2 PetController 구조 개선
```csharp
public class PetController : MonoBehaviour
{
    // 상태
    [SerializeField] private PetState state;
    
    // 핵심 컴포넌트
    private PetBehaviorCore behaviorCore;
    private PetNeedsCore needsCore;
    private PetEnvironmentCore environmentCore;
    private PetPlayerCore playerCore;
    
    // 간단한 Update
    void Update()
    {
        if (state.CurrentStatus == PetStatus.PlayerControl)
        {
            playerCore.HandlePlayerInput();
            return;
        }
        
        needsCore.UpdateNeeds();
        environmentCore.CheckEnvironment();
        UpdateActivity(); // Activity 선택
        behaviorCore.Update(); // 애니메이션/이동
    }
}
```

### 예상 결과
- 컨트롤러 수 50% 감소 (8개 → 4개)
- 각 컨트롤러의 역할이 명확
- 코드 탐색이 쉬워짐

---

## 실행 계획

### 우선순위 및 일정
1. **1단계 (상태 관리)**: 1-2일 - 가장 중요하고 안전
2. **2단계 (Activity)**: 1일 - 코드 가독성 크게 개선
3. **3단계 (컨트롤러)**: 2-3일 - 가장 큰 변화

### 안전한 진행 방법
1. 각 단계별로 **기능 테스트 목록** 작성
2. **한 번에 하나씩** 변경하고 테스트
3. 기존 코드는 주석 처리로 **백업 유지**
4. 각 단계 완료 후 **Git 커밋**

### 성공 지표
- ✅ 버그 없이 기존 기능 모두 동작
- ✅ 코드 라인 30% 이상 감소
- ✅ 새 기능 추가 시간 50% 단축
- ✅ 디버깅 시간 대폭 감소

---

## 주의사항
- **호환성 유지**: 다른 시스템에서 참조하는 public 메서드는 유지
- **점진적 개선**: 한 번에 모든 것을 바꾸지 않음
- **테스트 우선**: 각 변경사항마다 충분한 테스트
- **문서화**: 변경사항을 CLAUDE.md에 업데이트