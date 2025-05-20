// 수면 탐지, 접근, 수면 동작 및 졸림 수치 조절 전담
using System.Collections;
using UnityEngine;
using UnityEngine.AI;

public class PetSleepingController : MonoBehaviour
{
    private PetController petController;
    private GameObject targetSleepingArea;
    private float detectionRadius = 100f;
    private float sleepingDistance = 2f;
    private bool isSleeping = false;
    private float sleepDuration = 15f; // 잠자는 시간(초)
    private float sleepIncreaseRate = 0.1f; // 졸림 증가 속도
    
    // 마지막으로 졸림 상태를 표시한 시간
    private float lastSleepyEmotionTime = 0f;
    // 졸림 상태 표시 간격 (초)
    private float sleepyEmotionInterval = 30f;
    
    // 수면 공간을 위한 레이어 마스크
    private int sleepingAreaLayer;
    private const string SLEEPING_AREA_TAG = "SleepingArea";
    
    // 졸림에 따른 속도 감소 관련 변수
    private float minSpeedFactor = 0.3f; // 최소 속도 비율 (100% 졸림일 때)
    private float lastSleepinessCheck = 0f; // 마지막 졸림 체크 시간
    private float sleepinessCheckInterval = 5f; // 졸림 체크 간격
    
    // 강제 수면 관련 변수
    private float forceSleepThreshold = 100f; // 강제 수면 임계값
    private bool hasForceSleepTriggered = false; // 강제 수면 트리거 여부
    
    // 성격별 강제 수면 임계값
    private float lazyForceSleepThreshold = 70f; // 게으른 성격의 강제 수면 임계값
    private float playfulForceSleepThreshold = 90f; // 활발한 성격의 강제 수면 임계값
    private float braveForceSleepThreshold = 80f; // 용감한 성격의 강제 수면 임계값
    private float shyForceSleepThreshold = 100f; // 수줍은 성격의 강제 수면 임계값

    public void Init(PetController controller)
    {
        petController = controller;
        
        // "SleepingArea" 레이어를 사용하는 경우 아래 코드를 사용
        sleepingAreaLayer = LayerMask.GetMask("SleepingArea");
        
        // 레이어를 사용하지 않고 태그만 사용하는 경우 레이어 마스크를 전체로 설정
        if (sleepingAreaLayer == 0)
        {
            sleepingAreaLayer = Physics.DefaultRaycastLayers;
        }
    }

    public void UpdateSleeping()
    {
        // 자고 있지 않은 경우에만 졸림 증가
        if (!isSleeping)
        {
            // 시간에 따라 졸림 증가 (성격 기반 다른 증가율)
            float personalitySleepModifier = GetPersonalitySleepModifier();
            petController.sleepiness += Time.deltaTime * sleepIncreaseRate * personalitySleepModifier;
            petController.sleepiness = Mathf.Clamp(petController.sleepiness, 0f, 100f);
            
            // 졸림이 높으면 주기적으로 감정 표시 (졸림이 높을수록 더 자주 표시)
            float currentEmotionInterval = sleepyEmotionInterval * (1f - (petController.sleepiness / 100f) * 0.7f);
            if (petController.sleepiness > 60f && 
                Time.time - lastSleepyEmotionTime > currentEmotionInterval)
            {
                petController.ShowEmotion(EmotionType.Sleepy, 5f);
                lastSleepyEmotionTime = Time.time;
            }
            
            // 졸림에 따른 속도 조절 (5초마다 체크)
            if (Time.time - lastSleepinessCheck > sleepinessCheckInterval)
            {
                UpdateSpeedBasedOnSleepiness();
                lastSleepinessCheck = Time.time;
            }
            
            // 졸림이 일정 수준 이상이고 아직 잠자리 목표가 없으며 자고 있지 않은 경우 잠자리 탐지
            if (petController.sleepiness > 70f && targetSleepingArea == null && !isSleeping && !hasForceSleepTriggered)
            {
                DetectSleepingArea();
            }
            
            // 성격별 강제 수면 임계값 체크
            float personalityThreshold = GetPersonalityForceSleepThreshold();
            
            // 졸림이 성격별 임계값을 넘으면 강제 수면 시작
            if (petController.sleepiness >= personalityThreshold && !isSleeping && !hasForceSleepTriggered)
            {
                hasForceSleepTriggered = true;
                StartCoroutine(ForceSleepAtCurrentLocation());
            }
        }

        // 잠자리가 발견됐고 자고 있지 않은 경우
        if (targetSleepingArea != null && !isSleeping)
        {
            // 에이전트가 활성화되어 있고 네비메시에 있는지 확인
            if (petController.agent != null && petController.agent.enabled && petController.agent.isOnNavMesh)
            {
                // XZ 평면에서의 거리 계산 (Y축 차이 무시)
                Vector3 petPosition = petController.transform.position;
                Vector3 targetPosition = targetSleepingArea.transform.position;
                float xzDistance = Vector2.Distance(
                    new Vector2(petPosition.x, petPosition.z),
                    new Vector2(targetPosition.x, targetPosition.z)
                );
                
                // 목적지에 충분히 가까워졌는지 확인 (XZ 평면에서)
                if (xzDistance <= sleepingDistance || 
                    (petController.agent.remainingDistance <= sleepingDistance && !petController.agent.pathPending))
                {
                    // 잠자리에 도착했으면 수면 시작
                    Debug.Log($"{petController.petName}이(가) 수면 공간에 도착했습니다. 거리: {xzDistance}");
                    StartCoroutine(Sleep(true)); // 정규 수면 공간에서 자는 것으로 표시
                }
                else
                {
                    // 아직 도착하지 않았으면 계속 이동
                    petController.agent.SetDestination(targetSleepingArea.transform.position);
                }
            }
        }
    }

