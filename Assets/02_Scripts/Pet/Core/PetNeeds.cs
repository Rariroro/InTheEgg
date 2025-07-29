using System;
using UnityEngine;

/// <summary>
/// 펫의 욕구(필요) 상태를 관리하는 클래스
/// PetController에서 분리하여 단일 책임 원칙을 준수
/// </summary>
[Serializable]
public class PetNeeds : MonoBehaviour
{
    // 욕구 타입 정의
    public enum NeedType
    {
        Hunger,
        Sleepiness,
        Affection
    }
    
    [Header("욕구 상태 (실시간 값)")]
    [Range(0, 100)]
    [SerializeField] private float hunger = 0f;       // 배고픔 (0-100)
    [Range(0, 100)]
    [SerializeField] private float sleepiness = 0f;   // 졸림 (0-100)
    [Range(0, 100)]
    [SerializeField] private float affection = 50f;    // 친밀도 (0-100)
    
    [Header("욕구 증가율")]
    [Tooltip("초당 배고픔 증가량 (기본값: 0.5)")]
    [SerializeField] private float hungerIncreaseRate = 0.5f;      // 초당 배고픔 증가량
    [Tooltip("초당 졸림 증가량 (기본값: 0.3)")]
    [SerializeField] private float sleepinessIncreaseRate = 0.3f;  // 초당 졸림 증가량
    
    // 프로퍼티로 외부 접근 허용
    public float HungerIncreaseRate
    {
        get => hungerIncreaseRate;
        set => hungerIncreaseRate = value;
    }
    public float SleepinessIncreaseRate
    {
        get => sleepinessIncreaseRate;
        set => sleepinessIncreaseRate = value;
    }
    
    [Header("친밀도 설정")]
    [SerializeField] private float affectionDecreaseRateWhenHungry = 0.2f;  // 배고플 때 초당 친밀도 감소량 (2f → 0.2f로 감소)
    [SerializeField] private float hungerThresholdForAffectionDecrease = 80f;  // 친밀도가 감소하기 시작하는 배고픔 임계값
    [SerializeField] private float lowAffectionThreshold = 30f;  // 낮은 친밀도 임계값
    
    // 프로퍼티로 외부 접근 허용
    public float AffectionDecreaseRateWhenHungry
    {
        get => affectionDecreaseRateWhenHungry;
        set => affectionDecreaseRateWhenHungry = value;
    }
    public float HungerThresholdForAffectionDecrease
    {
        get => hungerThresholdForAffectionDecrease;
        set => hungerThresholdForAffectionDecrease = value;
    }
    public float LowAffectionThreshold
    {
        get => lowAffectionThreshold;
        set => lowAffectionThreshold = value;
    }
    
    [Header("욕구 임계값")]
    [SerializeField] private float hungryThreshold = 70f;    // 배고픔 임계값
    [SerializeField] private float sleepyThreshold = 70f;    // 졸림 임계값
    
    // 이벤트
    public event Action<NeedType, float> OnNeedChanged;      // 욕구 변화 이벤트
    public event Action<NeedType> OnNeedCritical;           // 욕구가 임계값을 넘었을 때
    public event Action<EmotionType> OnEmotionRequired;     // 감정 표현이 필요할 때
    
    // 프로퍼티
    public float Hunger => hunger;
    public float Sleepiness => sleepiness;
    public float Affection => affection;
    
    public bool IsHungry => hunger >= hungryThreshold;
    public bool IsSleepy => sleepiness >= sleepyThreshold;
    public bool HasLowAffection => affection <= lowAffectionThreshold;
    
    // 졸림 감정 표현 타이머
    private float sleepyEmotionTimer = 0f;
    private const float SLEEPY_EMOTION_INTERVAL = 10f;
    private const float SLEEPY_EMOTION_CHANCE = 0.3f;
    
    private PetController petController;
    private bool isInitialized = false;
    
    /// <summary>
    /// PetNeeds 초기화
    /// </summary>
    public void Init(PetController controller)
    {
        petController = controller;
        
        // 기본값으로 초기화 (이제는 PetNeeds 자체에서 관리)
        
        isInitialized = true;
    }
    
    /// <summary>
    /// Unity Update - 매 프레임 욕구 자동 업데이트
    /// </summary>
    private void Update()
    {
        if (!isInitialized) return;
        
        UpdateHunger();
        UpdateSleepiness();
        UpdateAffection();
    }
    
    /// <summary>
    /// 배고픔 업데이트
    /// </summary>
    private void UpdateHunger()
    {
        // 펫이 음식을 먹고 있지 않을 때만 배고픔 증가
        if (petController == null || petController.feedingController == null || !petController.feedingController.IsEatingOrSeeking())
        {
            float previousHunger = hunger;
            hunger = Mathf.Clamp(hunger + hungerIncreaseRate * Time.deltaTime, 0f, 100f);
            
            if (hunger != previousHunger)
            {
                OnNeedChanged?.Invoke(NeedType.Hunger, hunger);
                
                // 배고픔 임계값을 처음 넘었을 때
                if (!IsHungry && hunger >= hungryThreshold)
                {
                    OnNeedCritical?.Invoke(NeedType.Hunger);
                }
            }
        }
    }
    
