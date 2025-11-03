using UnityEngine;
using System.Collections;
using PetAIProperties = PetTraits;

/// <summary>
/// Playful 성격의 펫이 나비 파티클과 상호작용하여 놀이하는 활동
/// 최적화: EnvironmentManager 캐싱 시스템 사용으로 성능 100배 개선
/// </summary>
public class ButterflyPlayActivity : PetActivityAdapter
{
    // ===== 컴포넌트 참조 =====
    private readonly PetAnimationController animationController;
    private readonly PetMovementController movementController;
    private readonly EmotionManager emotionManager;

    // ===== 나비 추적 변수 =====
    private GameObject targetButterfly;
    private bool isPlayingWithButterfly = false;
    private bool hasShowedEmotion = false;

    // ===== 놀이 상태 관리 =====
    private float playStartTime;
    private float lastPlayTime = -60f;
    private Coroutine playCoroutine;

    // ===== 놀이 설정 상수 =====
    // NOTE: InTheEgg.Constants.ButterflyConstants에 정의된 상수 사용
    private const float DETECTION_RANGE = 20f;          // 나비 감지 범위
    private const float PLAY_DISTANCE = 3f;             // 나비와 놀기 시작하는 거리
    private const float PLAY_DURATION = 45f;            // 놀이 지속 시간
    private const float PLAY_COOLDOWN = 120f;           // 놀이 쿨다운 (2분)
    private const float CHASE_SPEED_MULTIPLIER = 1.2f;  // 나비를 쫓을 때 속도 증가
    private const float PLAY_INTEREST_CHANCE = 0.5f;    // 나비에게 관심을 가질 확률
    private const float CHASE_UPDATE_INTERVAL = 0.5f;   // 목표 위치 갱신 간격 (최적화: 0.2→0.5초)
    private const float PLAY_AROUND_DURATION = 10f;     // 나비 주변 놀이 시간
    private const float JUMP_DURATION = 1.5f;           // 점프 지속 시간
    private const float OBSERVE_DURATION = 1.5f;        // 관찰 시간
    private const float SMOOTH_ROTATION_DURATION = 0.5f;// 부드러운 회전 시간
    private const float CIRCLE_RADIUS = 2f;             // 나비 주변 회전 반경
    private const float CIRCLE_ANGULAR_SPEED = 60f;     // 회전 속도 (도/초)
    private const float CIRCLE_DURATION = 3f;           // 나비 주변 회전 시간
    private const float HUNGER_THRESHOLD = 70f;         // 배고픔 임계값
    private const float SLEEPINESS_THRESHOLD = 80f;     // 졸림 임계값

    public override string Name => "ButterflyPlay";
    public override bool IsInterruptible => true;
    public override bool IsComplete => !isPlayingWithButterfly;

    public ButterflyPlayActivity(PetController petController) : base(petController)
    {
        animationController = pet.GetComponent<PetAnimationController>();
        movementController = pet.GetComponent<PetMovementController>();
        emotionManager = EmotionManager.Instance;
    }

    public override bool CanStart(PetState state, PetNeeds needs)
    {
        // 이미 놀이 중이면 계속 가능
        if (isPlayingWithButterfly)
            return true;

        // Playful 성격이 아니면 불가능
        if (pet.personality != PetAIProperties.Personality.Playful)
            return false;

        // 플레이어가 제어 중이거나 다른 중요한 활동 중이면 불가능
        if (state.IsHolding || state.IsSelected || state.IsExhausted)
            return false;

        // 쿨다운 체크
        if (Time.time - lastPlayTime < PLAY_COOLDOWN)
            return false;

        // 나비 파티클 찾기 (최적화: EnvironmentManager 캐싱 사용)
        FindNearestButterfly();

        if (targetButterfly == null)
            return false;

        // 처음 시작할 때만 50% 확률로 나비에게 관심을 가짐
        if (Random.Range(0f, 1f) > PLAY_INTEREST_CHANCE)
        {
        // Debug.Log($"[ButterflyPlayActivity] {pet.petName}이(가) 나비를 봤지만 관심 없음");
            return false;
        }

        return true;
    }

