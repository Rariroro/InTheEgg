// 애니메이션 전담: 이동 상태에 따른 애니메이션 전환 및 특별 애니메이션 처리
using System.Collections;
using UnityEngine;

public class PetAnimationController : MonoBehaviour
{
    private PetController petController;
    private Vector3 lastPosition;
    private Vector3 smoothVelocity;
    private bool isSpecialAnimationPlaying = false;

    public void Init(PetController controller)
    {
        petController = controller;
        if (petController.petModelTransform != null)
            lastPosition = petController.petModelTransform.position;
    }

    // PetAnimationController.cs의 수정사항

    // 1. 애니메이션 상태를 추적하는 변수 추가
    private bool isContinuousAnimationPlaying = false;
    private int continuousAnimationIndex = -1;

    // 2. UpdateAnimation 메서드 수정
    // UpdateAnimation 메서드 수정
    // PetAnimationController.cs

    public void UpdateAnimation()
    { 
         // ★★★ 추가: 물 깊이에 따른 모델의 Y좌표 오프셋을 처리하는 로직을 이곳으로 이전합니다. ★★★
        if (petController.petModelTransform != null)
        {
            Vector3 targetLocalPos = new Vector3(0, petController.waterDepthOffset, 0);
            petController.petModelTransform.localPosition = Vector3.Lerp(
                petController.petModelTransform.localPosition, 
                targetLocalPos, 
                Time.deltaTime * 5f // 부드러운 전환을 위한 Lerp
            );
        }
        // ★ 추가: 선택되었거나 들고 있는 상태에서는 애니메이션 업데이트 스킵
        if (petController.isSelected || petController.isHolding)
        {
            return;
        }
        // 애니메이션 속도 조정은 유지합니다.
        // 펫의 현재 최대 속도(agent.speed)를 기본 속도(baseSpeed)로 나누어 애니메이션 배속을 조절합니다.
        // 이를 통해 '모이기' 등 특수 상황에서 빨라진 속도에 맞춰 애니메이션도 빠르게 재생됩니다.
        if (petController.animator != null && petController.agent != null && petController.baseSpeed > 0)
        {
            petController.animator.speed = petController.agent.speed / petController.baseSpeed;
        }

        // 특별 애니메이션(점프, 먹기 등)이나 행동 기반 애니메이션(휴식 등)이 재생 중일 때는
        // 아래의 기본 이동(Locomotion) 애니메이션 로직을 실행하지 않습니다.
        if (isSpecialAnimationPlaying || isContinuousAnimationPlaying)
            return;

        // NavMeshAgent의 실제 속도를 기준으로 애니메이션을 결정합니다.
        if (petController.agent != null && petController.agent.enabled && petController.agent.isOnNavMesh && petController.animator != null)
        {
            // 에이전트의 현재 속력을 가져옵니다.
            float agentVelocity = petController.agent.velocity.magnitude;

            // 에이전트가 실제로 움직이고 있는지 확인합니다. (미세한 움직임은 '정지'로 간주)
            if (agentVelocity > 0.1f)
            {
                // 에이전트에 설정된 speed 값에 따라 '걷기'와 '뛰기'를 구분합니다.
                // PetMovementController의 Running 상태에서 speed를 1.5배로 설정한 것을 기반으로 합니다.
                if (petController.agent.speed > petController.baseSpeed * 1.3f)
                {
                    petController.animator.SetInteger("animation", 2); // 뛰기(Run) 애니메이션
                }
                else
                {
                    petController.animator.SetInteger("animation", 1); // 걷기(Walk) 애니메이션
                }
            }
            else
            {
                // 움직이지 않을 때는 '정지' 애니메이션을 재생합니다.
                petController.animator.SetInteger("animation", 0); // 정지(Idle) 애니메이션
            }
        }
        else if (petController.animator != null)
        {
            // NavMeshAgent가 없거나 비활성화된 경우에도 '정지' 상태로 처리합니다.
            petController.animator.SetInteger("animation", 0); // 정지(Idle) 애니메이션
        }
    }

