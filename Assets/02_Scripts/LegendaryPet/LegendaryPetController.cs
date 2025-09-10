using System.Collections;
using UnityEngine;
using UnityEngine.AI;

namespace LegendaryPet
{
    /// <summary>
    /// 레전드 펫의 기본 컨트롤러
    /// 일반 펫과 독립적으로 작동하는 단순화된 시스템
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    // Animator는 자식 오브젝트에 있으므로 RequireComponent 제거
    public class LegendaryPetController : MonoBehaviour
    {
        [Header("레전드 펫 설정")]
        [SerializeField] private LegendaryPetType petType = LegendaryPetType.Dragon;
        [SerializeField] private string petName = "Legendary Pet";
        [SerializeField] private LegendaryPetTraits traits;
        
        [Header("컴포넌트 참조")]
        private NavMeshAgent agent;
        private Animator animator;
        private LegendaryPetAI ai;
        
        [Header("상태")]
        [SerializeField] private bool isActive = true;
        [SerializeField] private bool isMoving = false;
        [SerializeField] private bool isFlying = false;
        
        [Header("비행 설정")]
        [SerializeField] private float flyHeight = 5f;           // 비행 높이
        [SerializeField] private float ascendSpeed = 2f;         // 상승 속도
        [SerializeField] private float descendSpeed = 3f;        // 하강 속도
        [SerializeField] private float flySpeed = 8f;            // 비행 속도
        [SerializeField] private float bobAmount = 0.5f;         // 상하 움직임 크기
        [SerializeField] private float bobSpeed = 2f;            // 상하 움직임 속도
        
        private Vector3 flyDestination;
        private float currentFlyHeight;
        private float startFlyHeight;      // 시작 비행 높이
        private float targetFlyHeight;     // 목표 비행 높이
        private Coroutine flyingCoroutine;
        
        [Header("시각 효과")]
        [SerializeField] private ParticleSystem glowEffect;
        [SerializeField] private ParticleSystem auraEffect;
        [SerializeField] private Light petLight;
        
        // 프로퍼티
        public LegendaryPetType PetType => petType;
        public string PetName => petName;
        public LegendaryPetTraits Traits => traits;
        public bool IsActive => isActive;
        public bool IsMoving => isMoving;
        public bool IsFlying => isFlying;
        public NavMeshAgent Agent => agent;
        public Animator Animator => animator;
        
        private void Awake()
        {
            // traits만 먼저 초기화 (NavMeshAgent 속도 설정용)
            InitializeTraits();
        }
        
        private void Start()
        {
            // Components는 Start에서 초기화
            InitializeComponents();
            StartCoroutine(DelayedStart());
        }
        
        private IEnumerator DelayedStart()
        {
            // NavMesh 배치 대기
            yield return new WaitForSeconds(0.5f);
            
            if (agent != null && agent.isOnNavMesh)
            {
                ai = GetComponent<LegendaryPetAI>();
                if (ai == null)
                {
                    ai = gameObject.AddComponent<LegendaryPetAI>();
                }
                
                // 레전드 펫 매니저에 등록
                if (LegendaryPetManager.Instance != null)
                {
                    LegendaryPetManager.Instance.RegisterLegendaryPet(this);
                }
                
                SetupVisualEffects();
                Debug.Log($"[LegendaryPet] {petName} ({petType}) 초기화 완료");
            }
            else
            {
                Debug.LogWarning($"[LegendaryPet] {petName} NavMesh 배치 실패");
            }
        }
        
        private void InitializeComponents()
        {
            // NavMeshAgent 설정
            agent = GetComponent<NavMeshAgent>();
            if (agent != null)
            {
                agent.speed = traits.moveSpeed;
                agent.angularSpeed = 360f;
                agent.acceleration = 8f;
                agent.stoppingDistance = 1f;
                agent.autoBraking = true;
                agent.updateRotation = true;
                agent.updatePosition = true;
                agent.updateUpAxis = false;
            }
            
            // Animator 설정 (자식 오브젝트에서 검색)
            animator = GetComponentInChildren<Animator>(true);
            if (animator != null && !animator.enabled)
            {
                animator.enabled = true;
            }
        }
        
        
        private void InitializeTraits()
        {
            // 펫 타입에 따른 기본 특성 설정
            traits = LegendaryPetTraits.GetDefault(petType);
            
            // NavMeshAgent 속도 업데이트
            if (agent != null)
            {
                agent.speed = traits.moveSpeed;
            }
        }
        
