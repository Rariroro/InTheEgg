# 펫 상태 관리 개선 제안

## 현재 문제점 분석

### 1. 특수 플래그들의 과도한 사용
현재 시스템에서는 다음과 같은 특수 플래그들이 존재합니다:
- `isActionLocked`: 전역적인 행동 잠금 (나무 오르기, 수면 등)
- `isAnimationLocked`: 전역적인 애니메이션 잠금
- `isGatheringAnimationOverride`: 모이기 애니메이션 우선순위 제어

이러한 플래그들은 서로 간섭하며 예측 불가능한 상태를 만들어냅니다.

### 2. 우선순위 체계의 불명확성
```csharp
// 현재 Update 메서드의 복잡한 조건문
if ((petState.IsPlayerControlled && !isSelected) || isActionLocked) return;
if (!isGatheringAnimationOverride) { animationController?.UpdateAnimation(); }
```

어떤 플래그가 우선순위가 높은지 코드만으로는 파악하기 어렵습니다.

## 개선 방안

### 방안 1: 상태 우선순위 시스템 도입

```csharp
public enum StatePriority
{
    Normal = 0,        // 일반 활동 (Wander, Idle 등)
    Environmental = 1, // 환경 상호작용 (물, 나무)
    Social = 2,        // 펫 간 상호작용
    Need = 3,          // 욕구 기반 행동 (먹기, 자기)
    Command = 4,       // 플레이어 명령 (모이기)
    Emergency = 5,     // 긴급 상황 (벌 공격, 탈진)
    PlayerControl = 6  // 플레이어 직접 제어 (들기, 선택)
}

public class PetState
{
    private StatePriority currentPriority = StatePriority.Normal;
    
    public bool TrySetStatus(PetStatus newStatus, StatePriority priority)
    {
        // 우선순위가 높거나 같은 경우에만 상태 전환 허용
        if (priority >= currentPriority)
        {
            currentStatus = newStatus;
            currentPriority = priority;
            return true;
        }
        return false;
    }
}
```

### 방안 2: 액션 잠금을 Activity별로 분리

```csharp
public abstract class PetActivity
{
    // 각 Activity가 자신의 잠금 상태를 관리
    public virtual bool BlocksOtherActivities => false;
    public virtual bool BlocksAnimation => false;
    public virtual bool BlocksMovement => false;
    public virtual bool BlocksPlayerControl => false;
}

// 예시: 나무 오르기
public class ClimbTreeActivity : PetActivity
{
    public override bool BlocksOtherActivities => true; // 다른 활동 차단
    public override bool BlocksMovement => true; // 이동 차단
    public override bool BlocksPlayerControl => false; // 플레이어는 여전히 선택 가능
}
```

### 방안 3: 애니메이션 우선순위 시스템

```csharp
public class PetAnimationController
{
    private class AnimationRequest
    {
        public PetAnimationType type;
        public int priority;
        public float duration;
    }
    
    private AnimationRequest currentAnimation;
    
    public bool TryPlayAnimation(PetAnimationType type, int priority)
    {
        if (currentAnimation == null || priority >= currentAnimation.priority)
        {
            currentAnimation = new AnimationRequest { type = type, priority = priority };
            return true;
        }
        return false;
    }
}
```

### 방안 4: 통합 상태 머신 패턴

```csharp
public interface IPetState
{
    void Enter(PetController pet);
    void Update(PetController pet);
    void Exit(PetController pet);
    bool CanTransitionTo(IPetState newState);
    int Priority { get; }
}

public class PetStateMachine
{
    private IPetState currentState;
    private Dictionary<Type, IPetState> states = new Dictionary<Type, IPetState>();
    
    public bool TryChangeState<T>() where T : IPetState
    {
        var newState = states[typeof(T)];
        if (currentState.CanTransitionTo(newState) && newState.Priority >= currentState.Priority)
        {
            currentState.Exit(pet);
            currentState = newState;
            currentState.Enter(pet);
            return true;
        }
        return false;
    }
}
```

## 권장 구현 순서

1. **단기 개선 (최소 변경)**
   - `isActionLocked`를 Activity별로 분리
   - `isAnimationLocked`와 `isGatheringAnimationOverride`를 우선순위 시스템으로 대체
   - 명확한 우선순위 문서화

2. **중기 개선 (부분 리팩토링)**
   - StatePriority 열거형 도입
   - 각 Activity에 우선순위 부여
   - 충돌 해결 로직 중앙화

3. **장기 개선 (전체 리팩토링)**
   - 완전한 상태 머신 패턴 도입
   - 모든 특수 플래그 제거
   - 상태 전환 규칙의 명시적 정의

## 예상 효과

1. **명확한 우선순위**: 어떤 행동이 다른 행동을 덮어쓸 수 있는지 명확
2. **충돌 방지**: 상태 간 충돌이 시스템적으로 방지됨
3. **유지보수성 향상**: 새로운 상태 추가 시 기존 코드 수정 최소화
4. **디버깅 용이**: 상태 전환 로그를 통한 문제 추적 가능

## 결론

현재 시스템은 PetState 클래스 도입으로 많이 개선되었지만, 여전히 특수 플래그들(`isActionLocked`, `isAnimationLocked` 등)이 남아있어 복잡성을 유발합니다. 

단기적으로는 이러한 플래그들을 Activity별로 분리하고, 장기적으로는 완전한 상태 머신 패턴을 도입하는 것을 권장합니다.