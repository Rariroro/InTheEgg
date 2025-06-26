using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 펫의 물 속 행동을 전담하는 컨트롤러
/// </summary>
public class PetWaterBehaviorController : MonoBehaviour
{
    private PetController petController;
    
    // 물 상태 관련
    private bool isInWater = false;
    private float waterSpeedMultiplier = 0.3f;
    private float currentDepth = 0f;
    private float depthTransitionSpeed = 2f;

    public void Init(PetController controller)
    {
        petController = controller;
        
        // NavMeshAgent가 이미 활성화되어 NavMesh 위에 있을 때만 물 영역 비용을 설정
        if (petController.agent != null && petController.agent.enabled && petController.agent.isOnNavMesh)
        {
            int waterArea = NavMesh.GetAreaFromName("Water");
            if (waterArea != -1)
            {
                // 물 속성 펫은 물 영역 비용 낮게, 비물 속성은 높게 설정
                petController.agent.SetAreaCost(
                    waterArea,
                    petController.habitat == PetAIProperties.Habitat.Water ? 0.5f : 10f
                );
            }
        }
    }

    public void CheckWaterArea()
    {
        if (petController.agent == null || !petController.agent.enabled || !petController.agent.isOnNavMesh)
            return;

        // 현재 NavMesh 영역 확인
        NavMeshHit hit;
        if (NavMesh.SamplePosition(transform.position, out hit, 1f, NavMesh.AllAreas))
        {
            int waterArea = NavMesh.GetAreaFromName("Water");
            if (waterArea != -1)
            {
                // 현재 위치가 물 영역인지 확인
                bool currentlyInWater = (1 << waterArea) == hit.mask;

                if (currentlyInWater != isInWater)
                {
                    isInWater = currentlyInWater;
                    petController.isInWater = isInWater;

                    if (isInWater)
                    {
                        Debug.Log($"{petController.petName}: 물에 들어감");
                        OnEnterWater();
                    }
                    else
                    {
                        Debug.Log($"{petController.petName}: 물에서 나옴");
                        OnExitWater();
                    }
                }
            }
        }

        // 부드러운 깊이 전환
        float targetDepth = isInWater ? -petController.waterSinkDepth : 0f;
        currentDepth = Mathf.Lerp(currentDepth, targetDepth, Time.deltaTime * depthTransitionSpeed);
        petController.waterDepthOffset = currentDepth;
    }

    private void OnEnterWater()
    {
        // 물 서식지 펫은 덜 느려짐
        float speedMult = (petController.habitat == PetAIProperties.Habitat.Water)
                          ? 0.7f : waterSpeedMultiplier;

        // 속도 감소
        if (petController.agent != null && !petController.isGathering)
        {
            petController.agent.speed = petController.baseSpeed * speedMult;
            petController.agent.acceleration = petController.baseAcceleration * speedMult;
        }

        // 애니메이션 속도도 감소
        var anim = petController.GetComponent<PetAnimationController>();
        if (anim != null && petController.animator != null)
        {
            petController.animator.speed = speedMult;
        }
    }

    private void OnExitWater()
    {
        // 속도 복구
        if (petController.agent != null && !petController.isGathering)
        {
            // PersonalityBehavior 정보를 가져와야 하므로 PetMovementController와 연동 필요
            // 기본적으로는 baseSpeed로 복구
            petController.agent.speed = petController.baseSpeed;
            petController.agent.acceleration = petController.baseAcceleration;
        }

        // 애니메이션 속도 복구
        if (petController.animator != null)
        {
            petController.animator.speed = 1f;
        }
    }

    // PetMovementController에서 호출하는 메서드
    public void AdjustSpeedForWater()
    {
        if (isInWater && petController.agent != null)
        {
            float speedMult = (petController.habitat == PetAIProperties.Habitat.Water)
                              ? 0.7f : waterSpeedMultiplier;
            petController.agent.speed *= speedMult;
        }
    }

    // 현재 물 속에 있는지 확인하는 프로퍼티
    public bool IsInWater => isInWater;
}