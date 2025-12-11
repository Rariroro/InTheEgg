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
    private bool isDieAnimationPlaying = false; // 죽는 애니메이션 특별 처리용

    // 캐싱 - JobTempAlloc 메모리 누수 방지
    private PetAnimationType cachedCurrentAnimation = PetAnimationType.Idle;
    private float cachedAnimatorSpeed = 1f;
    private static readonly int AnimationHash = Animator.StringToHash("animation");

    // agent.velocity 캐싱 - 매 프레임 접근 방지
    private float velocityCacheTimer = 0f;
    private const float VELOCITY_CACHE_INTERVAL = 0.1f;
    private float cachedVelocityMagnitude = 0f;

    protected override void OnInitialize()
    {
        // 추가 초기화 로직 필요시 여기에 작성
    }
    
    // Unity Update - 매 프레임 애니메이션 업데이트
    private void Update()
    {
        // 특별한 애니메이션이 재생 중이 아닐 때만 업데이트
        if (!petController.isGatheringAnimationOverride)
        {
            UpdateAnimation();
        }
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

        // 애니메이션 속도 동기화 (상태 체크보다 먼저 실행)
        SyncAnimationSpeed();

        if (petController.State.IsSelected || petController.State.IsHolding || petController.State.IsInteracting || petController.State.IsActionLocked)
        {
            return;
        }

        // 특수 애니메이션이 재생 중이면 건너뛰기
        if (isSpecialAnimationPlaying)
            return;

        // 나무를 찾아 이동 중일 때도 자동 업데이트 건너뛰기
        var treeClimbingController = petController.GetComponent<PetTreeClimbingController>();
        if (treeClimbingController != null && treeClimbingController.IsSearchingForTree())
            return;

        // 연속 애니메이션 중이어도 Walk/Run은 자동 업데이트 허용
        if (isContinuousAnimationPlaying)
        {
            // Walk/Run 애니메이션은 이동 상태와 동기화 필요
            if (continuousAnimationType == PetAnimationType.Walk ||
                continuousAnimationType == PetAnimationType.Run)
            {
                UpdateMovementAnimation();  // 이동 상태 확인 및 업데이트
            }
            return;
        }

        // 이동 상태에 따른 자동 애니메이션 설정
        UpdateMovementAnimation();
    }
    
    /// <summary>
    /// 애니메이션 속도를 이동 속도와 동기화
    /// </summary>
    private void SyncAnimationSpeed()
    {
        // 특별 애니메이션이 재생 중이면 속도 변경하지 않음
        if (isSpecialAnimationPlaying)
        {
            return;
        }

        // ★★★ 추가: 나무 오르기/내려오기 중(수동 이동)일 때는 속도를 1로 고정 ★★★
        var treeClimbingController = petController.GetComponent<PetTreeClimbingController>();
        if (treeClimbingController != null && treeClimbingController.IsSearchingForTree())
        {
            // agent가 꺼져있어서 velocity가 0이 나오더라도 애니메이션은 계속 재생되어야 함
            if (petController.animator != null && petController.animator.speed != 1f)
            {
                petController.animator.speed = 1f;
                cachedAnimatorSpeed = 1f;
            }
            return;
        }

        if (petController.animator != null && petController.agent != null)
        {
            // GetInteger 제거 - 캐시된 값 사용으로 JobTempAlloc 방지
            float targetSpeed;

            // 걷기 또는 달리기 상태일 때만 이동 속도에 애니메이션 속도를 동기화
            if (cachedCurrentAnimation == PetAnimationType.Walk || cachedCurrentAnimation == PetAnimationType.Run)
            {
                if (petController.Movement.walkSpeed > 0)
                {
                    targetSpeed = petController.agent.velocity.magnitude / petController.Movement.walkSpeed;
                }
                else
                {
                    targetSpeed = 1f;
                }
            }
            else
            {
                // 다른 모든 정적 애니메이션은 항상 정상 속도(1.0)로 재생
                targetSpeed = 1f;
            }

            // 속도가 변경될 때만 설정 (JobTempAlloc 방지)
            if (!Mathf.Approximately(cachedAnimatorSpeed, targetSpeed))
            {
                petController.animator.speed = targetSpeed;
                cachedAnimatorSpeed = targetSpeed;
            }
        }
    }
    
    /// <summary>
    /// 이동 상태에 따른 자동 애니메이션 업데이트
    /// </summary>
    private void UpdateMovementAnimation()
    {
        PetAnimationType targetAnimation = PetAnimationType.Idle;

        if (petController.agent != null && petController.agent.enabled &&
            petController.agent.isOnNavMesh && petController.animator != null)
        {
            // velocity 캐싱 - 0.1초 주기로만 접근 (JobTempAlloc 방지)
            velocityCacheTimer += Time.deltaTime;
            if (velocityCacheTimer >= VELOCITY_CACHE_INTERVAL)
            {
                velocityCacheTimer = 0f;
                cachedVelocityMagnitude = petController.agent.velocity.magnitude;
            }

            float runThreshold = petController.Movement.walkSpeed * petController.Movement.runMultiplier * 0.8f;

            if (cachedVelocityMagnitude > 0.1f)
            {
                if (petController.agent.speed > runThreshold)
                {
                    targetAnimation = PetAnimationType.Run;
                }
                else
                {
                    targetAnimation = PetAnimationType.Walk;
                }
            }
        }

        // 값이 변경될 때만 SetInteger 호출 (JobTempAlloc 방지)
        SetAnimationInternal(targetAnimation);
    }

    /// <summary>
    /// 내부 애니메이션 설정 - 캐싱으로 중복 호출 방지
    /// </summary>
    private void SetAnimationInternal(PetAnimationType animation)
    {
        if (petController.animator == null) return;

        if (animation != cachedCurrentAnimation)
        {
            petController.animator.SetInteger(AnimationHash, (int)animation);
            cachedCurrentAnimation = animation;
        }
    }

    public void SetContinuousAnimation(PetAnimationType animationType)
    {
        SetAnimationInternal(animationType);
        isContinuousAnimationPlaying = true;
        continuousAnimationType = animationType;
    }

    public void StopContinuousAnimation()
    {
        SetAnimationInternal(PetAnimationType.Idle);
        isContinuousAnimationPlaying = false;
        continuousAnimationType = PetAnimationType.Idle;
    }

    public void ForceStopAllAnimations()
    {
        isSpecialAnimationPlaying = false;
        isContinuousAnimationPlaying = false;
        continuousAnimationType = PetAnimationType.Idle;
        isDieAnimationPlaying = false;

        SetAnimationInternal(PetAnimationType.Idle);
        if (petController.animator != null)
        {
            petController.animator.speed = 1.0f;
            cachedAnimatorSpeed = 1.0f;
        }
    }

    public IEnumerator PlayAnimationWithCustomDuration(PetAnimationType animationType, float duration, bool returnToIdle = true, bool resumeMovementAfter = true)
    {
        isSpecialAnimationPlaying = true;

        if (petController.animator != null)
        {
            petController.animator.SetInteger(AnimationHash, (int)animationType);
            cachedCurrentAnimation = animationType;
            yield return new WaitForSeconds(duration);

            if (returnToIdle)
            {
                petController.animator.SetInteger(AnimationHash, (int)PetAnimationType.Idle);
                cachedCurrentAnimation = PetAnimationType.Idle;
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

        // Die 애니메이션 특별 처리
        if (animationType == PetAnimationType.Die)
        {
            isDieAnimationPlaying = true;
        }

        try
        {
            if (petController.animator != null)
            {
                // 애니메이션 속도를 1.0으로 고정
                petController.animator.speed = 1.0f;
                cachedAnimatorSpeed = 1.0f;
                petController.animator.SetInteger(AnimationHash, (int)animationType);
                cachedCurrentAnimation = animationType;
                yield return null;
                float animationLength = petController.animator.GetCurrentAnimatorStateInfo(0).length;

                // Die 애니메이션은 더 길게 유지
                if (animationType == PetAnimationType.Die)
                {
                    // 애니메이션 재생 후 추가로 대기
                    yield return new WaitForSeconds(animationLength);

                    // Die 애니메이션 상태를 유지하면서 추가 대기 (총 4초)
                    petController.animator.SetInteger(AnimationHash, (int)PetAnimationType.Die);
                    cachedCurrentAnimation = PetAnimationType.Die;
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
            isDieAnimationPlaying = false;
            // ★ [Phase 4] PetState를 통한 상태 업데이트
            petController.State.SetAnimationLocked(false);

            if (petController.animator != null)
            {
                petController.animator.SetInteger(AnimationHash, (int)PetAnimationType.Idle);
                cachedCurrentAnimation = PetAnimationType.Idle;
                petController.animator.speed = 1.0f;
                cachedAnimatorSpeed = 1.0f;
            }

            // 나무 위에 있지 않을 때만 이동 재개
            if (!petController.State.IsClimbingTree && petController.agent != null && petController.agent.enabled && petController.agent.isOnNavMesh)
            {
                petController.ResumeMovement();
            }
        }
    }

    // Die 애니메이션 재생 중인지 확인하는 프로퍼티
    public bool IsDieAnimationPlaying => isDieAnimationPlaying;
}