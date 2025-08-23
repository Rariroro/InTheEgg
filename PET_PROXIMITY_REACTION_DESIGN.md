# 펫 근접 반응 시스템 설계 문서

## 개요
펫들이 서로 가까이 마주쳤을 때 성격에 따라 자연스러운 반응을 보이는 시스템입니다.
기존의 복잡한 상호작용과 달리, 일상적이고 가벼운 반응을 구현합니다.

## 시스템 아키텍처

### 구현 방식: Activity 기반
- **이유**: 가벼운 일상 반응으로 우선순위 시스템과 자연스럽게 통합
- **우선순위**: 0.3 (Wander(0.1)보다 높고, 다른 주요 활동보다 낮음)
- **상태 변경**: 없음 (Idle 상태 유지)

### 감지 방식: 콜라이더 기반
```
장점:
- Unity Physics 엔진의 최적화 활용
- OnTriggerEnter/Exit 이벤트 기반 처리
- Layer 필터링으로 펫만 감지
- 성능: O(1) - 공간 분할 자동 처리
```

### 시스템 구성 요소
1. **ProximityReactionActivity.cs**: 메인 반응 로직
2. **PetProximityDetector.cs**: 콜라이더 감지 컴포넌트
3. **PetController 수정**: 감지 컴포넌트 통합
4. **PetAI 수정**: Activity 등록

## 기술 사양

### 감지 설정
- **감지 범위**: 4m (SphereCollider radius)
- **Layer**: "PetProximity" (새로 생성)
- **체크 빈도**: 물리 엔진 자동 (FixedUpdate)

### 반응 설정
- **반응 시간**: 3-5초
- **쿨다운**: 15초 (특정 펫 쌍에 대해)
- **동시 반응**: 최대 1개 (다중 반응 방지)

### 쿨다운 시스템
```csharp
Dictionary<(PetController, PetController), float> pairCooldowns;
// 예: (고양이A, 강아지B) → 마지막 반응 시간
// 고양이A는 강아지C와는 즉시 반응 가능
```


## 성격별 반응 패턴 (16가지)

### 1. Lazy + Lazy
**패턴 A**: 서로 발견 → 멈춤 → 서로 쳐다보기 → 한 펫이 누움 → 다른 펫도 누움 → 잠시 후 각자 길 감
**패턴 B**: 서로 무시하고 지나감 (너무 게을러서 반응조차 안 함)

### 2. Lazy + Shy
천천히 접근 → Lazy는 누움 → Shy는 뒷걸음질 → Shy가 조심스럽게 다시 접근 → 냄새 맡기 → 헤어짐

### 3. Lazy + Brave
Brave가 빠르게 접근 → Lazy는 무반응으로 누움 → Brave가 주위를 돔 → Lazy 계속 무시 → Brave 흥미 잃고 떠남

### 4. Lazy + Playful
Playful이 신나게 접근 → Lazy는 누움 → Playful이 점프하며 놀자고 함 → Lazy 무시 → Playful 포기하고 떠남

### 5. Shy + Shy
서로 발견 → 둘 다 멈춤 → 긴 정적 → 동시에 뒷걸음 → 서로 다른 방향으로 도망

### 6. Shy + Brave
Brave가 당당히 접근 → Shy는 뒷걸음 → Brave가 천천히 따라감 → Shy 도망 → Brave는 잠시 쫓다가 포기

### 7. Shy + Playful
Playful이 뛰어옴 → Shy 깜짝 놀라 뒤로 → Playful이 점프하며 놀자고 함 → Shy 계속 뒷걸음 → Playful 혼자 놀다가 떠남

### 8. Brave + Brave
빠르게 접근 → 서로 정면 대치 → 서로 주위를 돔 (위엄 과시) → 짧은 달리기 시합 → 서로 인정하고 헤어짐

### 9. Brave + Playful
둘 다 빠르게 접근 → 서로 주위를 빙빙 돔 → Playful이 점프 → Brave도 점프 → 짧은 추격전 → 만족하고 헤어짐

### 10. Playful + Playful
신나게 달려옴 → 서로 주위를 돔 → 연속 점프 → 짧은 추격전 → 다시 점프 파티 → 신나게 놀다 헤어짐

### 11. Shy + Lazy
Shy가 조심스럽게 접근 → Lazy는 누워있음 → Shy가 냄새 맡으려 함 → Lazy 하품 → Shy 안심하고 옆에 앉음 → 평화롭게 헤어짐

### 12. Brave + Shy (보호 패턴)
Brave가 보호하듯 접근 → Shy 뒷걸음 → Brave가 멈춤 → Shy가 조금씩 접근 → Brave가 천천히 안내 → 함께 잠시 걷다 헤어짐