    // 성격에 따른 졸림 증가 배율 반환
    private float GetPersonalitySleepModifier()
    {
        switch (petController.personality)
        {
            case PetAIProperties.Personality.Lazy:
                return 1.5f; // 게으른 성격: 더 빨리 졸림
            case PetAIProperties.Personality.Playful:
                return 1.2f; // 활발한 성격: 평균보다 조금 더 빨리 졸림
            case PetAIProperties.Personality.Brave:
                return 0.8f; // 용감한 성격: 덜 졸림
            case PetAIProperties.Personality.Shy:
                return 1.0f; // 수줍은 성격: 기본 졸림
            default:
                return 1.0f;
        }
    }
    
    // 성격에 따른 강제 수면 임계값 반환
    private float GetPersonalityForceSleepThreshold()
    {
        switch (petController.personality)
        {
            case PetAIProperties.Personality.Lazy:
                return lazyForceSleepThreshold; // 게으른 성격: 더 빨리 강제 수면
            case PetAIProperties.Personality.Playful:
                return playfulForceSleepThreshold; // 활발한 성격: 기본보다 조금 빨리 강제 수면
            case PetAIProperties.Personality.Brave:
                return braveForceSleepThreshold; // 용감한 성격: 가장 늦게 강제 수면
            case PetAIProperties.Personality.Shy:
                return shyForceSleepThreshold; // 수줍은 성격: 중간 정도에 강제 수면
            default:
                return forceSleepThreshold;
        }
    }
    
    // 졸림에 따른 속도 조절
    private void UpdateSpeedBasedOnSleepiness()
    {
        if (petController.agent != null && petController.agent.enabled)
        {
            // 졸림 수치에 따라 속도 조절 (졸림이 심할수록 속도 감소)
            float sleepinessFactor = 1f - ((petController.sleepiness / 100f) * (1f - minSpeedFactor));
            petController.agent.speed = petController.baseSpeed * sleepinessFactor;
            
            // 엔진 디버그용 로그
            if (petController.sleepiness > 80f)
            {
                Debug.Log($"{petController.petName}이(가) 매우 졸려합니다. 현재 속도: {petController.agent.speed:F2} (기본 속도의 {sleepinessFactor*100:F0}%)");
            }
        }
    }
    
    // 현재 위치에서 강제 수면 시작
    private IEnumerator ForceSleepAtCurrentLocation()
    {
        Debug.Log($"{petController.petName}이(가) 너무 졸려서 현재 위치에서 잠듭니다. 졸림 수치: {petController.sleepiness:F0}");
        petController.ShowEmotion(EmotionType.Sleepy, 3f);
        yield return new WaitForSeconds(1f);
        
        // 현재 위치에서 수면 시작 (정규 수면 공간이 아닌 것으로 표시)
        yield return StartCoroutine(Sleep(false));
        
        // 강제 수면 상태 초기화
        hasForceSleepTriggered = false;
    }

