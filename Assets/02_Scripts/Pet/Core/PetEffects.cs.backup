using System.Collections;
using UnityEngine;

/// <summary>
/// 펫의 시각 효과와 감정 표현만을 담당하는 클래스
/// PetController에서 이펙트 관련 로직을 분리
/// </summary>
public class PetEffects : MonoBehaviour
{
    [Header("Effect Settings")]
    [SerializeField] private Transform emotionOrigin;
    [SerializeField] private float emotionHeight = 2f;
    [SerializeField] private float nameTextHeight = 2.5f;
    
    [Header("Emotion Display Settings")]
    [SerializeField] private float emotionDisplayDuration = 2f;
    [SerializeField] private float emotionCooldown = 3f;
    
    private PetController petController;
    private GameObject nameTextObject;
    private TextMesh nameTextMesh;
    private Coroutine currentEmotionCoroutine;
    private float lastEmotionTime = -10f;
    private bool isInitialized = false;
    
    // 이벤트
    public event System.Action<EmotionType> OnEmotionDisplayed;
    public event System.Action OnEmotionFinished;
    
    /// <summary>
    /// PetEffects 초기화
    /// </summary>
    public void Init(PetController controller)
    {
        petController = controller;
        
        // EmotionOrigin 찾기
        if (emotionOrigin == null)
        {
            Transform petModel = transform.Find("PetModel");
            if (petModel != null)
            {
                emotionOrigin = petModel.Find("EmotionOrigin");
            }
            
            if (emotionOrigin == null)
            {
                // EmotionOrigin이 없으면 기본 위치 사용
                GameObject emotionOriginGO = new GameObject("EmotionOrigin");
                emotionOriginGO.transform.SetParent(transform);
                emotionOriginGO.transform.localPosition = Vector3.up * emotionHeight;
                emotionOrigin = emotionOriginGO.transform;
            }
        }
        
        // 이름 텍스트 생성
        CreateNameText();
        
        isInitialized = true;
        Debug.Log($"[PetEffects] {petController.petName}: 이펙트 시스템 초기화 완료");
    }
    
    /// <summary>
    /// 감정 표시
    /// </summary>
    public void ShowEmotion(EmotionType emotionType, float duration = -1f)
    {
        if (!isInitialized || !CanShowEmotion())
            return;
            
        if (duration <= 0)
            duration = emotionDisplayDuration;
            
        // 기존 감정 표시 중지
        if (currentEmotionCoroutine != null)
        {
            StopCoroutine(currentEmotionCoroutine);
            currentEmotionCoroutine = null;
        }
        
        currentEmotionCoroutine = StartCoroutine(ShowEmotionCoroutine(emotionType, duration));
    }
    
    /// <summary>
    /// 특정 위치에 감정 표시
    /// </summary>
    public void ShowEmotionAt(EmotionType emotionType, Vector3 worldPosition, float duration = -1f)
    {
        if (!isInitialized || EmotionManager.Instance == null)
            return;
            
        if (duration <= 0)
            duration = emotionDisplayDuration;
            
        EmotionManager.Instance.ShowPetEmotion(petController, emotionType, duration);
        OnEmotionDisplayed?.Invoke(emotionType);
    }
    
    /// <summary>
    /// 이름 표시/숨기기
    /// </summary>
    public void ShowName(bool show)
    {
        if (nameTextObject != null)
        {
            nameTextObject.SetActive(show);
        }
    }
    
    /// <summary>
    /// 이름 텍스트 업데이트
    /// </summary>
    public void UpdateNameText(string newName)
    {
        if (nameTextMesh != null)
        {
            nameTextMesh.text = newName;
        }
    }
    
    /// <summary>
    /// 파티클 효과 재생
    /// </summary>
    public void PlayParticleEffect(string effectName, float duration = 3f)
    {
        if (!isInitialized)
            return;
            
        // 파티클 시스템 찾기 (하위 오브젝트에서)
        ParticleSystem[] particles = GetComponentsInChildren<ParticleSystem>();
        foreach (var particle in particles)
        {
            if (particle.gameObject.name.Contains(effectName))
            {
                particle.Play();
                if (duration > 0)
                {
                    StartCoroutine(StopParticleAfterDelay(particle, duration));
                }
                break;
            }
        }
    }
    
    /// <summary>
    /// 특수 효과 활성화/비활성화
    /// </summary>
    public void SetSpecialEffect(string effectName, bool active)
    {
        Transform effectTransform = transform.Find(effectName);
        if (effectTransform != null)
        {
            effectTransform.gameObject.SetActive(active);
        }
    }
    
    /// <summary>
    /// 발자국 효과
    /// </summary>
    public void CreateFootprint(Vector3 position, Quaternion rotation)
    {
        // 발자국 프리팹이 있다면 생성
        // 이 부분은 프로젝트에 따라 구현
    }
    
    /// <summary>
    /// 상태 표시 아이콘
    /// </summary>
    public void ShowStatusIcon(string iconName, float duration = 0f)
    {
        // 상태 아이콘 표시 (배고픔, 졸림 등)
        // UI 시스템과 연동하여 구현
    }
    
    // === Private 메서드들 ===
    
    private void CreateNameText()
    {
        if (nameTextObject == null)
        {
            nameTextObject = new GameObject("NameText");
            nameTextObject.transform.SetParent(transform);
            nameTextObject.transform.localPosition = Vector3.up * nameTextHeight;
            
            nameTextMesh = nameTextObject.AddComponent<TextMesh>();
            nameTextMesh.text = petController.petName;
            nameTextMesh.characterSize = 0.1f;
            nameTextMesh.fontSize = 50;
            nameTextMesh.alignment = TextAlignment.Center;
            nameTextMesh.anchor = TextAnchor.MiddleCenter;
            nameTextMesh.color = Color.white;
            
            // Billboard 효과 추가
            Billboard billboard = nameTextObject.AddComponent<Billboard>();
            
            // 기본적으로 숨김
            nameTextObject.SetActive(false);
        }
    }
    
    private bool CanShowEmotion()
    {
        // 감정 쿨다운 체크
        return Time.time - lastEmotionTime >= emotionCooldown;
    }
    
    private IEnumerator ShowEmotionCoroutine(EmotionType emotionType, float duration)
    {
        lastEmotionTime = Time.time;
        
        // EmotionManager를 통해 감정 표시
        if (EmotionManager.Instance != null)
        {
            Vector3 emotionPosition = emotionOrigin != null ? 
                emotionOrigin.position : 
                transform.position + Vector3.up * emotionHeight;
                
            EmotionManager.Instance.ShowPetEmotion(petController, emotionType, duration);
            OnEmotionDisplayed?.Invoke(emotionType);
        }
        
        yield return new WaitForSeconds(duration);
        
        OnEmotionFinished?.Invoke();
        currentEmotionCoroutine = null;
    }
    
    private IEnumerator StopParticleAfterDelay(ParticleSystem particle, float delay)
    {
        yield return new WaitForSeconds(delay);
        particle.Stop();
    }
    
    /// <summary>
    /// 디버그 정보 그리기
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        if (!isInitialized)
            return;
            
        // 감정 표시 위치
        if (emotionOrigin != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(emotionOrigin.position, 0.5f);
        }
        
        // 이름 표시 위치
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position + Vector3.up * nameTextHeight, 0.3f);
    }
    
    private void OnDestroy()
    {
        if (currentEmotionCoroutine != null)
        {
            StopCoroutine(currentEmotionCoroutine);
        }
    }
}