// 음식 탐지, 접근, 먹기 동작 및 배고픔 수치 조절 전담
using System.Collections;
using UnityEngine;
using UnityEngine.AI;

public class PetFeedingController : MonoBehaviour
{
    private PetController petController;
    private GameObject targetFood;
    private GameObject targetFeedingArea; // 먹이 먹는 구역
    private float detectionRadius = 100f;
    private float eatingDistance = 4f;
    private float feedingAreaDistance = 2f; // 먹이 구역과의 거리
    private bool isEating = false;
    private float hungerIncreaseRate = 0.1f; // 배고픔 증가 속도

    // 마지막으로 배고픔 상태를 표시한 시간
    private float lastHungryEmotionTime = 0f;
    // 배고픔 상태 표시 간격 (초)
    private float hungryEmotionInterval = 35f;

    // 먹이 공간을 위한 레이어 마스크
    private int feedingAreaLayer;
    private const string FEEDING_AREA_TAG = "FeedingArea";

    // 성격별 먹이 증가 배율
    private float lazyHungerModifier = 0.8f; // 게으른 성격은 덜 배고파짐
    private float playfulHungerModifier = 1.2f; // 활발한 성격은 더 배고파짐
    private float braveHungerModifier = 1.0f; // 용감한 성격은 보통 배고파짐
    private float shyHungerModifier = 0.9f; // 수줍은 성격은 약간 덜 배고파짐

    // 배고픔에 따른 속도 감소 관련 변수
    private float minSpeedFactor = 0.3f; // 최소 속도 비율 (100% 배고픔일 때)
    private float lastHungerCheck = 0f; // 마지막 배고픔 체크 시간
    private float hungerCheckInterval = 5f; // 배고픔 체크 간격

    // 배고픔 단계별 임계값
    private float veryHungryThreshold = 80f; // 매우 배고픔 단계 임계값
    private float extremeHungryThreshold = 95f; // 극도로 배고픔 단계 임계값

    // 애정도 감소 관련 변수
    private float affectionDecreaseRate = 0.05f; // 배고픔 최대 시 애정도 감소 속도
    private float lastAffectionDecreaseTime = 0f; // 마지막 애정도 감소 시간
    private float affectionDecreaseInterval = 10f; // 애정도 감소 간격 (초)

    // 앉기 상태 관련 변수
    private bool isSitting = false;
    private int sittingAnimationIndex = 5; // 앉기 애니메이션 인덱스 (프로젝트에 맞게 수정 필요)

    // 밥 먹을 때 애정도 증가량
    private float affectionIncreaseSmall = 2f; // 일반 음식 먹을 때 증가량
    private float affectionIncreaseLarge = 5f; // 먹이 구역에서 먹을 때 증가량

    public void Init(PetController controller)
    {
        petController = controller;

        // "FeedingArea" 레이어를 사용하는 경우 아래 코드를 사용
        feedingAreaLayer = LayerMask.GetMask("FeedingArea");

        // 레이어를 사용하지 않고 태그만 사용하는 경우 레이어 마스크를 전체로 설정
        if (feedingAreaLayer == 0)
        {
            feedingAreaLayer = Physics.DefaultRaycastLayers;
        }
    }

