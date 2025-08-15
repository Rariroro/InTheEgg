using UnityEngine;
using UnityEngine.AI;
using PetAIProperties = PetTraits;

/// <summary>
/// 펫의 물 관련 행동을 처리하는 컨트롤러
/// 물 영역 감지, 수심 애니메이션, 다이빙 기능 등을 관리합니다
/// </summary>
public class PetWaterBehaviorController : PetControllerBase
{
    // ===== 물 상태 관리 변수 =====
    private bool isInWater = false;           // 현재 물 속에 있는지 여부
    private float currentDepth = 0f;          // 현재 수심 (음수는 물 속을 의미)
    private float depthTransitionSpeed = 2f;  // 수심 변화 속도 (부드러운 전환용)
    private bool wasHolding = false;          // 이전 프레임에 들려있었는지 추적
    private float waterSurfaceY = 0f;         // 물 표면의 Y 좌표
    private Vector3 positionBeforeWater;      // 물 진입 전 위치 저장
    private GameObject cachedWaterObject;     // 캐싱된 Water 오브젝트

    // ===== 다이빙 관련 변수 =====
    private bool isDiving = false;            // 다이빙 중인지 여부
    private float divingStartTime = 0f;       // 다이빙 시작 시간
    private float divingDepth = 0f;           // 다이빙 깊이
    private const float DIVING_DURATION = 3f; // 다이빙 지속 시간 (3초)

    /// <summary>
    /// 컨트롤러 초기화
    /// 펫의 서식지에 따라 NavMesh의 물 영역 비용을 설정합니다
    /// </summary>
    protected override void OnInitialize()
    {
        // Water 오브젝트 캐싱 (한 번만 검색)
        cachedWaterObject = GameObject.FindWithTag("Water");
        
        // NavMesh 에이전트가 활성화되어 있고 NavMesh 위에 있는 경우
        if (petController.agent != null && petController.agent.enabled && petController.agent.isOnNavMesh)
        {
            int waterArea = NavMesh.GetAreaFromName("Water");
            if (waterArea != -1)
            {
                // 수생 펫은 물 영역을 쉽게 통과(1f), 
                // 육상 펫은 회피하도록 높은 비용(30f) 설정
                petController.agent.SetAreaCost(
                    waterArea,
                    petController.habitat == PetAIProperties.Habitat.Water ? 1f : 30f
                );
            }
        }
    }

    /// <summary>
    /// 매 프레임 호출되는 업데이트 메서드
    /// 펫이 들려있는 상태를 감지하고 수심 애니메이션을 업데이트합니다
    /// </summary>
    private void Update()
    {
        // 펫이 방금 들려진 경우 감지 (항상 체크 필요)
        if (petController.State.IsHolding && !wasHolding)
        {
            wasHolding = true;  // 들려있는 상태로 표시

            // 물 속에 있었다면 물 상태 해제
            // (들려있는 동안은 물 속에 있지 않은 것으로 처리)
            if (isInWater)
            {
                isInWater = false;
                petController.State.UpdateWaterState(false);
            }
        }

        // 수심 연출을 위한 애니메이션 업데이트
        // 물 속이거나 다이빙 중이거나 깊이가 0이 아닐 때만 업데이트
        if (isInWater || isDiving || Mathf.Abs(currentDepth) > 0.01f)
        {
            UpdateDepthAnimation();
        }
    }

    /// <summary>
    /// 수심 애니메이션 업데이트
    /// 펫의 Y축 위치를 조정하여 물에 잠긴 효과를 연출합니다
    /// </summary>
    private void UpdateDepthAnimation()
    {
        if (isDiving)
        {
            // 다이빙 중일 때는 특별한 깊이 업데이트 로직 사용
            UpdateDivingDepth();
        }
        else if (isInWater)
        {
            // 일반 물 속: 설정된 수심만큼 아래로
            // 음수 값으로 설정하여 펫이 아래로 내려가는 효과
            float targetDepth = -petController.waterSinkDepth;
            currentDepth = Mathf.Lerp(currentDepth, targetDepth, Time.deltaTime * depthTransitionSpeed);
        }
        else
        {
            // 물 밖: 원래 높이로 복귀
            currentDepth = Mathf.Lerp(currentDepth, 0f, Time.deltaTime * depthTransitionSpeed);
        }

        // 계산된 수심을 State에 저장
        petController.State.SetWaterDepthOffset(currentDepth);
    }

    /// <summary>
    /// 물 영역 진입 시 호출
    /// WaterTriggerDetector에서 OnTriggerEnter 시 호출합니다
    /// </summary>
    public void OnWaterEnter(float surfaceY)
    {
        if (!isInWater)
        {
            waterSurfaceY = surfaceY;
            isInWater = true;
            petController.State.UpdateWaterState(true);
            OnEnterWater();  // 물 진입 처리
            // Debug.Log($"{petController.petName}: 물에 들어감 (Trigger) - 수면 높이: {waterSurfaceY}");
        }
    }
    