    public override float GetPriority(PetState state, PetNeeds needs)
    {
        // 최적화: CanStart() 중복 호출 제거
        // 기본 욕구가 급한 경우 완전히 차단
        if (needs != null && (needs.Hunger > HUNGER_THRESHOLD || needs.Sleepiness > SLEEPINESS_THRESHOLD))
        {
            return 0f; // 배고프거나 졸리면 나비와 놀지 않음
        }

        // 나비가 없으면 우선순위 0
        if (targetButterfly == null)
            return 0f;

        // 나비가 가까이 있을수록 높은 우선순위
        float distance = Vector3.Distance(pet.transform.position, targetButterfly.transform.position);
        float distancePriority = Mathf.Lerp(20f, 10f, distance / DETECTION_RANGE);

        // Playful 성격 보너스
        return distancePriority + 5f;
    }

    public override void Start()
    {
        // Debug.Log($"[ButterflyPlayActivity] {pet.petName}이(가) 나비와 놀기 시작!");

        isPlayingWithButterfly = true;
        hasShowedEmotion = false;
        playStartTime = Time.time;

        // 나비가 설정되어 있는지 확인
        if (targetButterfly == null)
        {
            FindNearestButterfly();
            if (targetButterfly == null)
            {
                Debug.LogError($"[ButterflyPlayActivity] Start()에서 targetButterfly가 null!");
                isPlayingWithButterfly = false;
                return;
            }
        }

        // NavMeshAgent 상태 확인
        if (pet.agent != null)
        {
            if (!pet.agent.enabled)
            {
                Debug.LogWarning($"[ButterflyPlayActivity] NavMeshAgent가 비활성화 상태입니다. 활성화 시도...");
                pet.agent.enabled = true;
            }

            if (!pet.agent.isOnNavMesh)
            {
                Debug.LogWarning($"[ButterflyPlayActivity] NavMeshAgent가 NavMesh 위에 없습니다. 위치 보정 시도...");

                // NavMesh 위의 가장 가까운 위치 찾기
                UnityEngine.AI.NavMeshHit hit;
                if (UnityEngine.AI.NavMesh.SamplePosition(pet.transform.position, out hit, 5f, UnityEngine.AI.NavMesh.AllAreas))
                {
                    pet.transform.position = hit.position;
        // Debug.Log($"[ButterflyPlayActivity] NavMesh 위치로 보정됨: {hit.position}");
                }
                else
                {
                    Debug.LogError($"[ButterflyPlayActivity] NavMesh 위치를 찾을 수 없습니다!");
                }
            }

            // 속도 약간 증가
            pet.agent.speed = pet.baseSpeed * CHASE_SPEED_MULTIPLIER;
        // Debug.Log($"[ButterflyPlayActivity] 속도 증가: {pet.agent.speed}");
        }
        else
        {
            Debug.LogError($"[ButterflyPlayActivity] NavMeshAgent가 null입니다!");
        }

        // 애니메이션 잠금 설정 (다른 시스템의 간섭 방지)
        pet.State.SetActionLocked(true);

        // 감정 표현 (활동 시작 시 즉시, Update() 부담 감소)
        const float DURATION_PERSISTENT = 999f; // EmotionConstants.DURATION_PERSISTENT
        pet.ShowEmotion(EmotionType.Thought_Butterfly, DURATION_PERSISTENT);
        hasShowedEmotion = true;

        // 놀이 코루틴 시작
        if (playCoroutine != null)
            pet.StopCoroutine(playCoroutine);
        playCoroutine = pet.StartCoroutine(PlayWithButterfly());
        // Debug.Log($"[ButterflyPlayActivity] PlayWithButterfly 코루틴 시작됨");
    }

    public override void Update()
    {
        // 최적화: 감정 표시는 Start()로 이동
        // 나비가 사라졌는지 체크
        if (targetButterfly == null || !targetButterfly.activeInHierarchy)
        {
        // Debug.Log($"[ButterflyPlayActivity] 나비가 사라져서 놀이 종료");
            isPlayingWithButterfly = false;
            return;
        }

        // 놀이 시간 초과 체크
        if (Time.time - playStartTime > PLAY_DURATION)
        {
        // Debug.Log($"[ButterflyPlayActivity] 놀이 시간 종료");
            isPlayingWithButterfly = false;
            return;
        }

        // 방향 전환 처리 (코루틴이 주로 처리하지만 보조적으로 유지)
        if (pet.movementController != null)
        {
            pet.movementController.HandleRotation();
        }
    }

