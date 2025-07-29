using System.Collections;
using UnityEngine;

public class PetAnimationController : PetControllerBase
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

    private bool isSpecialAnimationPlaying = false;
    private bool isContinuousAnimationPlaying = false;
    private PetAnimationType continuousAnimationType = PetAnimationType.Idle;

    protected override void OnInitialize()
    {
        // 추가 초기화 로직 필요시 여기에 작성
    }

    public void UpdateAnimation()
    {
        if (petController.petModelTransform != null)
        {
            Vector3 targetLocalPos = new Vector3(0, petController.State.WaterDepthOffset, 0);
            petController.petModelTransform.localPosition = Vector3.Lerp(
                petController.petModelTransform.localPosition,
                targetLocalPos,
                Time.deltaTime * 5f
            );
        }

        if (petController.State.IsSelected || petController.State.IsHolding || petController.State.IsInteracting || petController.State.IsActionLocked)
        {
            return;
        }
        // 애니메이션 속도 동기화
        SyncAnimationSpeed();

        // 특수 애니메이션이나 연속 애니메이션이 재생 중이면 자동 애니메이션 업데이트 건너뛰기
        // 나무를 찾아 이동 중일 때도 자동 업데이트 건너뛰기
        var treeClimbingController = petController.GetComponent<PetTreeClimbingController>();
        if (isSpecialAnimationPlaying || isContinuousAnimationPlaying || 
            (treeClimbingController != null && treeClimbingController.IsSearchingForTree()))
            return;

        // 이동 상태에 따른 자동 애니메이션 설정
        UpdateMovementAnimation();
    }
    
    /// <summary>
    /// 애니메이션 속도를 이동 속도와 동기화
    /// </summary>
    private void SyncAnimationSpeed()
    {
        if (petController.animator != null && petController.agent != null)
        {
            var currentAnimation = (PetAnimationType)petController.animator.GetInteger("animation");

            // 걷기 또는 달리기 상태일 때만 이동 속도에 애니메이션 속도를 동기화
            if (currentAnimation == PetAnimationType.Walk || currentAnimation == PetAnimationType.Run)
            {
                if (petController.Movement.walkSpeed > 0)
                {
                    petController.animator.speed = petController.agent.velocity.magnitude / petController.Movement.walkSpeed;
                }
                else
                {
                    petController.animator.speed = 1f;
                }
            }
            else
            {
                // 다른 모든 정적 애니메이션은 항상 정상 속도(1.0)로 재생
                petController.animator.speed = 1.0f;
            }
        }
    }
    
    /// <summary>
    /// 이동 상태에 따른 자동 애니메이션 업데이트
    /// </summary>
    private void UpdateMovementAnimation()
    {
        if (petController.agent != null && petController.agent.enabled && 
            petController.agent.isOnNavMesh && petController.animator != null)
        {
            float agentVelocity = petController.agent.velocity.magnitude;
            float runThreshold = petController.Movement.walkSpeed * petController.Movement.runMultiplier * 0.8f;

            if (agentVelocity > 0.1f)
            {
                if (petController.agent.speed > runThreshold)
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
        // ★ [Phase 4] PetState를 통한 상태 업데이트
        petController.State.SetAnimationLocked(true);
        isSpecialAnimationPlaying = true;

        try
        {
            if (petController.animator != null)
            {
                petController.animator.SetInteger("animation", (int)animationType);
                yield return null;
                float animationLength = petController.animator.GetCurrentAnimatorStateInfo(0).length;
                
                // Die 애니메이션은 더 길게 유지
                if (animationType == PetAnimationType.Die)
                {
                    // 애니메이션 재생 후 추가로 대기
                    yield return new WaitForSeconds(animationLength);
                    
                    // Die 애니메이션 상태를 유지하면서 추가 대기 (총 4초)
                    petController.animator.SetInteger("animation", (int)PetAnimationType.Die);
                    yield return new WaitForSeconds(4f - animationLength);
                }
                else
                {
                    yield return new WaitForSeconds(animationLength);
                }
            }
            else
            {
                yield return new WaitForSeconds(2f);
            }
        }
        finally
        {
            isSpecialAnimationPlaying = false;
            // ★ [Phase 4] PetState를 통한 상태 업데이트
            petController.State.SetAnimationLocked(false);

            if (petController.animator != null)
            {
                petController.animator.SetInteger("animation", (int)PetAnimationType.Idle);
            }

            // 나무 위에 있지 않을 때만 이동 재개
            if (!petController.State.IsClimbingTree && petController.agent != null && petController.agent.enabled && petController.agent.isOnNavMesh)
            {
                petController.ResumeMovement();
            }
        }
    }
}