    // 기존 메서드 오버로드 (호환성 유지)
    public void OnWaterEnter()
    {
        // 캐싱된 물 오브젝트 사용
        if (cachedWaterObject != null)
        {
            OnWaterEnter(cachedWaterObject.transform.position.y);
        }
        else
        {
            OnWaterEnter(5.7f);  // 기본값
        }
    }

    /// <summary>
    /// 물 영역 탈출 시 호출
    /// WaterTriggerDetector에서 OnTriggerExit 시 호출합니다
    /// </summary>
    public void OnWaterExit()
    {
        // 다이빙 중에는 물에서 나간 것으로 처리하지 않음
        if (isInWater && !isDiving)
        {
            isInWater = false;
            petController.State.UpdateWaterState(false);
            OnExitWater();  // 물 탈출 처리
            // Debug.Log($"{petController.petName}: 물에서 나옴 (Trigger)");
        }
    }

    /// <summary>
    /// 물에 들어갈 때의 처리
    /// 이동 속도를 조정하고 루트 위치를 물 표면으로 이동합니다
    /// </summary>
    private void OnEnterWater()
    {
        // 들려있던 상태 플래그 리셋
        wasHolding = false;

        // 현재 위치 저장 (나중에 복구용)
        positionBeforeWater = petController.transform.position;
        
        // 루트 오브젝트를 물 표면 높이로 조정
        if (!petController.State.IsHolding)
        {
            Vector3 newPos = petController.transform.position;
            newPos.y = waterSurfaceY;
            petController.transform.position = newPos;
            
            // NavMesh 에이전트는 활성 상태 유지 (물 속에서도 이동 가능하도록)
            // 수생 펫과 육상 펫 모두 NavMesh를 사용하되 속도만 조정
            if (petController.agent != null && petController.agent.enabled)
            {
                // NavMesh 위치 동기화
                if (petController.agent.isOnNavMesh)
                {
                    petController.agent.Warp(newPos);
                    // Debug.Log($"{petController.petName}: 물에 진입, NavMesh 활성 유지, 위치 Y={waterSurfaceY}");
                }
            }
        }

        // 물 속 이동 속도 적용
        ApplyWaterSpeed();
    }

    /// <summary>
    /// 물 속 이동 속도 적용
    /// 수생 펫은 빠르게, 육상 펫은 느리게 이동합니다
    /// </summary>
    private void ApplyWaterSpeed()
    {
        // 수생 펫인지 확인
        bool isAquatic = petController.habitat == PetAIProperties.Habitat.Water;

        // NavMesh 에이전트 속도 조정 (모이기 명령 중에는 적용 안함)
        if (petController.agent != null && !petController.State.IsGathering)
        {
            petController.agent.speed = petController.Movement.GetWaterSpeed(isAquatic, petController.personality);
            petController.agent.acceleration = petController.Movement.acceleration *
                (isAquatic ? petController.Movement.aquaticWaterSpeedMultiplier : petController.Movement.waterSpeedMultiplier);
        }

        // 애니메이션 속도 조정
        if (petController.animator != null)
        {
            float animSpeedMult = isAquatic ? petController.Movement.aquaticWaterSpeedMultiplier : petController.Movement.waterSpeedMultiplier;
            petController.animator.speed = animSpeedMult;
        }
    }

    /// <summary>
    /// 물에서 나올 때의 처리
    /// 이동 속도를 원래대로 복구하고 NavMesh를 재활성화합니다
    /// </summary>
    private void OnExitWater()
    {
        // NavMesh 에이전트 재활성화 및 위치 복구
        if (petController.agent != null && !petController.agent.enabled && !petController.State.IsHolding)
        {
            // 지형에서 가장 가까운 NavMesh 위치 찾기
            UnityEngine.AI.NavMeshHit hit;
            Vector3 checkPos = petController.transform.position;
            checkPos.y = positionBeforeWater.y;  // 원래 지형 높이 근처에서 검색
            
            if (UnityEngine.AI.NavMesh.SamplePosition(checkPos, out hit, 5f, UnityEngine.AI.NavMesh.AllAreas))
            {
                petController.agent.enabled = true;
                petController.agent.Warp(hit.position);
                // Debug.Log($"{petController.petName}: NavMesh 재활성화, 지형으로 복귀 Y={hit.position.y}");
            }
        }
        
        // NavMesh 에이전트 속도 원상 복구
        if (petController.agent != null && !petController.State.IsGathering)
        {
            petController.agent.speed = petController.Movement.GetAdjustedWalkSpeed(petController.personality);
            petController.agent.acceleration = petController.Movement.acceleration;
        }

        // 애니메이션 속도 원상 복구
        if (petController.animator != null)
        {
            petController.animator.speed = 1f;
        }
    }

    /// <summary>
    /// 현재 물 상태에 따라 속도 조정
    /// 주로 다른 컨트롤러에서 호출하여 사용
    /// </summary>
    public void AdjustSpeedForWater()
    {
        if (isInWater && petController.agent != null)
        {
            bool isAquatic = petController.habitat == PetAIProperties.Habitat.Water;
            float speedMult = isAquatic ? petController.Movement.aquaticWaterSpeedMultiplier : petController.Movement.waterSpeedMultiplier;
            petController.agent.speed *= speedMult;
        }
    }

