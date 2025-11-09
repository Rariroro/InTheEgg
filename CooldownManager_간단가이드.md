# CooldownManager 간단 시작 가이드

## ⚡ 5분 안에 시작하기

### 1단계: Unity 에디터 열기

### 2단계: CooldownSettings 에셋 생성

**방법 A - 우클릭으로 생성 (추천)**
1. Project 창에서 `Assets/09_GameDatas` 폴더 우클릭
2. `Create` → `InTheEgg` → `Cooldown Settings` 클릭
3. 파일 이름: `CooldownSettings` (그대로 두기)

**방법 B - 메뉴에서 생성**
1. Unity 에디터 상단 메뉴바에서 `InTheEgg` 클릭
2. `Create Cooldown Settings Asset` 클릭
3. 자동으로 `Assets/09_GameDatas/CooldownSettings.asset` 생성됨

### 3단계: CooldownManager GameObject 생성

1. Hierarchy 창에서 빈 공간 우클릭
2. `Create Empty` 클릭
3. 이름을 `CooldownManager`로 변경
4. Inspector에서 `Add Component` 클릭
5. `CooldownManager` 검색 후 선택
6. `Settings` 필드에 방금 만든 `CooldownSettings` 에셋을 드래그 앤 드롭

### 4단계: 완료!

이제 CooldownManager가 작동합니다.

---

## 🎮 테스트해보기

### Hierarchy에서 CooldownManager 선택 후:

1. Inspector에서 우클릭
2. `테스트 쿨타임 추가` 클릭
3. Console 창에서 로그 확인

### 디버그 옵션 켜기:

- `Enable Debug Log` 체크 → 쿨타임 시작/완료 로그 출력
- `Show Active Cooldowns` 체크 → 활성 쿨타임 실시간 표시

---

## 📝 코드에서 사용하기

### 쿨타임 체크
```csharp
if (!CooldownManager.Instance.IsOnCooldown(
    CooldownManager.CooldownType.PetInteraction,
    "고양이"))
{
    // 상호작용 가능!
}
```

### 쿨타임 시작
```csharp
CooldownManager.Instance.StartCooldown(
    CooldownManager.CooldownType.Diving,
    "펭귄");
```

### 남은 시간 확인
```csharp
float remaining = CooldownManager.Instance.GetRemainingTime(
    CooldownManager.CooldownType.TreeClimbing,
    "다람쥐");

Debug.Log($"나무 오르기 쿨타임: {remaining}초 남음");
```

---

## ⚙️ 쿨타임 값 조정하기

1. Project 창에서 `CooldownSettings` 에셋 선택
2. Inspector에서 원하는 값 변경
3. 저장 (Ctrl+S)

**예시:**
- `Pet Interaction Cooldown`: 30초 → 20초로 변경
- `Diving Cooldown`: 30초 → 45초로 변경
- `Global Cooldown Multiplier`: 1.0 → 0.5 (모든 쿨타임 50% 감소!)

---

## 🐛 문제 해결

### "CooldownManager.Instance가 null입니다"
→ CooldownManager GameObject가 씬에 있는지 확인

### "CooldownSettings를 찾을 수 없습니다"
→ Settings 필드에 에셋이 할당되었는지 확인

### 쿨타임이 작동하지 않음
→ PetInteractionManager의 `Use Cooldown Manager` 체크 확인

---

## 📚 더 자세한 내용은

`CooldownManager_README.md` 파일 참고