    private void DetectSleepingArea()
    {
        // 콜라이더 기반으로 수면 공간 탐지
        Collider[] sleepingAreas = Physics.OverlapSphere(transform.position, detectionRadius, sleepingAreaLayer);
        
        // 태그로 필터링 및 가장 가까운 수면 공간 찾기
        GameObject nearestSleepingArea = null;
        float nearestDistance = float.MaxValue;
        
        foreach (Collider collider in sleepingAreas)
        {
            // 태그가 "SleepingArea"인 오브젝트만 고려
            if (collider.CompareTag(SLEEPING_AREA_TAG))
            {
                float distance = Vector3.Distance(transform.position, collider.transform.position);
                if (distance < nearestDistance)
                {
                    nearestSleepingArea = collider.gameObject;
                    nearestDistance = distance;
                }
            }
        }
        
        // 수면 공간을 찾았다면
        if (nearestSleepingArea != null)
        {
            targetSleepingArea = nearestSleepingArea;
            
            // 졸림 감정 표현
            petController.ShowEmotion(EmotionType.Sleepy, 5f);
            
            // 이동 시작
            if (petController.agent != null && petController.agent.enabled && petController.agent.isOnNavMesh)
            {
                // 수면 공간의 랜덤한 위치로 이동
                Vector3 sleepPosition = GetRandomPositionInSleepingArea(nearestSleepingArea);
                petController.agent.SetDestination(sleepPosition);
            }
            
            Debug.Log($"{petController.petName}이(가) 수면 공간을 찾아 이동합니다. 위치: {targetSleepingArea.transform.position}");
        }
        else
        {
            // 넓은 범위에서 다시 시도
            Collider[] wideSleepingAreas = Physics.OverlapSphere(transform.position, detectionRadius * 3f, sleepingAreaLayer);
            
            foreach (Collider collider in wideSleepingAreas)
            {
                if (collider.CompareTag(SLEEPING_AREA_TAG))
                {
                    targetSleepingArea = collider.gameObject;
                    
                    // 졸림 감정 표현
                    petController.ShowEmotion(EmotionType.Sleepy, 5f);
                    
                    if (petController.agent != null && petController.agent.enabled && petController.agent.isOnNavMesh)
                    {
                        Vector3 sleepPosition = GetRandomPositionInSleepingArea(collider.gameObject);
                        petController.agent.SetDestination(sleepPosition);
                    }
                    
                    Debug.Log($"{petController.petName}이(가) 더 넓은 범위에서 수면 공간을 찾았습니다. 위치: {collider.transform.position}");
                    return;
                }
            }
            
            Debug.Log($"{petController.petName}이(가) 수면 공간을 찾지 못했습니다.");
        }
    }
    
    // 수면 공간 내의, NavMesh 위의 랜덤한 위치 찾기
    private Vector3 GetRandomPositionInSleepingArea(GameObject sleepingArea)
    {
        Collider sleepingAreaCollider = sleepingArea.GetComponent<Collider>();
        if (sleepingAreaCollider == null)
        {
            return sleepingArea.transform.position;
        }
        
        // 콜라이더 중심 위치 구하기
        Vector3 colliderCenter;
        
        if (sleepingAreaCollider is BoxCollider)
        {
            BoxCollider boxCollider = sleepingAreaCollider as BoxCollider;
            // 로컬 중심점을 월드 좌표로 변환
            colliderCenter = sleepingArea.transform.TransformPoint(boxCollider.center);
        }
        else if (sleepingAreaCollider is SphereCollider)
        {
            SphereCollider sphereCollider = sleepingAreaCollider as SphereCollider;
            colliderCenter = sleepingArea.transform.TransformPoint(sphereCollider.center);
        }
        else
        {
            // 기타 콜라이더 타입
            colliderCenter = sleepingAreaCollider.bounds.center;
        }
        
        // 콜라이더 중심에서 아래로 레이캐스트하여 NavMesh 접근 가능한 지점 찾기
        RaycastHit rayHit;
        // 중심에서 바닥으로 레이캐스트 (최대 10유닛)
        if (Physics.Raycast(colliderCenter, Vector3.down, out rayHit, 10f))
        {
            // 레이캐스트 히트 포인트에서 NavMesh 위치 샘플링
            NavMeshHit navHit;
            if (NavMesh.SamplePosition(rayHit.point, out navHit, 2f, NavMesh.AllAreas))
            {
                Debug.Log($"{petController.petName}이(가) NavMesh 위의 수면 위치를 찾았습니다: {navHit.position}");
                return navHit.position;
            }
        }
        
        // NavMesh 위치 직접 샘플링 (레이캐스트 실패 시)
        NavMeshHit directHit;
        if (NavMesh.SamplePosition(colliderCenter, out directHit, 5f, NavMesh.AllAreas))
        {
            Debug.Log($"{petController.petName}이(가) 콜라이더 근처의 NavMesh 위치를 찾았습니다: {directHit.position}");
            return directHit.position;
        }
        
        // 모든 방법이 실패하면 XZ 좌표만 사용하고 Y 좌표는 펫의 현재 Y 좌표 사용
        Vector3 fallbackPosition = new Vector3(
            colliderCenter.x,
            petController.transform.position.y,
            colliderCenter.z
        );
        
        Debug.LogWarning($"{petController.petName}이(가) NavMesh 위치를 찾지 못했습니다. 대체 위치 사용: {fallbackPosition}");
        return fallbackPosition;
    }