        private void SetupVisualEffects()
        {
            // 발광 효과 설정
            if (petLight == null)
            {
                GameObject lightObj = new GameObject("PetLight");
                lightObj.transform.SetParent(transform);
                lightObj.transform.localPosition = Vector3.up * 1f;
                petLight = lightObj.AddComponent<Light>();
                petLight.type = LightType.Point;
                petLight.intensity = traits.glowIntensity;
                petLight.range = 5f;
                petLight.color = GetPetColor();
            }
            
            // 파티클 효과는 프리팹에서 설정하도록 남겨둠
            if (glowEffect != null)
            {
                var main = glowEffect.main;
                main.maxParticles = Mathf.RoundToInt(traits.particleAmount);
            }
            
            if (traits.hasAura && auraEffect != null)
            {
                auraEffect.gameObject.SetActive(true);
            }
        }
        
        private Color GetPetColor()
        {
            // 펫 타입별 색상
            switch (petType)
            {
                case LegendaryPetType.Dragon:
                    return new Color(1f, 0.5f, 0f); // 주황색
                case LegendaryPetType.Phoenix:
                    return new Color(1f, 0.2f, 0f); // 붉은색
                case LegendaryPetType.Unicorn:
                    return new Color(0.8f, 0.8f, 1f); // 은백색
                case LegendaryPetType.Griffin:
                    return new Color(1f, 0.9f, 0.5f); // 금색
                case LegendaryPetType.Pegasus:
                    return new Color(0.7f, 0.9f, 1f); // 하늘색
                case LegendaryPetType.Cerberus:
                    return new Color(0.5f, 0f, 0f); // 암적색
                case LegendaryPetType.Sphinx:
                    return new Color(1f, 0.8f, 0.4f); // 모래색
                case LegendaryPetType.Hydra:
                    return new Color(0f, 0.5f, 0f); // 녹색
                default:
                    return Color.white;
            }
        }
        
        // 지형 높이를 가져오는 헬퍼 메서드
        private float GetGroundHeight(Vector3 position)
        {
            RaycastHit hit;
            // 위에서 아래로 레이캐스트를 쏴서 지형 높이 확인
            if (Physics.Raycast(new Vector3(position.x, 100f, position.z), Vector3.down, out hit, 200f))
            {
                return hit.point.y;
            }
            // 레이캐스트가 실패하면 NavMesh 높이 사용
            NavMeshHit navHit;
            if (NavMesh.SamplePosition(position, out navHit, 10f, NavMesh.AllAreas))
            {
                return navHit.position.y;
            }
            return position.y;
        }
        
        public void SetActive(bool active)
        {
            isActive = active;
            if (ai != null)
            {
                ai.enabled = active;
            }
            
            if (!active && agent != null && agent.enabled)
            {
                agent.isStopped = true;
                SetMoving(false);
            }
        }
        
        public void SetMoving(bool moving)
        {
            isMoving = moving;
            
            // 애니메이션 업데이트 (AnimatorController가 있을 때만)
            if (animator != null && animator.runtimeAnimatorController != null)
            {
                if (moving)
                {
                    // 속도에 따라 걷기/뛰기 구분
                    if (agent != null && agent.velocity.magnitude > traits.moveSpeed * 0.7f)
                    {
                        animator.SetInteger("animation", traits.runAnimIndex);
                    }
                    else
                    {
                        animator.SetInteger("animation", traits.walkAnimIndex);
                    }
                }
                else
                {
                    animator.SetInteger("animation", 1); // Idle (1번이 Idle)
                }
            }
        }
        
        public void MoveTo(Vector3 destination)
        {
            if (!isActive || agent == null || !agent.isOnNavMesh) return;
            
            agent.SetDestination(destination);
            agent.isStopped = false;
            SetMoving(true);
        }
        
        public void StopMoving()
        {
            if (agent != null && agent.enabled && agent.isOnNavMesh)
            {
                agent.isStopped = true;
                agent.ResetPath();
                SetMoving(false);
            }
        }
        
