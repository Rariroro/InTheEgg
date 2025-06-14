using System.Collections;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 펫이 물에 들어갔을 때의 효과를 처리하는 컴포넌트
/// </summary>
public class WaterEffect : MonoBehaviour
{
    private PetController petController;
    private Transform petModelTransform;
    
    [Header("Water Effect Settings")]
    private float targetDepth = 0f;
    private float currentDepth = 0f;
    private float depthTransitionSpeed = 2f;
    private float speedMultiplier = 1f;
    
    private bool isInWater = false;
    private Coroutine waterEffectCoroutine;
    
    // 원래 속도 저장
    private float originalSpeed;
    private float originalAcceleration;
    private float originalAnimSpeed;
    
    private void Awake()
    {
        petController = GetComponent<PetController>();
        petModelTransform = petController.petModelTransform;
    }
    
    public void EnterWater(float waterDepth, float speedMult)
    {
        if (isInWater) return;
        
        isInWater = true;
        targetDepth = -waterDepth;
        speedMultiplier = speedMult;
        
        // 원래 값 저장
        if (petController.agent != null && petController.agent.enabled)
        {
            originalSpeed = petController.agent.speed;
            originalAcceleration = petController.agent.acceleration;
        }
        
        if (petController.animator != null)
        {
            originalAnimSpeed = petController.animator.speed;
        }
        
        // 효과 시작
        if (waterEffectCoroutine != null)
        {
            StopCoroutine(waterEffectCoroutine);
        }
        waterEffectCoroutine = StartCoroutine(WaterEffectRoutine());
        
        // 속도 감소 적용
        ApplySpeedReduction();
    }
    
    public void ExitWater()
    {
        if (!isInWater) return;
        
        isInWater = false;
        targetDepth = 0f;
        
        // 속도 복구
        RestoreOriginalSpeed();
    }
    
    private IEnumerator WaterEffectRoutine()
    {
        while (enabled)
        {
            // 부드러운 깊이 전환
            currentDepth = Mathf.Lerp(currentDepth, targetDepth, Time.deltaTime * depthTransitionSpeed);
            
            // 모델 위치 업데이트
            if (petModelTransform != null)
            {
                petModelTransform.localPosition = new Vector3(0, currentDepth, 0);
            }
            
            // 물에서 나왔고 깊이가 거의 0이면 종료
            if (!isInWater && Mathf.Abs(currentDepth) < 0.01f)
            {
                currentDepth = 0f;
                if (petModelTransform != null)
                {
                    petModelTransform.localPosition = Vector3.zero;
                }
                break;
            }
            
            yield return null;
        }
        
        waterEffectCoroutine = null;
    }
    
    private void ApplySpeedReduction()
    {
        // 모으기 중이면 속도 변경하지 않음
        if (petController.isGathering) return;
        
        if (petController.agent != null && petController.agent.enabled)
        {
            petController.agent.speed = originalSpeed * speedMultiplier;
            petController.agent.acceleration = originalAcceleration * speedMultiplier;
        }
        
        if (petController.animator != null)
        {
            petController.animator.speed = speedMultiplier;
        }
    }
    
    private void RestoreOriginalSpeed()
    {
        // 모으기 중이면 속도 복구하지 않음
        if (petController.isGathering) return;
        
        if (petController.agent != null && petController.agent.enabled)
        {
            petController.agent.speed = originalSpeed;
            petController.agent.acceleration = originalAcceleration;
        }
        
        if (petController.animator != null)
        {
            petController.animator.speed = originalAnimSpeed;
        }
    }
    
    private void OnDisable()
    {
        // 컴포넌트가 비활성화되면 효과 정리
        if (waterEffectCoroutine != null)
        {
            StopCoroutine(waterEffectCoroutine);
        }
        
        if (petModelTransform != null)
        {
            petModelTransform.localPosition = Vector3.zero;
        }
    }
}