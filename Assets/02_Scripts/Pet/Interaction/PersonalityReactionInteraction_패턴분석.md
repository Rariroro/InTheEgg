# PersonalityReactionInteraction 이동 패턴 분석

> 성격별 펫 상호작용 - 어떻게 움직이는가?

**최종 업데이트**: 2025-12-07

---

## 🎯 핵심 개념

**11가지 성격 조합**마다 고유한 이동 패턴 실행
- 거리가 가까우면 접근 단계 스킵 → 즉시 반응
- 성격에 맞는 속도로 이동 (Lazy는 느리게, Playful은 빠르게)
- 펫 크기를 고려한 거리 자동 조정 (`CalculateApproachDistance()`)
- 토스트 알림 없음 (IsPriorityInteraction = true)

---

## 🎭 11가지 성격 조합별 움직임

### **1. Lazy + Lazy** (게으름 + 게으름)
```
천천히 접근 → 마주보기 → 차례로 누움 → 함께 휴식 → 천천히 헤어짐
```
- **속도**: 매우 느림 (30% 속도)
- **특징**: 모든 동작이 느리고 귀찮아함
- **지속**: ~10초
- **구현 메서드**: `LazyLazyReaction()`
- **단계**:
  1. 목표 거리로 정밀 이동 (`MoveToPositionsPrecise`)
  2. 멈춰서 서로 마주보기
  3. Sleep 감정 표시 후 동시에 누움
  4. 함께 휴식 (30% 확률로 자세 변경)
  5. 순차적으로 일어남
  6. 서로 마주보기
  7. 천천히 각자의 길로 헤어짐

---

### **2. Lazy + Shy** (게으름 + 수줍음)
```
Lazy만 느리게 접근 → Lazy 누움 → Shy 놀라서 도망
→ Shy 멈춰서 돌아봄 → 조심스럽게 다시 접근 → 냄새 맡기
```
- **속도**: Lazy 40%, Shy 도망 시 150%
- **특징**: 비대칭 움직임 (한쪽만 이동)
- **지속**: ~8초
- **구현 메서드**: `LazyShyReaction()`
- **단계**:
  1. Lazy가 천천히 접근 (`SafeSetNavMeshAgent`)
  2. Lazy가 피곤해서 누움
  3. Shy가 놀라서 점프 후 도망 (`QuickRetreat`)
  4. Shy가 멈춰서 돌아봄
  5. Shy가 조심스럽게 다시 접근
  6. 냄새 맡기 동작

---

### **3. Lazy + Brave** (게으름 + 용감함)

**가까이 있을 때 (< 5m):**
```
Lazy 즉시 누움 → Brave 당황 점프 → Brave가 주위 빙빙 돌기
```

**멀리 있을 때:**
```
Brave 빠르게 접근 → Lazy 누움 → Brave가 주위 원 운동
→ Brave 점프 자랑 → Brave 떠남
```
- **속도**: Brave 150%, Lazy는 제자리
- **특징**: 원 운동 (Brave가 Lazy 중심으로 한 바퀴)
- **지속**: ~12초
- **구현 메서드**: `LazyBraveReaction()`
- **핵심 메서드**: `CircleAroundTarget()`, `SmoothMoveToPosition()`

---

### **4. Lazy + Playful** (게으름 + 장난기)

**가까이 있을 때 (< 5m):**
```
Lazy 누움 → Playful 연속 점프 2회 → Playful이 주위 빙빙
```

**멀리 있을 때:**
```
Playful 신나게 접근 → Lazy 누움 → Playful 연속 점프 3회
→ Playful이 주위 원 운동 → Playful 실망
```
- **속도**: Playful 200% (매우 빠름!)
- **특징**: 점프 많음, 원 운동
- **지속**: ~13초
- **구현 메서드**: `LazyPlayfulReaction()`

---

### **5. Shy + Shy** (수줍음 + 수줍음)

**가까이 있을 때 (< 5m):**
```
둘 다 즉시 점프 → 반대 방향으로 동시 도망
```

**멀리 있을 때:**
```
멈춰서 마주보기 → 긴 정적 → 둘 다 점프
→ 반대 방향으로 동시 도망 (5m) → 멈춰서 다시 돌아봄
→ 완전히 반대 방향으로 도망 (8m)
```
- **속도**: 도망 시 150~180%
- **특징**: 대칭적 움직임 (정반대 방향)
- **지속**: ~8초
- **구현 메서드**: `ShyShyReaction()`
- **핵심 메서드**: `QuickRetreat()` (동시 호출)

---

### **6. Shy + Brave** (수줍음 + 용감함)