### 13. Playful + Lazy
Playful이 Lazy 주위를 뱅뱅 돔 → Lazy 귀찮아함 → Playful이 더 신나게 점프 → Lazy 일어나서 자리 옮김 → Playful 따라가다 포기

### 14. Brave + Lazy
Brave가 Lazy를 조사하듯 접근 → Lazy 누워서 배 보임 (항복 자세) → Brave 만족하고 떠남

### 15. Playful + Shy
Playful의 갑작스런 접근 → Shy 놀라서 숨음 → Playful이 찾기 놀이 시작 → Shy 조금씩 나옴 → 조심스러운 상호작용 → 헤어짐

### 16. 추가 변형 패턴
각 조합에서 상황에 따라 약간의 변형 가능

## 성격별 주요 행동 요소 및 애니메이션 매핑

### Lazy (게으른)
- **누움**: Rest (5) 애니메이션
- **하품**: Idle (0) 유지
- **느린 움직임**: Walk (1) + speed 0.5배
- **무반응/무시**: Idle (0) 또는 Rest (5)
- **자리 이동 최소화**: 짧은 거리 이동

### Shy (수줍은)
- **뒷걸음질**: Walk (1) + 반대 방향 이동
- **조심스러운 접근**: Walk (1) + speed 0.7배
- **도망**: Run (2) + speed 1.2배
- **숨기**: Eat (4) 애니메이션 (웅크리기)

### Brave (용감한)
- **빠른 접근**: Run (2) + speed 1.2배
- **정면 대치**: Idle (0) + LookAt
- **주위 돌기**: Walk (1) + 원형 경로
- **보호 행동**: Attack (6) 짧게 재생

### Playful (장난기 많은)
- **점프**: Jump (3) 애니메이션
- **빙빙 돌기**: Run (2) + 원형 경로
- **추격전 시도**: Run (2) + 타겟 추적
- **신나는 움직임**: Jump (3) 연속 재생

## 반응 우선순위 규칙

### 반응 시작 우선순위
1. **성격 기반 우선순위**
   - Playful > Brave > Shy > Lazy
   - 더 활발한 성격이 먼저 반응 시작

2. **여러 펫 감지 시 선택 기준**
   - 가장 가까운 펫 우선
   - 같은 거리일 경우 성격 상성이 좋은 펫 선택
   - 이미 반응 중인 펫은 제외

3. **반응 중단 조건**
   - 플레이어 터치/홀드 (즉시 중단)
   - 중요 Activity 시작 (Eat, Sleep 등)
   - 펫 간 거리가 8m 이상 벌어짐
   - 반응 시간 초과 (5초)

### 크기별 특수 처리
```csharp
// PetTraits.Size 기반 거리 조정
Small + Small: 기본 거리 * 0.7
Small + Large: 기본 거리 * 1.2  
Large + Large: 기본 거리 * 1.5

// 크기 차이별 반응 변화
코끼리 + 쥐: 코끼리는 천천히 조심스럽게, 쥐는 더 빠르게
기린 + 토끼: 기린은 고개 숙이기, 토끼는 올려다보기
```

## 구현 상세

### ReactionPattern 데이터 구조
```csharp
public class ReactionPattern
{
    public float approachSpeed;        // 접근 속도 배율
    public float reactionDuration;     // 반응 지속 시간
    public PetAnimationType[] animationSequence;  // 애니메이션 시퀀스
    public MovementType movementType;  // 이동 패턴 (직선, 원형, 뒷걸음 등)
}

public enum MovementType
{
    Direct,      // 직선 이동
    Circle,      // 원형 이동
    Backward,    // 뒷걸음
    Stop,        // 정지
    Escape       // 도망
}
```

### ProximityReactionActivity 구조
```csharp
public class ProximityReactionActivity : PetActivityAdapter
{
    // 감지된 근처 펫
    private PetController nearbyPet;
    private PetProximityDetector detector;
    
    // 반응 패턴 캐시
    private static Dictionary<(Personality, Personality), ReactionPattern> patternCache;
    
    // 쿨다운 관리
    private static Dictionary<(PetController, PetController), float> pairCooldowns;
    
    public override bool CanStart(PetState state, PetNeeds needs)
    {
        // 1. detector.NearbyPets에서 가장 가까운 펫 선택
        // 2. 쿨다운 체크 (pairCooldowns)
        // 3. 다른 중요 활동 중이 아닌지 확인
        return nearbyPet != null && !IsInCooldown(nearbyPet);
    }
    
    public override float GetPriority(PetState state, PetNeeds needs)
    {
        return 0.3f; // 가벼운 일상 반응
    }
    
    private IEnumerator PerformReaction(Personality myPersonality, Personality otherPersonality)
    {
        // 캐시에서 패턴 가져오기
        var pattern = GetReactionPattern(myPersonality, otherPersonality);
        
        // 패턴에 따라 애니메이션과 이동 실행
        yield return ExecutePattern(pattern);
    }
}
```

