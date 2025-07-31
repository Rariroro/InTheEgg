using UnityEngine;
using UnityEngine.AI;
using PetAIProperties = PetTraits;

/// <summary>
/// 펫의 물 속 행동을 전담하는 컨트롤러
/// </summary>
public class PetWaterBehaviorController : PetControllerBase
{
    // 물 상태 관련
    private bool isInWater = false;
    private float currentDepth = 0f;
    private float depthTransitionSpeed = 2f;

    protected override void OnInitialize()
    {
        // NavMeshAgent가 이미 활성화되어 NavMesh 위에 있을 때만 물 영역 비용을 설정
        if (petController.agent != null && petController.agent.enabled && petController.agent.isOnNavMesh)
        {
            int waterArea = NavMesh.GetAreaFromName("Water");
            if (waterArea != -1)
            {
                // 물 속성 펫은 물 영역 비용 낮게, 비물 속성은 높게 설정
                // Unity는 1보다 작은 cost 값에 대해 경고를 표시하므로 최소값을 1로 설정
                petController.agent.SetAreaCost(
                    waterArea,
                    petController.habitat == PetAIProperties.Habitat.Water ? 1f : 10f
                );
            }
        }
    }
    
    // Unity Update - 매 프레임 물 영역 체크
    private void Update()
    {
        CheckWaterArea();
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
                    // ★ [Phase 4] PetState를 통한 상태 업데이트
                    petController.State.UpdateWaterState(isInWater);

                    if (isInWater)
                    {
                        // Debug.Log($"{petController.petName}: 물에 들어감");
                        OnEnterWater();
                    }
                    else
                    {
                        // Debug.Log($"{petController.petName}: 물에서 나옴");
                        OnExitWater();
                    }
                }
            }
        }

        // 부드러운 깊이 전환
        float targetDepth = isInWater ? -petController.waterSinkDepth : 0f;
        currentDepth = Mathf.Lerp(currentDepth, targetDepth, Time.deltaTime * depthTransitionSpeed);
        // ★ [Phase 4] PetState를 통한 오프셋 업데이트
        petController.State.SetWaterDepthOffset(currentDepth);
    }

    private void OnEnterWater()
    {
        // MovementSettings를 통해 물 속 속도 계산
        bool isAquatic = petController.habitat == PetAIProperties.Habitat.Water;
        
        // 속도 감소
        if (petController.agent != null && !petController.State.IsGathering)
        {
            petController.agent.speed = petController.Movement.GetWaterSpeed(isAquatic, petController.personality);
            petController.agent.acceleration = petController.Movement.acceleration * 
                (isAquatic ? petController.Movement.aquaticWaterSpeedMultiplier : petController.Movement.waterSpeedMultiplier);
        }

        // 애니메이션 속도도 감소
        if (petController.animator != null)
        {
            float animSpeedMult = isAquatic ? petController.Movement.aquaticWaterSpeedMultiplier : petController.Movement.waterSpeedMultiplier;
            petController.animator.speed = animSpeedMult;
        }
    }

    private void OnExitWater()
    {
        // 속도 복구 - 성격이 적용된 속도로
        if (petController.agent != null && !petController.State.IsGathering)
        {
            petController.agent.speed = petController.Movement.GetAdjustedWalkSpeed(petController.personality);
            petController.agent.acceleration = petController.Movement.acceleration;
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
            bool isAquatic = petController.habitat == PetAIProperties.Habitat.Water;
            float speedMult = isAquatic ? petController.Movement.aquaticWaterSpeedMultiplier : petController.Movement.waterSpeedMultiplier;
            petController.agent.speed *= speedMult;
        }
    }

    // 현재 물 속에 있는지 확인하는 프로퍼티
    public bool IsInWater => isInWater;
}