        // 비행 시작
        public bool StartFlying(Vector3 destination)
        {
            // 유니콘과 드래곤만 비행 가능
            if (!traits.canFly || !(petType == LegendaryPetType.Dragon || petType == LegendaryPetType.Unicorn))
            {
                Debug.Log($"[LegendaryPet] {petName}은(는) 비행할 수 없습니다.");
                return false;
            }
            
            if (isFlying)
            {
                Debug.Log($"[LegendaryPet] {petName}은(는) 이미 비행 중입니다.");
                return false;
            }
            
            // 목적지가 NavMesh 범위 내에 있는지 사전 검증
            NavMeshHit navHit;
            if (!NavMesh.SamplePosition(destination, out navHit, 30f, NavMesh.AllAreas))
            {
                Debug.LogWarning($"[LegendaryPet] {petName}: 비행 목적지가 NavMesh 범위를 벗어났습니다. 가장 가까운 유효한 위치로 조정합니다.");
                
                // 더 넓은 범위에서 재시도
                if (!NavMesh.SamplePosition(destination, out navHit, 50f, NavMesh.AllAreas))
                {
                    Debug.LogError($"[LegendaryPet] {petName}: 유효한 비행 목적지를 찾을 수 없습니다.");
                    return false;
                }
            }
            
            flyDestination = navHit.position;
            
            // 시작점과 목적지의 지형 높이 계산
            float startGroundHeight = GetGroundHeight(transform.position);
            float destinationGroundHeight = GetGroundHeight(flyDestination);
            
            // 시작과 끝 중 더 높은 지형 기준으로 비행 높이 설정
            float maxGroundHeight = Mathf.Max(startGroundHeight, destinationGroundHeight);
            startFlyHeight = startGroundHeight + flyHeight;
            targetFlyHeight = maxGroundHeight + flyHeight;
            
            Debug.Log($"[LegendaryPet] {petName}: 비행 목적지 설정 - {flyDestination}, 시작 높이: {startFlyHeight}, 목표 높이: {targetFlyHeight}");
            
            if (flyingCoroutine != null)
            {
                StopCoroutine(flyingCoroutine);
            }
            

            
            flyingCoroutine = StartCoroutine(FlyToDestination());
            return true;
        }
        
        // 비행 중지
        public void StopFlying()
        {
            if (!isFlying) return;
            
            // 착륙 플래그만 설정하고 FlyToDestination이 자연스럽게 종료되도록 함
            // 목적지를 현재 위치로 설정하여 즉시 착륙 유도
            flyDestination = new Vector3(transform.position.x, 0, transform.position.z);
            Debug.Log($"[LegendaryPet] {petName}: 비행 중지 요청 - 현재 위치에서 착륙");
        }
        
