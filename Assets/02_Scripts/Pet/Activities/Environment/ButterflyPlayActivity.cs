using UnityEngine;
using System.Collections;
using PetAIProperties = PetTraits;

/// <summary>
/// Playful 성격의 펫이 나비 파티클과 상호작용하여 놀이하는 활동
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
    private const float DETECTION_RANGE = 20f;          // 나비 감지 범위
    private const float PLAY_DISTANCE = 3f;             // 나비와 놀기 시작하는 거리
    private const float PLAY_DURATION = 45f;            // 놀이 지속 시간
    private const float PLAY_COOLDOWN = 60f;            // 놀이 쿨다운
    private const float CHASE_SPEED_MULTIPLIER = 1.2f;  // 나비를 쫓을 때 속도 증가
    
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
        // Playful 성격이 아니면 불가능
        if (pet.personality != PetAIProperties.Personality.Playful)
            return false;
            
        // 플레이어가 제어 중이거나 다른 중요한 활동 중이면 불가능
        if (state.IsHolding || state.IsSelected || state.IsExhausted)
            return false;
            
        // 쿨다운 체크
        if (Time.time - lastPlayTime < PLAY_COOLDOWN)
            return false;
            
        // 나비 파티클 찾기
        FindNearestButterfly();
        
        return targetButterfly != null;
    }
    
    public override float GetPriority(PetState state, PetNeeds needs)
    {
        if (!CanStart(state, needs))
            return 0f;
            
        // 기본 욕구가 급한 경우 낮은 우선순위
        if (needs.Hunger > 70f || needs.Sleepiness > 80f)
            return 5f;
            
        // 나비가 가까이 있을수록 높은 우선순위
        if (targetButterfly != null)
        {
            float distance = Vector3.Distance(pet.transform.position, targetButterfly.transform.position);
            float distancePriority = Mathf.Lerp(20f, 10f, distance / DETECTION_RANGE);
            
            // Playful 성격 보너스
            return distancePriority + 5f;
        }
        
        return 15f; // 기본 놀이 우선순위
    }
    
    public override void Start()
    {
        Debug.Log($"[ButterflyPlayActivity] {pet.petName}이(가) 나비와 놀기 시작!");
        
        isPlayingWithButterfly = true;
        hasShowedEmotion = false;
        playStartTime = Time.time;
        
        // 속도 약간 증가
        if (pet.agent != null)
        {
            pet.agent.speed = pet.baseSpeed * CHASE_SPEED_MULTIPLIER;
        }
        
        // 놀이 코루틴 시작
        if (playCoroutine != null)
            pet.StopCoroutine(playCoroutine);
        playCoroutine = pet.StartCoroutine(PlayWithButterfly());
    }
    
    public override void Update()
    {
        // 감정 표현 (한 번만)
        if (!hasShowedEmotion && emotionManager != null)
        {
            emotionManager.ShowPetEmotion(pet, EmotionType.Happy, 3f);
            hasShowedEmotion = true;
        }
        
        // 나비가 사라졌는지 체크
        if (targetButterfly == null || !targetButterfly.activeInHierarchy)
        {
            Debug.Log($"[ButterflyPlayActivity] 나비가 사라져서 놀이 종료");
            isPlayingWithButterfly = false;
            return;
        }
        
        // 놀이 시간 초과 체크
        if (Time.time - playStartTime > PLAY_DURATION)
        {
            Debug.Log($"[ButterflyPlayActivity] 놀이 시간 종료");
            isPlayingWithButterfly = false;
            return;
        }
        
        // 방향 전환 처리
        if (pet.movementController != null)
        {
            pet.movementController.HandleRotation();
        }
    }
    
    public override void Stop()
    {
        Debug.Log($"[ButterflyPlayActivity] {pet.petName}의 나비 놀이 종료");
        
        // 코루틴 정지
        if (playCoroutine != null)
        {
            pet.StopCoroutine(playCoroutine);
            playCoroutine = null;
        }
        
        // 속도 원래대로
        if (pet.agent != null)
        {
            pet.agent.speed = pet.baseSpeed;
        }
        
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
    
    private void FindNearestButterfly()
    {
        targetButterfly = null;
        float nearestDistance = DETECTION_RANGE;
        
        // "ButterflyParticle" 태그를 가진 모든 오브젝트 찾기
        GameObject[] butterflies = GameObject.FindGameObjectsWithTag("ButterflyParticle");
        
        if (butterflies.Length == 0)
        {
            // 태그가 없으면 이름으로도 찾아보기 (폴백)
            GameObject butterfly = GameObject.Find("ButterflyParticle");
            if (butterfly != null && butterfly.activeInHierarchy)
            {
                float distance = Vector3.Distance(pet.transform.position, butterfly.transform.position);
                if (distance < DETECTION_RANGE)
                {
                    targetButterfly = butterfly;
                    Debug.Log($"[ButterflyPlayActivity] 이름으로 나비 발견: {distance:F1}m 거리");
                }
            }
            return;
        }
        
        // 가장 가까운 나비 찾기
        foreach (GameObject butterfly in butterflies)
        {
            if (butterfly == null || !butterfly.activeInHierarchy)
                continue;
                
            float distance = Vector3.Distance(pet.transform.position, butterfly.transform.position);
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                targetButterfly = butterfly;
            }
        }
        
        if (targetButterfly != null)
        {
            Debug.Log($"[ButterflyPlayActivity] 나비 발견: {nearestDistance:F1}m 거리");
        }
    }
    
    private IEnumerator PlayWithButterfly()
    {
        // 나비에게 접근
        while (targetButterfly != null && isPlayingWithButterfly)
        {
            float distance = Vector3.Distance(pet.transform.position, targetButterfly.transform.position);
            
            if (distance > PLAY_DISTANCE)
            {
                // 나비에게 이동
                if (pet.agent != null && pet.agent.enabled)
                {
                    pet.agent.SetDestination(targetButterfly.transform.position);
                    
                    // 달리기 애니메이션
                    if (animationController != null)
                    {
                        animationController.SetContinuousAnimation(PetAnimationController.PetAnimationType.Run);
                    }
                }
            }
            else
            {
                // 나비 주변에서 놀기
                yield return PlayAroundButterfly();
            }
            
            yield return new WaitForSeconds(0.2f);
        }
    }
    
    private IEnumerator PlayAroundButterfly()
    {
        Debug.Log($"[ButterflyPlayActivity] {pet.petName}이(가) 나비 주변에서 놀기 시작!");
        
        float playTime = 0f;
        
        while (targetButterfly != null && isPlayingWithButterfly && playTime < 10f)
        {
            // 랜덤한 행동 선택
            int action = Random.Range(0, 3);
            
            switch (action)
            {
                case 0: // 점프
                    if (animationController != null)
                    {
                        pet.StartCoroutine(animationController.PlayAnimationWithCustomDuration(
                            PetAnimationController.PetAnimationType.Jump, 1.5f));
                    }
                    yield return new WaitForSeconds(1.5f);
                    break;
                    
                case 1: // 나비 주변 돌기
                    yield return CircleAroundButterfly();
                    break;
                    
                case 2: // 제자리에서 관찰
                    if (animationController != null)
                    {
                        animationController.SetContinuousAnimation(PetAnimationController.PetAnimationType.Idle);
                    }
                    
                    // 나비를 바라보기
                    if (targetButterfly != null)
                    {
                        Vector3 lookDirection = (targetButterfly.transform.position - pet.transform.position).normalized;
                        lookDirection.y = 0;
                        pet.transform.rotation = Quaternion.LookRotation(lookDirection);
                    }
                    
                    yield return new WaitForSeconds(2f);
                    break;
            }
            
            playTime += 2f;
            
            // 가끔 기쁨 표현
            if (Random.Range(0f, 1f) < 0.3f && emotionManager != null)
            {
                emotionManager.ShowPetEmotion(pet, EmotionType.Happy, 1f);
            }
        }
    }
    
    private IEnumerator CircleAroundButterfly()
    {
        if (targetButterfly == null || pet.agent == null)
            yield break;
            
        float circleTime = 0f;
        float radius = 2f;
        float angleSpeed = 60f; // 초당 회전 각도
        
        Vector3 butterflyPos = targetButterfly.transform.position;
        
        while (circleTime < 3f && targetButterfly != null && isPlayingWithButterfly)
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
}