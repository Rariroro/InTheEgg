# 펫 상호작용 거리 조정 시스템 분석

> 작성일: 2025-01-20
> 목적: 상호작용별 거리 조정 방식 및 펫 크기 고려 여부 분석

---

## 📋 목차

1. [개요](#개요)
2. [BasePetInteraction 자동 거리 조정 시스템](#basepetinteraction-자동-거리-조정-시스템)
3. [크기 배율 시스템](#크기-배율-시스템)
4. [상호작용별 분석](#상호작용별-분석)
5. [자체 로직 사용 상호작용 상세](#자체-로직-사용-상호작용-상세)
6. [특별 케이스](#특별-케이스)
7. [문제점 및 개선 제안](#문제점-및-개선-제안)

---


## 개요

InTheEgg 프로젝트의 펫 상호작용 시스템은 **두 가지 방식**으로 펫 간 거리를 조정합니다:

### ✅ 방식 1: BasePetInteraction의 통일된 자동 거리 조정
- 대부분의 상호작용 (약 70%)
- 펫 크기를 자동으로 고려
- `CalculateAdjustedDistance()` 메서드 사용

### ❌ 방식 2: 개별 상호작용의 자체 로직
- 특수한 상호작용 (약 30%)
- 경주, 나란히 걷기 등 특별한 배치가 필요한 경우
- **일부는 펫 크기를 고려하지 않음** ⚠️

---

## BasePetInteraction 자동 거리 조정 시스템

### 처리 흐름

**위치**: `BasePetInteraction.cs` (97-115줄)

```csharp
// 1. 상호작용 시작 시 자동으로 실행
protected IEnumerator StartInteraction(PetController pet1, PetController pet2)
{
    // ... NavMesh 확인 등 ...

    // 2. 펫 크기에 따른 거리 조정
    float adjustedDistance = CalculateAdjustedDistance(pet1, pet2);

    // 3. 중간점 기준으로 양쪽에 배치
    Vector3 midpoint = (pet1.transform.position + pet2.transform.position) / 2f;
    Vector3 pet1TargetPos = midpoint - direction * (adjustedDistance / 2f);
    Vector3 pet2TargetPos = midpoint + direction * (adjustedDistance / 2f);

    // 4. 계산된 위치로 이동
    yield return StartCoroutine(MoveToPositions(pet1, pet2, pet1TargetPos, pet2TargetPos));

    // 5. 이동 완료 후 PerformInteraction() 호출
    yield return StartCoroutine(PerformInteraction(pet1, pet2));
}
```

### CalculateAdjustedDistance() 메서드

**위치**: `BasePetInteraction.cs` (557-572줄)

```csharp
protected float CalculateAdjustedDistance(PetController pet1, PetController pet2)
{
    // 각 펫의 크기 배율 가져오기
    float multiplier1 = pet1.Profile.GetInteractionDistanceMultiplier();
    float multiplier2 = pet2.Profile.GetInteractionDistanceMultiplier();

    // 두 펫의 평균 배율 계산
    float averageMultiplier = (multiplier1 + multiplier2) / 2f;

    // 기본 거리(5m)에 평균 배율 적용
    float adjustedDistance = interactionStartDistance * averageMultiplier;

    return adjustedDistance;
}
```

---

## 크기 배율 시스템

### GetInteractionDistanceMultiplier() 메서드

**위치**: `PetProfile.cs` (73-86줄)

```csharp
public float GetInteractionDistanceMultiplier()
{
    switch (size)
    {
        case PetTraits.Size.Small:
            return 0.6f;  // 기본 거리의 60%
        case PetTraits.Size.Medium:
            return 1.0f;  // 기본 거리 100%
        case PetTraits.Size.Large:
            return 1.5f;  // 기본 거리의 150%
        default:
            return 1.0f;
    }
}
```

### 크기별 거리 예시

| 조합 | 배율 계산 | 기본 거리 5m 적용 시 |
|------|----------|---------------------|
| Small + Small | (0.6 + 0.6) / 2 = 0.6 | **3.0m** |
| Small + Medium | (0.6 + 1.0) / 2 = 0.8 | **4.0m** |
| Medium + Medium | (1.0 + 1.0) / 2 = 1.0 | **5.0m** |
| Medium + Large | (1.0 + 1.5) / 2 = 1.25 | **6.25m** |
| Large + Large | (1.5 + 1.5) / 2 = 1.5 | **7.5m** |

**예시**:
- 생쥐 + 토끼 (Small + Small) → 3m
- 코끼리 + 기린 (Large + Large) → 7.5m

---

## 상호작용별 분석

### 전체 상호작용 거리 조정 방식 비교

| 상호작용 | Base 자동<br/>거리 조정 | 자체 로직<br/>추가 사용 | 펫 크기<br/>고려 | 비고 |
|---------|:---:|:---:|:---:|------|
| **FightInteraction** | ✅ | ✅ | ✅ | 이중 거리 조정 (중복) |
| **HeadbuttInteraction** | ✅ | ❌ | ✅ | 정상 작동 |
| **ChaseAndRunInteraction** | ✅ | ❌ | ✅ | 정상 작동 |
| **ChameleonCamouflageInteraction** | ✅ | ❌ | ✅ | 정상 작동 |
| **SkunkDefenseInteraction** | ✅ | ❌ | ✅ | 정상 작동 |
| **PersonalityReactionInteraction** | ✅ | ❌ | ✅ | 정상 작동 |
| **PredatorMoleInteraction** | ✅ | ❌ | ✅ | 정상 작동 |
| **PredatorPossumPrankInteraction** | ✅ | ❌ | ✅ | 정상 작동 |
| **CamelAlpacaSpitFightInteraction** | ✅ | ❌ | ✅ | 정상 작동 |
| **RestAndSleepTogetherInteraction** | ✅ | ✅ | ❌ | 이중 위치 계산, 크기 미고려 |
| **RideAndWalkInteraction** | ❌ | ✅ | N/A | 거리 조정 불필요 (탑승) |
| **TurtleRabbitRace** | ❌ | ✅ | ❌ | 경주 로직, **크기 미고려** ⚠️ |
| **SlothKoalaRaceInteraction** | ❌ | ✅ | ❌ | 경주 로직, **크기 미고려** ⚠️ |
| **WalkTogetherInteraction** | ❌ | ✅ | ❌ | 나란히 걷기, **크기 미고려** ⚠️ |

### 분류 요약

#### ✅ BasePetInteraction 자동 거리 조정 사용 (10개)
- FightInteraction
- HeadbuttInteraction
- ChaseAndRunInteraction
- ChameleonCamouflageInteraction
- SkunkDefenseInteraction
- PersonalityReactionInteraction
- PredatorMoleInteraction
- PredatorPossumPrankInteraction
- CamelAlpacaSpitFightInteraction
- RestAndSleepTogetherInteraction (부분적)

#### ❌ 자체 로직 사용 (4개)
- RideAndWalkInteraction
- TurtleRabbitRace
- SlothKoalaRaceInteraction
- WalkTogetherInteraction

---

## 자체 로직 사용 상호작용 상세

### 1. RideAndWalkInteraction (타고 걷기)

**위치**: `RideAndWalkInteraction.cs` (155-189줄)

**거리 조정 방식**:
```csharp
protected override IEnumerator PerformInteraction(PetController pet1, PetController pet2)
{
    // 역할 식별 (rider, mount)
    // 바로 WaitUntilAgentIsReady() 호출
    // 거리 조정 없음 - 만난 위치에서 바로 탑승
}
```

**특징**:
- ❌ BasePetInteraction 자동 거리 조정 건너뜀
- ✅ 이유: 탑승 동작이므로 거리 조정 불필요
- 탑승 위치는 `ridePoint` Transform 또는 Collider 기반 자동 계산

**크기 고려**: N/A (탑승 위치는 개별 계산)

---

### 2. TurtleRabbitRace (토끼와 거북이 경주)

**위치**: `TurtleRabbitRace.cs` (189-249줄)

**거리 조정 방식**:
```csharp
protected override IEnumerator PerformInteraction(...)
{
    // 1. WaitUntilAgentIsReady() 호출 (207-208줄)

    // 2. 결승선 위치 무작위 설정 (241-249줄)
    Vector3 finishLine = initialCenter + randomDirection * raceDistance;

    // 3. 출발선 계산
    CalculateStartPositions(fastPet, slowPet, out fastStart, out slowStart, 3f);

    // 4. 출발선으로 이동
    yield return StartCoroutine(MoveToPositions(...));
}
```

**특징**:
- ❌ BasePetInteraction 자동 거리 조정 건너뜀
- ✅ 이유: 경주 특성상 출발선-결승선 개념 필요
- ❌ **문제**: 출발선 간격 3f 고정 (펫 크기 미고려)

**크기 고려**: ❌ 없음

**개선 제안**:
```csharp
// 현재
CalculateStartPositions(fastPet, slowPet, out fastStart, out slowStart, 3f);

// 개선안
float averageMultiplier = (fastPet.Profile.GetInteractionDistanceMultiplier() +
                           slowPet.Profile.GetInteractionDistanceMultiplier()) / 2f;
float adjustedSpacing = 3f * averageMultiplier;
CalculateStartPositions(fastPet, slowPet, out fastStart, out slowStart, adjustedSpacing);
```

---

### 3. SlothKoalaRaceInteraction (느린 경주)

**위치**: `SlothKoalaRaceInteraction.cs` (70-145줄)

**거리 조정 방식**:
```csharp
protected override IEnumerator PerformInteraction(...)
{
    // 1. WaitUntilAgentIsReady() 호출 (77-78줄)

    // 2. 결승선 계산
    // 3. 출발선 계산 (145줄)
    CalculateStartPositions(sloth, koala, out slothStart, out koalaStart, 3f);

    // 4. 출발선으로 이동
    yield return StartCoroutine(MoveToPositions(...));
}
```

**특징**:
- ❌ BasePetInteraction 자동 거리 조정 건너뜀
- ✅ 이유: 경주 특성상 출발선-결승선 개념 필요
- ❌ **문제**: 출발선 간격 3f 고정 (펫 크기 미고려)

**크기 고려**: ❌ 없음

**개선 제안**: TurtleRabbitRace와 동일

---

### 4. WalkTogetherInteraction (함께 걷기)

**위치**: `WalkTogetherInteraction.cs` (103-183줄)

**거리 조정 방식**:
```csharp
protected override IEnumerator PerformInteraction(...)
{
    // 1. WaitUntilAgentIsReady() 호출 (108-109줄)

    // 2. PrepareWalkPhase 호출 (142줄)
    PrepareWalkPhase() {
        // 시작 위치 계산 (나란히 서기)
        CalculateStartPositions(pet1, pet2, out pos1, out pos2, petSpacing);

        // 시작 위치로 이동 (183줄)
        yield return StartCoroutine(MoveToPositions(...));
    }
}
```

**특징**:
- ❌ BasePetInteraction 자동 거리 조정 건너뜀
- ✅ 이유: 나란히 걷기 위해 특정 간격(petSpacing = 2.5f) 필요
- ❌ **문제**: 간격 2.5f 고정 (펫 크기 미고려)

**크기 고려**: ❌ 없음

**개선 제안**:
```csharp
// 현재
public float petSpacing = 2.5f;
CalculateStartPositions(pet1, pet2, out pos1, out pos2, petSpacing);

// 개선안
float averageMultiplier = (pet1.Profile.GetInteractionDistanceMultiplier() +
                           pet2.Profile.GetInteractionDistanceMultiplier()) / 2f;
float adjustedSpacing = petSpacing * averageMultiplier;
CalculateStartPositions(pet1, pet2, out pos1, out pos2, adjustedSpacing);
```

---

## 특별 케이스

### 1. FightInteraction (이중 거리 조정)

**위치**: `FightInteraction.cs` (120-215줄)

**문제점**: 거리를 **두 번** 조정함

```csharp
protected override IEnumerator PerformInteraction(...)
{
    // 1단계: BasePetInteraction의 자동 거리 조정 완료 상태
    // → 이미 5m × 크기배율로 배치됨
    yield return StartCoroutine(WaitUntilAgentIsReady(...)); (125줄)

    // 2단계: PrepareFightPhase에서 다시 거리 재계산
    PrepareFightPhase() {
        Vector3 fightSpot = FindInteractionSpot(pet1, pet2); (187줄)

        // 자체 크기 조정 로직 (중복!)
        float adjustedFightDistance = CalculateDistanceBySize(pet1, pet2); (192줄)

        // 싸움 위치로 다시 이동
        yield return StartCoroutine(MoveToPositions(...)); (202줄)
    }
}
```

**CalculateDistanceBySize() 메서드**:
```csharp
// FightInteraction.cs (374-389줄)
private float CalculateDistanceBySize(PetController pet1, PetController pet2)
{
    float pet1Multiplier = pet1.Profile.GetInteractionDistanceMultiplier();
    float pet2Multiplier = pet2.Profile.GetInteractionDistanceMultiplier();
    float averageMultiplier = (pet1Multiplier + pet2Multiplier) / 2f;
    float adjustedDistance = fightDistance * averageMultiplier;

    return adjustedDistance;
}
```

**문제점**:
- ✅ 크기 고려함
- ❌ `BasePetInteraction.CalculateAdjustedDistance()`와 **로직 완전 중복**
- ❌ 펫이 **두 번 이동**함 (비효율)

**개선 제안**:
```csharp
// 옵션 1: BasePetInteraction의 자동 거리 조정만 사용
// → PrepareFightPhase에서 CalculateDistanceBySize() 제거

// 옵션 2: 싸움만의 특별한 거리가 필요하다면
// → BasePetInteraction.interactionStartDistance = fightDistance로 설정
```

---

### 2. RestAndSleepTogetherInteraction (이중 위치 계산)

**위치**: `RestAndSleepTogetherInteraction.cs` (123-232줄)

**문제점**: 위치를 **두 번** 계산함

```csharp
protected override IEnumerator PerformInteraction(...)
{
    // 1단계: BasePetInteraction의 자동 거리 조정 완료 상태
    // → 이미 5m × 크기배율로 배치됨
    yield return StartCoroutine(WaitUntilAgentIsReady(...)); (133줄)

    // 2단계: PreparePhase에서 위치 재계산
    PreparePhase() {
        // 수면 장소 찾기
        Vector3 interactionSpot = FindInteractionSpot(pet1, pet2, 2f); (225줄)

        // 수면 위치 계산 (크기 미고려!)
        CalculateStartPositions(pet1, pet2, out target1, out target2, distance); (232줄)

        // 수면 위치로 다시 이동
        yield return StartCoroutine(MoveToPositions(...));
    }
}
```

**문제점**:
- ❌ 크기 고려 안 함
- ❌ 펫이 **두 번 이동**함 (비효율)

**개선 제안**:
```csharp
// PreparePhase에서 크기 배율 적용
float averageMultiplier = (pet1.Profile.GetInteractionDistanceMultiplier() +
                           pet2.Profile.GetInteractionDistanceMultiplier()) / 2f;
float adjustedDistance = distance * averageMultiplier;
CalculateStartPositions(pet1, pet2, out target1, out target2, adjustedDistance);
```

---

## 문제점 및 개선 제안

### 🔴 문제점 요약

#### 1. 일관성 부족
- 70%는 BasePetInteraction 자동 거리 조정 사용
- 30%는 자체 로직 사용
- 개발자가 혼란스러울 수 있음

#### 2. 크기 고려 누락
다음 상호작용들은 **펫 크기를 고려하지 않음**:
- TurtleRabbitRace (간격 3f 고정)
- SlothKoalaRaceInteraction (간격 3f 고정)
- WalkTogetherInteraction (간격 2.5f 고정)
- RestAndSleepTogetherInteraction (크기 미고려)

**증상**:
- 코끼리 + 토끼 경주 시 3f 간격 (너무 가까움)
- 생쥐 + 생쥐 함께 걷기 시 2.5f 간격 (너무 멀음)

#### 3. 코드 중복
- `FightInteraction.CalculateDistanceBySize()` ≈ `BasePetInteraction.CalculateAdjustedDistance()`
- 동일한 로직을 두 곳에서 유지보수

#### 4. 비효율적인 이중 이동
- FightInteraction: 펫이 두 번 이동
- RestAndSleepTogetherInteraction: 펫이 두 번 이동

---

### 💡 개선 제안

#### 옵션 1: BasePetInteraction 활용 (권장 ⭐)

**장점**:
- 모든 상호작용이 자동으로 크기 고려
- 코드 중복 제거
- 일관성 확보

**방법**:
```csharp
// 자체 로직이 필요한 상호작용도 크기 배율 적용

// 예: WalkTogetherInteraction
float averageMultiplier = (pet1.Profile.GetInteractionDistanceMultiplier() +
                           pet2.Profile.GetInteractionDistanceMultiplier()) / 2f;
float adjustedSpacing = petSpacing * averageMultiplier;
CalculateStartPositions(pet1, pet2, out pos1, out pos2, adjustedSpacing);
```

---

#### 옵션 2: Helper 메서드 추가

`BasePetInteraction`에 헬퍼 메서드 추가:

```csharp
// BasePetInteraction.cs
protected float ApplySizeMultiplier(float baseDistance, PetController pet1, PetController pet2)
{
    float averageMultiplier = (pet1.Profile.GetInteractionDistanceMultiplier() +
                               pet2.Profile.GetInteractionDistanceMultiplier()) / 2f;
    return baseDistance * averageMultiplier;
}
```

**사용 예시**:
```csharp
// TurtleRabbitRace
float adjustedSpacing = ApplySizeMultiplier(3f, fastPet, slowPet);
CalculateStartPositions(fastPet, slowPet, out fastStart, out slowStart, adjustedSpacing);

// WalkTogetherInteraction
float adjustedSpacing = ApplySizeMultiplier(petSpacing, pet1, pet2);
CalculateStartPositions(pet1, pet2, out pos1, out pos2, adjustedSpacing);
```

---

#### 옵션 3: 현재 상태 유지

**선택 이유**: "작동하면 건드리지 않기"

**단점**:
- 크기 미고려로 인한 시각적 어색함
- 코드 일관성 부족
- 유지보수 어려움

---

## 코드 참조

### 주요 파일 및 메서드

| 파일 | 메서드/위치 | 설명 |
|------|-----------|------|
| `BasePetInteraction.cs` | 97-115줄 | StartInteraction() - 자동 거리 조정 |
| `BasePetInteraction.cs` | 557-572줄 | CalculateAdjustedDistance() |
| `PetProfile.cs` | 73-86줄 | GetInteractionDistanceMultiplier() |
| `FightInteraction.cs` | 374-389줄 | CalculateDistanceBySize() (중복) |
| `RideAndWalkInteraction.cs` | 155-189줄 | PerformInteraction() |
| `TurtleRabbitRace.cs` | 189-249줄 | PerformInteraction() |
| `SlothKoalaRaceInteraction.cs` | 70-145줄 | PerformInteraction() |
| `WalkTogetherInteraction.cs` | 103-183줄 | PerformInteraction() |
| `RestAndSleepTogetherInteraction.cs` | 123-232줄 | PerformInteraction() |

---

## 결론

### 현재 상태
- ✅ **70%의 상호작용**: 펫 크기 자동 고려 (정상 작동)
- ⚠️ **30%의 상호작용**: 자체 로직 사용
  - RideAndWalkInteraction: 문제 없음 (거리 조정 불필요)
  - **TurtleRabbitRace, SlothKoalaRaceInteraction, WalkTogetherInteraction**: 크기 미고려 ❌
  - FightInteraction: 중복 로직 ❌
  - RestAndSleepTogetherInteraction: 크기 미고려 + 이중 이동 ❌

### 권장 사항
1. **TurtleRabbitRace, SlothKoalaRaceInteraction, WalkTogetherInteraction**에 크기 배율 적용
2. **FightInteraction**의 중복 로직 제거 (BasePetInteraction 활용)
3. **RestAndSleepTogetherInteraction**의 이중 이동 최적화 및 크기 고려 추가

### 우선순위
1. 🔴 **High**: 크기 미고려 상호작용 수정 (시각적 어색함 개선)
2. 🟡 **Medium**: 중복 로직 제거 (유지보수성 향상)
3. 🟢 **Low**: 이중 이동 최적화 (성능 개선)

---

**작성자**: Claude Code
**검토 필요**: 크기 미고려 상호작용의 간격 조정