    private IEnumerator Sleep(bool isProperSleepingArea)
    {
        isSleeping = true;
        petController.StopMovement();
        
        // 애니메이션을 위한 랜덤 방향 설정 (잠자리는 NavMesh 영역이므로 방향이 없음)
        if (petController.petModelTransform != null)
        {
            Vector3 randomDirection = new Vector3(Random.Range(-1f, 1f), 0, Random.Range(-1f, 1f)).normalized;
            if (randomDirection != Vector3.zero)
            {
                Quaternion targetRotation = Quaternion.LookRotation(randomDirection);
                float rotationTime = 0f;
                Quaternion startRotation = petController.petModelTransform.rotation;
                
                // 부드럽게 회전
                while (rotationTime < 1f)
                {
                    rotationTime += Time.deltaTime * 2f;
                    petController.petModelTransform.rotation = Quaternion.Slerp(startRotation, targetRotation, rotationTime);
                    yield return null;
                }
            }
        }
        
        // 수면 전체 기간 동안 지속되는 졸림 감정 표시 (sleepDuration과 동일하게 설정)
        petController.ShowEmotion(EmotionType.Sleepy, sleepDuration);
        
        // 잠자는 애니메이션 재생 (애니메이션 번호는 실제 프로젝트에 맞게 조정)
        int sleepAnimationIndex = 5; // 수면 애니메이션 인덱스 (프로젝트에 맞게 수정 필요)
        
        // 커스텀 애니메이션 재생 (잠자는 동안 계속 재생)
        yield return StartCoroutine(petController.GetComponent<PetAnimationController>().PlayAnimationWithCustomDuration(
            sleepAnimationIndex, sleepDuration, true, false));
        
        // 졸림 감소 (정규 수면 공간인지 여부에 따라 다른 양 감소)
        if (isProperSleepingArea)
        {
            // 정규 수면 공간에서는 80 감소
            petController.sleepiness -= 80f;
            Debug.Log($"{petController.petName}이(가) 수면 공간에서 깊은 잠을 자고 일어났습니다. 졸림 감소: 80");
        }
        else
        {
            // 일반 장소에서는 50 감소 (30 남김)
            petController.sleepiness -= 50f;
            Debug.Log($"{petController.petName}이(가) 아무데서나 잠을 자고 일어났습니다. 졸림 감소: 50");
        }
        
        petController.sleepiness = Mathf.Clamp(petController.sleepiness, 0f, 100f);
        
        // 기분 좋은 감정 표현
        petController.ShowEmotion(EmotionType.Happy, 3f);
        
        // 원래 속도로 복원 (졸림 수치에 따라 다시 조절될 것임)
        if (petController.agent != null && petController.agent.enabled)
        {
            petController.agent.speed = petController.baseSpeed;
        }
        
        // 잠에서 깨고 이동 재개
        targetSleepingArea = null;
        isSleeping = false;
        
        // NavMeshAgent 재활성화 확인 및 이동 재개
        if (petController.agent != null)
        {
            // NavMeshAgent가 비활성화된 경우 다시 활성화
            if (!petController.agent.enabled)
            {
                petController.agent.enabled = true;
            }
            
            // NavMesh 위에 있는지 확인
            NavMeshHit navHit;
            if (NavMesh.SamplePosition(petController.transform.position, out navHit, 5f, NavMesh.AllAreas))
            {
                if (Vector3.Distance(petController.transform.position, navHit.position) > 0.1f)
                {
                    // 위치 조정이 필요하면 조정
                    petController.transform.position = navHit.position;
                }
            }
            
            // 에이전트 속성 재설정
            petController.agent.updateRotation = true;
            petController.agent.isStopped = false;
            petController.agent.updatePosition = true;
        }
        
        // 명시적으로 움직임 재개
        petController.ResumeMovement();
        
        // 약간의 지연 후 랜덤 목적지 설정
        yield return new WaitForSeconds(0.5f);
        
        // 랜덤한 위치로 이동 시도 (여러 번 시도)
        for (int attempts = 0; attempts < 3; attempts++)
        {
            if (petController.agent != null && petController.agent.enabled && petController.agent.isOnNavMesh)
            {
                petController.GetComponent<PetMovementController>().SetRandomDestination();
                break;
            }
            yield return new WaitForSeconds(0.2f);
        }
        
        // 움직임 상태 로그 출력
        Debug.Log($"{petController.petName}이(가) 수면 후 이동 재개. 에이전트 활성화: {petController.agent.enabled}, NavMesh 위치: {petController.agent.isOnNavMesh}, 정지 상태: {petController.agent.isStopped}");
    }
    