### PetProximityDetector 구조
```csharp
public class PetProximityDetector : MonoBehaviour
{
    private SphereCollider proximityCollider;
    private List<PetController> nearbyPets = new List<PetController>();
    
    public IReadOnlyList<PetController> NearbyPets => nearbyPets;
    public event System.Action<PetController> OnPetEntered;
    public event System.Action<PetController> OnPetExited;
    
    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Pet"))
        {
            PetController pet = other.GetComponent<PetController>();
            if (pet != null && !nearbyPets.Contains(pet))
            {
                nearbyPets.Add(pet);
                OnPetEntered?.Invoke(pet);  // Activity에 알림
            }
        }
    }
    
    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Pet"))
        {
            PetController pet = other.GetComponent<PetController>();
            if (pet != null && nearbyPets.Contains(pet))
            {
                nearbyPets.Remove(pet);
                OnPetExited?.Invoke(pet);  // Activity에 알림
            }
        }
    }
}
```

## 성능 최적화

### 1. 콜라이더 최적화
- Layer 마스크로 펫만 감지
- Trigger 모드 사용 (물리 연산 최소화)
- Kinematic Rigidbody 사용

### 2. 캐싱 전략
- 16가지 반응 패턴 미리 생성
- 성격 조합별 패턴 캐시
- 근처 펫 리스트 유지

### 3. 조기 종료 조건
- 이미 반응 중이면 스킵
- 쿨다운 중이면 즉시 반환
- 다른 중요 활동 중이면 제외

### 4. 거리 기반 필터링 (카메라 뷰 체크 대신)
- 플레이어/카메라로부터 30m 이내에서만 반응
- 카메라 frustum 체크는 하지 않음 (성능 오버헤드)
- 카메라 밖에서도 자연스러운 반응 유지

### 5. 성능 고려사항
```csharp
// ❌ 피해야 할 방식 (성능 악화)
CalculateFrustumPlanes() + TestPlanesAABB() // 매번 6개 평면 계산

// ✅ 권장 방식 (최적화)
Vector3.Distance(Camera.main.transform.position, pet.position) < 30f
```

### 6. 최대 동시 반응 제한
- 전체 씬에서 동시 3쌍까지만 반응
- 우선순위: 플레이어와 가까운 펫 우선

## 테스트 계획

### 플레이 테스트
1. **성격 조합 테스트**
   - 각 성격 조합별 반응 확인
   - 애니메이션 전환 자연스러움
   - 타이밍과 속도 조절

2. **시스템 통합 테스트**
   - 다른 Activity와의 우선순위 경쟁
   - 기존 상호작용 시스템과 충돌 없음 확인
   - 쿨다운 시스템 동작 확인

3. **성능 테스트**
   - 20마리 이상 펫 동시 배치
   - FPS 모니터링 (60FPS 유지)
   - 메모리 사용량 체크

### 시나리오 테스트
1. 3마리 이상 동시 접근
2. 연속 반응 시도
3. 다양한 환경에서 테스트 (물가, 나무 근처 등)


## 구현 시 주의사항

### 핵심 원칙
1. **상태 변경 금지**: 반응 중에도 Idle 상태 유지
2. **기존 시스템 우선**: 중요한 활동(먹기, 자기)이 우선
3. **자연스러움**: 과도한 반응 피하기
4. **성능 우선**: 프레임 드롭 없도록 최적화

### 애니메이션 처리
- **애니메이션 전환 시간**: 0.2초 블렌딩
- **이동 속도 범위**: baseSpeed * 0.5 ~ 1.5배
- **회전 smoothing**: Quaternion.Slerp 사용 (t=3f)

### 예외 처리
- **NavMesh 밖**: 반응 스킵
- **물속/나무 위**: 특수 상태 우선
- **플레이어 터치**: 즉시 중단 (IsSelected/IsHolding 체크)

### Physics 설정
```
Layer 생성: "PetProximity" (Layer 10 권장)
Physics Settings:
- PetProximity ↔ PetProximity: ✓ (체크)
- PetProximity ↔ Default: ✗ (체크 해제)
- PetProximity ↔ Water: ✗ (체크 해제)
```

### 디버깅 팁
- `Debug.DrawLine()`으로 반응 범위 시각화
- 쿨다운 타이머 Inspector에 노출
- 반응 로그는 조건부 컴파일 (#if UNITY_EDITOR)

---

*이 문서는 펫 근접 반응 시스템의 설계 가이드라인입니다.*
*구현 시 이 문서를 참고하여 일관성 있는 개발을 진행하세요.*