    public void UpdateFeeding()
    {
        // 먹고 있지 않은 경우에만 배고픔 증가
        if (!isEating)
        {
            // 시간에 따라 배고픔 증가 (성격 기반 다른 증가율)
            float personalityHungerModifier = GetPersonalityHungerModifier();
            petController.hunger += Time.deltaTime * hungerIncreaseRate * personalityHungerModifier;
            petController.hunger = Mathf.Clamp(petController.hunger, 0f, 100f);

            // 배고픔이 높으면 주기적으로 감정 표시
            if (petController.hunger > 60f &&
                Time.time - lastHungryEmotionTime > hungryEmotionInterval)
            {
                petController.ShowEmotion(EmotionType.Hungry, 5f);
                lastHungryEmotionTime = Time.time;
            }

            // 배고픔에 따른 속도 조절 (5초마다 체크)
            if (Time.time - lastHungerCheck > hungerCheckInterval)
            {
                UpdateBehaviorBasedOnHunger();
                lastHungerCheck = Time.time;
            }

            // 배고픔이 100%일 때 애정도 감소
            if (petController.hunger >= 100f &&
                Time.time - lastAffectionDecreaseTime > affectionDecreaseInterval)
            {
                petController.affection -= affectionDecreaseRate;
                petController.affection = Mathf.Clamp(petController.affection, 0f, 100f);
                lastAffectionDecreaseTime = Time.time;

                // 로그
                Debug.Log($"{petController.petName}이(가) 너무 배고파서 애정도가 감소합니다. 현재 애정도: {petController.affection:F1}");
            }

            // 배고픔이 일정 수준 이상이고 아직 음식 목표나 먹이 구역 목표가 없으며 먹고 있지 않은 경우
            if (petController.hunger > 70f && targetFood == null && targetFeedingArea == null && !isEating)
            {
                // 먼저 먹이 구역을 탐지
                DetectFeedingArea();

                // 먹이 구역을 찾지 못했다면 일반 음식을 탐지
                if (targetFeedingArea == null)
                {
                    try
                    {
                        DetectFood();
                    }
                    catch (UnityException)
                    {
                        // Food 태그가 없는 경우 무시
                    }
                }

                // 먹이나 먹이구역을 찾았고, 앉아있는 상태라면 일어나서 먹으러 가기
                if ((targetFood != null || targetFeedingArea != null) && isSitting)
                {
                    StopSitting();
                }
            }
        }

        // 먹이 구역이 발견됐고 먹고 있지 않은 경우
        if (targetFeedingArea != null && !isEating && !isSitting)
        {
            // 에이전트가 활성화되어 있고 네비메시에 있는지 확인
            if (petController.agent != null && petController.agent.enabled && petController.agent.isOnNavMesh)
            {
                // XZ 평면에서의 거리 계산 (Y축 차이 무시)
                Vector3 petPosition = petController.transform.position;
                Vector3 targetPosition = targetFeedingArea.transform.position;
                float xzDistance = Vector2.Distance(
                    new Vector2(petPosition.x, petPosition.z),
                    new Vector2(targetPosition.x, targetPosition.z)
                );

                // 목적지에 충분히 가까워졌는지 확인 (XZ 평면에서)
                if (xzDistance <= feedingAreaDistance ||
                    (petController.agent.remainingDistance <= feedingAreaDistance && !petController.agent.pathPending))
                {
                    // 먹이 구역에 도착했으면 먹기 시작
                    Debug.Log($"{petController.petName}이(가) 먹이 구역에 도착했습니다. 거리: {xzDistance}");
                    StartCoroutine(EatAtFeedingArea());
                }
                else
                {
                    // 아직 도착하지 않았으면 계속 이동
                    petController.agent.SetDestination(targetFeedingArea.transform.position);
                }
            }
        }
        // 일반 음식이 발견됐고 먹고 있지 않은 경우
        else if (targetFood != null && !isEating && !isSitting)
        {
            // 에이전트가 활성화되어 있고 네비메시에 있는지 확인
            if (petController.agent != null && petController.agent.enabled && petController.agent.isOnNavMesh)
            {
                petController.agent.SetDestination(targetFood.transform.position);

                // 음식에 충분히 가까워졌는지 확인
                if (Vector3.Distance(petController.transform.position, targetFood.transform.position) <= eatingDistance)
                {
                    StartCoroutine(EatFood());
                }
            }
        }
    }

    // 배고픔에 따른 행동 업데이트
    private void UpdateBehaviorBasedOnHunger()
    {
        if (petController.agent != null && petController.agent.enabled)
        {
            // 극도로 배고픈 상태 (95% 이상) - 앉아서 쉬기
            if (petController.hunger >= extremeHungryThreshold && !isSitting)
            {
                StartCoroutine(SitDueToHunger());
                return;
            }
            // 매우 배고픈 상태 (80% ~ 95%) - 속도 크게 감소
            else if (petController.hunger >= veryHungryThreshold)
            {
                float hungerFactor = 1f - ((petController.hunger / 100f) * (1f - minSpeedFactor));
                petController.agent.speed = petController.baseSpeed * hungerFactor;

                Debug.Log($"{petController.petName}이(가) 매우 배고파서 느리게 움직입니다. 현재 속도: {petController.agent.speed:F2} (기본 속도의 {hungerFactor * 100:F0}%)");
            }
            // 보통 배고픈 상태 (80% 미만) - 정상 속도
            else
            {
                // 앉아있던 상태에서 배고픔이 줄었다면 일어나기
                if (isSitting && petController.hunger < extremeHungryThreshold)
                {
                    StopSitting();
                }

                // 속도를 정상으로 복원
                petController.agent.speed = petController.baseSpeed;
            }
        }
    }

