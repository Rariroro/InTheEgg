using UnityEngine;
using System.Collections;
using PetAIProperties = PetTraits;

public class DivingActivity : PetActivityAdapter
{
   
    private static PetController currentDiver = null;
    
   
    private Transform divingSpot;
    private bool isMovingToSpot = false;
    private bool isDiving = false;
    private float lastDivingTime = -60f;
    private float failedAttemptTime = -60f;
    private Coroutine divingCoroutine = null;
    private const float DIVING_COOLDOWN = 30f;
    private const float FAILED_ATTEMPT_COOLDOWN = 60f;
    private const float SPOT_ARRIVAL_DISTANCE = 2f;
    private const float MAX_DISTANCE_TO_WATER = 50f;
    
   
    private Vector3 jumpStartPosition;
    private Vector3 jumpTargetPosition;
    private float jumpProgress = 0f;
    private float jumpHeight = 5f;
    private float jumpDuration = 1.5f;
    
    public override string Name => "Diving";
    public override bool IsInterruptible => !isDiving;
    
    public DivingActivity(PetController petController) : base(petController)
    {
    }
    
    public override bool CanStart(PetState state, PetNeeds needs)
    {
       
        if (pet.personality != PetAIProperties.Personality.Playful)
        {
           
            return false;
        }
            
       
        if (isMovingToSpot || isDiving)
            return true;
            
       
        if (state.IsHolding || state.IsSelected || state.IsGathering)
            return false;
            
       
        if (needs.Hunger > 70f || needs.Sleepiness > 70f)
            return false;
            
       
        if (Time.time - lastDivingTime < DIVING_COOLDOWN)
            return false;
            
       
        if (Time.time - failedAttemptTime < FAILED_ATTEMPT_COOLDOWN)
            return false;
            
       
        if (currentDiver != null && currentDiver != pet)
            return false;
            
       
        GameObject spotObject = GameObject.FindWithTag("DivingSpot");
        if (spotObject == null)
        {
           
            return false;
        }
            
        divingSpot = spotObject.transform;
        
       
        float distanceToSpot = Vector3.Distance(pet.transform.position, divingSpot.position);
        if (distanceToSpot > MAX_DISTANCE_TO_WATER)
            return false;
            
       
       
        return true;
    }
    
    public override float GetPriority(PetState state, PetNeeds needs)
    {
        if (!CanStart(state, needs))
            return 0f;
            
       
        if (isMovingToSpot || isDiving)
            return 7f;
            
       
        return 5f;
    }
    
    public override void Start()
    {
       
        
       
        currentDiver = pet;
        isMovingToSpot = true;
        isDiving = false;
        
       
        if (divingSpot == null)
        {
            GameObject spotObject = GameObject.FindWithTag("DivingSpot");
            if (spotObject != null)
            {
                divingSpot = spotObject.transform;
               
            }
            else
            {
               
                Stop();
                return;
            }
        }
        
       
        if (pet.movementController != null)
        {
            pet.movementController.StopMovement();
        }
        
       
        if (pet.agent != null)
        {
            if (!pet.agent.enabled)
            {
               
                pet.agent.enabled = true;
            }
            if (!pet.agent.isOnNavMesh)
            {
               
                pet.agent.Warp(pet.transform.position);
            }
            
           
            pet.agent.isStopped = false; 
            pet.agent.speed = pet.baseSpeed; 
            pet.agent.acceleration = 8f; 
            pet.agent.updateRotation = true; 
            pet.agent.ResetPath(); 
            
           
        }
        else
        {
           
            Stop();
            return;
        }
        
       
        divingCoroutine = pet.StartCoroutine(MoveToSpotAndDive());
    }
    
