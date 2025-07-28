using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// PetController에 새로운 컴포넌트 시스템을 추가하는 패치
/// 이 스크립트를 PetController와 같은 GameObject에 추가하면 자동으로 새로운 시스템을 활성화
/// </summary>
[RequireComponent(typeof(PetController))]
public class PetControllerPatch : MonoBehaviour
{
    [Header("새로운 컴포넌트 시스템 활성화")]
    [SerializeField] private bool enableMovementSystem = true;
    [SerializeField] private bool enableAnimationSystem = true;
    [SerializeField] private bool enableSensorSystem = true;
    [SerializeField] private bool enableEffectsSystem = true;
    [SerializeField] private bool enableInteractionSystem = true;
    
    private PetController petController;
    private PetMovement petMovement;
    private PetAnimator petAnimator;
    private PetSensor petSensor;
    private PetEffects petEffects;
    private PetInteractor petInteractor;
    
    void Awake()
    {
        petController = GetComponent<PetController>();
        if (petController == null)
        {
            Debug.LogError("[PetControllerPatch] PetController를 찾을 수 없습니다!");
            return;
        }
        
        InitializeNewComponents();
    }
    
    private void InitializeNewComponents()
    {
        // 1. 이동 시스템
        if (enableMovementSystem && petController.agent != null)
        {
            petMovement = gameObject.AddComponent<PetMovement>();
            petMovement.Init(petController, petController.agent);
            Debug.Log($"[PetControllerPatch] {petController.petName}: 새로운 이동 시스템 활성화");
        }
        
        // 2. 애니메이션 시스템
        if (enableAnimationSystem && petController.animator != null)
        {
            petAnimator = gameObject.AddComponent<PetAnimator>();
            petAnimator.Init(petController, petController.animator);
            Debug.Log($"[PetControllerPatch] {petController.petName}: 새로운 애니메이션 시스템 활성화");
        }
        
        // 3. 감지 시스템
        if (enableSensorSystem)
        {
            petSensor = gameObject.AddComponent<PetSensor>();
            petSensor.Init(petController);
            Debug.Log($"[PetControllerPatch] {petController.petName}: 새로운 감지 시스템 활성화");
        }
        
        // 4. 이펙트 시스템
        if (enableEffectsSystem)
        {
            petEffects = gameObject.AddComponent<PetEffects>();
            petEffects.Init(petController);
            Debug.Log($"[PetControllerPatch] {petController.petName}: 새로운 이펙트 시스템 활성화");
        }
        
        // 5. 상호작용 시스템
        if (enableInteractionSystem)
        {
            petInteractor = gameObject.AddComponent<PetInteractor>();
            // PetState는 기존 시스템의 플래그로 대체
            var petState = petController.State;
            petInteractor.Init(petController, petState, petEffects, petMovement);
            
            // 이벤트 연결
            SetupInteractionEvents();
            
            Debug.Log($"[PetControllerPatch] {petController.petName}: 새로운 상호작용 시스템 활성화");
        }
    }
    
    private void SetupInteractionEvents()
    {
        if (petInteractor == null) return;
        
        petInteractor.OnSelected += () => {
            petController.isSelected = true;
            // SyncStateWithFlags는 PetController의 Update에서 자동으로 호출됨
        };
        
        petInteractor.OnDeselected += () => {
            petController.isSelected = false;
            // SyncStateWithFlags는 PetController의 Update에서 자동으로 호출됨
        };
        
        petInteractor.OnHoldStarted += () => {
            petController.isHolding = true;
            // SyncStateWithFlags는 PetController의 Update에서 자동으로 호출됨
        };
        
        petInteractor.OnHoldEnded += () => {
            petController.isHolding = false;
            // SyncStateWithFlags는 PetController의 Update에서 자동으로 호출됨
        };
    }
    
    // 공개 프로퍼티로 새로운 컴포넌트 접근 제공
    public PetMovement Movement => petMovement;
    public PetAnimator Animator => petAnimator;
    public PetSensor Sensor => petSensor;
    public PetEffects Effects => petEffects;
    public PetInteractor Interactor => petInteractor;
    
    /// <summary>
    /// 기존 메서드들을 새로운 시스템으로 리다이렉트하는 예시
    /// </summary>
    public void ShowEmotionWithNewSystem(EmotionType emotion, float duration = 3f)
    {
        if (enableEffectsSystem && petEffects != null)
        {
            petEffects.ShowEmotion(emotion, duration);
        }
        else
        {
            petController.ShowEmotion(emotion, duration);
        }
    }
    
    public List<PetController> GetNearbyPetsWithNewSystem(float radius = 10f)
    {
        if (enableSensorSystem && petSensor != null)
        {
            return petSensor.DetectNearbyPets(radius);
        }
        else
        {
            // 기존 방식으로 폴백
            return new List<PetController>();
        }
    }
}