        // 비행 코루틴
        private IEnumerator FlyToDestination()
        {
            isFlying = true;
            
            // NavMeshAgent 비활성화
            if (agent != null && agent.enabled)
            {
                if (agent.isOnNavMesh)
                {
                    agent.isStopped = true;
                }
                agent.enabled = false;
            }
            
            // 비행 애니메이션 시작 (AnimatorController가 있을 때만)
            if (animator != null && animator.runtimeAnimatorController != null && traits.flyAnimIndex > 0)
            {
                animator.SetInteger("animation", traits.flyAnimIndex);
            }
            
            // 상승 (시작 비행 높이로)
            float startHeight = transform.position.y;
            float elapsed = 0f;
            
            while (elapsed < 1f / ascendSpeed)
            {
                elapsed += Time.deltaTime;
                float t = elapsed * ascendSpeed;
                
                Vector3 pos = transform.position;
                pos.y = Mathf.Lerp(startHeight, startFlyHeight, t);
                transform.position = pos;
                
                yield return null;
            }
            
            currentFlyHeight = startFlyHeight;
            
            // 목적지로 비행
            float flightTimeout = 0f;
            Vector3 startPosition = transform.position;
            float totalDistance = Vector3.Distance(new Vector3(startPosition.x, 0, startPosition.z), 
                                                  new Vector3(flyDestination.x, 0, flyDestination.z));
            
            // 거리에 따른 동적 비행 시간 계산 (거리/속도 + 여유시간)
            float expectedFlightTime = totalDistance / flySpeed;  // 예상 비행 시간
            float maxFlightTime = expectedFlightTime + 10f;       // 10초 여유 추가
            
            Debug.Log($"[LegendaryPet] {petName}: 비행 시작 - 거리: {totalDistance:F1}, 예상 시간: {expectedFlightTime:F1}초, 제한 시간: {maxFlightTime:F1}초");
            
            while (Vector3.Distance(new Vector3(transform.position.x, 0, transform.position.z), 
                                   new Vector3(flyDestination.x, 0, flyDestination.z)) > 1f)
            {
                // 비행 시간 초과 체크
                flightTimeout += Time.deltaTime;
                if (flightTimeout > maxFlightTime)
                {
                    Debug.LogWarning($"[LegendaryPet] {petName}: 비행 시간 초과 ({flightTimeout:F1}/{maxFlightTime:F1}초), 강제 착륙합니다.");
                    break;
                }
                
                // 목적지 유효성 재검증 (주기적으로)
                if (flightTimeout % 5f < Time.deltaTime) // 5초마다 체크
                {
                    NavMeshHit navHit;
                    if (!NavMesh.SamplePosition(flyDestination, out navHit, 30f, NavMesh.AllAreas))
                    {
                        Debug.LogWarning($"[LegendaryPet] {petName}: 목적지가 더 이상 유효하지 않습니다. 착륙합니다.");
                        break;
                    }
                }
                
                // XZ 평면에서 이동
                Vector3 direction = (flyDestination - transform.position).normalized;
                direction.y = 0;
                
                // 방향이 유효하지 않으면 중단
                if (direction.magnitude < 0.01f)
                {
                    Debug.Log($"[LegendaryPet] {petName}: 목적지에 도달했습니다.");
                    break;
                }
                
                Vector3 newPos = transform.position + direction * flySpeed * Time.deltaTime;
                
                // 진행도 계산 (0~1)
                float currentDistance = Vector3.Distance(new Vector3(transform.position.x, 0, transform.position.z),
                                                        new Vector3(flyDestination.x, 0, flyDestination.z));
                float progress = 1f - (currentDistance / totalDistance);
                progress = Mathf.Clamp01(progress);
                
                // 높이를 진행도에 따라 부드럽게 보간
                currentFlyHeight = Mathf.Lerp(startFlyHeight, targetFlyHeight, progress);
                
                // 착륙이 가까워지면 상하 움직임 감소 (착륙 준비)
                float remainingDistance = Vector3.Distance(new Vector3(transform.position.x, 0, transform.position.z),
                                                          new Vector3(flyDestination.x, 0, flyDestination.z));
                float bobMultiplier = Mathf.Clamp01(remainingDistance / 10f); // 10미터 이내에서 감소 시작
                
                // 부드러운 상하 움직임 추가 (착륙 가까이에서는 감소)
                newPos.y = currentFlyHeight + Mathf.Sin(Time.time * bobSpeed) * bobAmount * bobMultiplier;
                
                // 맵 경계 체크 (예시: -200 ~ 200 범위)
                float mapBoundary = 200f;
                newPos.x = Mathf.Clamp(newPos.x, -mapBoundary, mapBoundary);
                newPos.z = Mathf.Clamp(newPos.z, -mapBoundary, mapBoundary);
                
                transform.position = newPos;
                
                // 회전
                if (direction != Vector3.zero)
                {
                    Quaternion targetRotation = Quaternion.LookRotation(direction);
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 5f);
                }
                
                yield return null;
            }
            
            // 착륙 전 높이 정규화 (Sin 파동 제거)
            Vector3 normalizedPos = transform.position;
            normalizedPos.y = currentFlyHeight; // Sin 파동 없는 실제 비행 높이
            transform.position = normalizedPos;
            Debug.Log($"[LegendaryPet] {petName}: 착륙 준비 - 정규화된 높이: {normalizedPos.y:F2}");
            
            // 착륙
            yield return StartCoroutine(Land());
        }
        