    // 배고픔으로 인한 앉기 코루틴
    private IEnumerator SitDueToHunger()
    {
        if (isSitting)
            yield break;

        isSitting = true;
        petController.StopMovement();

        // 배고픔 감정 표현
        petController.ShowEmotion(EmotionType.Hungry, 5f);

        // 애니메이션 컨트롤러에 특별 애니메이션 재생 중임을 알림
        PetAnimationController animController = petController.GetComponent<PetAnimationController>();
        if (animController != null)
        {
            // PetAnimationController의 전용 메서드 사용
            yield return StartCoroutine(animController.PlayAnimationWithCustomDuration(
                sittingAnimationIndex, // 앉기 애니메이션 인덱스
                1f, // 초기 재생 기간
                false, // 기본 애니메이션으로 돌아가지 않음
                false  // 이동을 재개하지 않음
            ));
        }
        else if (petController.animator != null)
        {
            // 애니메이션 컨트롤러가 없는 경우 직접 애니메이터 조작
            petController.animator.SetInteger("animation", sittingAnimationIndex);
        }

        Debug.Log($"{petController.petName}이(가) 너무 배고파서 앉아서 쉽니다. 배고픔 수치: {petController.hunger:F0}");

        // 앉아있는 동안 주기적으로 먹이 탐지 시도
        float lastFoodSearchTime = 0f;
        float foodSearchInterval = 5f; // 5초마다 먹이 탐지

        // 극도로 배고픈 상태가 해소될 때까지 계속 앉아있음
        while (petController.hunger >= extremeHungryThreshold)
        {
            // 주기적으로 배고픔 이모션 표시
            if (Time.time - lastHungryEmotionTime > hungryEmotionInterval * 0.5f)
            {
                petController.ShowEmotion(EmotionType.Hungry, 5f);
                lastHungryEmotionTime = Time.time;
            }

            // 주기적으로 먹이 탐지
            if (Time.time - lastFoodSearchTime > foodSearchInterval)
            {
                // 먹이 구역 탐지
                DetectFeedingArea();

                // 먹이 구역을 찾지 못했다면 일반 음식을 탐지
                if (targetFeedingArea == null)
                {
                    try
                    {
                        DetectFood();
                    }
                    catch (UnityException)
                    {
                        // Food 태그가 없는 경우 무시
                    }
                }

                // 먹이나 먹이구역을 찾았다면 일어나서 먹으러 가기
                if (targetFood != null || targetFeedingArea != null)
                {
                    StopSitting();
                    break; // while 루프 종료
                }

                lastFoodSearchTime = Time.time;
            }

            // 앉기 애니메이션 유지
            if (petController.animator != null &&
                petController.animator.GetInteger("animation") != sittingAnimationIndex)
            {
                petController.animator.SetInteger("animation", sittingAnimationIndex);
            }

            yield return new WaitForSeconds(1f);
        }

        // 배고픔이 줄어들었거나 먹이를 찾았으면 앉기 중단
        StopSitting();
    }

    // 3. StopSitting 메서드도 함께 수정
    // PetFeedingController.cs의 StopSitting() 메서드 수정
private void StopSitting()
{
    if (!isSitting)
        return;

    isSitting = false;

    // 애니메이션 컨트롤러 참조 가져오기
    PetAnimationController animController = petController.GetComponent<PetAnimationController>();

    // 애니메이션을 기본으로 되돌림
    if (petController.animator != null)
    {
        petController.animator.SetInteger("animation", 0);
    }

    // 연속 애니메이션 상태 해제 - 이 부분이 중요!
    if (animController != null)
    {
        animController.StopContinuousAnimation();
    }

    // 이동 재개
    petController.ResumeMovement();
    petController.GetComponent<PetMovementController>().SetRandomDestination();

    Debug.Log($"{petController.petName}이(가) 배고픔이 줄어들어 다시 움직입니다. 현재 배고픔: {petController.hunger:F0}");
}