    private IEnumerator MoveToSpotAndDive()
    {
       
        
       
       
       
       
        if (pet.State.IsHolding)
        {
           
            Stop();
            yield break;
        }
        
       
       
       
       
        if (pet.agent == null)
        {
           
            Stop();
            yield break;
        }
        
        if (!pet.agent.enabled)
        {
           
            pet.agent.enabled = true;
        }
        
        if (!pet.agent.isOnNavMesh)
        {
           
            pet.agent.Warp(pet.transform.position);
        }
        
        
        
        
       
        if (pet.agent.isStopped)
        {
           
            pet.agent.isStopped = false;
        }
        
        pet.agent.SetDestination(divingSpot.position);
       
        
       
        yield return null;
        
       
       
        if (!pet.agent.hasPath && !pet.agent.pathPending)
        {
           
            failedAttemptTime = Time.time;
            isMovingToSpot = false;
            isDiving = false;
            if (currentDiver == pet)
            {
                currentDiver = null;
            }
            yield break;
        }
        
       
        float timeoutCounter = 0f;
        int retryCount = 0;
        const int MAX_RETRIES = 3;
       
        
        while (isMovingToSpot && Vector3.Distance(pet.transform.position, divingSpot.position) > SPOT_ARRIVAL_DISTANCE)
        {
           
            if (pet.State.IsHolding)
            {
               
                Stop();
                yield break;
            }
            
           
            if (pet.State.IsSelected)
            {
               
                Stop();
                yield break;
            }
            
           
           
            {
               
               
            }
            
           
            timeoutCounter += 0.1f;
            if (timeoutCounter > 30f)
            {
               
                failedAttemptTime = Time.time;
                isMovingToSpot = false;
                isDiving = false;
                if (currentDiver == pet)
                {
                    currentDiver = null;
                }
                if (pet.agent != null && !pet.agent.enabled)
                {
                    pet.agent.enabled = true;
                    pet.agent.Warp(pet.transform.position);
                }
                yield break;
            }
            
           
            if (pet.agent.velocity.magnitude < 0.1f && timeoutCounter > 1f)
            {
               
                if (!pet.agent.hasPath || pet.agent.pathStatus == UnityEngine.AI.NavMeshPathStatus.PathInvalid)
                {
                    retryCount++;
                    if (retryCount >= MAX_RETRIES)
                    {
                       
                        failedAttemptTime = Time.time;
                        isMovingToSpot = false;
                        isDiving = false;
                        if (currentDiver == pet)
                        {
                            currentDiver = null;
                        }
                        yield break;
                    }
                    
                   
                    
                   
                    if (pet.agent != null && pet.agent.enabled && pet.agent.isOnNavMesh)
                    {
                        pet.agent.ResetPath();
                        yield return new WaitForSeconds(0.5f);
                        
                       
                        if (pet.State.IsHolding || pet.State.IsSelected)
                        {
                           
                            Stop();
                            yield break;
                        }
                        
                        if (pet.agent != null && pet.agent.enabled && pet.agent.isOnNavMesh)
                        {
                           
                            pet.agent.isStopped = false;
                            pet.agent.speed = pet.baseSpeed;
                            pet.agent.SetDestination(divingSpot.position);
                           
                        }
                        else
                        {
                           
                            failedAttemptTime = Time.time;
                            Stop();
                            yield break;
                        }
                    }
                    else
                    {
                       
                        failedAttemptTime = Time.time;
                        Stop();
                        yield break;
                    }
                }
            }
            
            yield return new WaitForSeconds(0.1f);
        }
        
        // ===== 스팟 도착, 다이빙 준비 =====
        // 마지막 상태 체크
        if (pet.State.IsHolding)
        {
            Stop();
            yield break;
        }
        
        isMovingToSpot = false;
        isDiving = true;
        
        // 다이빙 중에는 NavMesh 에이전트 비활성화
        // (점프 애니메이션을 직접 제어하기 위해)
        if (pet.agent != null && pet.agent.enabled)
        {
            pet.agent.enabled = false;
        }
        
       
       
       
       
        jumpStartPosition = pet.transform.position;
        
       
       
       
        
       
       
       
        float randomAngle = Random.Range(-60f, 60f);
        Vector3 toWater = Quaternion.AngleAxis(randomAngle, Vector3.up) * divingSpot.forward;
        jumpTargetPosition = divingSpot.position + toWater * 6f;
        
       
       
        jumpTargetPosition.y = 5f;
        
       
        
       
       
       
        pet.ShowEmotion(EmotionType.Happy);
        
       
       
       
        if (pet.animator != null)
        {
            pet.animator.SetInteger("animation", (int)PetAnimationController.PetAnimationType.Jump);
        }
        
       
       
       
       
        jumpProgress = 0f;
        while (jumpProgress < 1f)
        {
           
            if (pet.State.IsHolding)
            {
               
               
                if (pet.agent != null && !pet.agent.enabled)
                {
                    pet.agent.enabled = true;
                    pet.agent.Warp(pet.transform.position);
                }
                Stop();
                yield break;
            }
            
            jumpProgress += Time.deltaTime / jumpDuration;
            
           
            Vector3 currentPos = Vector3.Lerp(jumpStartPosition, jumpTargetPosition, jumpProgress);
            float parabola = 4f * jumpHeight * jumpProgress * (1f - jumpProgress);
            currentPos.y += parabola;
            
            pet.transform.position = currentPos;
            
           
            Vector3 direction = jumpTargetPosition - jumpStartPosition;
            direction.y = 0;
            if (direction != Vector3.zero)
            {
                pet.transform.rotation = Quaternion.LookRotation(direction);
            }
            
            yield return null;
        }
        
       
       
       
       
       
       
       
        var waterController = pet.GetComponent<PetWaterBehaviorController>();
        if (waterController != null)
        {
            waterController.StartDivingSequence();
           
        }
        
       
       
       
       
       
        yield return new WaitForSeconds(3f);
        
       
        if (pet.animator != null)
        {
            pet.animator.SetInteger("animation", 0);
        }
        
       
       
       
       
       
       
        if (!pet.State.IsHolding && pet.agent != null && !pet.agent.enabled)
        {
            pet.agent.enabled = true;
            pet.agent.Warp(pet.transform.position);
        }
        
       
       
       
        isDiving = false;
        lastDivingTime = Time.time;
        
       
        if (currentDiver == pet)
        {
            currentDiver = null;
        }
        
       
    }
    
    public override void Update()
    {
       
    }
    
    public override void Stop()
    {
       
        
       
        if (divingCoroutine != null)
        {
            pet.StopCoroutine(divingCoroutine);
            divingCoroutine = null;
           
        }
        
       
        isMovingToSpot = false;
        isDiving = false;
        
       
        if (!pet.State.IsHolding && pet.agent != null && !pet.agent.enabled)
        {
           
            UnityEngine.AI.NavMeshHit hit;
            if (UnityEngine.AI.NavMesh.SamplePosition(pet.transform.position, out hit, 2f, UnityEngine.AI.NavMesh.AllAreas))
            {
                pet.agent.enabled = true;
                pet.agent.Warp(hit.position);
            }
            else
            {
               
            }
        }
        
       
        if (currentDiver == pet)
        {
            currentDiver = null;
        }
    }
}