    /// <summary>
    /// 현재 물 속에 있는지 여부를 반환하는 프로퍼티
    /// </summary>
    public bool IsInWater => isInWater;

    /// <summary>
    /// 드롭 준비 메서드
    /// PetInputController에서 펫을 물에 떨어뜨리기 전에 호출
    /// wasHolding 플래그를 설정하여 물튀김 효과가 나타나도록 준비
    /// </summary>
    public void PrepareForDrop()
    {
        wasHolding = true;
        // Debug.Log($"{petController.petName}: 물 드롭 준비 완료");
    }

    /// <summary>
    /// 다이빙 시퀀스 시작
    /// DivingActivity에서 호출하여 펫이 깊이 잠수하는 효과를 연출
    /// </summary>
    public void StartDivingSequence(float surfaceY = -1f)
    {
        // 다이빙 상태 설정
        isDiving = true;
        divingStartTime = Time.time;
        divingDepth = petController.waterSinkDepth * 2.5f;  // 일반 수심의 2.5배 깊이로 다이빙

        // 물 표면 높이 업데이트 (전달받은 경우)
        if (surfaceY > -1f)
        {
            waterSurfaceY = surfaceY;
        }
        
        // 루트를 물 표면으로 이동 (아직 물 속이 아닌 경우)
        if (!isInWater && !petController.State.IsHolding)
        {
            Vector3 newPos = petController.transform.position;
            newPos.y = waterSurfaceY;
            petController.transform.position = newPos;
            
            // NavMesh 비활성화
            if (petController.agent != null && petController.agent.enabled)
            {
                petController.agent.enabled = false;
            }
        }

        // 다이빙 시작 시 큰 물튀김 효과
        CreateDivingSplash();

        // 물 상태 강제 설정 (다이빙 중에는 항상 물 속)
        isInWater = true;
        petController.State.UpdateWaterState(true);

        // 즉시 목표 깊이로 설정 (부드러운 전환 없이)
        // 빠르게 잠수하는 효과를 위해 즉시 깊이 적용
        currentDepth = -divingDepth;
        petController.State.SetWaterDepthOffset(currentDepth);

        // Debug.Log($"{petController.petName}: 다이빙 시퀀스 시작! 깊이: {divingDepth}, 수면: {waterSurfaceY}");
    }

    /// <summary>
    /// 다이빙 깊이 업데이트
    /// 시간에 따라 잠수했다가 천천히 부상하는 효과 구현
    /// </summary>
    private void UpdateDivingDepth()
    {
        // 다이빙 시작 후 경과 시간
        float elapsed = Time.time - divingStartTime;

        if (elapsed >= DIVING_DURATION)
        {
            // 3초 경과: 다이빙 종료
            isDiving = false;
            // 이후 UpdateDepthAnimation()에서 일반 수심으로 부드럽게 전환
            // Debug.Log($"{petController.petName}: 다이빙 완료, 일반 수심으로 복귀");
        }
        else
        {
            // 다이빙 중: 시간에 따른 깊이 변화
            if (elapsed < 1f)
            {
                // Debug.Log("UpdateDivingDepth():잠수");
                // 0-1초: 최대 깊이 유지 (잠수 상태)
                currentDepth = -divingDepth;
            }
            else
            {
                // Debug.Log("UpdateDivingDepth():부상");
                // 1-3초: 천천히 부상
                float t = (elapsed - 1f) / 2f;  // 0-1로 정규화
                t = 1f - Mathf.Pow(1f - t, 2f);  // 부드러운 감속 커브
                currentDepth = Mathf.Lerp(-divingDepth, -petController.waterSinkDepth, t);
            }
        }
    }

    /// <summary>
    /// 다이빙 물튀김 효과 생성
    /// 일반 물튀김보다 크고 화려한 효과를 생성합니다
    /// </summary>
    private void CreateDivingSplash()
    {
        if (EnvironmentManager.Instance != null &&
            EnvironmentManager.Instance.waterSplashParticlePrefab != null)
        {
            // 물튀김 위치 설정
            Vector3 splashPosition = transform.position;
            splashPosition.y += 0.5f;

            GameObject splash = Instantiate(
                EnvironmentManager.Instance.waterSplashParticlePrefab,
                splashPosition,
                Quaternion.identity
            );

            // 다이빙 물튀김은 일반 물튀김보다 2배 크게
            // 더 강력한 입수 효과를 연출
            Renderer renderer = petController.GetComponentInChildren<Renderer>();
            if (renderer != null)
            {
                float scale = (renderer.bounds.size.x + renderer.bounds.size.z) / 2f;
                splash.transform.localScale = Vector3.one * scale * 1.5f;  // 2배 크기
            }
            else if (petController.agent != null)
            {
                float scale = petController.agent.radius * 6f;  // 일반 3f의 2배
                splash.transform.localScale = Vector3.one * scale;
            }

            // 4초 후 제거 (일반보다 1초 더 오래 지속)
            Destroy(splash, 4f);

            // 물 속 이동 속도 적용
            ApplyWaterSpeed();

            // Debug.Log($"{petController.petName}: 다이빙 물보라 효과 생성!");
        }
    }
}