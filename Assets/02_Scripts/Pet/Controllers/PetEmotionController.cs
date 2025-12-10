using UnityEngine;
using System.Collections;
using InTheEgg.Constants;

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
        UpdateTreasureEmotion();
    }

    /// <summary>
    /// 보물 감정 파티클을 업데이트합니다.
    /// </summary>
    private void UpdateTreasureEmotion()
    {
        // Early return으로 중첩 제거
        if (!isTreasureEmotionActive || activeParticle == null)
            return;

        ParticleSystem ps = activeParticle.GetComponent<ParticleSystem>();
        if (ps != null && !ps.isPlaying)
        {
            ps.Play(); // 파티클이 멈추면 다시 재생
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
    /// <param name="emotion">표현할 감정 타입</param>
    /// <param name="duration">지속 시간 (기본: 10초)</param>
    public void ShowEmotion(EmotionType emotion, float duration = EmotionConstants.DURATION_LONG)
    {
        // Early return: 보물 감정 우선순위 체크
        if (!CanShowEmotion(emotion))
            return;

        // 감정 타입별 처리
        ProcessEmotionFlags(emotion, ref duration);

        // 기존 감정 제거
        HideEmotion();

        // 새 감정 표시
        CreateEmotionParticle(emotion, duration);
    }

    /// <summary>
    /// 감정 표시 가능 여부를 확인합니다.
    /// </summary>
    private bool CanShowEmotion(EmotionType emotion)
    {
        // 보물 감정이 활성 중이면 보물 감정만 허용
        if (isTreasureEmotionActive && !IsTreasureEmotion(emotion))
        {
            return false;
        }
        return true;
    }

    /// <summary>
    /// 감정 타입에 따른 플래그를 처리합니다.
    /// </summary>
    private void ProcessEmotionFlags(EmotionType emotion, ref float duration)
    {
        // 보물 감정 특별 처리
        if (IsTreasureEmotion(emotion))
        {
            isTreasureEmotionActive = true;
            duration = EmotionConstants.DURATION_PERSISTENT; // 무제한
        }
    }

    /// <summary>
    /// 감정 파티클을 생성하고 관리합니다.
    /// </summary>
    private void CreateEmotionParticle(EmotionType emotion, float duration)
    {
        if (EmotionManager.Instance == null)
            return;

        Transform targetTransform = emotionOrigin ?? transform;
        GameObject emotionObject = EmotionManager.Instance.ShowPetEmotion(petController, emotion, duration);

        if (emotionObject == null)
            return;

        activeParticle = emotionObject;
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
                // 단, 탈진(Exhausted) 상태일 때는 ExhaustedActivity에서 별도로 감정을 처리하므로 여기서는 무시
                if (!petController.State.IsExhausted)
                {
                    ShowEmotion(EmotionType.Scared, EmotionConstants.DURATION_SHORT);
                }
                break;
            // case PetStatus.Interacting:
            //     // 상호작용 시작 시 기쁨 이모티콘
            //     if (Random.value < 0.5f)
            //         ShowEmotion(EmotionType.Happy, EmotionConstants.DURATION_SHORT);
            //     break;
        }
    }

    /// <summary>
    /// 욕구 시스템에서 요청한 감정 표현
    /// </summary>
    public void OnEmotionRequired(EmotionType emotionType)
    {
        ShowEmotion(emotionType, EmotionConstants.DURATION_SHORT);
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
}