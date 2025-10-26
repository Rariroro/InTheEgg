using UnityEngine;
using System.Collections;

/// <summary>
/// 펫의 감정 표현(이모티콘, 파티클)을 관리하는 컨트롤러
/// </summary>
public class PetEmotionController : MonoBehaviour
{
    [Header("Emotion Settings")]
    [Tooltip("감정 표현(이모티콘, 파티클)이 생성될 기준 위치입니다. 비워두면 자식 중 'EmotionOrigin'을 자동으로 찾습니다.")]
    [SerializeField] private Transform emotionOrigin;
    
    // 현재 활성화된 감정 표현
    private GameObject activeParticle;
    
    // 배고픔 감정 관련
    private bool isShowingHungerEmotion = false;
    private EmotionType currentFoodEmotion;
    private float hungerEmotionTimer = 0f;
    private float hungerEmotionChangeInterval = 7f; // 7초마다 음식 종류 변경
    private bool hungerEmotionPaused = false; // 다른 감정으로 인한 일시 중단

    // 보물 감정 관련
    private bool isTreasureEmotionActive = false; // 보물 발견 시 활성화
    
    // 참조
    private PetController petController;
    
    private void Awake()
    {
        petController = GetComponent<PetController>();
    }
    
    private void Update()
    {
        // 배고픔 감정 표시 시스템
        if (petController != null && petController.Needs != null)
        {
            bool isHungry = petController.Needs.Hunger >= 70f;
            
            if (isHungry && !hungerEmotionPaused)
            {
                if (!isShowingHungerEmotion)
                {
                    // 배고픔 감정 시작
                    StartShowingHungerEmotion();
                }
                else
                {
                    // 일정 시간마다 음식 종류 변경
                    hungerEmotionTimer += Time.deltaTime;
                    if (hungerEmotionTimer >= hungerEmotionChangeInterval)
                    {
                        hungerEmotionTimer = 0f;
                        hungerEmotionChangeInterval = UnityEngine.Random.Range(15f, 25f); // 15-25초 랜덤
                        ChangeHungerEmotion();
                    }
                }
            }
            else if (!isHungry && isShowingHungerEmotion)
            {
                // 배고픔이 해결되면 감정 중단
                StopShowingHungerEmotion();
            }
        }
    }
    
    /// <summary>
    /// 컨트롤러를 초기화합니다.
    /// </summary>
    public void Init(PetController controller)
    {
        petController = controller;
        Initialize(controller.petModelTransform);
    }
    
    /// <summary>
    /// EmotionOrigin을 초기화합니다. PetController의 InitializeControllers에서 호출됩니다.
    /// </summary>
    public void Initialize(Transform petModelTransform)
    {
        // EmotionOrigin 자동 탐색 (인스펙터에서 할당하지 않은 경우만)
        if (emotionOrigin == null)
        {
            // 1차: petModelTransform에서 찾기
            Transform rootToSearch = petModelTransform != null ? petModelTransform : transform;
            // Debug.Log($"[PetEmotionController] {petController.petName}: EmotionOrigin 탐색 시작. 1차 탐색 루트: {rootToSearch.name}");
            emotionOrigin = FindDeepChild(rootToSearch, "EmotionOrigin");
            
            // 2차: 못 찾으면 최상위 GameObject (PetController가 있는 곳)에서 찾기
            if (emotionOrigin == null && petModelTransform != null)
            {
                // Debug.Log($"[PetEmotionController] {petController.petName}: 1차에서 못 찾음. 2차 탐색 루트: {transform.name}");
                emotionOrigin = FindDeepChild(transform, "EmotionOrigin");
            }
            
            // 결과 로깅
            if (emotionOrigin != null)
            {
                // Debug.Log($"[PetEmotionController] {petController.petName}: EmotionOrigin 찾음! 위치: {emotionOrigin.position}, 부모: {emotionOrigin.parent.name}");
            }
            else
            {
                Debug.LogWarning($"[PetEmotionController] {petController.petName}: EmotionOrigin을 찾을 수 없음. 기본값(transform) 사용");
                emotionOrigin = transform;
            }
        }
        else
        {
            // Debug.Log($"[PetEmotionController] {petController.petName}: EmotionOrigin이 이미 인스펙터에서 할당됨. 위치: {emotionOrigin.position}");
        }
    }
    