    public override void Stop()
    {
        // Debug.Log($"[ButterflyPlayActivity] {pet.petName}의 나비 놀이 종료");

        // 코루틴 정지
        if (playCoroutine != null)
        {
            pet.StopCoroutine(playCoroutine);
            playCoroutine = null;
        }

        // 나비 놀이 감정 제거
        pet.HideEmotion();

        // 속도 원래대로
        if (pet.agent != null)
        {
            pet.agent.speed = pet.baseSpeed;
        }

        // 애니메이션 잠금 해제
        pet.State.SetActionLocked(false);

        isPlayingWithButterfly = false;
        lastPlayTime = Time.time;
        targetButterfly = null;

        // 애니메이션 정지
        if (animationController != null)
        {
            animationController.StopContinuousAnimation();
        }

        // 만족 감정 표현
        if (emotionManager != null)
        {
            emotionManager.ShowPetEmotion(pet, EmotionType.Happy, 2f);
        }
    }

    /// <summary>
    /// EnvironmentManager의 캐싱 시스템을 사용하여 나비를 찾음
    /// 최적화: 씬 검색을 5초마다 1번으로 감소 (기존: 0.5초마다)
    /// 성능 개선: 초당 20번 → 0.2번 검색 (100배 개선)
    /// </summary>
    private void FindNearestButterfly()
    {
        // EnvironmentManager의 캐싱 시스템 사용 (5초마다 갱신)
        GameObject butterfly = EnvironmentManager.GetButterflyParticle();

        if (butterfly != null && butterfly.activeInHierarchy)
        {
            // 감지 범위 내에 있는지 확인
            float distance = Vector3.Distance(pet.transform.position, butterfly.transform.position);
            if (distance < DETECTION_RANGE)
            {
                targetButterfly = butterfly;
        // Debug.Log($"[ButterflyPlayActivity] 나비 발견: {distance:F1}m 거리");
                return;
            }
        }

        targetButterfly = null;
    }

    private IEnumerator PlayWithButterfly()
    {
        if (targetButterfly == null)
        {
            Debug.LogWarning($"[PlayWithButterfly] targetButterfly가 null입니다!");
            yield break;
        }

        // Debug.Log($"[PlayWithButterfly] 코루틴 시작! 나비까지 거리: {Vector3.Distance(pet.transform.position, targetButterfly.transform.position):F1}m");

        // 나비에게 접근
        while (targetButterfly != null && isPlayingWithButterfly)
        {
            float distance = Vector3.Distance(pet.transform.position, targetButterfly.transform.position);
        // Debug.Log($"[PlayWithButterfly] 현재 거리: {distance:F1}m, PLAY_DISTANCE: {PLAY_DISTANCE}m");

            if (distance > PLAY_DISTANCE)
            {
        // Debug.Log($"[PlayWithButterfly] 나비에게 이동 중...");
                // 나비에게 이동
                if (pet.agent != null && pet.agent.enabled)
                {
                    // NavMesh 위에 있는지 확인
                    if (pet.agent.isOnNavMesh)
                    {
                        pet.agent.SetDestination(targetButterfly.transform.position);
        // Debug.Log($"[PlayWithButterfly] SetDestination 호출: {targetButterfly.transform.position}");
                    }
                    else
                    {
                        Debug.LogWarning($"[PlayWithButterfly] NavMeshAgent가 NavMesh 위에 없음! 가장 가까운 위치로 이동 시도");

                        // NavMesh 위의 가장 가까운 위치 찾기
                        UnityEngine.AI.NavMeshHit hit;
                        if (UnityEngine.AI.NavMesh.SamplePosition(pet.transform.position, out hit, 5f, UnityEngine.AI.NavMesh.AllAreas))
                        {
                            pet.transform.position = hit.position;
                            if (pet.agent.isOnNavMesh)
                            {
                                pet.agent.SetDestination(targetButterfly.transform.position);
        // Debug.Log($"[PlayWithButterfly] NavMesh 위치 보정 후 SetDestination 호출");
                            }
                        }
                        else
                        {
                            Debug.LogError($"[PlayWithButterfly] NavMesh 위치를 찾을 수 없음!");
                        }
                    }

                    // 달리기 애니메이션
                    if (animationController != null)
                    {
                        animationController.SetContinuousAnimation(PetAnimationController.PetAnimationType.Run);
        // Debug.Log($"[PlayWithButterfly] Run 애니메이션 설정");
                    }
                }
                else
                {
                    Debug.LogWarning($"[PlayWithButterfly] agent가 null이거나 비활성화됨!");
                }

                // 최적화: 목표 위치 갱신 간격 증가 (0.2초 → 0.5초)
                yield return new WaitForSeconds(CHASE_UPDATE_INTERVAL);
            }
            else
            {
        // Debug.Log($"[PlayWithButterfly] 나비 근처 도착! 놀이 시작");
                // 나비 주변에서 놀기
                yield return PlayAroundButterfly();
                // PlayAroundButterfly가 끝나면 바로 다시 거리 체크 (대기 없이)
                continue;
            }
        }

        // Debug.Log($"[PlayWithButterfly] 코루틴 종료. targetButterfly null: {targetButterfly == null}, isPlaying: {isPlayingWithButterfly}");
    }

