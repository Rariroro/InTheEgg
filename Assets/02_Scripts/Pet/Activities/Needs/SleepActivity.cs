using UnityEngine;

/// <summary>
/// 펫의 수면 활동을 담당하는 클래스
/// </summary>
public class SleepActivity : PetActivityAdapter
{
    private readonly PetSleepingController sleepingController;
    private bool isPreparingToSleep;
    
    // 잠잘 곳을 찾지 못했을 때의 광역 배회 로직을 위한 변수
    private float searchTimer = 0f;
    private const float WIDE_WANDER_INTERVAL = 5.0f; // 5초마다 새로운 목적지 탐색
    private const float SLEEP_SEARCH_RADIUS = 150f;  // 잠잘 곳 탐색 시 배회 반경
    
    public override string Name => "Sleep";
    public override bool IsInterruptible => true; // 수면은 중단 가능
    
    public SleepActivity(PetController petController, PetSleepingController sleeping) : base(petController)
    {
        sleepingController = sleeping;
    }
    
    public override bool CanStart(PetState state, PetNeeds needs)
    {
        // 기본 상태 체크
        if (state.CurrentStatus != PetStatus.Idle)
            return false;
            
        // 터치/홀드 상태에서는 잠자기 중단
        if (pet.State.IsHolding || pet.State.IsSelected)
            return false;
            
        // 나무 위에 있어도 잠들 수 있음
        
        // 이미 잠을 자거나 잠잘 곳을 찾는 중이라면 계속
        if (sleepingController.IsSleepingOrSeeking())
            return true;
            
        // 졸림 확인 (70 이상)
        return needs != null ? needs.IsSleepy : pet.Needs.Sleepiness >= 70f;
    }
    
    public override float GetPriority(PetState state, PetNeeds needs)
    {
        if (!CanStart(state, needs))
            return 0f;

        // 이미 잠을 자거나 잠잘 곳을 찾는 중이라면 매우 높은 우선순위 (중단 방지)
        if (sleepingController.IsSleepingOrSeeking())
            return 35.0f; // 진행 중 보호 (30→35)

        // 졸림 수치에 비례하여 우선순위 증가
        float sleepiness = needs != null ? needs.Sleepiness : pet.Needs.Sleepiness;

        // 생존 본능을 반영한 우선순위
        if (sleepiness >= 85f)
        {
            // 매우 졸림 - 긴급 수준 (선형 증가)
            return 20.0f + ((sleepiness - 85f) / 15f * 10.0f); // 20.0 ~ 30.0
        }
        else if (sleepiness >= 70f)
        {
            // 졸림 시작 - 중간 수준 (선형 증가)
            return 10.0f + ((sleepiness - 70f) / 15f * 8.0f); // 10.0 ~ 18.0
        }

        return 0f;
    }
    
    
    public override void Start()
    {
        // Debug.Log($"[SleepActivity] {pet.petName}: 수면 활동 시작 (졸림: {pet.Needs.Sleepiness:F1})");

        // 졸림 감정 즉시 표시
        pet.ShowEmotion(EmotionType.Sleepy, 999f);

        isPreparingToSleep = sleepingController.TryStartSleepingSequence();
        searchTimer = 0f;
    }
    
    public override void Update()
    {
        if (isPreparingToSleep)
        {
            sleepingController.UpdateMovementToSleep();
        }
        else
        {
            // 잠잘 곳을 찾지 못했을 때의 로직
            searchTimer += Time.deltaTime;
            if (searchTimer >= WIDE_WANDER_INTERVAL)
            {
                // Debug.Log($"[SleepActivity] {pet.petName}: 잠잘 곳을 찾기 위해 주변을 넓게 탐색합니다.");
                searchTimer = 0f;
                
                // PetMovementController를 통해 더 넓은 반경으로 목적지를 설정
                pet.GetComponent<PetMovementController>()?.SetRandomDestination(SLEEP_SEARCH_RADIUS);

                // 감정은 이미 Start()에서 표시했으므로 중복 표시 제거
                // (기존 코드 제거)

                // 다시 잠잘 곳 탐색을 시도
                isPreparingToSleep = sleepingController.TryStartSleepingSequence();
            }
            
            if (pet.movementController != null)
            {
                pet.movementController.HandleRotation();
            }
        }
    }
    
    public override void Stop()
    {
        // Debug.Log($"[SleepActivity] {pet.petName}: 수면 활동 종료");

        // 졸림 감정 제거
        pet.HideEmotion();

        sleepingController.InterruptSleep();
    }
}