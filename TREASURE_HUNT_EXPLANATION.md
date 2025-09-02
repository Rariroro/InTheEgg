# 🎯 보물찾기 시스템 완벽 가이드

## 📚 전체 흐름도

```
1. 시작
   ├─ TreasureHuntManager.StartTreasureHunt()
   ├─ 맵에 보물 생성 (TreasureSpot에 배치)
   └─ 모든 펫 상태 → TreasureHunting

2. 탐색
   ├─ 각 펫이 FindNewTarget() 호출
   ├─ 70m 이내 가장 가까운 보물 검색
   ├─ targetSpot 설정
   └─ 보물 향해 달려감 (속도 3배)

3. 경쟁
   ├─ 여러 펫이 같은 보물 추적 가능
   ├─ competingPets 리스트에 등록
   └─ 먼저 도착한 펫이 승자

4. 획득
   ├─ TryCollect() 호출
   ├─ 성공 → 보물 들고 대기 위치로
   └─ 실패 → 실망 후 새 보물 찾기

5. 축하
   ├─ 대기 위치에서 보물 내려놓기
   ├─ 계속 점프하며 기뻐함
   └─ 유저가 클릭하면 수집 완료
```

## 🔑 핵심 컴포넌트

### 1. TreasureHuntManager (매니저)
- 보물찾기 시작/종료 관리
- 보물 생성 및 배치
- 전체 펫 상태 관리

### 2. TreasureSpot (보물 위치)
- 보물이 나타날 수 있는 지점
- 보물 소유권 관리 (occupyingPet)
- 경쟁 펫 리스트 관리 (competingPets)

### 3. TreasureHuntActivity (펫 행동)
- 보물 탐색 로직
- 이동 및 획득 처리
- 축하 애니메이션

## 🐛 주요 버그와 해결법

### 문제 1: 다른 펫이 가져간 보물을 계속 추적

**상황:**
```
펫 A, B, C → 보물 X 추적
펫 A 획득 성공
펫 B, C는 계속 보물 X 위치에 머물러 있음 ❌
```

**원인:**
- `CheckCurrentTarget()`가 0.5초마다 실행되지만
- `targetSpot.HasTreasure`가 여전히 true를 반환
- 펫들이 보물이 아직 있다고 착각

**해결 방법들:**

#### 방법 1: hasTreasure 즉시 false 설정
```csharp
// TreasureSpot.cs - TryCollect()
public bool TryCollect(PetController pet) {
    occupyingPet = pet;
    hasTreasure = false;  // 즉시 없는 것으로 표시
}

// 문제: 보물 찾은 펫도 자기 보물이 없다고 인식
```

#### 방법 2: HasTreasure 프로퍼티 수정
```csharp
// TreasureSpot.cs
public bool HasTreasure => hasTreasure && currentTreasure != null && occupyingPet == null;

// 문제: 새 보물 찾기도 실패 (모든 곳에서 이 프로퍼티 사용)
```

#### 방법 3: 알림 시스템 ⭐ (추천)
```csharp
// TreasureSpot.cs
private void NotifyLosingPets(PetController winner) {
    foreach (var loser in competingPets) {
        // 각 펫의 TreasureHuntActivity.OnTargetLost() 호출
        // 즉시 새로운 보물 탐색 시작
    }
}
```

### 문제 2: 첫 보물 이후 다른 보물을 못 찾음

**원인:**
- `FindNearestAvailableSpot()`이 `HasTreasure` 체크
- 누군가 차지한 보물은 검색에서 제외됨

**해결:**
- 보물 상태를 더 세밀하게 관리
- 차지됨(occupied) vs 사라짐(collected) 구분

## 💡 디버깅 팁

### 로그 확인 포인트
```csharp
// 1. 보물 탐색
Debug.Log($"{pet.petName}: 새 보물 타겟 설정 - {targetSpot.name}");

// 2. 경쟁 상황
Debug.Log($"{pet.petName}이(가) {name} 보물을 목표로 설정했습니다.");

// 3. 획득 결과
Debug.Log($"{pet.petName}: 보물 획득 성공!");
Debug.Log($"{loser.petName}은(는) {winner.petName}에게 보물을 빼앗겼습니다.");

// 4. 타겟 무효화
Debug.Log($"[TreasureHunt] {pet.petName}: 타겟 무효화 감지, 새 보물 찾기");
```

### 주요 변수 체크
- `hasFoundTreasure`: 펫이 보물을 찾았는지
- `targetSpot`: 현재 추적 중인 보물
- `occupyingPet`: 보물을 차지한 펫
- `competingPets`: 같은 보물을 노리는 펫들

## 🎮 테스트 시나리오

### 시나리오 1: 단독 획득
1. 펫 1마리만 활성화
2. 보물찾기 시작
3. 정상적으로 찾고 점프하는지 확인

### 시나리오 2: 경쟁 획득
1. 펫 3마리 활성화
2. 보물 1개만 생성
3. 승자는 점프, 패자는 배회하는지 확인

### 시나리오 3: 연속 획득
1. 펫 3마리, 보물 5개
2. 모든 보물이 수집될 때까지 관찰
3. 각 펫이 새 보물을 찾는지 확인

## 📋 체크리스트

- [ ] 보물 찾은 펫이 계속 점프하는가?
- [ ] 놓친 펫이 즉시 다른 보물을 찾는가?
- [ ] 모든 보물이 정상적으로 수집되는가?
- [ ] 경쟁 상황이 올바르게 처리되는가?
- [ ] 로그에 에러가 없는가?

## 🔧 권장 수정사항

1. **알림 시스템 구현**
   - NotifyLosingPets()에서 실제 알림 전송
   - OnTargetLost() 메서드 추가

2. **상태 분리**
   - hasTreasure: 보물 존재 여부
   - isOccupied: 누군가 차지했는지
   - isCollected: 유저가 수집했는지

3. **타이밍 조정**
   - CheckCurrentTarget() 주기: 0.5초 → 0.3초
   - 더 빠른 반응성

---

*이 문서는 보물찾기 시스템의 이해를 돕기 위해 작성되었습니다.*