**가까이 있을 때 (< 3m):**
```
마주보기 → (아래 단계 2부터 시작)
```

**멀리 있을 때:**
```
Brave 접근 → 마주보기 → Shy 도망 (5m)
→ Brave 천천히 따라감 → Shy 멈춰서 돌아봄
→ Brave 점프 인사 → Shy 완전 도망 (10m)
→ Brave 잠시 추격 후 포기
```
- **속도**: Shy 200%, Brave 70~150%
- **특징**: 추격 패턴 (2단계 도망)
- **지속**: ~10초
- **구현 메서드**: `ShyBraveReaction()`

---

### **7. Shy + Playful** (수줍음 + 장난기)

**가까이 있을 때 (< 3m):**
```
마주보기 → (아래 단계 2부터 시작)
```

**멀리 있을 때:**
```
Playful 접근 → 마주보기 → Shy 도망 (7m)
→ Shy 멈춰서 돌아봄 → Playful 점프
→ Shy 다시 도망 (10m, 더 멀리!) → Playful 실망
```
- **속도**: Playful 150%, Shy 150%
- **특징**: 2번 도망 (점점 더 멀리)
- **지속**: ~11초
- **구현 메서드**: `ShyPlayfulReaction()`

---

### **8. Brave + Brave** (용감함 + 용감함)

**가까이 있을 때 (< 5m):**
```
정면 대치 → 둘 다 경계 점프 → (원 운동부터 시작)
```

**멀리 있을 때:**
```
중간점으로 빠르게 동시 접근 → 정면 대치
→ 서로 주위를 돌며 위엄 과시 → 동시 점프
→ 같은 목표로 달리기 시합 (8m) → 서로 인정
```
- **속도**: 150~200%
- **특징**: 양방향 원 운동, 경주
- **지속**: ~13초
- **구현 메서드**: `BraveBraveReaction()`
- **핵심 메서드**: `CircleAroundEachOther()`

---

### **9. Brave + Playful** (용감함 + 장난기)

**가까이 있을 때 (< 5m):**
```
마주보기 → Playful 점프 → Brave 점프 → (추격전부터 시작)
```

**멀리 있을 때:**
```
중간점으로 신나게 동시 접근 → 서로 주위를 빙빙 돔
→ Playful 점프 → Brave 점프
→ 추격전 1단계 (Playful 도망, Brave 추격, 6m)
→ 추격전 2단계 (역할 교체) → 만족하고 헤어짐
```
- **속도**: 180~200%
- **특징**: 원 운동 + 양방향 추격
- **지속**: ~14초
- **구현 메서드**: `BravePlayfulReaction()`

---

### **10. Playful + Playful** (장난기 + 장난기) ⭐ 가장 복잡

**가까이 있을 때 (< 5m):**
```
마주보기 → 점프 파티 5회! → (추격전부터 시작)
```

**멀리 있을 때:**
```
중간점으로 신나게 달림 → 빠르게 원 운동
→ 점프 파티 3회
→ 추격전 1단계 (Pet1 도망, Pet2 추격, 7m)
→ 추격전 2단계 (역할 교체)
→ 다시 점프 파티 2회
→ 마지막 원 운동 한 바퀴 → 헤어짐
```
- **속도**: 200% (최고 속도!)
- **특징**: 점프 파티 3번 + 원 운동 2번 + 추격 2번
- **지속**: ~18초 (가장 길고 복잡)
- **구현 메서드**: `PlayfulPlayfulReaction()`

---

### **11. Default** (기본)
```
중간점으로 간단히 접근 → 마주보기 → 헤어짐
```
- **속도**: 100%
- **특징**: 가장 단순
- **지속**: ~4초
- **구현 메서드**: `DefaultReaction()`

---

## 🔧 핵심 이동 메커니즘 (8가지)

### **1. MoveToPositionsPrecise** - 정밀 양방향 이동
- 두 펫이 동시에 정확한 위치로 이동
- 먼저 도착한 펫은 상대를 기다림
- 도착 판정: `preciseThreshold` (0.3m 이내)
- **사용**: Lazy_Lazy
- **위치**: `PersonalityReactionInteraction.cs:1991`

---

### **2. QuickRetreat** - 빠른 도망
- 0.2초 만에 뒤돌아보기
- 150% 속도로 빠르게 도망
- NavMeshAgent 자동 회전 활용
- **사용**: Shy 계열 모두 (8회)
- **위치**: `PersonalityReactionInteraction.cs:1847`

---