    /// <summary>
    /// 감정을 표현합니다.
    /// </summary>
    public void ShowEmotion(EmotionType emotion, float duration = 10f)
    {
        // 보물 감정이 활성 중이면 보물 감정만 허용
        if (isTreasureEmotionActive && !IsTreasureEmotion(emotion))
        {
            return; // 다른 감정은 차단
        }

        // Debug.Log("감정 실행됨");
        // 음식 감정이 아닌 다른 감정이면 배고픔 감정 일시 중단
        if (!IsFoodEmotion(emotion) && isShowingHungerEmotion)
        {
            hungerEmotionPaused = true;
        }

        // 보물 감정이면 플래그 설정
        if (IsTreasureEmotion(emotion))
        {
            isTreasureEmotionActive = true;
            duration = 999f; // 무제한으로 설정
        }

        // 모든 경우에 기존 감정을 먼저 제거 (음식 감정 포함)
        HideEmotion();

        if (EmotionManager.Instance != null)
        {
            Transform targetTransform = emotionOrigin != null ? emotionOrigin : transform;
            // Debug.Log($"[PetEmotionController] {petController.petName}: 감정 표시 - {emotion}, emotionOrigin 사용: {emotionOrigin != null}, 타겟 위치: {targetTransform.position}");

            GameObject emotionObject = EmotionManager.Instance.ShowPetEmotion(petController, emotion, duration);

            if (emotionObject != null)
            {
                // 파티클 시스템을 activeParticle에 저장합니다.
                activeParticle = emotionObject;
                // Debug.Log($"[PetEmotionController] {petController.petName}: 파티클 생성됨. 위치: {emotionObject.transform.position}");

                // 음식 감정이나 보물 감정이 아닌 경우에만 타이머 설정
                if (!IsFoodEmotion(emotion) && !IsTreasureEmotion(emotion) && duration > 0)
                {
                    StartCoroutine(RestoreHungerEmotionAfterDelay(duration));
                }
            }
        }
    }

    /// <summary>
    /// 현재 표시 중인 감정 표현을 숨깁니다.
    /// </summary>
    public void HideEmotion()
    {
        // 활성화된 파티클이 있다면 제거합니다.
        if (activeParticle != null)
        {
            Destroy(activeParticle);
            activeParticle = null;
        }
    }
    
    /// <summary>
    /// 상태 변경에 따른 감정 표현
    /// </summary>
    public void OnStatusChanged(PetStatus newStatus)
    {
        switch (newStatus)
        {
            case PetStatus.Emergency:
                // 긴급 상태일 때 경고 이모티콘
                ShowEmotion(EmotionType.Scared, 2f);
                break;
            // case PetStatus.Interacting:
            //     // 상호작용 시작 시 기쁨 이모티콘
            //     if (Random.value < 0.5f)
            //         ShowEmotion(EmotionType.Happy, 2f);
            //     break;
        }
    }
    
    /// <summary>
    /// 욕구 시스템에서 요청한 감정 표현
    /// </summary>
    public void OnEmotionRequired(EmotionType emotionType)
    {
        ShowEmotion(emotionType, 3f);
    }
    
    /// <summary>
    /// 자식 오브젝트를 재귀적으로 탐색하여 이름이 일치하는 Transform을 찾습니다.
    /// </summary>
    private Transform FindDeepChild(Transform parent, string name)
    {
        foreach (Transform child in parent)
        {
            if (child.name == name)
                return child;
            
            Transform result = FindDeepChild(child, name);
            if (result != null)
                return result;
        }
        return null;
    }
    
    private void OnDestroy()
    {
        // 정리 작업
        HideEmotion();
    }
    
    // 디버깅용
    public bool HasActiveEmotion()
    {
        return activeParticle != null;
    }
    
    public Transform GetEmotionOrigin()
    {
        return emotionOrigin;
    }
    