    // 성격에 따른 배고픔 증가 배율 반환
    private float GetPersonalityHungerModifier()
    {
        switch (petController.personality)
        {
            case PetAIProperties.Personality.Lazy:
                return lazyHungerModifier; // 게으른 성격: 천천히 배고파짐
            case PetAIProperties.Personality.Playful:
                return playfulHungerModifier; // 활발한 성격: 빨리 배고파짐
            case PetAIProperties.Personality.Brave:
                return braveHungerModifier; // 용감한 성격: 보통으로 배고파짐
            case PetAIProperties.Personality.Shy:
                return shyHungerModifier; // 수줍은 성격: 약간 천천히 배고파짐
            default:
                return 1.0f;
        }
    }

    private void DetectFeedingArea()
    {
        // 콜라이더 기반으로 먹이 구역 탐지
        Collider[] feedingAreas = Physics.OverlapSphere(transform.position, detectionRadius, feedingAreaLayer);

        // 태그로 필터링 및 가장 가까운 먹이 구역 찾기
        GameObject nearestFeedingArea = null;
        float nearestDistance = float.MaxValue;

        foreach (Collider collider in feedingAreas)
        {
            // 태그가 "FeedingArea"인 오브젝트만 고려
            if (collider.CompareTag(FEEDING_AREA_TAG))
            {
                float distance = Vector3.Distance(transform.position, collider.transform.position);
                if (distance < nearestDistance)
                {
                    nearestFeedingArea = collider.gameObject;
                    nearestDistance = distance;
                }
            }
        }

        // 먹이 구역을 찾았다면
        if (nearestFeedingArea != null)
        {
            targetFeedingArea = nearestFeedingArea;

            // 배고픔 감정 표현
            petController.ShowEmotion(EmotionType.Hungry, 5f);

            // 이동 시작
            if (petController.agent != null && petController.agent.enabled && petController.agent.isOnNavMesh)
            {
                // 먹이 구역의 위치로 이동
                Vector3 feedingPosition = GetPositionInFeedingArea(nearestFeedingArea);
                petController.agent.SetDestination(feedingPosition);
            }

            Debug.Log($"{petController.petName}이(가) 먹이 구역을 찾아 이동합니다. 위치: {targetFeedingArea.transform.position}");
        }
        else
        {
            // 넓은 범위에서 다시 시도
            Collider[] wideFeedingAreas = Physics.OverlapSphere(transform.position, detectionRadius * 3f, feedingAreaLayer);

            foreach (Collider collider in wideFeedingAreas)
            {
                if (collider.CompareTag(FEEDING_AREA_TAG))
                {
                    targetFeedingArea = collider.gameObject;

                    // 배고픔 감정 표현
                    petController.ShowEmotion(EmotionType.Hungry, 5f);

                    if (petController.agent != null && petController.agent.enabled && petController.agent.isOnNavMesh)
                    {
                        Vector3 feedingPosition = GetPositionInFeedingArea(collider.gameObject);
                        petController.agent.SetDestination(feedingPosition);
                    }

                    Debug.Log($"{petController.petName}이(가) 더 넓은 범위에서 먹이 구역을 찾았습니다. 위치: {collider.transform.position}");
                    return;
                }
            }

            Debug.Log($"{petController.petName}이(가) 먹이 구역을 찾지 못했습니다.");
        }
    }