        // 착륙 코루틴
        private IEnumerator Land()
        {
            // Sin 파동 효과 제거한 실제 현재 높이 계산
            Vector3 currentPos = transform.position;
            // bobAmount 효과를 제거하여 실제 비행 높이 복원
            float actualHeight = currentPos.y;
            
            Debug.Log($"[LegendaryPet] {petName}: 착륙 시작 - 현재 높이: {actualHeight:F2}");
            
            Vector3 targetLandingPos = currentPos;
            
            // 현재 위치에서 바로 아래로 Raycast를 쏴서 실제 지면 찾기
            RaycastHit groundHit;
            bool foundGround = false;
            
            // 현재 위치에서 바로 아래로 검색 (위로 올라가지 않음)
            if (Physics.Raycast(new Vector3(currentPos.x, actualHeight, currentPos.z), Vector3.down, out groundHit, actualHeight + 100f))
            {
                targetLandingPos = groundHit.point;
                foundGround = true;
                Debug.Log($"[LegendaryPet] {petName}: 지면 감지 - 목표 높이: {groundHit.point.y:F2}, 거리: {(actualHeight - groundHit.point.y):F2}");
            }
            
            // Raycast가 실패하면 NavMesh에서 찾기
            NavMeshHit navHit;
            if (!foundGround)
            {
                // 현재 XZ 위치에서 NavMesh 찾기 (높이는 무시)
                if (NavMesh.SamplePosition(new Vector3(currentPos.x, 0, currentPos.z), out navHit, 30f, NavMesh.AllAreas))
                {
                    targetLandingPos = navHit.position;
                    Debug.Log($"[LegendaryPet] {petName}: NavMesh 위치 사용 - {targetLandingPos}");
                }
                else
                {
                    // 더 넓은 범위에서 재시도
                    if (NavMesh.SamplePosition(currentPos, out navHit, 50f, NavMesh.AllAreas))
                    {
                        targetLandingPos = navHit.position;
                        Debug.LogWarning($"[LegendaryPet] {petName}: 대체 착륙 위치 - {targetLandingPos}");
                    }
                    else
                    {
                        // 최후의 수단: 원점 근처
                        targetLandingPos = Vector3.zero;
                        Debug.LogError($"[LegendaryPet] {petName}: 비상 착륙!");
                    }
                }
            }
            
            // NavMesh 위치와 지면 높이 중 더 높은 값 사용 (땅 속에 빠지지 않도록)
            if (NavMesh.SamplePosition(targetLandingPos, out navHit, 10f, NavMesh.AllAreas))
            {
                if (navHit.position.y > targetLandingPos.y)
                {
                    targetLandingPos.y = navHit.position.y;
                }
            }
            
            // 부드러운 하강 애니메이션
            float startHeight = actualHeight; // transform.position.y 대신 actualHeight 사용
            float targetHeight = targetLandingPos.y;
            Vector3 startPos = new Vector3(currentPos.x, actualHeight, currentPos.z); // 정확한 시작 위치
            Vector3 endPos = targetLandingPos;
            
            // 하강 시간 계산 (거리에 비례)
            float descendDistance = Mathf.Abs(startHeight - targetHeight);
            float descendTime = Mathf.Max(0.5f, descendDistance / (descendSpeed * 3f)); // 최소 0.5초
            
            Debug.Log($"[LegendaryPet] {petName}: 하강 시작 - 시작 높이: {startHeight:F2}, 목표 높이: {targetHeight:F2}, 예상 시간: {descendTime:F2}초");
            
            float elapsed = 0f;
            
            while (elapsed < descendTime)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / descendTime;
                
                // Ease-out 곡선 적용 (착지 직전 감속)
                float easedT = 1f - Mathf.Pow(1f - t, 3f);
                
                // 위치 보간 (XYZ 모두 함께)
                Vector3 currentPosition = Vector3.Lerp(startPos, endPos, easedT);
                
                // 착지 직전에 다시 한번 지면 체크 (동적 지형 대응)
                if (t > 0.8f) // 80% 이상 진행됐을 때
                {
                    if (Physics.Raycast(new Vector3(currentPosition.x, currentPosition.y + 1f, currentPosition.z), 
                                       Vector3.down, out groundHit, 5f))
                    {
                        // 실시간으로 착지 높이 조정
                        if (groundHit.point.y > targetHeight)
                        {
                            Debug.Log($"[LegendaryPet] {petName}: 지면 높이 재조정 - {targetHeight:F2} → {groundHit.point.y:F2}");
                            targetHeight = groundHit.point.y;
                            endPos.y = targetHeight;
                        }
                    }
                }
                
                transform.position = currentPosition;
                
                // 디버그용 현재 높이 출력 (매 10프레임마다)
                if (Time.frameCount % 10 == 0)
                {
                    Debug.Log($"[LegendaryPet] {petName}: 하강 중 - 진행도: {(t*100):F0}%, 현재 높이: {currentPosition.y:F2}");
                }
                
                yield return null;
            }
            
