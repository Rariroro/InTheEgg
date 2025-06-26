// Pet/Actions/ExhaustedAction.cs
using UnityEngine;

/// <summary>
/// 펫이 배고픔으로 완전히 탈진했을 때의 행동을 정의합니다.
/// 이 상태에서는 다른 모든 행동을 중단하고 제자리에서 계속 쉬게 됩니다.
/// </summary>
public class ExhaustedAction : IPetAction
{
    private readonly PetController _pet;
    private readonly PetAnimationController _animController;
    private const int REST_ANIMATION_INDEX = 5; // 휴식 애니메이션 인덱스

    public ExhaustedAction(PetController pet)
    {
        _pet = pet;
        _animController = pet.GetComponent<PetAnimationController>();
    }

    public float GetPriority()
    {
        // 배고픔 수치가 100 이상이면, '모이기'나 다른 어떤 상호작용보다도 높은 최상위 우선순위를 반환합니다. (예: 50.0f)
        return (_pet.hunger >= 100f) ? 50.0f : 0f;
    }

    public void OnEnter()
    {
        _pet.isExhausted = true;

        Debug.LogWarning($"{_pet.petName}: 배고픔으로 탈진하여 모든 행동을 중단합니다.");

        // 1. 다른 행동으로 인해 발생할 수 있는 모든 움직임 관련 코루틴 중지
        _pet.StopAllCoroutines();
        _pet.GetComponent<PetMovementController>()?.ForceStopCurrentBehavior();
        _pet.GetComponent<PetFeedingController>()?.CancelFeeding();
        _pet.GetComponent<PetSleepingController>()?.InterruptSleep();
        _pet.GetComponent<PetTreeClimbingController>()?.ForceCancelClimbing();

        // 2. 물리적인 움직임 정지
        _pet.StopMovement();

        // 3. 탈진 상태(휴식) 애니메이션을 계속 재생하도록 설정
        _animController?.SetContinuousAnimation(REST_ANIMATION_INDEX);

        // 4. 탈진 상태임을 알리는 이모티콘 표시 (예: 어지러움)
        _pet.ShowEmotion(EmotionType.Hungry, 10f); // 10초간 표시
    }

    public void OnUpdate()
    {
        // 탈진 상태에서는 아무것도 하지 않습니다. OnEnter에서 설정된 상태를 계속 유지합니다.
        // 움직이지도, 회전하지도 않습니다.
    }

    public void OnExit()
    {        _pet.isExhausted = false;

        Debug.Log($"{_pet.petName}: 탈진 상태에서 회복됩니다.");

        // 이 행동이 끝난다는 것은 배고픔이 해결되었다는 의미입니다.
        // 1. 휴식 애니메이션을 중지합니다.
        _animController?.StopContinuousAnimation();

        // 2. 이모티콘을 숨깁니다.
        _pet.HideEmotion();

        // 3. 다시 움직일 수 있도록 허용합니다.
        _pet.ResumeMovement();
    }
}