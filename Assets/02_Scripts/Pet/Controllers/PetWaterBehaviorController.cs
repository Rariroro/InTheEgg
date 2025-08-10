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
    private bool wasHolding = false;  // 펫이 들려있었는지 추적
    private float lastSplashTime = -10f;  // 마지막 물보라 생성 시간
    private const float splashCooldown = 1f;  // 물보라 생성 쿨다운 (1초)
    
    // 다이빙 관련
    private bool isDiving = false;  // 다이빙 중인지 여부
    private float divingStartTime = 0f;  // 다이빙 시작 시간
    private float divingDepth = 0f;  // 다이빙 최대 깊이
    private const float DIVING_DURATION = 3f;  // 다이빙 전체 지속 시간

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
                // 비물속성 펫은 30으로 설정하여 더 적극적으로 물을 회피하도록 함
                petController.agent.SetAreaCost(
                    waterArea,
                    petController.habitat == PetAIProperties.Habitat.Water ? 1f : 30f
                );
            }
        }
    }
    
    
    // Unity Update - 매 프레임 물 영역 체크
    private void Update()
    {
        // 들기 상태 추적
        if (petController.State.IsHolding && !wasHolding)
        {
            wasHolding = true;

            // 펫이 들릴 때 물 상태를 리셋하여
            // 다시 놓을 때 물 진입으로 인식되도록 함
            if (isInWater)
            {
                isInWater = false;
                petController.State.UpdateWaterState(false);
            }
        }

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

        // 다이빙 중일 때 특별 처리
        if (isDiving)
        {
            UpdateDivingDepth();
        }
        else
        {
            // 일반적인 부드러운 깊이 전환
            float targetDepth = isInWater ? -petController.waterSinkDepth : 0f;
            currentDepth = Mathf.Lerp(currentDepth, targetDepth, Time.deltaTime * depthTransitionSpeed);
        }
        
        // ★ [Phase 4] PetState를 통한 오프셋 업데이트
        petController.State.SetWaterDepthOffset(currentDepth);
    }

    private void OnEnterWater()
    {
        // 쿨다운 체크 - 최근에 물보라를 생성했으면 스킵
        if (Time.time - lastSplashTime < splashCooldown)
        {
            wasHolding = false;
            // 물 속도만 적용하고 리턴
            ApplyWaterSpeed();
            return;
        }
        
        // 펫이 들려있다가 물에 놓아질 때만 파티클 생성
        if (wasHolding && EnvironmentManager.Instance != null && 
            EnvironmentManager.Instance.waterSplashParticlePrefab != null)
        {
            // 물튀김 파티클 생성 (Y값 보정하여 물 표면 위에 생성)
            Vector3 splashPosition = transform.position;
            splashPosition.y += 1.2f;  // 물 표면 위로 보정
            
            GameObject splash = Instantiate(
                EnvironmentManager.Instance.waterSplashParticlePrefab,
                splashPosition, 
                Quaternion.identity
            );
            
            // 파티클 크기를 펫의 실제 3D 모델 크기에 맞게 조정
            Renderer renderer = petController.GetComponentInChildren<Renderer>();
            if (renderer != null)
            {
                // 렌더러의 bounds를 사용하여 실제 모델 크기 측정
                // x와 z축의 평균을 사용 (y축은 높이이므로 제외)
                float scale = (renderer.bounds.size.x + renderer.bounds.size.z) / 2f;
                splash.transform.localScale = Vector3.one * scale;
            }
            else if (petController.agent != null)
            {
                // 렌더러가 없으면 NavMeshAgent radius를 폴백으로 사용
                float scale = petController.agent.radius * 3f;
                splash.transform.localScale = Vector3.one * scale;
            }
            
            // 3초 후 파티클 제거
            Destroy(splash, 3f);
            
            // 마지막 물보라 생성 시간 기록
            lastSplashTime = Time.time;
            
            // Debug.Log($"{petController.petName}: 물에 놓아져서 물튀김 효과 생성!");
        }
        
        // wasHolding 플래그 리셋
        wasHolding = false;
        
        // 물 속도 적용
        ApplyWaterSpeed();
    }
    
    private void ApplyWaterSpeed()
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
    
    // 외부에서 물보라 생성 시간을 기록할 수 있는 메서드
    public void RecordSplashTime()
    {
        lastSplashTime = Time.time;
    }
    
    /// <summary>
    /// 다이빙 시퀀스 시작 (입수 → 잠수 → 부상)
    /// </summary>
    public void StartDivingSequence()
    {
        // 다이빙 상태 시작
        isDiving = true;
        divingStartTime = Time.time;
        divingDepth = petController.waterSinkDepth * 2.5f; // 일반 깊이의 2.5배 깊이로 다이빙
        
        // 큰 물보라 효과 생성
        CreateDivingSplash();
        
        // 물 상태 업데이트
        isInWater = true;
        petController.State.UpdateWaterState(true);
        
        // 초기 깊이 설정 (즉시 깊은 곳으로)
        currentDepth = -divingDepth;
        petController.State.SetWaterDepthOffset(currentDepth);
        
        Debug.Log($"{petController.petName}: 다이빙 시퀀스 시작! 깊이: {divingDepth}");
    }
    
    /// <summary>
    /// 다이빙 중 깊이 업데이트 (잠수 → 부상)
    /// </summary>
    private void UpdateDivingDepth()
    {
        float elapsed = Time.time - divingStartTime;
        
        if (elapsed >= DIVING_DURATION)
        {
            // 다이빙 종료
            isDiving = false;
            currentDepth = -petController.waterSinkDepth; // 일반 수심으로 복귀
            Debug.Log($"{petController.petName}: 다이빙 완료, 일반 수심으로 복귀");
        }
        else
        {
            // 다이빙 진행 중: 처음 1초는 깊이 유지, 나머지 2초는 천천히 부상
            if (elapsed < 1f)
            {
                // 깊은 곳 유지
                currentDepth = -divingDepth;
            }
            else
            {
                // 천천히 부상 (Ease-out 커브)
                float t = (elapsed - 1f) / 2f; // 0 ~ 1로 정규화
                t = 1f - Mathf.Pow(1f - t, 2f); // Ease-out
                currentDepth = Mathf.Lerp(-divingDepth, -petController.waterSinkDepth, t);
            }
        }
    }
    
    /// <summary>
    /// 다이빙으로 인한 큰 물보라 효과 생성
    /// </summary>
    private void CreateDivingSplash()
    {
        if (EnvironmentManager.Instance != null && 
            EnvironmentManager.Instance.waterSplashParticlePrefab != null)
        {
            // 큰 물튀김 파티클 생성 (Y값 보정하여 물 표면 위에 생성)
            Vector3 splashPosition = transform.position;
            splashPosition.y += 1.2f;  // 물 표면 위로 보정
            
            GameObject splash = Instantiate(
                EnvironmentManager.Instance.waterSplashParticlePrefab,
                splashPosition, 
                Quaternion.identity
            );
            
            // 다이빙 물보라는 일반 물보라보다 2배 크게
            Renderer renderer = petController.GetComponentInChildren<Renderer>();
            if (renderer != null)
            {
                float scale = (renderer.bounds.size.x + renderer.bounds.size.z) / 2f;
                splash.transform.localScale = Vector3.one * scale * 2f; // 2배 크기
            }
            else if (petController.agent != null)
            {
                float scale = petController.agent.radius * 6f; // 2배 크기
                splash.transform.localScale = Vector3.one * scale;
            }
            
            // 4초 후 파티클 제거 (더 오래 지속)
            Destroy(splash, 4f);
            
            // 물보라 생성 시간 기록
            lastSplashTime = Time.time;
            
            // 물 속도 적용
            ApplyWaterSpeed();
            
            Debug.Log($"{petController.petName}: 다이빙 물보라 효과 생성!");
        }
    }
}