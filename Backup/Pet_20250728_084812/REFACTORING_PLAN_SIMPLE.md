# 펫 시스템 심플 리팩토링 계획

## 🎯 핵심 원칙
1. **단순함 우선**: 복잡한 패턴보다 직관적인 구조
2. **최소 의존성**: 컴포넌트 간 느슨한 결합
3. **명확한 책임**: 각 클래스는 하나의 명확한 역할만

## 📋 리팩토링 단계

### Step 1: 파일 정리
**목표**: 깨끗한 시작점 확보

1. **삭제할 파일들**
   - `PetController_Improved.cs`
   - `PetControllerPatch.cs`
   - `PetEventSystemPatch.cs`
   - `_deprecated` 폴더 전체

2. **이동할 파일들**
   - `PetEventSystemExample.cs` → `Examples/`
   - `Core/Food.cs` → `Items/`

---

### Step 2: 핵심 구조 단순화
**목표**: 복잡한 시스템을 심플하게

#### 2.1 이벤트 시스템 단순화
```csharp
// PetEvents.cs - 단순한 static 클래스
public static class PetEvents
{
    // 핵심 이벤트만 3-4개
    public static event Action<PetController, PetStatus> OnStatusChanged;
    public static event Action<PetController, string> OnActivityChanged;
    public static event Action<PetController, EmotionType> OnEmotionShown;
    
    // 심플한 발행 메서드
    public static void StatusChanged(PetController pet, PetStatus newStatus)
    {
        OnStatusChanged?.Invoke(pet, newStatus);
    }
}
```

#### 2.2 Activity 시스템 단순화
```csharp
// IPetActivity.cs - 최소한의 인터페이스
public interface IPetActivity
{
    string Name { get; }
    float GetPriority();
    void Execute();
    bool IsFinished { get; }
}

// 복잡한 CanStart, Update, Stop 대신
// GetPriority()가 0이면 실행 불가
// Execute()에서 모든 로직 처리
// IsFinished로 완료 체크
```

#### 2.3 PetBrain으로 AI 단순화
```csharp
// PetBrain.cs - PetAI 대신 더 직관적인 이름
public class PetBrain : MonoBehaviour
{
    private List<IPetActivity> activities;
    private IPetActivity currentActivity;
    
    void Update()
    {
        // 1. 현재 활동이 끝났으면
        if (currentActivity == null || currentActivity.IsFinished)
        {
            // 2. 가장 높은 우선순위 활동 선택
            currentActivity = activities
                .OrderByDescending(a => a.GetPriority())
                .FirstOrDefault(a => a.GetPriority() > 0);
        }
        
        // 3. 실행
        currentActivity?.Execute();
    }
}
```

---

### Step 3: 폴더 구조 정리
**목표**: 직관적인 구조

```
Pet/
├── Core/           # 핵심 컴포넌트
│   ├── PetController.cs
│   ├── PetBrain.cs
│   ├── PetState.cs
│   └── PetNeeds.cs
├── Activities/     # 모든 활동
│   ├── WanderActivity.cs
│   ├── EatActivity.cs
│   └── ...
├── Components/     # 기능별 컴포넌트
│   ├── PetMovement.cs
│   ├── PetAnimator.cs
│   └── PetSensor.cs
└── Shared/         # 공통 정의
    ├── PetDefinitions.cs  # 열거형, 상수
    └── PetEvents.cs       # 이벤트
```

---

### Step 4: 의존성 정리
**목표**: 단방향 의존성

```
PetController (최상위)
    ↓
PetBrain / PetState / PetNeeds
    ↓
Activities
    ↓
Components (최하위)
```

- 하위는 상위를 모름
- 이벤트로만 통신

---

## 🎨 예시: 간단한 Activity
```csharp
public class EatActivity : IPetActivity
{
    private PetController pet;
    private GameObject targetFood;
    
    public string Name => "Eat";
    public bool IsFinished { get; private set; }
    
    public float GetPriority()
    {
        // 배고프고 음식이 있으면 높은 우선순위
        if (pet.Needs.Hunger > 70 && FindFood() != null)
            return 10f;
        return 0f;
    }
    
    public void Execute()
    {
        if (targetFood == null)
            targetFood = FindFood();
            
        if (targetFood == null)
        {
            IsFinished = true;
            return;
        }
        
        // 음식으로 이동
        pet.Movement.MoveTo(targetFood.transform.position);
        
        // 도착했으면 먹기
        if (Vector3.Distance(pet.transform.position, targetFood.transform.position) < 1f)
        {
            pet.Needs.Hunger -= 50;
            Destroy(targetFood);
            IsFinished = true;
        }
    }
}
```

---

## 📊 장점
1. **이해하기 쉬움**: 누구나 코드 흐름을 바로 파악
2. **디버깅 용이**: 복잡한 상태 전이 없음
3. **확장 간편**: 새 Activity 추가가 매우 쉬움
4. **성능 향상**: 불필요한 추상화 제거

---

## ⚠️ 주의사항
- 과도한 최적화 금지
- 미래를 위한 설계 금지
- 필요할 때 추가하기 (YAGNI 원칙)