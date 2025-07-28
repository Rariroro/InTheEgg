# 펫 시스템 리팩토링 계획

## 📋 리팩토링 개요

펫 시스템의 코드 품질과 유지보수성을 향상시키기 위한 단계적 리팩토링 계획입니다.

### 주요 개선 목표
- 중복 코드 제거 및 파일 정리
- 이벤트 기반 아키텍처로 전환
- Activity/Action 시스템 통합
- 상태 관리 일원화

### 작업 우선순위
1. **중복 파일 정리** → 깨끗한 코드베이스에서 시작
2. **이벤트 시스템 확장** → 다른 리팩토링의 기반이 됨
3. **Activity/Action 통합** → 이벤트 시스템을 활용한 통합
4. **상태 관리 통합** → 통합된 시스템에서 상태 관리 일원화

---

## 🔧 Step 1: 중복 파일 정리 및 백업

### 목표
- 불필요한 중복 파일 제거
- deprecated 폴더 구조 정리
- 깨끗한 코드베이스 확보

### 작업 내용

#### 1.1 백업 생성
```bash
# Pet 폴더 전체 백업
cp -r Assets/02_Scripts/Pet Assets/02_Scripts/Pet_backup_[날짜]
```

#### 1.2 파일 분석 및 정리
| 파일명 | 현재 상태 | 처리 방안 |
|--------|-----------|-----------|
| `PetController_Improved.cs` | 중복/실험적 코드 | 유용한 부분 추출 후 삭제 |
| `PetControllerPatch.cs` | 임시 패치 | 본체에 통합 후 삭제 |
| `PetEventSystemPatch.cs` | 이벤트 패치 | 이벤트 시스템에 통합 후 삭제 |
| `PetEventSystemExample.cs` | 예제 코드 | Examples 폴더로 이동 |

#### 1.3 _deprecated 폴더 정리
- Git 히스토리 확인
- 참조 확인 후 안전하게 삭제
- 필요시 아카이브 폴더로 이동

### 체크리스트
- [ ] 백업 완료
- [ ] 중복 파일 내용 분석
- [ ] 유용한 코드 추출 및 통합
- [ ] 불필요한 파일 삭제
- [ ] Git 커밋

---

## 🔧 Step 2: 이벤트 시스템 통합 및 확장

### 목표
- 분산된 이벤트 시스템 파일들을 하나로 통합
- 중앙 집중식 이벤트 버스 구축
- 실제로 사용 가능한 간단한 구조로 개선

### 작업 내용

#### 2.1 이벤트 시스템 파일 통합
##### 현재 파일 구조 정리
| 파일명 | 역할 | 처리 방안 |
|--------|------|-----------|
| PetEvent.cs | 이벤트 클래스 정의 | 유지 (필요한 것만) |
| PetEventBus.cs | 발행/구독 시스템 | PetEventManager로 통합 |
| PetEventSystem.cs | 통합 클래스 | PetEventManager로 통합 |
| PetEventSystemPatch.cs | 패치 | 삭제 |
| PetEventSystemExample.cs | 예제 | Examples 폴더로 이동 |

