# CooldownManager 마이그레이션 완료 보고서

## ✅ 마이그레이션 완료 목록

### 1. **ButterflyPlayActivity** ✅
- **파일**: `Assets/02_Scripts/Pet/Activities/Environment/ButterflyPlayActivity.cs`
- **변경 내용**:
  - `PLAY_COOLDOWN` 상수 제거
  - `CanStart()`: CooldownManager.IsOnCooldown() 사용
  - `Stop()`: CooldownManager.StartCooldown() 사용
- **쿨타임 타입**: `CooldownType.ButterflyPlay`
- **설정값**: CooldownSettings의 `butterflyPlayCooldown`

### 2. **DivingActivity** ✅
- **파일**: `Assets/02_Scripts/Pet/Activities/Basic/DivingActivity.cs`
- **변경 내용**:
  - `DIVING_COOLDOWN`, `FAILED_ATTEMPT_COOLDOWN` 상수 제거
  - 성공/실패 헬퍼 메서드 추가: `SetDivingSuccessCooldown()`, `SetDivingFailedCooldown()`
  - 모든 실패 지점에서 SetDivingFailedCooldown() 호출
- **쿨타임 타입**:
  - `CooldownType.Diving` (성공)
  - `CooldownType.DivingFailed` (실패)
- **설정값**:
  - `divingCooldown` (30초)
  - `divingFailedCooldown` (60초)

### 3. **PetTreeClimbingController** ✅
- **파일**: `Assets/02_Scripts/Pet/Controllers/PetTreeClimbingController.cs`
- **변경 내용**:
  - `treeSearchCooldown` 변수 제거
  - `CheckForTreeClimbing()`: CooldownManager 사용
- **쿨타임 타입**: `CooldownType.TreeClimbing`
- **설정값**: `treeClimbingCooldown` (10초)

### 4. **EnvironmentManager** ✅
- **파일**: `Assets/02_Scripts/Manager/EnvironmentManager.cs`
- **변경 내용**:
  - `TOUCH_COOLDOWN` 상수 제거
  - `Update()`: 터치 쿨다운 체크
  - `HandleGiftTouch()`: 터치 시 쿨다운 시작
- **쿨타임 타입**: `CooldownType.EnvironmentTouch`
- **설정값**: `environmentTouchCooldown` (0.1초)
- **특징**: 전역 쿨타임 (entityId = null)

### 5. **PetInteractionManager** ✅ (이전에 완료)
- **파일**: `Assets/02_Scripts/Manager/PetInteractionManager.cs`
- **변경 내용**:
  - `useCooldownManager` 토글 추가
  - 레거시 호환성 유지
- **쿨타임 타입**: `CooldownType.PetInteraction`
- **설정값**: `petInteractionCooldown` (30초)

---

## 📊 쿨타임 요약 테이블

| 시스템 | 쿨타임 타입 | 기본값 | entityId | 설명 |
|--------|------------|--------|----------|------|
| 나비 놀이 | ButterflyPlay | 0초 | 펫 이름 | Playful 펫의 나비 놀이 |
| 다이빙 성공 | Diving | 30초 | 펫 이름 | 다이빙 성공 후 재시도 |
| 다이빙 실패 | DivingFailed | 60초 | 펫 이름 | 다이빙 실패 페널티 |
| 나무 오르기 | TreeClimbing | 10초 | 펫 이름 | 나무 탐색 쿨다운 |
| 환경 터치 | EnvironmentTouch | 0.1초 | null | 전역 터치 쿨다운 |
| 펫 상호작용 | PetInteraction | 30초 | 펫 이름 | 펫 간 상호작용 |

---

## 🎮 Unity에서 테스트하기

### 1. CooldownManager 디버그 켜기
```
Hierarchy > CooldownManager > Inspector
- [✅] Enable Debug Log
- [✅] Show Active Cooldowns
```

### 2. 각 시스템 테스트

#### 나비 놀이 테스트
1. Playful 성격 펫 생성
2. FlowersEnvironment 배치
3. 나비와 놀기 후 Console 확인:
   ```
   [CooldownManager] 쿨타임 시작: ButterflyPlay (고양이) - X초
   ```

#### 다이빙 테스트
1. Playful 성격 펫 생성
2. PondEnvironment + DivingSpot 배치
3. 다이빙 성공/실패 후 쿨타임 확인

#### 나무 오르기 테스트
1. Tree 서식지 펫 생성
2. ForestEnvironment 배치
3. 나무 오르기 후 10초 쿨다운 확인

---

## 🔧 런타임 쿨타임 조정

### CooldownSettings에서 값 변경
```
Project > Assets > 09_GameDatas > CooldownSettings
```

변경 가능한 값들:
- `Butterfly Play Cooldown`: 0 → 30 (나비 놀이 30초 쿨다운)
- `Diving Cooldown`: 30 → 15 (다이빙 15초로 단축)
- `Tree Climbing Cooldown`: 10 → 5 (나무 오르기 5초로 단축)
- `Global Cooldown Multiplier`: 1.0 → 0.5 (모든 쿨다운 50% 감소)

---

## 🐛 트러블슈팅

### "쿨타임이 적용되지 않음"
1. CooldownManager GameObject가 씬에 있는지 확인
2. CooldownSettings가 할당되었는지 확인
3. 디버그 로그 켜서 쿨타임 시작/완료 확인

### "레거시 방식으로만 작동함"
1. CooldownManager.Instance가 null이 아닌지 확인
2. 게임 시작 시 CooldownManager가 먼저 초기화되는지 확인

---

## 📈 개선 효과

### Before (분산 관리)
- 각 클래스마다 하드코딩된 쿨타임 값
- 런타임 조정 불가능
- 디버깅 어려움

### After (중앙 관리)
- 모든 쿨타임을 CooldownSettings에서 관리
- 런타임 조정 가능
- 디버그 UI로 상태 확인
- 일관된 쿨타임 처리 로직

---

## 🚀 향후 개선 사항

- [ ] UI에 쿨타임 표시 (진행바, 남은 시간)
- [ ] 쿨타임 감소 아이템 추가
- [ ] 성격별 쿨타임 배율 적용
- [ ] 쿨타임 저장/로드 시스템

---

*마이그레이션 완료: 2024년 모든 쿨타임 시스템이 CooldownManager로 통합되었습니다.*