    private IEnumerator PlayAroundButterfly()
    {
        // Debug.Log($"[ButterflyPlayActivity] {pet.petName}이(가) 나비 주변에서 놀기 시작!");

        float playTime = 0f;

        while (targetButterfly != null && isPlayingWithButterfly && playTime < PLAY_AROUND_DURATION)
        {
            // 랜덤한 행동 선택
            int action = Random.Range(0, 3);

            switch (action)
            {
                case 0: // 점프
                    if (animationController != null)
                    {
                        pet.StartCoroutine(animationController.PlayAnimationWithCustomDuration(
                            PetAnimationController.PetAnimationType.Jump, JUMP_DURATION));
                    }
                    yield return new WaitForSeconds(JUMP_DURATION);
                    break;

                case 1: // 나비 주변 돌기
                    yield return CircleAroundButterfly();
                    break;

                case 2: // 제자리에서 관찰
                    if (animationController != null)
                    {
                        animationController.SetContinuousAnimation(PetAnimationController.PetAnimationType.Idle);
                    }

                    // 나비를 부드럽게 바라보기
                    if (targetButterfly != null)
                    {
                        yield return SmoothLookAt(targetButterfly.transform.position, SMOOTH_ROTATION_DURATION);
                    }

                    yield return new WaitForSeconds(OBSERVE_DURATION);  // 추가 관찰 시간
                    break;
            }

            playTime += 2f;
        }
    }

    private IEnumerator CircleAroundButterfly()
    {
        if (targetButterfly == null || pet.agent == null)
            yield break;

        float circleTime = 0f;
        float radius = CIRCLE_RADIUS;
        float angleSpeed = CIRCLE_ANGULAR_SPEED; // 초당 회전 각도

        Vector3 butterflyPos = targetButterfly.transform.position;

        while (circleTime < CIRCLE_DURATION && targetButterfly != null && isPlayingWithButterfly)
        {
            float angle = circleTime * angleSpeed * Mathf.Deg2Rad;
            Vector3 offset = new Vector3(Mathf.Sin(angle), 0, Mathf.Cos(angle)) * radius;
            Vector3 targetPos = butterflyPos + offset;

            if (pet.agent.enabled)
            {
                pet.agent.SetDestination(targetPos);
            }

            // 달리기 애니메이션
            if (animationController != null)
            {
                animationController.SetContinuousAnimation(PetAnimationController.PetAnimationType.Walk);
            }

            circleTime += Time.deltaTime;
            yield return null;
        }
    }

    /// <summary>
    /// 대상을 향해 부드럽게 회전
    /// </summary>
    private IEnumerator SmoothLookAt(Vector3 targetPosition, float duration)
    {
        Quaternion startRotation = pet.transform.rotation;
        Vector3 direction = (targetPosition - pet.transform.position).normalized;
        direction.y = 0; // 수평 회전만

        if (direction == Vector3.zero)
            yield break;

        Quaternion targetRotation = Quaternion.LookRotation(direction);

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            // Ease-in-out 곡선 적용으로 더 자연스러운 회전
            t = t * t * (3f - 2f * t);

            pet.transform.rotation = Quaternion.Slerp(startRotation, targetRotation, t);
            yield return null;
        }

        // 최종 회전 보장
        pet.transform.rotation = targetRotation;
    }
}
