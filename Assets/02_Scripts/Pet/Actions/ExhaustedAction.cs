// Pet/Actions/ExhaustedAction.cs
using UnityEngine;

/// <summary>
/// 펫이 배고픔으로 완전히 탈진했을 때의 행동을 정의합니다.
/// ★★★ 수정: 이제 이 상태는 주변의 음식을 적극적으로 탐색하고, 발견 시 먹으려고 시도합니다. ★★★
/// </summary>
public class ExhaustedAction : IPetAction
{
    private readonly PetController _pet;
    private readonly PetAnimationController _animController;
    private readonly PetFeedingController _feedingController; // ★★★ 추가: 식사 컨트롤러 참조

    private float _searchTimer = 0f; // ★★★ 추가: 주기적인 음식 탐색을 위한 타이머
    private const float FOOD_SEARCH_INTERVAL = 1.0f; // ★★★ 추가: 1초마다 주변 음식 다시 탐색
    private bool _isTryingToEat = false; // ★★★ 추가: 음식을 발견하고 먹으러 가는 중인지 여부

    private const int REST_ANIMATION_INDEX = 5; // 휴식 애니메이션 인덱스

    public ExhaustedAction(PetController pet)
    {
        _pet = pet;
        _animController = pet.GetComponent<PetAnimationController>();
        _feedingController = pet.GetComponent<PetFeedingController>(); // ★★★ 추가
    }

    public float GetPriority()
    {
        // ★★★ 수정: 플레이어가 직접 선택했을 때는 우선순위를 낮춰 상호작용이 가능하게 합니다. ★★★
        if (_pet.isSelected)
        {
            return 0f;
        }
        // 배고픔 수치가 100 이상이면 최상위 우선순위를 가집니다.
        return (_pet.hunger >= 100f) ? 50.0f : 0f;
    }

    public void OnEnter()
    {
        _pet.isExhausted = true;
        _isTryingToEat = false;
        _searchTimer = 0f;

        // Debug.LogWarning($"{_pet.petName}: 배고픔으로 탈진. 주변의 음식을 찾습니다.");

        // 기존의 모든 행동 중지는 유지하되, 바로 음식 탐색을 시작합니다.
        _pet.StopAllCoroutines();
        _pet.GetComponent<PetMovementController>()?.ForceStopCurrentBehavior();
        _pet.GetComponent<PetSleepingController>()?.InterruptSleep();
        _pet.GetComponent<PetTreeClimbingController>()?.ForceCancelClimbing();
        _pet.StopMovement();

        // ★★★ 핵심 수정: 즉시 음식 탐색 시도 ★★★
        AttemptToEat();
        
        // 음식을 찾지 못했다면, 일단 탈진 상태를 표시합니다.
        if (!_isTryingToEat)
        {
            _animController?.SetContinuousAnimation(REST_ANIMATION_INDEX);
            _pet.ShowEmotion(EmotionType.Hungry, 2f);
        }
    }

    public void OnUpdate()
    {
        // ★★★ 핵심 수정: OnUpdate 로직 추가 ★★★

        if (_isTryingToEat)
        {
            // 음식을 찾아 이동 중이거나 먹는 중인 경우, PetFeedingController에 업데이트를 위임합니다.
            _feedingController?.UpdateMovementToFood();
        }
        else
        {
            // 아직 음식을 찾지 못했다면, 주기적으로 다시 탐색합니다.
            _searchTimer += Time.deltaTime;
            if (_searchTimer >= FOOD_SEARCH_INTERVAL)
            {
                _searchTimer = 0f;
                AttemptToEat();
            }
        }
    }
    
    /// <summary>
    /// ★★★ 새로 추가된 헬퍼 메서드 ★★★
    /// PetFeedingController를 통해 음식 탐색을 시도하고 결과를 내부 상태에 반영합니다.
    /// </summary>
    private void AttemptToEat()
    {
        // isEatingOrSeeking 상태를 먼저 초기화 해줘야 새로운 탐색이 가능합니다.
        _feedingController?.CancelFeeding(); 
        
        if (_feedingController != null && _feedingController.TryStartFeedingSequence())
        {
            _isTryingToEat = true;
            _animController?.StopContinuousAnimation(); // 휴식 애니메이션 중지
            _pet.HideEmotion(); // 배고픔 이모티콘 숨기기
            // Debug.Log($"{_pet.petName}: 탈진 상태에서 음식을 발견하여 이동을 시작합니다.");
        }
        else
        {
             _isTryingToEat = false;
        }
    }


    public void OnExit()
    {
        _pet.isExhausted = false;
        _isTryingToEat = false;

        // Debug.Log($"{_pet.petName}: 탈진 상태에서 회복됩니다.");

        // 식사 시도를 하고 있었다면 완전히 취소하여 상태를 정리합니다.
        _feedingController?.CancelFeeding();

        // 애니메이션과 이모티콘을 정리합니다.
        _animController?.StopContinuousAnimation();
        _pet.HideEmotion();

        // 다시 움직일 수 있도록 허용합니다.
        _pet.ResumeMovement();
    }
}