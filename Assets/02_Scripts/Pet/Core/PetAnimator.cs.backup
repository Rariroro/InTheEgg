using System.Collections;
using UnityEngine;

/// <summary>
/// 펫의 애니메이션 기능만을 담당하는 클래스
/// PetController에서 애니메이션 관련 로직을 분리
/// </summary>
public class PetAnimator : MonoBehaviour
{
    // 애니메이션 타입 (기존 PetAnimationController와 호환)
    public enum AnimationType
    {
        Idle = 0,
        Walk = 1,
        Run = 2,
        Jump = 3,
        Attack = 4,
        Die = 5,
        Rest = 6,
        Eat = 7,
        Happy = 8,
        Sad = 9,
        // 추가 애니메이션 타입들...
    }
    
    [Header("Animation Settings")]
    [SerializeField] private float animationTransitionTime = 0.2f;
    [SerializeField] private string animationParameterName = "animation";
    
    private Animator animator;
    private PetController petController;
    private AnimationType currentAnimation = AnimationType.Idle;
    private Coroutine currentAnimationCoroutine;
    private bool isContinuousAnimation = false;
    private bool isInitialized = false;
    
    // 프로퍼티
    public AnimationType CurrentAnimation => currentAnimation;
    public bool IsPlaying => currentAnimationCoroutine != null;
    public bool IsContinuous => isContinuousAnimation;
    
    /// <summary>
    /// PetAnimator 초기화
    /// </summary>
    public void Init(PetController controller, Animator anim)
    {
        petController = controller;
        animator = anim;
        
        if (animator == null)
        {
            Debug.LogError($"[PetAnimator] {petController.petName}: Animator가 없습니다!");
            return;
        }
        
        isInitialized = true;
        Debug.Log($"[PetAnimator] {petController.petName}: 애니메이션 시스템 초기화 완료");
    }
    
    /// <summary>
    /// 일회성 애니메이션 재생
    /// </summary>
    public void PlayOnce(AnimationType animType, float duration = -1f)
    {
        if (!CanPlayAnimation())
            return;
            
        StopCurrentAnimation();
        currentAnimationCoroutine = StartCoroutine(PlayOnceCoroutine(animType, duration));
    }
    
    /// <summary>
    /// 연속 애니메이션 설정
    /// </summary>
    public void PlayContinuous(AnimationType animType)
    {
        if (!CanPlayAnimation())
            return;
            
        StopCurrentAnimation();
        
        currentAnimation = animType;
        isContinuousAnimation = true;
        animator.SetInteger(animationParameterName, (int)animType);
        
        Debug.Log($"[PetAnimator] {petController.petName}: 연속 애니메이션 시작 - {animType}");
    }
    
    /// <summary>
    /// 현재 애니메이션 중지
    /// </summary>
    public void Stop()
    {
        StopCurrentAnimation();
        PlayContinuous(AnimationType.Idle);
    }
    
    /// <summary>
    /// 이동 속도에 따른 애니메이션 자동 업데이트
    /// </summary>
    public void UpdateMovementAnimation(float velocity, float maxSpeed)
    {
        if (!isInitialized || animator == null || !isContinuousAnimation)
            return;
            
        AnimationType targetAnim;
        
        if (velocity < 0.1f)
        {
            targetAnim = AnimationType.Idle;
        }
        else if (velocity < maxSpeed * 0.5f)
        {
            targetAnim = AnimationType.Walk;
        }
        else
        {
            targetAnim = AnimationType.Run;
        }
        
        if (currentAnimation != targetAnim)
        {
            PlayContinuous(targetAnim);
        }
    }
    
    /// <summary>
    /// 애니메이션 속도 설정
    /// </summary>
    public void SetSpeed(float speed)
    {
        if (animator != null)
        {
            animator.speed = speed;
        }
    }
    
    /// <summary>
    /// 특정 애니메이션이 재생 중인지 확인
    /// </summary>
    public bool IsPlayingAnimation(AnimationType animType)
    {
        return currentAnimation == animType;
    }
    
    /// <summary>
    /// 애니메이션 블렌드 설정
    /// </summary>
    public void SetBlendParameter(string parameterName, float value)
    {
        if (animator != null && HasParameter(animator, parameterName))
        {
            animator.SetFloat(parameterName, value);
        }
    }
    
    /// <summary>
    /// 애니메이션 트리거
    /// </summary>
    public void Trigger(string triggerName)
    {
        if (animator != null && HasParameter(animator, triggerName))
        {
            animator.SetTrigger(triggerName);
        }
    }
    
    // === Private 메서드들 ===
    
    private void StopCurrentAnimation()
    {
        if (currentAnimationCoroutine != null)
        {
            StopCoroutine(currentAnimationCoroutine);
            currentAnimationCoroutine = null;
        }
        isContinuousAnimation = false;
    }
    
    private bool CanPlayAnimation()
    {
        if (!isInitialized || animator == null)
        {
            Debug.LogWarning($"[PetAnimator] 애니메이션 시스템이 초기화되지 않았습니다!");
            return false;
        }
        
        // 애니메이션 잠금 상태 체크
        if (petController.isAnimationLocked)
        {
            return false;
        }
        
        return true;
    }
    
    private IEnumerator PlayOnceCoroutine(AnimationType animType, float duration)
    {
        currentAnimation = animType;
        animator.SetInteger(animationParameterName, (int)animType);
        
        // 지속 시간이 지정되지 않았으면 애니메이션 클립 길이 사용
        if (duration <= 0)
        {
            AnimationClip[] clips = animator.runtimeAnimatorController.animationClips;
            foreach (var clip in clips)
            {
                if (clip.name.Contains(animType.ToString()))
                {
                    duration = clip.length;
                    break;
                }
            }
            
            // 기본값
            if (duration <= 0)
                duration = 1f;
        }
        
        Debug.Log($"[PetAnimator] {petController.petName}: 일회성 애니메이션 재생 - {animType} ({duration}초)");
        
        yield return new WaitForSeconds(duration);
        
        // Idle로 복귀
        PlayContinuous(AnimationType.Idle);
        currentAnimationCoroutine = null;
    }
    
    // Animator 파라미터 확인 메서드
    private bool HasParameter(Animator animator, string parameterName)
    {
        if (animator == null) return false;
        
        foreach (AnimatorControllerParameter param in animator.parameters)
        {
            if (param.name == parameterName)
                return true;
        }
        return false;
    }
}