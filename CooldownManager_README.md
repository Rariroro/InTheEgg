# CooldownManager 시스템 가이드

## 📋 개요
CooldownManager는 InTheEgg 프로젝트의 모든 쿨타임을 중앙에서 통합 관리하는 시스템입니다.

## 🚀 초기 설정

### 1. CooldownSettings 에셋 생성
Unity 에디터에서:
```
메뉴 > InTheEgg > Create Cooldown Settings Asset
```
또는
```
메뉴 > InTheEgg > Create Cooldown Settings in Resources
```

### 2. CooldownManager 게임오브젝트 생성
1. Hierarchy에서 빈 GameObject 생성
2. 이름을 "CooldownManager"로 변경
3. CooldownManager 컴포넌트 추가
4. CooldownSettings 에셋을 Settings 필드에 할당

## 💻 사용 방법

### 기본 사용법

#### 쿨타임 체크
```csharp
// 펫의 상호작용 쿨타임 체크
if (!CooldownManager.Instance.IsOnCooldown(CooldownType.PetInteraction, pet.petName))
{
    // 상호작용 가능
    StartInteraction();
}
```

#### 쿨타임 시작
```csharp
// 상호작용 후 쿨타임 시작
CooldownManager.Instance.StartCooldown(
    CooldownManager.CooldownType.PetInteraction,
    pet.petName);

// 커스텀 시간 설정 (선택사항)
CooldownManager.Instance.StartCooldown(
    CooldownManager.CooldownType.Diving,
    pet.petName,
    customDuration: 45f);
```

#### 남은 시간 확인
```csharp
float remaining = CooldownManager.Instance.GetRemainingTime(
    CooldownType.Diving,
    pet.petName);

if (remaining > 0)
{
    Debug.Log($"다이빙 쿨타임 {remaining}초 남음");
}
```

#### 쿨타임 리셋
```csharp
// 특정 쿨타임 리셋
CooldownManager.Instance.ResetCooldown(CooldownType.TreeClimbing, pet.petName);

// 펫의 모든 쿨타임 리셋
CooldownManager.Instance.ResetEntityCooldowns(pet.petName);

// 모든 쿨타임 리셋
CooldownManager.Instance.ResetAllCooldowns();
```

### 고급 기능

#### 콜백 사용
```csharp
// 쿨타임 완료 시 콜백
CooldownManager.Instance.StartCooldown(
    CooldownType.ButterflyPlay,
    pet.petName,
    customDuration: 10f,
    onComplete: () => {
        Debug.Log("나비 놀이 쿨타임 완료!");
        // 추가 작업
    });
```

#### 이벤트 구독
```csharp
void Start()
{
    // 쿨타임 시작/완료 이벤트 구독
    CooldownManager.Instance.OnCooldownStarted += OnCooldownStarted;
    CooldownManager.Instance.OnCooldownComplete += OnCooldownComplete;
}

void OnCooldownStarted(CooldownType type, string entityId)
{
    Debug.Log($"쿨타임 시작: {type} - {entityId}");
}

void OnCooldownComplete(CooldownType type, string entityId)
{
    Debug.Log($"쿨타임 완료: {type} - {entityId}");
}
```

## 🔧 설정 값 조정

### Unity 에디터에서
1. `Assets/09_GameDatas/CooldownSettings.asset` 선택
2. Inspector에서 원하는 쿨타임 값 조정
3. Global Cooldown Multiplier로 전체 쿨타임 배율 조정

### 런타임에서
```csharp
// 특정 쿨타임 수정 (진행 중인 쿨타임에 적용)
CooldownManager.Instance.ModifyCooldown(
    CooldownType.PetInteraction,
    "고양이",
    newDuration: 15f);

// Settings 값 변경 (새로 시작하는 쿨타임에 적용)
var settings = Resources.Load<CooldownSettings>("CooldownSettings");
settings.petInteractionCooldown = 20f;
```

## 🔍 디버그 기능