    // 먹이 구역 내의, NavMesh 위의 위치 찾기
    private Vector3 GetPositionInFeedingArea(GameObject feedingArea)
    {
        Collider feedingAreaCollider = feedingArea.GetComponent<Collider>();
        if (feedingAreaCollider == null)
        {
            return feedingArea.transform.position;
        }

        // 콜라이더 중심 위치 구하기
        Vector3 colliderCenter;

        if (feedingAreaCollider is BoxCollider)
        {
            BoxCollider boxCollider = feedingAreaCollider as BoxCollider;
            // 로컬 중심점을 월드 좌표로 변환
            colliderCenter = feedingArea.transform.TransformPoint(boxCollider.center);
        }
        else if (feedingAreaCollider is SphereCollider)
        {
            SphereCollider sphereCollider = feedingAreaCollider as SphereCollider;
            colliderCenter = feedingArea.transform.TransformPoint(sphereCollider.center);
        }
        else
        {
            // 기타 콜라이더 타입
            colliderCenter = feedingAreaCollider.bounds.center;
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
                Debug.Log($"{petController.petName}이(가) NavMesh 위의 먹이 위치를 찾았습니다: {navHit.position}");
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

    private void DetectFood()
    {
        GameObject[] foods = GameObject.FindGameObjectsWithTag("Food");
        GameObject nearestFood = null;
        float nearestDistance = float.MaxValue;
        foreach (GameObject food in foods)
        {
            float distance = Vector3.Distance(petController.transform.position, food.transform.position);
            if (distance < nearestDistance && distance <= detectionRadius)
            {
                nearestFood = food;
                nearestDistance = distance;
            }
        }
        if (nearestFood != null)
        {
            targetFood = nearestFood;

            // 배고픔 감정 표현
            petController.ShowEmotion(EmotionType.Hungry, 5f);

            if (petController.agent != null && petController.agent.enabled && petController.agent.isOnNavMesh)
            {
                petController.agent.SetDestination(targetFood.transform.position);
            }
            Debug.Log($"{petController.petName}이(가) 음식을 발견하고 접근 중입니다.");
        }
    }

    // 먹이 구역에서 먹기
    private IEnumerator EatAtFeedingArea()
    {
        isEating = true;
        petController.StopMovement();

        // 먹이 구역을 향해 방향 설정
        if (petController.petModelTransform != null && targetFeedingArea != null)
        {
            // 먹이 그릇의 중심으로 향하는 방향 벡터
            Vector3 lookDirection = targetFeedingArea.transform.position - petController.transform.position;
            lookDirection.y = 0; // Y축은 무시하여 수평 회전만 고려

            if (lookDirection != Vector3.zero)
            {
                Quaternion targetRotation = Quaternion.LookRotation(lookDirection);
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

        // 배고픔 감소
        petController.hunger -= 60f;
        petController.hunger = Mathf.Clamp(petController.hunger, 0f, 100f);

        // 애정도 증가 - 먹이 구역에서 먹을 때 더 많이 증가
        petController.affection += affectionIncreaseLarge;
        petController.affection = Mathf.Clamp(petController.affection, 0f, 100f);

        Debug.Log($"{petController.petName}이(가) 먹이 구역에서 음식을 먹었습니다. 배고픔: {petController.hunger:F0}, 애정도: {petController.affection:F0} (+{affectionIncreaseLarge})");

        // 배고픔 감정 숨기기
        petController.HideEmotion();

        // 먹는 애니메이션 재생 (애니메이션 번호는 실제 프로젝트에 맞게 조정)
        int eatAnimationIndex = 4; // 먹는 애니메이션 인덱스 (프로젝트에 맞게 수정 필요)

        // 먹는 애니메이션 실행
        yield return StartCoroutine(petController.GetComponent<PetAnimationController>().PlayAnimationWithCustomDuration(
            eatAnimationIndex, 5f, true, false)); // 5초 동안 먹는 애니메이션

        // 먹은 후 기분 좋은 감정 표현
        petController.ShowEmotion(EmotionType.Happy, 3f);

        // 다시 이동 시작
        targetFeedingArea = null;
        isEating = false;

        // 만약 앉아있던 상태였다면 앉기 중단
        if (isSitting)
        {
            StopSitting();
        }
        else
        {
            petController.ResumeMovement();
            petController.GetComponent<PetMovementController>().SetRandomDestination();
        }

        Debug.Log($"{petController.petName}이(가) 먹이 구역에서 식사를 마쳤습니다.");
    }

    private IEnumerator EatFood()
    {
        isEating = true;
        petController.StopMovement();

        // 음식을 향해 방향 설정
        if (petController.petModelTransform != null && targetFood != null)
        {
            // 음식으로 향하는 방향 벡터
            Vector3 lookDirection = targetFood.transform.position - petController.transform.position;
            lookDirection.y = 0; // Y축은 무시하여 수평 회전만 고려

            if (lookDirection != Vector3.zero)
            {
                Quaternion targetRotation = Quaternion.LookRotation(lookDirection);
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

        // 배고픔 감소
        petController.hunger -= 30f;
        petController.hunger = Mathf.Clamp(petController.hunger, 0f, 100f);

        // 애정도 증가 - 일반 음식 먹을 때
        petController.affection += affectionIncreaseSmall;
        petController.affection = Mathf.Clamp(petController.affection, 0f, 100f);

        Debug.Log($"{petController.petName}이(가) 음식을 먹었습니다. 배고픔: {petController.hunger:F0}, 애정도: {petController.affection:F0} (+{affectionIncreaseSmall})");

        // 배고픔 감정 숨기기
        petController.HideEmotion();

        // 먹는 애니메이션 재생
        yield return StartCoroutine(petController.GetComponent<PetAnimationController>().PlaySpecialAnimation(4));

        if (targetFood != null)
        {
            Destroy(targetFood);
            targetFood = null;
            Debug.Log($"{petController.petName}이(가) 음식을 다 먹었습니다.");
        }

        // 먹은 후 기분 좋은 감정 표현
        petController.ShowEmotion(EmotionType.Happy, 3f);

        // 만약 앉아있던 상태였다면 앉기 중단
        if (isSitting)
        {
            StopSitting();
        }
        else
        {
            petController.GetComponent<PetMovementController>().SetRandomDestination();
        }

        isEating = false;
    }

    // 외부에서 특정 먹이 구역으로 가서 먹도록 지시하는 메서드
    public void FeedAtArea(GameObject feedingArea)
    {
        if (!isEating && feedingArea != null)
        {
            targetFeedingArea = feedingArea;

            // 만약 앉아있던 상태였다면 앉기 중단
            if (isSitting)
            {
                StopSitting();
            }

            if (petController.agent != null && petController.agent.enabled && petController.agent.isOnNavMesh)
            {
                // 먹이 구역의 접근 가능한 위치 찾기
                Vector3 feedingPosition = GetPositionInFeedingArea(feedingArea);

                // 이동 시작
                petController.agent.SetDestination(feedingPosition);
                Debug.Log($"{petController.petName}이(가) 지정된 먹이 구역으로 이동합니다: {feedingPosition}");

                // 배고픔 감정 표현
                petController.ShowEmotion(EmotionType.Hungry, 3f);
            }
        }
    }

    // 외부에서 근처에 먹이를 생성하여 바로 먹도록 하는 메서드
    public void CreateFoodNearby()
    {
        if (!isEating)
        {
            // 만약 앉아있던 상태였다면 앉기 중단
            if (isSitting)
            {
                StopSitting();
            }

            // 펫 주변에 음식 오브젝트 생성 (프로젝트에 맞게 수정 필요)
            GameObject foodPrefab = Resources.Load<GameObject>("FoodItem"); // 음식 프리팹 경로

            if (foodPrefab != null)
            {
                // 펫 앞에 음식 생성
                Vector3 spawnPosition = petController.transform.position + petController.transform.forward * 2f;
                GameObject newFood = Instantiate(foodPrefab, spawnPosition, Quaternion.identity);
                newFood.tag = "Food";

                // 방금 생성한 음식을 목표로 설정
                targetFood = newFood;

                // 이동 시작
                if (petController.agent != null && petController.agent.enabled && petController.agent.isOnNavMesh)
                {
                    petController.agent.SetDestination(targetFood.transform.position);
                    Debug.Log($"{petController.petName}이(가) 생성된 음식으로 이동합니다.");
                }
            }
            else
            {
                Debug.LogWarning("음식 프리팹을 찾을 수 없습니다.");
            }
        }
    }

    // 외부에서 강제로 먹이를 주는 메서드 (즉시 배고픔 감소)
    public void ForceFeed(float amount)
    {
        petController.hunger -= amount;
        petController.hunger = Mathf.Clamp(petController.hunger, 0f, 100f);

        // 애정도 증가 - 강제 급식 시
        petController.affection += affectionIncreaseSmall;
        petController.affection = Mathf.Clamp(petController.affection, 0f, 100f);

        // 먹는 애니메이션 실행 (짧게)
        StartCoroutine(petController.GetComponent<PetAnimationController>().PlaySpecialAnimation(4));

        // 먹은 후 기분 좋은 감정 표현
        petController.ShowEmotion(EmotionType.Happy, 3f);

        // 만약 앉아있던 상태였다면 앉기 중단
        if (isSitting)
        {
            StopSitting();
        }

        Debug.Log($"{petController.petName}이(가) 강제 급식을 받았습니다. 배고픔: {petController.hunger:F0}, 애정도: {petController.affection:F0} (+{affectionIncreaseSmall})");
    }
}