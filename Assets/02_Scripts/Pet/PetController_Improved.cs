// PetController_Improved.cs
// 기존 PetController에 새로운 분리된 컴포넌트들을 점진적으로 통합하는 버전
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 개선된 PetController
/// Phase 4의 분리된 컴포넌트들을 활용하면서 기존 시스템과의 호환성 유지
/// </summary>
public partial class PetController : MonoBehaviour
{
    // ===== Phase 4: 새로운 분리된 컴포넌트들 =====
    private PetMovement newMovement;
    private PetAnimator newAnimator;
    private PetSensor newSensor;
    private PetEffects newEffects;
    private PetInteractor newInteractor;
    
    // 새로운 컴포넌트 활성화 플래그
    [Header("New Component System")]
    [SerializeField] private bool useNewMovementSystem = false;
    [SerializeField] private bool useNewAnimationSystem = false;
    [SerializeField] private bool useNewSensorSystem = false;
    [SerializeField] private bool useNewEffectsSystem = false;
    [SerializeField] private bool useNewInteractionSystem = false;
    
    // 기본 활성화 상태 (기존 시스템과의 호환성)
    [HideInInspector] public bool isActive = true;
    
    /// <summary>
    /// 새로운 컴포넌트 시스템 초기화 (기존 Awake에 추가)
    /// </summary>
    private void InitializeNewComponents()
    {
        // 1. 새로운 이동 시스템
        if (useNewMovementSystem)
        {
            newMovement = gameObject.AddComponent<PetMovement>();
            newMovement.Init(this, agent);
            Debug.Log($"[PetController] {petName}: 새로운 이동 시스템 활성화");
        }
        
        // 2. 새로운 애니메이션 시스템
        if (useNewAnimationSystem)
        {
            newAnimator = gameObject.AddComponent<PetAnimator>();
            newAnimator.Init(this, animator);
            Debug.Log($"[PetController] {petName}: 새로운 애니메이션 시스템 활성화");
        }
        
        // 3. 새로운 감지 시스템
        if (useNewSensorSystem)
        {
            newSensor = gameObject.AddComponent<PetSensor>();
            newSensor.Init(this);
            Debug.Log($"[PetController] {petName}: 새로운 감지 시스템 활성화");
        }
        
        // 4. 새로운 이펙트 시스템
        if (useNewEffectsSystem)
        {
            newEffects = gameObject.AddComponent<PetEffects>();
            newEffects.Init(this);
            Debug.Log($"[PetController] {petName}: 새로운 이펙트 시스템 활성화");
        }
        
        // 5. 새로운 상호작용 시스템
        if (useNewInteractionSystem)
        {
            newInteractor = gameObject.AddComponent<PetInteractor>();
            newInteractor.Init(this, petState, newEffects ?? null, newMovement ?? null);
            
            // 이벤트 연결
            if (newInteractor != null)
            {
                newInteractor.OnSelected += () => {
                    isSelected = true;
                    // SyncStateWithFlags는 Update에서 자동으로 호출됨
                };
                
                newInteractor.OnDeselected += () => {
                    isSelected = false;
                    // SyncStateWithFlags는 Update에서 자동으로 호출됨
                };
                
                newInteractor.OnHoldStarted += () => {
                    isHolding = true;
                    // SyncStateWithFlags는 Update에서 자동으로 호출됨
                };
                
                newInteractor.OnHoldEnded += () => {
                    isHolding = false;
                    // SyncStateWithFlags는 Update에서 자동으로 호출됨
                };
            }
            
            Debug.Log($"[PetController] {petName}: 새로운 상호작용 시스템 활성화");
        }
    }
    
    /// <summary>
    /// 이동 명령 (새로운 시스템과 기존 시스템 선택)
    /// </summary>
    public bool MoveTo(Vector3 destination)
    {
        if (useNewMovementSystem && newMovement != null)
        {
            return newMovement.MoveTo(destination);
        }
        else
        {
            // 기존 이동 로직
            if (agent != null && agent.enabled)
            {
                agent.SetDestination(destination);
                agent.isStopped = false;
                return true;
            }
            return false;
        }
    }
    
    /// <summary>
    /// 애니메이션 재생 (새로운 시스템과 기존 시스템 선택)
    /// </summary>
    public void PlayAnimation(string animationName, bool loop = true)
    {
        if (useNewAnimationSystem && newAnimator != null)
        {
            // AnimationType enum으로 변환
            if (Enum.TryParse<PetAnimator.AnimationType>(animationName, out var animType))
            {
                if (loop)
                    newAnimator.PlayContinuous(animType);
                else
                    newAnimator.PlayOnce(animType);
            }
        }
        else if (animationController != null)
        {
            // 기존 애니메이션 시스템 사용
            if (Enum.TryParse<PetAnimationController.PetAnimationType>(animationName, out var petAnimType))
            {
                if (loop)
                    animationController.SetContinuousAnimation(petAnimType);
                else
                    StartCoroutine(animationController.PlayAnimationWithCustomDuration(petAnimType, 2f));
            }
        }
    }
    
    /// <summary>
    /// 감정 표시 오버라이드 (새로운 시스템 사용 시)
    /// </summary>
    public void ShowEmotionNew(EmotionType emotion, float duration = 3f)
    {
        if (useNewEffectsSystem && newEffects != null)
        {
            newEffects.ShowEmotion(emotion, duration);
        }
        else
        {
            // 기존 ShowEmotion 메서드 호출
            ShowEmotion(emotion, duration);
        }
    }
    
    /// <summary>
    /// 주변 펫 감지 (새로운 시스템 사용)
    /// </summary>
    public List<PetController> GetNearbyPets(float radius = 10f)
    {
        if (useNewSensorSystem && newSensor != null)
        {
            return newSensor.DetectNearbyPets(radius);
        }
        else
        {
            // 기존 감지 로직
            List<PetController> nearbyPets = new List<PetController>();
            Collider[] colliders = Physics.OverlapSphere(transform.position, radius, LayerMask.GetMask("Pet"));
            
            foreach (var collider in colliders)
            {
                PetController otherPet = collider.GetComponent<PetController>();
                if (otherPet != null && otherPet != this && otherPet.isActive)
                {
                    nearbyPets.Add(otherPet);
                }
            }
            
            return nearbyPets;
        }
    }
    
    /// <summary>
    /// 터치 처리 (새로운 시스템 사용)
    /// </summary>
    public void OnTouched()
    {
        if (useNewInteractionSystem && newInteractor != null)
        {
            newInteractor.HandleTouch();
        }
        else
        {
            // 기존 터치 처리 로직
            isSelected = true;
            if (animationController != null)
            {
                animationController.StopContinuousAnimation();
            }
        }
    }
    
    // 프로퍼티로 새로운 컴포넌트 접근 제공
    public PetMovement Movement => newMovement;
    public PetAnimator AnimatorNew => newAnimator;
    public PetSensor Sensor => newSensor;
    public PetEffects Effects => newEffects;
    public PetInteractor Interactor => newInteractor;
}

/// <summary>
/// 기존 Awake 메서드에 추가할 초기화 코드
/// </summary>
public static class PetControllerExtensions
{
    public static void InitializeImprovedSystems(this PetController controller)
    {
        // InitializeNewComponents를 호출하여 새로운 시스템 초기화
        var method = controller.GetType().GetMethod("InitializeNewComponents", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        method?.Invoke(controller, null);
    }
}