    // 3. 연속적인 애니메이션 설정 메서드 수정
    public void SetContinuousAnimation(int animationNumber)
    {
        if (petController.animator != null)
        {
            petController.animator.SetInteger("animation", animationNumber);
            isContinuousAnimationPlaying = true;
            continuousAnimationIndex = animationNumber;
        }
    }

    // 4. 연속적인 애니메이션 종료 메서드 수정
    public void StopContinuousAnimation()
    {
        if (petController.animator != null)
        {
            petController.animator.SetInteger("animation", 0);
            isContinuousAnimationPlaying = false;
            continuousAnimationIndex = -1;
        }
    }
    // 애니메이션을 강제로 중단하는 메서드 추가
    public void ForceStopAllAnimations()
    {
        isSpecialAnimationPlaying = false;
        isContinuousAnimationPlaying = false;
        continuousAnimationIndex = -1;

        if (petController.animator != null)
        {
            petController.animator.SetInteger("animation", 0);
            petController.animator.speed = 1.0f;
        }
    }
    // 5. PlayAnimationWithCustomDuration 메서드 수정
    public IEnumerator PlayAnimationWithCustomDuration(int animationNumber, float duration, bool returnToIdle = true, bool resumeMovementAfter = true)
    {
        isSpecialAnimationPlaying = true;

        if (petController.animator != null)
        {
            // 현재 애니메이션 상태 저장
            int previousAnimation = petController.animator.GetInteger("animation");

            // 새 애니메이션 설정
            petController.animator.SetInteger("animation", animationNumber);

            // 지정된 시간만큼 대기
            yield return new WaitForSeconds(duration);

            // 기본 애니메이션으로 돌아갈지 여부
            if (returnToIdle)
            {
                petController.animator.SetInteger("animation", 0);
            }
            else
            {
                // 연속 애니메이션으로 전환
                isContinuousAnimationPlaying = true;
                continuousAnimationIndex = animationNumber;
            }
        }
        else
        {
            yield return new WaitForSeconds(duration);
        }

        isSpecialAnimationPlaying = false;

        // 이동을 재개할지 여부
        if (resumeMovementAfter && petController.agent != null &&
            petController.agent.enabled && petController.agent.isOnNavMesh)
        {
            petController.ResumeMovement();
        }
    }


    // PetAnimationController.cs 파일의 PlaySpecialAnimation 메서드를 아래 코드로 교체하세요.

   // PetAnimationController.cs

public IEnumerator PlaySpecialAnimation(int animationNumber, bool isBlocking = true)
{
    // 1. 다른 상호작용을 막기 위해 잠금을 설정합니다.
    petController.isAnimationLocked = true;
    isSpecialAnimationPlaying = true;

    try
    {
        if (petController.animator != null)
        {
            petController.animator.SetInteger("animation", animationNumber);

            // ★ 중요: 애니메이터가 새로운 상태로 전환될 시간을 주기 위해 한 프레임을 기다립니다.
            // 이렇게 해야 다음 줄에서 정확한 애니메이션 길이를 가져올 수 있습니다.
            yield return null; 

            // ★ 중요: isBlocking 값과 상관없이, 항상 애니메이션의 실제 길이를 가져옵니다.
            float animationLength = petController.animator.GetCurrentAnimatorStateInfo(0).length;

            // ★ 애니메이션 길이만큼 대기하여 재생 시간을 보장합니다.
            yield return new WaitForSeconds(animationLength);
        }
        else
        {
            // 애니메이터가 없을 경우, 2초간 대기 (기존과 동일)
            yield return new WaitForSeconds(2f);
        }
    }
    finally
    {
        // 2. 애니메이션이 모두 끝나면 잠금을 해제합니다.
        isSpecialAnimationPlaying = false;
        petController.isAnimationLocked = false;

        // 3. 펫의 상태를 기본(Idle)으로 되돌립니다.
        if (petController.animator != null)
        {
            petController.animator.SetInteger("animation", 0);
        }

        // 4. 펫의 움직임을 다시 시작합니다.
        if (petController.agent != null && petController.agent.enabled && petController.agent.isOnNavMesh)
        {
            petController.ResumeMovement();
        }
    }
}

}