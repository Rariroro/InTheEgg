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
    private EmotionBubble activeBubble;
    private GameObject activeParticle;
    
    // 참조
    private PetController petController;
    
    private void Awake()
    {
        petController = GetComponent<PetController>();
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
            Debug.Log($"[PetEmotionController] {petController.petName}: EmotionOrigin 탐색 시작. 1차 탐색 루트: {rootToSearch.name}");
            emotionOrigin = FindDeepChild(rootToSearch, "EmotionOrigin");
            
            // 2차: 못 찾으면 최상위 GameObject (PetController가 있는 곳)에서 찾기
            if (emotionOrigin == null && petModelTransform != null)
            {
                Debug.Log($"[PetEmotionController] {petController.petName}: 1차에서 못 찾음. 2차 탐색 루트: {transform.name}");
                emotionOrigin = FindDeepChild(transform, "EmotionOrigin");
            }
            
            // 결과 로깅
            if (emotionOrigin != null)
            {
                Debug.Log($"[PetEmotionController] {petController.petName}: EmotionOrigin 찾음! 위치: {emotionOrigin.position}, 부모: {emotionOrigin.parent.name}");
            }
            else
            {
                Debug.LogWarning($"[PetEmotionController] {petController.petName}: EmotionOrigin을 찾을 수 없음. 기본값(transform) 사용");
                emotionOrigin = transform;
            }
        }
        else
        {
            Debug.Log($"[PetEmotionController] {petController.petName}: EmotionOrigin이 이미 인스펙터에서 할당됨. 위치: {emotionOrigin.position}");
        }
    }
    
    /// <summary>
    /// 감정을 표현합니다.
    /// </summary>
    public void ShowEmotion(EmotionType emotion, float duration = 10f)
    {
        // 기존에 표시되던 감정 표현(말풍선 또는 파티클)을 먼저 제거합니다.
        HideEmotion();

        if (EmotionManager.Instance != null)
        {
            Transform targetTransform = emotionOrigin != null ? emotionOrigin : transform;
            Debug.Log($"[PetEmotionController] {petController.petName}: 감정 표시 - {emotion}, emotionOrigin 사용: {emotionOrigin != null}, 타겟 위치: {targetTransform.position}");
            
            GameObject emotionObject = EmotionManager.Instance.ShowPetEmotion(petController, emotion, duration);
            
            if (emotionObject != null)
            {
                // 반환된 오브젝트가 EmotionBubble 타입인지 확인하고, activeBubble에 할당합니다.
                if (emotionObject.TryGetComponent<EmotionBubble>(out EmotionBubble bubble))
                {
                    activeBubble = bubble;
                    Debug.Log($"[PetEmotionController] {petController.petName}: 말풍선 생성됨. 초기 위치: {bubble.transform.position}");
                }
                else
                {
                    // 파티클 시스템인 경우 activeParticle에 저장합니다.
                    activeParticle = emotionObject;
                    Debug.Log($"[PetEmotionController] {petController.petName}: 파티클 생성됨. 위치: {emotionObject.transform.position}");
                }
            }
        }
    }

    /// <summary>
    /// 현재 표시 중인 감정 표현을 숨깁니다.
    /// </summary>
    public void HideEmotion()
    {
        // 활성화된 말풍선이 있다면 풀에 반환합니다.
        if (activeBubble != null)
        {
            if (EmotionManager.Instance != null)
            {
                EmotionManager.Instance.ReturnBubbleToPool(activeBubble);
            }
            activeBubble = null;
        }

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
            case PetStatus.Interacting:
                // 상호작용 시작 시 기쁨 이모티콘
                if (Random.value < 0.5f)
                    ShowEmotion(EmotionType.Happy, 2f);
                break;
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
        return activeBubble != null || activeParticle != null;
    }
    
    public Transform GetEmotionOrigin()
    {
        return emotionOrigin;
    }
}