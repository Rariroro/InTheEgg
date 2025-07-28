# 펫 시스템 정리 계획 (기존 구조 유지)

## 🎯 목표
- 새로운 시스템 추가 ❌
- 기존 구조에서 불필요한 것만 제거 ✅
- 사용하지 않는 코드 정리 ✅
- 중복 제거 및 통합 ✅

## 📋 정리 작업

### Step 1: 사용하지 않는 파일 삭제

#### 1.1 즉시 삭제 가능한 파일들
```bash
# 패치/개선 파일들 (이미 통합되었거나 사용 안함)
- PetController_Improved.cs
- PetControllerPatch.cs  
- PetEventSystemPatch.cs

# deprecated 폴더 (이미 Activity로 전환 완료)
- _deprecated/ 폴더 전체
```

#### 1.2 정리 필요한 파일들
```bash
# 사용되지 않는 이벤트 시스템
- Core/PetEvent.cs       # 복잡한 이벤트 클래스들
- Core/PetEventBus.cs    # 사용 안함
- Core/PetEventSystem.cs # 사용 안함

# 예제는 따로 보관
- Examples/PetEventSystemExample.cs → Examples 폴더로 이동
```

---

### Step 2: 코드 내부 정리

#### 2.1 PetController.cs 정리
```csharp
// 제거할 것들:
- private List<IPetAction> _allActions;        // Activity로 전환됨
- private IPetAction _currentAction;           // Activity로 전환됨  
- private bool useActivitySystem = true;       // 항상 true이므로 불필요
- InitializeActions() 메서드                   // 사용 안함
- UpdateAI() 메서드                           // Activity 시스템만 사용
- SyncStateWithFlags() 메서드                 // Phase 1 임시 메서드

// 정리할 것들:
- 중복된 플래그들 (isInteracting 등) → PetState만 사용
- 사용하지 않는 public 메서드들
- 주석 처리된 코드들
```

#### 2.2 PetAIProperties 분리
```csharp
// PetController.cs 내부의 static class를
// Core/PetDefinitions.cs로 이동

public static class PetAIProperties → namespace Pet.Core
{
    public enum Personality { ... }
    public enum DietaryFlags { ... }  
    public enum Habitat { ... }
}
```

#### 2.3 Activity 시스템 정리
```csharp
// IPetActivity.cs에서 제거:
- PetActivityAdapter 클래스 (IPetAction 관련)
- ActionToActivityAdapter import

// 각 Activity에서 제거:
- IPetAction 관련 메서드들 (GetPriority(), OnEnter() 등)
- PetActivityAdapter 상속 → 직접 IPetActivity 구현
```

---

### Step 3: 중복 통합

#### 3.1 이벤트 시스템 통합
```csharp
// 현재: PetNeeds가 자체 이벤트 시스템 사용
public event Action<NeedType, float> OnNeedChanged;
public event Action<NeedType> OnNeedCritical;

// 통합: 기존 이벤트를 그대로 사용 (새로운 시스템 만들지 않음)
// 단지 PetEvent, PetEventBus, PetEventSystem 삭제만
```

#### 3.2 상태 관리 통합
```csharp
// 현재: 플래그와 PetState 혼재
if (isInteracting) { ... }
if (petState.IsInteracting) { ... }

// 통합: PetState만 사용하도록 일관성 확보
if (petState.IsInteracting) { ... }
```

---

### Step 4: 폴더 구조 정리 (이동만)

```
Pet/
├── Core/
│   ├── (유지) PetState.cs, PetNeeds.cs, PetAI.cs
│   ├── (유지) PetMovement.cs, PetAnimator.cs, PetSensor.cs
│   ├── (이동) PetDefinitions.cs ← PetAIProperties
│   ├── (삭제) PetEvent*.cs 파일들
│   └── (이동) Food.cs → ../Items/
├── Activities/
│   └── (유지) 현재 구조 그대로
├── Controllers/
│   └── (유지) 현재 구조 그대로
├── Examples/
│   └── (이동) PetEventSystemExample.cs
└── (삭제) _deprecated/, 패치 파일들
```

---

### Step 5: 간단한 리팩토링

#### 5.1 매직 넘버 상수화
```csharp
// PetController.cs 상단에 추가
private const float AI_UPDATE_INTERVAL = 0.5f;
private const float WATER_DEPTH_LERP_SPEED = 5f;
private const float SLEEPY_EMOTION_INTERVAL = 10f;
// ... 기존 매직 넘버들을 상수로
```

#### 5.2 디버그 코드 정리
```csharp
// 조건부 컴파일로 변경
#if UNITY_EDITOR
    Debug.Log($"[PetController] {petName}: 상태 변경");
#endif
```

---

## 📊 예상 결과

### 삭제되는 것들
- 파일 10개 이상 삭제
- 코드 라인 수 30% 감소
- 중복 시스템 제거

### 유지되는 것들
- 현재 작동하는 모든 기능
- Activity 시스템
- State/Needs 시스템
- 기존 폴더 구조

### 개선되는 것들
- 일관된 상태 관리 (PetState만 사용)
- 명확한 코드 (중복 제거)
- 가독성 (불필요한 추상화 제거)

---

## ✅ 체크리스트

### 파일 삭제
- [ ] PetController_Improved.cs 삭제
- [ ] PetControllerPatch.cs 삭제
- [ ] PetEventSystemPatch.cs 삭제
- [ ] _deprecated 폴더 삭제
- [ ] PetEvent.cs 삭제
- [ ] PetEventBus.cs 삭제
- [ ] PetEventSystem.cs 삭제

### 코드 정리
- [ ] PetController의 Action 시스템 코드 제거
- [ ] useActivitySystem 플래그 제거
- [ ] SyncStateWithFlags 메서드 제거
- [ ] 중복 플래그 제거 (PetState 사용)
- [ ] PetAIProperties를 별도 파일로 분리

### 파일 이동
- [ ] PetEventSystemExample.cs → Examples/
- [ ] Food.cs → Items/
- [ ] PetAIProperties → Core/PetDefinitions.cs

### 최종 확인
- [ ] 모든 기능 정상 작동 확인
- [ ] 컴파일 에러 없음 확인
- [ ] Git commit