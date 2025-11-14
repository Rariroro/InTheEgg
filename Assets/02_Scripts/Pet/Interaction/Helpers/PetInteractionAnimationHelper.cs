using UnityEngine;
using System.Collections;

/// <summary>
/// 펫 상호작용에서 애니메이션 관련 기능을 제공하는 헬퍼 클래스
/// BasePetInteraction에서 분리하여 코드 재사용성을 높입니다
/// </summary>
public static class PetInteractionAnimationHelper
{
    /// <summary>
    /// 두 펫이 서로 마주보게 합니다 (즉시)
    /// </summary>
    public static void LookAtEachOther(PetController pet1, PetController pet2)
    {
        if (pet1 == null || pet2 == null) return;

        // 첫 번째 펫이 두 번째 펫을 바라보도록
        Vector3 direction1 = pet2.transform.position - pet1.transform.position;
        direction1.y = 0; // 높이 무시
        if (direction1.sqrMagnitude > 0.01f)
        {
            pet1.transform.rotation = Quaternion.LookRotation(direction1);
            if (pet1.petModelTransform != null)
            {
                pet1.petModelTransform.rotation = pet1.transform.rotation;
            }
        }

        // 두 번째 펫이 첫 번째 펫을 바라보도록
        Vector3 direction2 = pet1.transform.position - pet2.transform.position;
        direction2.y = 0; // 높이 무시
        if (direction2.sqrMagnitude > 0.01f)
        {
            pet2.transform.rotation = Quaternion.LookRotation(direction2);
            if (pet2.petModelTransform != null)
            {
                pet2.petModelTransform.rotation = pet2.transform.rotation;
            }
        }
    }

    /// <summary>
    /// 한 펫이 다른 펫을 바라보게 합니다
    /// </summary>
    public static void LookAtOther(PetController looker, PetController target)
    {
        if (looker == null || target == null) return;

        Vector3 direction = target.transform.position - looker.transform.position;
        direction.y = 0; // 높이 무시

        if (direction.sqrMagnitude > 0.01f)
        {
            looker.transform.rotation = Quaternion.LookRotation(direction);
            if (looker.petModelTransform != null)
            {
                looker.petModelTransform.rotation = looker.transform.rotation;
            }
        }
    }

    /// <summary>
    /// 지정된 시간 동안 두 펫이 서로를 부드럽게 바라보도록 회전시킵니다
    /// </summary>
    public static IEnumerator SmoothlyLookAtEachOther(PetController pet1, PetController pet2, float duration = 0.5f)
    {
        if (pet1 == null || pet2 == null) yield break;

        // 목표 회전값 계산
        Vector3 direction1 = pet2.transform.position - pet1.transform.position;
        direction1.y = 0;
        Vector3 direction2 = pet1.transform.position - pet2.transform.position;
        direction2.y = 0;

        Quaternion pet1TargetRotation = direction1.sqrMagnitude > 0.01f
            ? Quaternion.LookRotation(direction1)
            : pet1.transform.rotation;

        Quaternion pet2TargetRotation = direction2.sqrMagnitude > 0.01f
            ? Quaternion.LookRotation(direction2)
            : pet2.transform.rotation;

        // 현재 회전값 저장
        Quaternion pet1StartRotation = pet1.transform.rotation;
        Quaternion pet2StartRotation = pet2.transform.rotation;

        // 회전 각도가 충분히 큰 경우에만 걷기 애니메이션 재생
        float pet1Angle = Quaternion.Angle(pet1StartRotation, pet1TargetRotation);
        float pet2Angle = Quaternion.Angle(pet2StartRotation, pet2TargetRotation);

        // 회전 각도가 30도 이상인 경우 걷기 애니메이션 시작
        bool pet1NeedsWalkAnim = pet1Angle > 30f;
        bool pet2NeedsWalkAnim = pet2Angle > 30f;

        var pet1AnimController = pet1.GetComponent<PetAnimationController>();
        var pet2AnimController = pet2.GetComponent<PetAnimationController>();

        if (pet1NeedsWalkAnim && pet1AnimController != null)
        {
            pet1AnimController.SetContinuousAnimation(PetAnimationController.PetAnimationType.Walk);
        }
        if (pet2NeedsWalkAnim && pet2AnimController != null)
        {
            pet2AnimController.SetContinuousAnimation(PetAnimationController.PetAnimationType.Walk);
        }

        // 부드러운 회전 수행
        float elapsedTime = 0f;
        while (elapsedTime < duration)
        {
            float t = elapsedTime / duration;

            // Slerp를 사용하여 부드럽게 회전
            pet1.transform.rotation = Quaternion.Slerp(pet1StartRotation, pet1TargetRotation, t);
            pet2.transform.rotation = Quaternion.Slerp(pet2StartRotation, pet2TargetRotation, t);

            // 모델도 함께 회전
            if (pet1.petModelTransform != null)
                pet1.petModelTransform.rotation = pet1.transform.rotation;
            if (pet2.petModelTransform != null)
                pet2.petModelTransform.rotation = pet2.transform.rotation;

            elapsedTime += Time.deltaTime;
            yield return null;
        }

        // 최종 회전값으로 정확하게 설정
        pet1.transform.rotation = pet1TargetRotation;
        pet2.transform.rotation = pet2TargetRotation;

        if (pet1.petModelTransform != null)
            pet1.petModelTransform.rotation = pet1.transform.rotation;
        if (pet2.petModelTransform != null)
            pet2.petModelTransform.rotation = pet2.transform.rotation;

        // 회전이 끝난 후 걷기 애니메이션 중지
        if (pet1NeedsWalkAnim && pet1AnimController != null)
        {
            pet1AnimController.StopContinuousAnimation();
        }
        if (pet2NeedsWalkAnim && pet2AnimController != null)
        {
            pet2AnimController.StopContinuousAnimation();
        }
    }