#### 2.2 통합된 PetEventManager 생성
```csharp
// Core/PetEventManager.cs (새 파일 - 기존 3개 파일 통합)
public class PetEventManager : MonoBehaviour
{
    private static PetEventManager instance;
    public static PetEventManager Instance
    {
        get
        {
            if (instance == null)
            {
                GameObject go = new GameObject("PetEventManager");
                instance = go.AddComponent<PetEventManager>();
                DontDestroyOnLoad(go);
            }
            return instance;
        }
    }
    
    // 간단한 C# 이벤트 방식 사용 (복잡한 이벤트 클래스 대신)
    // 상태 관련 이벤트
    public static event Action<PetController, PetStatus, PetStatus> OnStatusChanged;
    public static event Action<PetController, bool> OnHoldingStateChanged;
    public static event Action<PetController, bool> OnSelectedStateChanged;
    
    // 활동 관련 이벤트
    public static event Action<PetController, IPetActivity, IPetActivity> OnActivityChanged;
    public static event Action<PetController, string> OnActivityStarted;
    public static event Action<PetController, string> OnActivityCompleted;
    
    // 욕구 관련 이벤트
    public static event Action<PetController, PetNeeds.NeedType, float> OnNeedChanged;
    public static event Action<PetController, PetNeeds.NeedType> OnNeedCritical;
    
    // 감정 관련 이벤트
    public static event Action<PetController, EmotionType> OnEmotionRequested;
    public static event Action<PetController, EmotionType> OnEmotionShown;
    
    // 환경 관련 이벤트
    public static event Action<PetController, bool> OnWaterStateChanged;
    public static event Action<PetController, Transform> OnTreeClimbingStateChanged;
    
    // 디버그 설정
    [SerializeField] private bool enableDebugLog = false;
    
    // 이벤트 발행 메서드들 (static으로 간단하게)
    public static void RaiseStatusChanged(PetController pet, PetStatus oldStatus, PetStatus newStatus)
    {
        if (Instance.enableDebugLog)
            Debug.Log($"[PetEvent] {pet.petName}: Status {oldStatus} → {newStatus}");
            
        OnStatusChanged?.Invoke(pet, oldStatus, newStatus);
    }
    
    public static void RaiseActivityChanged(PetController pet, IPetActivity oldActivity, IPetActivity newActivity)
    {
        if (Instance.enableDebugLog)
            Debug.Log($"[PetEvent] {pet.petName}: Activity {oldActivity?.Name ?? "None"} → {newActivity?.Name ?? "None"}");
            
        OnActivityChanged?.Invoke(pet, oldActivity, newActivity);
    }
    
    public static void RaiseNeedChanged(PetController pet, PetNeeds.NeedType needType, float value)
    {
        OnNeedChanged?.Invoke(pet, needType, value);
    }
    
    // ... 기타 Raise 메서드들
}
```

#### 2.3 기존 파일 정리
```csharp
// PetEvent.cs 간소화 (필요한 것만 유지)
public enum PetEventType
{
    StatusChanged,
    ActivityChanged,
    NeedChanged,
    EmotionExpressed,
    // ... 필요한 것만
}

// 복잡한 이벤트 클래스들은 제거하고 단순한 Action<> 델리게이트 사용
```

#### 2.4 이벤트 사용 예시
```csharp
// PetController.cs에서
private void HandleStatusChanged(PetStatus oldStatus, PetStatus newStatus)
{
    PetEventManager.RaiseStatusChanged(this, oldStatus, newStatus);
}

// PetAnimationController.cs에서
private void OnEnable()
{
    PetEventManager.OnStatusChanged += HandleStatusChanged;
    PetEventManager.OnActivityChanged += HandleActivityChanged;
}

private void OnDisable()
{
    PetEventManager.OnStatusChanged -= HandleStatusChanged;
    PetEventManager.OnActivityChanged -= HandleActivityChanged;
}

private void HandleStatusChanged(PetController pet, PetStatus oldStatus, PetStatus newStatus)
{
    if (pet != petController) return;
    
    // 상태에 따른 애니메이션 처리
    switch (newStatus)
    {
        case PetStatus.Idle:
            SetContinuousAnimation(PetAnimationType.Idle);
            break;
    }
}
```

### 체크리스트
- [ ] 기존 이벤트 시스템 파일 백업
- [ ] PetEventManager.cs 생성 (3개 파일 통합)
- [ ] PetEvent.cs 간소화
- [ ] PetEventBus.cs, PetEventSystem.cs 삭제
- [ ] PetEventSystemPatch.cs 삭제
- [ ] PetEventSystemExample.cs → Examples 폴더 이동
- [ ] PetController에서 이벤트 발행 코드 추가
- [ ] 각 Controller에서 이벤트 구독 코드 추가
- [ ] 테스트 및 디버깅

---

## 🔧 Step 3: Activity/Action 시스템 통합

### 목표
- IPetAction을 IPetActivity로 완전히 전환
- 기존 Action들을 모두 Activity로 마이그레이션
- 중복 시스템 제거로 일관성 확보

### 작업 내용