### Inspector 옵션
- **Enable Debug Log**: 쿨타임 시작/완료 로그 출력
- **Show Active Cooldowns**: 활성 쿨타임 목록을 콘솔에 표시

### Context Menu 기능
CooldownManager 컴포넌트에서 우클릭:
- **모든 쿨타임 상태 출력**: 현재 활성 쿨타임 정보 출력
- **모든 쿨타임 강제 리셋**: 모든 쿨타임 즉시 제거
- **테스트 쿨타임 추가**: 테스트용 쿨타임 3개 추가

### 디버그 모드 (CooldownSettings)
- **Debug Ignore Cooldowns**: 모든 쿨타임을 0으로 만듦
- **Debug Fixed Cooldown**: 모든 쿨타임을 특정 값으로 고정

## 📊 쿨타임 타입 목록

| 타입 | 기본값 | 설명 |
|-----|-------|------|
| PetInteraction | 30초 | 펫 간 상호작용 |
| Diving | 30초 | 다이빙 성공 후 |
| DivingFailed | 60초 | 다이빙 실패 후 |
| TreeClimbing | 10초 | 나무 오르기 탐색 |
| ButterflyPlay | 0초 | 나비 놀이 |
| TreasureHunt | 60초 | 보물 찾기 |
| EnvironmentTouch | 0.1초 | 환경 터치 입력 |
| Feeding | 10초 | 먹이 먹기 |
| ChaseAndRun | 30초 | 추격전 |
| WalkTogether | 30초 | 함께 걷기 |

## 🔄 마이그레이션 가이드

### 기존 코드 변경 예시

#### Before (기존 방식)
```csharp
// PetInteractionManager.cs
private Dictionary<PetController, float> lastInteractionTime;

private bool IsOnCooldown(PetController pet)
{
    if (lastInteractionTime.TryGetValue(pet, out float lastTime))
    {
        return Time.time - lastTime < interactionCooldown;
    }
    return false;
}
```

#### After (CooldownManager 사용)
```csharp
private bool IsOnCooldown(PetController pet)
{
    return CooldownManager.Instance.IsOnCooldown(
        CooldownManager.CooldownType.PetInteraction,
        pet.petName);
}
```

### 단계별 마이그레이션

1. **PetInteractionManager** ✅ 완료
   - useCooldownManager 플래그로 전환 가능

2. **DivingActivity** (예정)
   ```csharp
   // 기존 상수 제거
   // private const float DIVING_COOLDOWN = 30f;

   // CooldownManager 사용
   CooldownManager.Instance.StartCooldown(
       CooldownType.Diving, pet.petName);
   ```

3. **기타 Activity들** (예정)
   - 각자의 쿨타임 변수를 CooldownManager로 이관

## 📈 성격별 쿨타임 배율

CooldownSettings에서 성격별 배율 설정 가능:
- Playful: 0.8x (20% 감소)
- Lazy: 1.2x (20% 증가)
- Brave: 0.9x (10% 감소)
- Shy: 1.1x (10% 증가)

```csharp
// 성격을 고려한 쿨타임 가져오기
float duration = settings.GetCooldownDurationWithPersonality(
    CooldownType.Diving,
    pet.personality);
```

## ⚠️ 주의사항

1. **싱글톤 초기화**: CooldownManager는 Awake에서 초기화되므로, Start나 그 이후에 접근
2. **null 체크**: 항상 `CooldownManager.Instance != null` 확인
3. **entityId 일관성**: 같은 펫은 항상 같은 entityId 사용 (예: pet.petName)
4. **메모리 관리**: 펫이 제거될 때 `ResetEntityCooldowns()` 호출

## 🎯 향후 개선 사항

- [ ] UI에 쿨타임 표시 기능
- [ ] 쿨타임 저장/로드 시스템
- [ ] 쿨타임 감소/증가 아이템
- [ ] 활동별 세부 쿨타임 타입 추가
- [ ] 쿨타임 애니메이션 효과