### **3. SmoothlyLookAtEachOther** - 서로 마주보기 (개선됨)
- 부드럽게 회전하여 서로 응시
- Y축만 회전 (고개 끄덕임 방지)
- Walk 애니메이션 없이 순수 회전만 처리 (BasePetInteraction과 다름)
- EaseInOut 커브 적용으로 더 부드러운 회전
- **사용**: 모든 패턴 (34회 - 가장 많이 사용!)
- **위치**: `PersonalityReactionInteraction.cs:1428` (재정의됨)

---

### **4. CircleAroundTarget** - 단방향 원 운동
- 한 펫이 다른 펫 주위를 한 바퀴 돔
- 펫 크기에 따라 반지름 자동 조정 (`CalculateCircleRadius()`)
- 타겟을 계속 바라보면서 이동
- **사용**: Lazy_Brave, Lazy_Playful
- **위치**: `PersonalityReactionInteraction.cs:1493`

---

### **5. CircleAroundEachOther** - 양방향 원 운동
- 두 펫이 중심점 기준으로 동시에 원 운동
- 정반대 위치에서 시작 (180° 차이)
- 서로를 바라보면서 이동
- **사용**: Brave_Brave, Brave_Playful, Playful_Playful
- **위치**: `PersonalityReactionInteraction.cs:1565`

---

### **6. SmoothMoveToPosition** - 부드러운 접근
- 먼저 회전 → 그 다음 이동
- 회전 각도 작으면 (< 10°) 즉시 이동
- agent.updateRotation 비활성화 후 수동 회전
- **사용**: 모든 빠른 접근 (Brave, Playful)
- **위치**: `PersonalityReactionInteraction.cs:1783`

---

### **7. SafeSetNavMeshAgent** - 안전한 NavMeshAgent 조작
- NavMeshAgent 유효성 검사 포함
- isStopped, speed, destination 동시 설정
- 실패 시 경고 로그 출력
- **사용**: 모든 패턴에서 이동 전
- **위치**: `PersonalityReactionInteraction.cs:1406`

---

### **8. ForceCompleteCleanup** - 강제 정리
- 유저 입력으로 상호작용 중단 시 호출
- 애니메이션 즉시 중단
- NavMeshAgent 상태 초기화
- AI 강제 재시작
- **위치**: `PersonalityReactionInteraction.cs:1670`

---

## 📊 속도 비교

| 성격 | 속도 범위 | 특징 |
|------|----------|------|
| **Lazy** | 30~40% | 매우 느림 |
| **Shy** | 100~200% | 도망칠 때 빠름 |
| **Brave** | 70~200% | 상황에 따라 가변 |
| **Playful** | 150~200% | 항상 빠름 |

---

## 🎯 거리별 행동 변화

### 가까이 있을 때 (< 3~5m)
- ✅ **접근 단계 스킵** (불필요한 이동 제거)
- ✅ **즉시 반응** (더 자연스러움)
- 예: Lazy는 바로 누워버림, Shy는 즉시 도망
- 각 패턴별 `skipApproachThreshold` 값 사용

### 멀리 있을 때
- 정상적인 접근 단계 실행
- 부드러운 회전 후 이동 (`SmoothMoveToPosition`)

---

## 💡 핵심 특징

### 1. 성격 반영
- Lazy: 느린 속도, 누워서 휴식, 무반응
- Shy: 도망, 돌아보기, 호기심
- Brave: 당당한 접근, 위엄 과시, 추격
- Playful: 빠른 속도, 점프 많음, 추격전

### 2. 펫 크기 고려
- `CalculateApproachDistance()`: CapsuleCollider 반지름 기반 거리 계산
- `CalculateCircleRadius()`: 원 운동 반지름 크기 고려
- `CalculateCircleDuration()`: 원 둘레와 속도 기반 시간 계산
- 큰 펫끼리는 더 먼 거리 유지

### 3. 자연스러운 움직임
- 부드러운 회전 (Slerp, SmoothStep)
- NavMesh 기반 안전한 이동
- 모든 목표 위치 유효성 검증 (`FindValidPositionOnNavMesh`)

### 4. 안전 장치
- NavMeshAgent 유효성 체크 (`SafeSetNavMeshAgent`)
- 타임아웃 설정 (무한 대기 방지)
- 상태 자동 복원 (속도, 회전 등)
- 강제 정리 메서드 (`OnForceCleanup`, `ForceCompleteCleanup`)

---

## 🎬 움직임 패턴 요약