#### 3.1 IPetActivity 인터페이스 개선
```csharp
// Core/IPetActivity.cs (기존 파일 개선)
public enum ActivityCategory
{
    Basic,      // 기본 활동 (배회, 휴식)
    Needs,      // 욕구 활동 (먹기, 자기)
    Emergency,  // 긴급 활동 (도망, 탈진)
    Social,     // 사회적 활동 (상호작용, 모이기)
    Environment // 환경 활동 (나무오르기, 수영)
}

public interface IPetActivity
{
    // 기존 인터페이스에 카테고리 추가
    string Name { get; }
    ActivityCategory Category { get; }
    
    bool CanStart(PetState state, PetNeeds needs);
    float GetPriority(PetState state, PetNeeds needs);
    
    void Start();
    void Update();
    void Stop();
    
    bool IsComplete { get; }
    bool IsInterruptible { get; }
}
```

#### 3.2 남은 Action들을 Activity로 전환

##### 현재 상태 분석
| 구분 | 파일 위치 | 상태 |
|------|----------|------|
| 기존 Action (deprecated) | `_deprecated/Actions/` | 보관 중 |
| 새 Activity | `Activities/` 폴더 | 일부 전환 완료 |
| 아직 사용 중인 Action | PetController 내부 | 전환 필요 |

##### 전환 필요 항목
- ActionToActivityAdapter 제거
- PetActivityAdapter의 IPetAction 상속 제거
- `_allActions` → `_allActivities`로 완전 전환
- `useActivitySystem` 플래그 제거 (항상 Activity 사용)

#### 3.3 PetAI 클래스 분리
```csharp
// Core/PetAI.cs (새 파일)
public class PetAI : MonoBehaviour
{
    [SerializeField] private List<IPetActivity> activities;
    [SerializeField] private IPetActivity currentActivity;
    
    private PetController petController;
    private PetState petState;
    private PetNeeds petNeeds;
    
    // 활동별 카테고리 관리
    private Dictionary<ActivityCategory, List<IPetActivity>> categorizedActivities;
    
    public void Initialize(PetController controller)
    {
        petController = controller;
        petState = controller.State;
        petNeeds = controller.Needs;
        
        // 활동 초기화 및 카테고리별 분류
        InitializeActivities();
        CategorizeActivities();
    }
    
    public void UpdateAI()
    {
        // 상태에 따른 카테고리 우선순위 결정
        var priorityCategories = GetPriorityCategories(petState);
        
        // 최적의 활동 선택
        var bestActivity = SelectBestActivity(priorityCategories);
        
        // 활동 전환
        if (bestActivity != currentActivity)
        {
            TransitionToActivity(bestActivity);
        }
        
        // 현재 활동 업데이트
        currentActivity?.Update();
    }
}
```

#### 3.4 기존 시스템 정리
```csharp
// PetController.cs 수정 사항
public partial class PetController : MonoBehaviour
{
    // 제거할 항목들
    // private List<IPetAction> _allActions;  // 제거
    // private IPetAction _currentAction;     // 제거
    // private bool useActivitySystem = true; // 제거
    
    // 유지/개선할 항목
    private PetAI petAI;  // 새로 추가
    
    private void Awake()
    {
        // ... 기존 초기화
        
        // PetAI 컴포넌트 추가 및 초기화
        petAI = gameObject.AddComponent<PetAI>();
        petAI.Initialize(this);
    }
    
    private void Update()
    {
        // ... 기존 상태 체크
        
        // AI 업데이트 위임
        petAI.UpdateAI();
    }
}
```

### 체크리스트
- [ ] IPetActivity 인터페이스에 Category 추가
- [ ] 모든 Action을 Activity로 전환 완료
- [ ] ActionToActivityAdapter 제거
- [ ] PetActivityAdapter에서 IPetAction 상속 제거
- [ ] PetAI 클래스 생성 및 AI 로직 분리
- [ ] PetController에서 Action 관련 코드 제거
- [ ] useActivitySystem 플래그 및 관련 분기 제거
- [ ] _deprecated/Actions 폴더 최종 정리

---

## 🔧 Step 4: 상태 관리 시스템 통합

### 목표
- 기존 플래그와 PetState 시스템 일원화
- 명확한 상태 전이 규칙 확립
- 상태 기반 의사결정 강화

### 작업 내용

