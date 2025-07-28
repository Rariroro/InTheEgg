using System.Collections;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 펫의 이동 관련 공용 유틸리티 메서드들을 제공합니다.
/// 리팩토링: 배회 로직은 WanderAction으로 이동됨
/// </summary>
public class PetMovementController : MonoBehaviour
{
    private PetController petController;
    
    /// <summary>
    /// 물 속성 펫이 물 vs 육지 목적지를 고를 확률 (0~1).
    /// </summary>
    [Range(0f, 1f)] public float waterDestinationChance = 0.8f;

    // === 초기화 ===
    public void Init(PetController controller)
    {
        petController = controller;
    }

    // === 공용 유틸리티 메서드들 (다른 곳에서 호출됨) ===
    
    /// <summary>
    /// 행동을 강제로 중단합니다. (SelectedAction, ExhaustedAction 등에서 호출)
    /// </summary>
    public void ForceStopCurrentBehavior()
    {
        // 애니메이션 정지
        var animController = petController.GetComponent<PetAnimationController>();
        if (animController != null)
        {
            animController.StopContinuousAnimation();
        }
        
        // 이동 정지
        if (petController.agent != null && petController.agent.enabled)
        {
            try
            {
                petController.agent.isStopped = true;
                petController.agent.ResetPath();
                petController.agent.velocity = Vector3.zero;
            }
            catch { /* 예외 무시 */ }
        }
    }
    
    /// <summary>
    /// 다음 행동을 결정합니다. (상호작용 종료 후 호출됨)
    /// </summary>
    public void DecideNextBehavior()
    {
        // WanderAction이 활성화되도록 간단히 처리
        // 실제 행동 결정은 WanderAction에서 수행됨
        Debug.Log($"[PetMovementController] {petController.petName}: 다음 행동 결정 요청");
    }

    /// <summary>
    /// 지정된 탐색 반경 내에서 무작위 목적지를 설정합니다.
    /// </summary>
    /// <param name="searchRadius">목적지를 찾을 반경</param>
    public void SetRandomDestination(float searchRadius)
    {
        if (petController.agent == null || !petController.agent.enabled || !petController.agent.isOnNavMesh)
            return;

        int waterArea = NavMesh.GetAreaFromName("Water");
        int mask;

        // 물/육지 선호도에 따른 영역 마스크 설정
        if (petController.habitat == PetAIProperties.Habitat.Water && waterArea != -1)
        {
            mask = (Random.value < waterDestinationChance) ? (1 << waterArea) : NavMesh.AllAreas;
        }
        else
        {
            mask = (waterArea != -1) ? (NavMesh.AllAreas & ~(1 << waterArea)) : NavMesh.AllAreas;
        }

        Vector3 dir = Random.insideUnitSphere * searchRadius + transform.position;
        if (NavMesh.SamplePosition(dir, out NavMeshHit hit, searchRadius, mask))
        {
            try
            {
                petController.agent.SetDestination(hit.position);
                petController.ResumeMovement();
                
                // 기본 걷기 애니메이션 설정
                var anim = petController.GetComponent<PetAnimationController>();
                anim?.SetContinuousAnimation(PetAnimationController.PetAnimationType.Walk);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[PetMovementController] {petController.petName}: SetDestination 실패 - {e.Message}");
            }
        }
    }
    
    public void SetRandomDestination()
    {
        SetRandomDestination(50f); // 기본 반경 50f
    }
    
    // === 레거시 메서드들 (점진적 마이그레이션을 위해 유지) ===
    
    // ExecuteWanderBehavior는 더 이상 사용되지 않음 (WanderAction에서 직접 처리)
    [System.Obsolete("Use WanderAction directly")]
    public void ExecuteWanderBehavior()
    {
        Debug.LogWarning("[PetMovementController] ExecuteWanderBehavior는 더 이상 사용되지 않습니다. WanderAction을 직접 사용하세요.");
    }
}