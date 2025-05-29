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
public void UpdateAnimation()
{
    // 애니메이션 속도 조정은 유지
    if (petController.animator != null && petController.agent != null)
    {
        petController.animator.speed = petController.agent.speed / petController.baseSpeed;
    }

    if (isSpecialAnimationPlaying || isContinuousAnimationPlaying)
        return;

    // ★ 이동 속도 계산을 부모 오브젝트 기준으로 변경
    Vector3 currentVelocity = (transform.position - lastPosition) / Time.deltaTime;
    smoothVelocity = Vector3.Lerp(smoothVelocity, currentVelocity, Time.deltaTime / petController.smoothTime);
    lastPosition = transform.position;

    // ★ 회전 처리 제거 (부모가 이미 회전하므로)
    if (smoothVelocity.magnitude > 1f)
    {
        // 회전 코드 제거
        if (petController.animator != null)
        {
            petController.animator.SetInteger("animation", 1);
        }
    }
    else
    {
        if (petController.animator != null)
        {
            petController.animator.SetInteger("animation", 0);
        }
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


  public IEnumerator PlaySpecialAnimation(int animationNumber)
{
    isSpecialAnimationPlaying = true;
    if (petController.animator != null)
    {
        petController.animator.SetInteger("animation", animationNumber);
        // 애니메이터의 현재 상태 길이만큼 대기
        yield return new WaitForSeconds(petController.animator.GetCurrentAnimatorStateInfo(0).length);
        petController.animator.SetInteger("animation", 0);
    }
    else
    {
        yield return new WaitForSeconds(2f);
    }
    isSpecialAnimationPlaying = false;
    
    // NavMeshAgent가 활성화되어 있고 NavMesh 위에 있는 경우에만 ResumeMovement 호출
    if (petController.agent != null && petController.agent.enabled && petController.agent.isOnNavMesh)
    {
        petController.ResumeMovement();
    }
}


}