#### 4.1 플래그 제거 및 PetState 활용

##### 플래그 마이그레이션 매핑
| 기존 플래그 | PetState 접근 방법 |
|------------|-------------------|
| `isInteracting` | `petState.IsInteracting` |
| `isSelected` | `petState.IsSelected` |
| `isHolding` | `petState.IsHolding` |
| `isGathering` | `petState.CurrentStatus == PetStatus.Gathering` |
| `isClimbingTree` | `petState.IsClimbingTree` |
| `isInWater` | `petState.IsInWater` |

#### 4.2 상태 전이 이벤트 활용
```csharp
// PetController 수정
private void Awake()
{
    // ... 기존 초기화
    
    // 상태 변경 이벤트 구독
    petState.OnStatusChanged += HandleStatusChanged;
}

private void HandleStatusChanged(PetStatus oldStatus, PetStatus newStatus)
{
    // 이벤트 버스로 전파
    PetEventBus.RaiseStatusChanged(this, oldStatus, newStatus);
    
    // 필요한 추가 처리
    switch (newStatus)
    {
        case PetStatus.PlayerControl:
            // 플레이어 제어 시작 처리
            break;
        case PetStatus.Idle:
            // 유휴 상태 전환 처리
            break;
        // ...
    }
}
```

#### 4.3 SyncStateWithFlags 메서드 제거
- 모든 상태 체크를 PetState를 통해 수행
- 기존 플래그 참조를 제거
- Update 메서드 단순화

### 체크리스트
- [ ] 기존 플래그 변수 제거
- [ ] 플래그 참조를 PetState 참조로 변경
- [ ] SyncStateWithFlags 메서드 제거
- [ ] 상태 전이 로직 정리
- [ ] 상태 기반 조건 체크 리팩토링

---

## 🔧 Step 5: 테스트 및 검증

### 목표
- 리팩토링된 시스템의 안정성 확보
- 성능 개선 확인
- 버그 및 회귀 방지

### 작업 내용

#### 5.1 단위 테스트
```csharp
// Tests/PetBehaviorTests.cs
[TestFixture]
public class PetBehaviorTests
{
    [Test]
    public void WanderBehavior_HasLowestPriority()
    {
        // Arrange
        var behavior = new WanderBehavior(mockPet);
        
        // Act
        float priority = behavior.GetPriority(idleState, normalNeeds);
        
        // Assert
        Assert.Less(priority, 1.0f);
    }
}
```

#### 5.2 통합 테스트 시나리오
1. **다중 펫 상호작용**
   - 5마리 이상의 펫이 동시에 활동
   - 상호작용 우선순위 확인
   
2. **플레이어 개입**
   - 선택/들기 시 즉각 반응
   - 상태 전환 정확성
   
3. **긴급 상황 처리**
   - 벌 공격 시 도망
   - 탈진 시 행동 중단

#### 5.3 성능 측정
- Update 호출 빈도 측정
- 이벤트 시스템 오버헤드 분석
- 메모리 사용량 비교

### 체크리스트
- [ ] 단위 테스트 작성
- [ ] 통합 테스트 시나리오 실행
- [ ] 성능 프로파일링
- [ ] 버그 수정
- [ ] 최종 검증

---

## 📊 예상 결과

### 코드 품질 개선
- 중복 코드 90% 감소
- 파일 수 30% 감소
- 가독성 대폭 향상

### 유지보수성
- 새로운 행동 추가 시간 50% 단축
- 버그 수정 용이성 향상
- 테스트 커버리지 증가

### 성능
- Update 최적화로 5-10% 성능 향상 예상
- 메모리 사용량 소폭 감소

---

## 🚀 다음 단계

이 리팩토링 완료 후 고려할 수 있는 추가 개선사항:
1. PetAIProperties 분리
2. PetController 크기 축소
3. 매직 넘버 상수화
4. 감정 시스템 분리
5. NavMesh 초기화 로직 개선

---

## 📝 참고사항

- 각 단계는 독립적으로 테스트 가능하도록 설계
- Git 브랜치 전략: feature/pet-refactoring-step-[번호]
- 각 단계 완료 시 코드 리뷰 권장
- 롤백 계획 수립 필요