            // 부드러운 하강이 끝났으므로 추가 위치 조정 없음
            // transform.position = targetLandingPos; // 이 줄 제거!
            
            // NavMeshAgent 재활성화
            if (agent != null)
            {
                // 활성화 전에 위치 확인
                if (NavMesh.SamplePosition(transform.position, out navHit, 5f, NavMesh.AllAreas))
                {
                    // 너무 큰 차이가 있을 때만 조정
                    if (Vector3.Distance(transform.position, navHit.position) > 0.5f)
                    {
                        transform.position = navHit.position;
                    }
                }
                
                agent.enabled = true;
                
                // agent가 완전히 활성화되도록 충분한 대기
                yield return new WaitForSeconds(0.1f);
                
                if (agent.enabled && agent.isOnNavMesh)
                {
                    agent.isStopped = false;
                    Debug.Log($"[LegendaryPet] {petName}: 착륙 완료!");
                }
                else
                {
                    Debug.LogWarning($"[LegendaryPet] {petName}: NavMesh 재배치 실패, 재시도...");
                    // 한 번 더 시도
                    yield return new WaitForSeconds(0.1f);
                    if (!agent.isOnNavMesh)
                    {
                        agent.enabled = false;
                        yield return null;
                        agent.enabled = true;
                    }
                }
            }
            
            // 걷기 애니메이션으로 전환 (AnimatorController가 있을 때만)
            if (animator != null && animator.runtimeAnimatorController != null && traits.walkAnimIndex > 0)
            {
                animator.SetInteger("animation", traits.walkAnimIndex);
            }
            
            isFlying = false;
            flyingCoroutine = null;
        }
        
        private void Update()
        {
            if (!isActive) return;
            
            // 비행 중이면 이동 상태 업데이트 건너뛰기
            if (!isFlying)
            {
                // 이동 상태 업데이트 - 실제 속도 기반으로 판단
                if (agent != null && agent.enabled && agent.isOnNavMesh)
                {
                    float currentSpeed = agent.velocity.magnitude;
                    bool actuallyMoving = currentSpeed > 0.1f; // 0.1 이하는 정지 상태로 간주
                    
                    // 실제 움직임과 isMoving 상태가 다르면 업데이트
                    if (actuallyMoving != isMoving)
                    {
                        SetMoving(actuallyMoving);
                    }
                    // 애니메이션 직접 업데이트 (속도 기반)
                    else if (animator != null && animator.runtimeAnimatorController != null)
                    {
                        if (actuallyMoving)
                        {
                            // 속도에 따라 걷기/뛰기 애니메이션 선택
                            if (currentSpeed > traits.moveSpeed * 0.7f)
                            {
                                if (animator.GetInteger("animation") != traits.runAnimIndex)
                                    animator.SetInteger("animation", traits.runAnimIndex);
                            }
                            else
                            {
                                if (animator.GetInteger("animation") != traits.walkAnimIndex)
                                    animator.SetInteger("animation", traits.walkAnimIndex);
                            }
                        }
                        else
                        {
                            // 정지 상태 - Idle 애니메이션
                            if (animator.GetInteger("animation") != 1)
                                animator.SetInteger("animation", 1); // Idle
                        }
                    }
                }
            }
            
            // 발광 효과 업데이트 (부드러운 펄스 효과)
            if (petLight != null)
            {
                float pulse = Mathf.Sin(Time.time * 2f) * 0.2f + 1f;
                petLight.intensity = traits.glowIntensity * pulse;
            }
        }
        
        private void OnDestroy()
        {
            // 매니저에서 등록 해제
            if (LegendaryPetManager.Instance != null)
            {
                LegendaryPetManager.Instance.UnregisterLegendaryPet(this);
            }
        }
        
        // 디버그용
        private void OnDrawGizmosSelected()
        {
            if (agent != null && agent.hasPath)
            {
                Gizmos.color = GetPetColor();
                var path = agent.path.corners;
                for (int i = 0; i < path.Length - 1; i++)
                {
                    Gizmos.DrawLine(path[i], path[i + 1]);
                }
            }
        }
    }
}