| 조합 | 핵심 움직임 | 복잡도 | 주요 메서드 |
|------|-----------|--------|------------|
| Lazy_Lazy | 느린 접근 → 함께 누움 | ⭐⭐ 낮음 | MoveToPositionsPrecise |
| Lazy_Shy | 비대칭 접근 → 도망 → 재접근 | ⭐⭐ 낮음 | QuickRetreat |
| Lazy_Brave | 원 운동 (단방향) | ⭐⭐⭐ 중간 | CircleAroundTarget |
| Lazy_Playful | 점프 + 원 운동 | ⭐⭐⭐ 중간 | CircleAroundTarget |
| Shy_Shy | 대칭 도망 (2단계) | ⭐⭐⭐ 중간 | QuickRetreat (동시) |
| Shy_Brave | 추격 패턴 | ⭐⭐⭐⭐ 높음 | QuickRetreat + 추격 |
| Shy_Playful | 2번 도망 | ⭐⭐⭐ 중간 | QuickRetreat |
| Brave_Brave | 원 운동 + 경주 | ⭐⭐⭐⭐ 높음 | CircleAroundEachOther |
| Brave_Playful | 원 운동 + 양방향 추격 | ⭐⭐⭐⭐ 높음 | CircleAroundEachOther |
| **Playful_Playful** | **점프×3 + 원×2 + 추격×2** | **⭐⭐⭐⭐⭐ 최고** | 모든 메서드 사용 |
| Default | 간단 접근 | ⭐ 최소 | SetDestination |

---

## 🚀 새로운 패턴 만들 때 체크리스트

1. **거리 임계값 정하기** (3m or 5m)
   - 가까우면 접근 스킵
   - `skipApproachThreshold` 변수 사용

2. **속도 정하기**
   - 성격에 맞는 속도 배율 설정
   - `pet.baseSpeed * 배율` 형식 사용

3. **이동 방식 선택**
   - 정밀 이동? → `MoveToPositionsPrecise`
   - 도망? → `QuickRetreat`
   - 빠른 접근? → `SmoothMoveToPosition`
   - 원 운동? → `CircleAroundTarget` 또는 `CircleAroundEachOther`
   - 안전한 설정? → `SafeSetNavMeshAgent`

4. **애니메이션 선택**
   - 이동: Walk (느림) / Run (빠름)
   - 액션: Jump, Rest, Eat
   - 연속: `SetContinuousAnimation`
   - 일회성: `PlayAnimationWithCustomDuration`

5. **마주보기 추가**
   - `SmoothlyLookAtEachOther` (거의 모든 패턴에 사용)
   - PersonalityReaction 전용 버전은 Walk 애니메이션 없음

6. **정리 코드 추가**
   - `finally` 블록에서 상태 복원
   - `wasInterrupted` 체크로 강제 정리 처리

---

## ⚠️ 주의사항

1. **NavMeshAgent 체크 필수**: 모든 이동 전 `SafeSetNavMeshAgent()` 호출
2. **속도 복원**: 임시로 변경한 속도는 반드시 원래 값으로 복원
3. **애니메이션 정리**: 패턴 종료 시 `StopContinuousAnimation()` 호출
4. **회전 업데이트 복원**: `updateRotation = false` 후 반드시 `true`로 복원

---

## 📝 코드 구조

```
PersonalityReactionInteraction.cs (약 2000줄)
├── 설정 필드 (35-68줄)
│   ├── reactionDuration, approachDistance, preciseThreshold
│   ├── fleeDistance, moveTimeout
│   └── pauseDuration, lookDuration, jumpInterval, chaseDuration
├── 생명주기 메서드
│   ├── PerformInteraction() - 메인 진입점
│   ├── OnForceCleanup() - 강제 종료 시 정리
│   └── ForceCompleteCleanup() - 완전 정리
├── 패턴 실행
│   ├── ExecuteReactionPattern() - 패턴 분기
│   ├── LazyLazyReaction() ~ PlayfulPlayfulReaction() - 10개 패턴
│   └── DefaultReaction() - 기본 패턴
└── 헬퍼 메서드
    ├── SafeSetNavMeshAgent() - NavMesh 안전 조작
    ├── SmoothlyLookAtEachOther() - 마주보기 (재정의)
    ├── CircleAroundTarget() - 단방향 원 운동
    ├── CircleAroundEachOther() - 양방향 원 운동
    ├── SmoothMoveToPosition() - 부드러운 이동
    ├── QuickRetreat() - 빠른 도망
    ├── MoveToPositionsPrecise() - 정밀 이동
    ├── CalculateApproachDistance() - 크기별 거리
    ├── CalculateCircleRadius() - 원 운동 반지름
    └── CalculateCircleDuration() - 원 운동 시간
```

---

**문서 끝**