    /// <summary>
    /// 배고픔 감정 표시 시작
    /// </summary>
    private void StartShowingHungerEmotion()
    {
        isShowingHungerEmotion = true;
        hungerEmotionPaused = false;
        hungerEmotionTimer = 0f;
        hungerEmotionChangeInterval = UnityEngine.Random.Range(15f, 25f); // 15-25초로 증가
        
        currentFoodEmotion = GetRandomFoodEmotionByDiet();
        ShowEmotion(currentFoodEmotion, 999f); // 매우 긴 시간 설정 (실제로는 계속 유지됨)
    }
    
    /// <summary>
    /// 배고픔 감정 종류 변경
    /// </summary>
    private void ChangeHungerEmotion()
    {
        if (!hungerEmotionPaused)
        {
            currentFoodEmotion = GetRandomFoodEmotionByDiet();
            ShowEmotion(currentFoodEmotion, 999f);
        }
    }
    
    /// <summary>
    /// 배고픔 감정 표시 중단
    /// </summary>
    private void StopShowingHungerEmotion()
    {
        isShowingHungerEmotion = false;
        hungerEmotionPaused = false;
        if (IsFoodEmotion(currentFoodEmotion))
        {
            HideEmotion();
        }
    }
    
    /// <summary>
    /// 다른 감정 표시 후 배고픔 감정 복원
    /// </summary>
    private System.Collections.IEnumerator RestoreHungerEmotionAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        
        if (isShowingHungerEmotion && hungerEmotionPaused)
        {
            hungerEmotionPaused = false;
            // 배고픔 상태가 계속되면 음식 감정 다시 표시
            if (petController.Needs.Hunger >= 70f)
            {
                ShowEmotion(currentFoodEmotion, 999f);
            }
        }
    }
    
    /// <summary>
    /// 음식 관련 감정인지 확인
    /// </summary>
    private bool IsFoodEmotion(EmotionType emotion)
    {
        return emotion == EmotionType.Thought_Food_Meat ||
               emotion == EmotionType.Thought_Food_Fish ||
               emotion == EmotionType.Thought_Food_Grass ||
               emotion == EmotionType.Thought_Food_Grain ||
               emotion == EmotionType.Thought_Food_Fruit ||
               emotion == EmotionType.Thought_Food_Vegetable;
    }

    /// <summary>
    /// 보물 관련 감정인지 확인
    /// </summary>
    private bool IsTreasureEmotion(EmotionType emotion)
    {
        return emotion == EmotionType.Tresure;
    }

    /// <summary>
    /// 보물 감정 중단 (유저가 보물 수집 시 호출)
    /// </summary>
    public void StopTreasureEmotion()
    {
        if (isTreasureEmotionActive)
        {
            isTreasureEmotionActive = false;
            HideEmotion();
        }
    }
    
    /// <summary>
    /// 펫의 식성에 따른 랜덤 음식 감정 선택
    /// </summary>
    private EmotionType GetRandomFoodEmotionByDiet()
    {
        var diet = petController.diet;
        var foodEmotions = new System.Collections.Generic.List<EmotionType>();
        
        // 식성에 따라 가능한 음식 감정 추가
        if ((diet & PetTraits.DietaryFlags.Meat) != 0)
            foodEmotions.Add(EmotionType.Thought_Food_Meat);
            
        if ((diet & PetTraits.DietaryFlags.Fish) != 0)
            foodEmotions.Add(EmotionType.Thought_Food_Fish);
            
        if ((diet & PetTraits.DietaryFlags.Grass) != 0)
            foodEmotions.Add(EmotionType.Thought_Food_Grass);
            
        if ((diet & PetTraits.DietaryFlags.SeedsAndGrains) != 0)
            foodEmotions.Add(EmotionType.Thought_Food_Grain);
            
        if ((diet & PetTraits.DietaryFlags.FruitsAndVegetables) != 0)
        {
            foodEmotions.Add(EmotionType.Thought_Food_Fruit);
            foodEmotions.Add(EmotionType.Thought_Food_Vegetable);
        }
        
        // 식성이 없거나 매칭되는 음식 감정이 없으면 기본 Hungry 반환
        if (foodEmotions.Count == 0)
            return EmotionType.Hungry;
        
        // 랜덤으로 하나 선택
        return foodEmotions[UnityEngine.Random.Range(0, foodEmotions.Count)];
    }
}