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

  public void UpdateAnimation()
{
    // 현재 agent의 속도에 비례해서 애니메이션 속도를 조정합니다.
    if (petController.animator != null && petController.agent != null)
    {
        petController.animator.speed = petController.agent.speed / petController.baseSpeed;
    }

    if (isSpecialAnimationPlaying)
        return;

    if (petController.petModelTransform != null)
    {
        // 이동 속도 평활화 계산
        Vector3 currentVelocity = (petController.petModelTransform.position - lastPosition) / Time.deltaTime;
        smoothVelocity = Vector3.Lerp(smoothVelocity, currentVelocity, Time.deltaTime / petController.smoothTime);
        lastPosition = petController.petModelTransform.position;

        // 이동 중이면 걷기 애니메이션, 그렇지 않으면 idle 처리
        if (smoothVelocity.magnitude > 1f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(smoothVelocity);
            petController.petModelTransform.rotation = Quaternion.Slerp(
                petController.petModelTransform.rotation,
                targetRotation,
                Time.deltaTime * petController.rotationSpeed
            );
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

// 커스텀 지속 시간과 애니메이션 후 행동을 지정할 수 있는 메서드
public IEnumerator PlayAnimationWithCustomDuration(int animationNumber, float duration, bool returnToIdle = true, bool resumeMovementAfter = true)
{
    isSpecialAnimationPlaying = true;
    
    if (petController.animator != null)
    {
        petController.animator.SetInteger("animation", animationNumber);
        
        // 지정된 시간만큼 대기
        yield return new WaitForSeconds(duration);
        
        // 기본 애니메이션으로 돌아갈지 여부
        if (returnToIdle)
        {
            petController.animator.SetInteger("animation", 0);
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

// 연속적인 애니메이션 설정 메서드 (특별 애니메이션 플래그 없이 계속 이동하며 애니메이션 유지)
public void SetContinuousAnimation(int animationNumber)
{
    if (petController.animator != null)
    {
        petController.animator.SetInteger("animation", animationNumber);
    }
}

// 연속적인 애니메이션 종료 메서드
public void StopContinuousAnimation()
{
    if (petController.animator != null)
    {
        petController.animator.SetInteger("animation", 0);
    }
}
}