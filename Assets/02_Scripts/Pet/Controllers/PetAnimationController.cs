using System.Collections;
using UnityEngine;

public class PetAnimationController : MonoBehaviour
{
    // ▼▼▼ 사용자께서 확정해주신 최종 애니메이션 열거형입니다 ▼▼▼
    public enum PetAnimationType
    {
        Idle = 0,       // 기본 서 있는 상태
        Walk = 1,       // 걷기
        Run = 2,        // 달리기
        Jump = 3,       // 점프, 기지개, 승리 포즈 등
        Eat = 4,        // 앉기, 먹기, 땅파기, 자세 바꾸기 등
        Rest = 5,       // 쉬기, 잠자기, 웅크리기 등
        Attack = 6,     // 공격, 장난치기, 침 뱉기, 박치기 등
        Damage = 7,     // 피격, 아파하기 등
        Die = 8         // 죽은 척, 특별한 행동 등
    }

    private PetController petController;
    private bool isSpecialAnimationPlaying = false;
    private bool isContinuousAnimationPlaying = false;
    private PetAnimationType continuousAnimationType = PetAnimationType.Idle;

    public void Init(PetController controller)
    {
        petController = controller;
    }

    public void UpdateAnimation()
    {
        if (petController.petModelTransform != null)
        {
            Vector3 targetLocalPos = new Vector3(0, petController.waterDepthOffset, 0);
            petController.petModelTransform.localPosition = Vector3.Lerp(
                petController.petModelTransform.localPosition,
                targetLocalPos,
                Time.deltaTime * 5f
            );
        }

        if (petController.isSelected || petController.isHolding)
        {
            return;
        }

        if (petController.animator != null && petController.agent != null && petController.baseSpeed > 0)
        {
            petController.animator.speed = petController.agent.speed / petController.baseSpeed;
        }

        if (isSpecialAnimationPlaying || isContinuousAnimationPlaying)
            return;

        if (petController.agent != null && petController.agent.enabled && petController.agent.isOnNavMesh && petController.animator != null)
        {
            float agentVelocity = petController.agent.velocity.magnitude;

            if (agentVelocity > 0.1f)
            {
                if (petController.agent.speed > petController.baseSpeed * 1.3f)
                {
                    petController.animator.SetInteger("animation", (int)PetAnimationType.Run);
                }
                else
                {
                    petController.animator.SetInteger("animation", (int)PetAnimationType.Walk);
                }
            }
            else
            {
                petController.animator.SetInteger("animation", (int)PetAnimationType.Idle);
            }
        }
        else if (petController.animator != null)
        {
            petController.animator.SetInteger("animation", (int)PetAnimationType.Idle);
        }
    }

    public void SetContinuousAnimation(PetAnimationType animationType)
    {
        if (petController.animator != null)
        {
            petController.animator.SetInteger("animation", (int)animationType);
            isContinuousAnimationPlaying = true;
            continuousAnimationType = animationType;
        }
    }

    public void StopContinuousAnimation()
    {
        if (petController.animator != null)
        {
            petController.animator.SetInteger("animation", (int)PetAnimationType.Idle);
            isContinuousAnimationPlaying = false;
            continuousAnimationType = PetAnimationType.Idle;
        }
    }

    public void ForceStopAllAnimations()
    {
        isSpecialAnimationPlaying = false;
        isContinuousAnimationPlaying = false;
        continuousAnimationType = PetAnimationType.Idle;

        if (petController.animator != null)
        {
            petController.animator.SetInteger("animation", (int)PetAnimationType.Idle);
            petController.animator.speed = 1.0f;
        }
    }

    public IEnumerator PlayAnimationWithCustomDuration(PetAnimationType animationType, float duration, bool returnToIdle = true, bool resumeMovementAfter = true)
    {
        isSpecialAnimationPlaying = true;

        if (petController.animator != null)
        {
            petController.animator.SetInteger("animation", (int)animationType);
            yield return new WaitForSeconds(duration);

            if (returnToIdle)
            {
                petController.animator.SetInteger("animation", (int)PetAnimationType.Idle);
            }
            else
            {
                isContinuousAnimationPlaying = true;
                continuousAnimationType = animationType;
            }
        }
        else
        {
            yield return new WaitForSeconds(duration);
        }

        isSpecialAnimationPlaying = false;

        if (resumeMovementAfter && petController.agent != null &&
            petController.agent.enabled && petController.agent.isOnNavMesh)
        {
            petController.ResumeMovement();
        }
    }

    public IEnumerator PlaySpecialAnimation(PetAnimationType animationType, bool isBlocking = true)
    {
        petController.isAnimationLocked = true;
        isSpecialAnimationPlaying = true;

        try
        {
            if (petController.animator != null)
            {
                petController.animator.SetInteger("animation", (int)animationType);
                yield return null;
                float animationLength = petController.animator.GetCurrentAnimatorStateInfo(0).length;
                yield return new WaitForSeconds(animationLength);
            }
            else
            {
                yield return new WaitForSeconds(2f);
            }
        }
        finally
        {
            isSpecialAnimationPlaying = false;
            petController.isAnimationLocked = false;

            if (petController.animator != null)
            {
                petController.animator.SetInteger("animation", (int)PetAnimationType.Idle);
            }

            if (petController.agent != null && petController.agent.enabled && petController.agent.isOnNavMesh)
            {
                petController.ResumeMovement();
            }
        }
    }
}