    // 외부에서 강제로 수면을 중단시키는 메서드
    public void InterruptSleep()
    {
        if (isSleeping)
        {
            StopAllCoroutines();
            isSleeping = false;
            targetSleepingArea = null;
            hasForceSleepTriggered = false;
            
            // 원래 속도로 복원
            if (petController.agent != null && petController.agent.enabled)
            {
                petController.agent.speed = petController.baseSpeed;
            }
            
            // 기분 나쁜 감정 표현
            petController.ShowEmotion(EmotionType.Angry, 3f);
            
            // 이동 재개
            if (petController.agent != null && petController.agent.enabled && petController.agent.isOnNavMesh)
            {
                petController.ResumeMovement();
                petController.GetComponent<PetMovementController>().SetRandomDestination();
            }
        }
    }
    
    // 외부에서 강제로 수면을 시작시키는 메서드 (Vector3 위치 기반)
    public void ForceSleep(Vector3 sleepPosition)
    {
        if (!isSleeping)
        {
            // NavMesh 위치 샘플링
            NavMeshHit navHit;
            if (NavMesh.SamplePosition(sleepPosition, out navHit, 2f, NavMesh.AllAreas))
            {
                // 펫을 NavMesh 위에 배치
                petController.transform.position = navHit.position;
            }
            else
            {
                // NavMesh 샘플링 실패 시 XZ 좌표만 사용
                petController.transform.position = new Vector3(
                    sleepPosition.x,
                    petController.transform.position.y,
                    sleepPosition.z
                );
            }
            
            StartCoroutine(Sleep(false)); // 외부에서 강제로 수면 시작 (정규 수면 공간 아님)
        }
    }
    
    // 외부에서 특정 수면 공간으로 가서 자도록 지시하는 메서드
    public void SleepAtArea(GameObject sleepingArea)
    {
        if (!isSleeping && sleepingArea != null)
        {
            targetSleepingArea = sleepingArea;
            
            if (petController.agent != null && petController.agent.enabled && petController.agent.isOnNavMesh)
            {
                // 콜라이더 위치에서 NavMesh 위의 접근 가능한 위치 찾기
                Vector3 sleepPosition = GetRandomPositionInSleepingArea(sleepingArea);
                
                // 이동 시작
                petController.agent.SetDestination(sleepPosition);
                Debug.Log($"{petController.petName}이(가) 지정된 수면 공간으로 이동합니다: {sleepPosition}");
                
                // 졸림 감정 표현
                petController.ShowEmotion(EmotionType.Sleepy, 3f);
            }
        }
    }
}