    /// <summary>
    /// 동시에 특별 애니메이션을 재생합니다
    /// </summary>
    public static IEnumerator PlaySimultaneousAnimations(
        PetController pet1, PetController pet2,
        PetAnimationController.PetAnimationType anim1Type,
        PetAnimationController.PetAnimationType anim2Type,
        float duration = 2.0f)
    {
        // 두 펫의 애니메이션 컨트롤러
        PetAnimationController anim1Controller = pet1.GetComponent<PetAnimationController>();
        PetAnimationController anim2Controller = pet2.GetComponent<PetAnimationController>();

        if (anim1Controller == null || anim2Controller == null)
        {
            Debug.LogWarning("애니메이션 컨트롤러를 찾을 수 없습니다.");
            yield break;
        }

        // 동시에 애니메이션 시작
        Coroutine anim1Coroutine = pet1.StartCoroutine(
            anim1Controller.PlayAnimationWithCustomDuration(anim1Type, duration, false, false));

        yield return pet2.StartCoroutine(
            anim2Controller.PlayAnimationWithCustomDuration(anim2Type, duration, false, false));
    }

    /// <summary>
    /// 승자와 패자 애니메이션을 재생합니다
    /// </summary>
    public static IEnumerator PlayWinnerLoserAnimations(
        PetController winner, PetController loser,
        string interactionName,
        PetAnimationController.PetAnimationType winnerAnimType = PetAnimationController.PetAnimationType.Jump,
        PetAnimationController.PetAnimationType loserAnimType = PetAnimationController.PetAnimationType.Eat)
    {
        Debug.Log($"[{interactionName}] 결과: {winner.petName}이(가) 승리!");

        PetAnimationController winnerAnimController = winner.GetComponent<PetAnimationController>();
        PetAnimationController loserAnimController = loser.GetComponent<PetAnimationController>();

        if (winnerAnimController != null)
        {
            winner.StartCoroutine(
                winnerAnimController.PlayAnimationWithCustomDuration(winnerAnimType, 2.0f, false, false));
        }

        if (loserAnimController != null)
        {
            yield return loser.StartCoroutine(
                loserAnimController.PlayAnimationWithCustomDuration(loserAnimType, 2.0f, false, false));
        }
    }

    /// <summary>
    /// 펫에게 특정 애니메이션을 재생합니다
    /// </summary>
    public static IEnumerator PlayAnimation(
        PetController pet,
        PetAnimationController.PetAnimationType animType,
        float duration = 2.0f,
        bool loop = false)
    {
        PetAnimationController animController = pet.GetComponent<PetAnimationController>();

        if (animController == null)
        {
            Debug.LogWarning($"{pet.petName}의 애니메이션 컨트롤러를 찾을 수 없습니다.");
            yield break;
        }

        yield return animController.PlayAnimationWithCustomDuration(animType, duration, loop, false);
    }

    /// <summary>
    /// 연속 애니메이션을 설정합니다
    /// </summary>
    public static void SetContinuousAnimation(PetController pet, PetAnimationController.PetAnimationType animType)
    {
        PetAnimationController animController = pet.GetComponent<PetAnimationController>();

        if (animController != null)
        {
            animController.SetContinuousAnimation(animType);
        }
    }

    /// <summary>
    /// 연속 애니메이션을 중지합니다
    /// </summary>
    public static void StopContinuousAnimation(PetController pet)
    {
        PetAnimationController animController = pet.GetComponent<PetAnimationController>();

        if (animController != null)
        {
            animController.StopContinuousAnimation();
        }
    }

    /// <summary>
    /// 애니메이션 상태를 완전히 초기화합니다
    /// </summary>
    public static void ResetAnimationState(PetController pet)
    {
        PetAnimationController animController = pet.GetComponent<PetAnimationController>();

        if (animController != null)
        {
            animController.StopAllCoroutines();
            animController.StopContinuousAnimation();
            // Idle 상태로 명시적 전환
            animController.SetContinuousAnimation(PetAnimationController.PetAnimationType.Idle);
        }
    }
}