    /// <summary>
    /// 졸림 업데이트
    /// </summary>
    private void UpdateSleepiness()
    {
        // 펫이 자고 있거나 잠잘 곳을 찾아가는 중이 아닐 때만 졸림 증가
        if (petController == null || petController.sleepingController == null || !petController.sleepingController.IsSleepingOrSeeking())
        {
            float previousSleepiness = sleepiness;
            sleepiness = Mathf.Clamp(sleepiness + sleepinessIncreaseRate * Time.deltaTime, 0f, 100f);
            
            if (sleepiness != previousSleepiness)
            {
                OnNeedChanged?.Invoke(NeedType.Sleepiness, sleepiness);
                
                // 졸림 임계값을 처음 넘었을 때
                if (!IsSleepy && sleepiness >= sleepyThreshold)
                {
                    OnNeedCritical?.Invoke(NeedType.Sleepiness);
                }
            }
            
            // 졸릴 때 간헐적으로 감정 표현
            if (sleepiness >= sleepyThreshold)
            {
                sleepyEmotionTimer += Time.deltaTime;
                if (sleepyEmotionTimer >= SLEEPY_EMOTION_INTERVAL)
                {
                    if (UnityEngine.Random.value < SLEEPY_EMOTION_CHANCE)
                    {
                        OnEmotionRequired?.Invoke(EmotionType.Sleepy);
                    }
                    sleepyEmotionTimer = 0f;
                }
            }
        }
    }
    
    /// <summary>
    /// 친밀도 업데이트
    /// </summary>
    private void UpdateAffection()
    {
        // 배고픔에 따른 친밀도 감소
        if (hunger >= hungerThresholdForAffectionDecrease)
        {
            float affectionDecreaseRate = affectionDecreaseRateWhenHungry * (hunger / 100f);
            float previousAffection = affection;
            affection = Mathf.Clamp(affection - affectionDecreaseRate * Time.deltaTime, 0f, 100f);
            
            if (affection != previousAffection)
            {
                OnNeedChanged?.Invoke(NeedType.Affection, affection);
                
                // 친밀도가 낮은 임계값 이하로 떨어지고, 이전에는 그보다 높았다면
                if (affection <= lowAffectionThreshold && previousAffection > lowAffectionThreshold)
                {
                    OnEmotionRequired?.Invoke(EmotionType.Sad);
                    Debug.Log($"[PetNeeds] {petController.petName}의 친밀도가 낮아졌습니다: {affection:F1}");
                }
            }
        }
    }
    
    /// <summary>
    /// 배고픔 감소 (음식 섭취)
    /// </summary>
    public void ReduceHunger(float amount)
    {
        float previousHunger = hunger;
        hunger = Mathf.Clamp(hunger - amount, 0f, 100f);
        
        if (hunger != previousHunger)
        {
            OnNeedChanged?.Invoke(NeedType.Hunger, hunger);
            Debug.Log($"[PetNeeds] {petController.petName}의 배고픔 감소: {hunger:F1}");
        }
    }
    
    /// <summary>
    /// 졸림 리셋 (수면 완료)
    /// </summary>
    public void ResetSleepiness()
    {
        sleepiness = 0f;
        sleepyEmotionTimer = 0f;
        OnNeedChanged?.Invoke(NeedType.Sleepiness, sleepiness);
        Debug.Log($"[PetNeeds] {petController.petName}이(가) 충분히 잠을 잤습니다.");
    }
    
    /// <summary>
    /// 친밀도 증가
    /// </summary>
    public void IncreaseAffection(float amount)
    {
        float previousAffection = affection;
        affection = Mathf.Clamp(affection + amount, 0f, 100f);
        
        if (affection != previousAffection)
        {
            OnNeedChanged?.Invoke(NeedType.Affection, affection);
            
            // 친밀도가 크게 증가했을 때 기쁨 표현
            if (amount >= 10f)
            {
                OnEmotionRequired?.Invoke(EmotionType.Happy);
            }
            
            Debug.Log($"[PetNeeds] {petController.petName}의 친밀도 증가: {affection:F1} (+{amount})");
        }
    }
    
    /// <summary>
    /// 배고픔 값 직접 설정 (호환성용)
    /// </summary>
    public void SetHunger(float value)
    {
        hunger = Mathf.Clamp(value, 0f, 100f);
        OnNeedChanged?.Invoke(NeedType.Hunger, hunger);
    }
    
    /// <summary>
    /// 졸림 값 직접 설정 (호환성용)
    /// </summary>
    public void SetSleepiness(float value)
    {
        sleepiness = Mathf.Clamp(value, 0f, 100f);
        sleepyEmotionTimer = 0f;
        OnNeedChanged?.Invoke(NeedType.Sleepiness, sleepiness);
    }
    
    /// <summary>
    /// 친밀도 값 직접 설정 (호환성용)
    /// </summary>
    public void SetAffection(float value)
    {
        affection = Mathf.Clamp(value, 0f, 100f);
        OnNeedChanged?.Invoke(NeedType.Affection, affection);
    }
    
    /// <summary>
    /// 현재 욕구 상태를 디버그용 문자열로 반환
    /// </summary>
    public override string ToString()
    {
        return $"Hunger: {hunger:F1}, Sleepiness: {sleepiness:F1}, Affection: {affection:F1}";
    }
}