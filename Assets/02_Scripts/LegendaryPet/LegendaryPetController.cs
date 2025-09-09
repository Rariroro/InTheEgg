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
        
        public void PlaySpecialAnimation()
        {
            // AnimatorController 체크 추가
            if (animator != null && animator.runtimeAnimatorController != null)
            {
                // 특별 애니메이션 재생 (펫 타입별로 다른 애니메이션)
                switch (petType)
                {
                    case LegendaryPetType.Dragon:
                    case LegendaryPetType.Phoenix:
                        animator.SetTrigger("Roar");
                        break;
                    case LegendaryPetType.Unicorn:
                    case LegendaryPetType.Pegasus:
                        animator.SetTrigger("Rear");
                        break;
                    default:
                        animator.SetTrigger("Special");
                        break;
                }
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
            Debug.Log($"[LegendaryPet] {petName}: 비행 목적지 설정 - {flyDestination}");
            
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
            
            if (flyingCoroutine != null)
            {
                StopCoroutine(flyingCoroutine);
                flyingCoroutine = null;
            }
            
            StartCoroutine(Land());
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
            
            // 상승
            float startHeight = transform.position.y;
            float targetHeight = startHeight + flyHeight;
            float elapsed = 0f;
            
            while (elapsed < 1f / ascendSpeed)
            {
                elapsed += Time.deltaTime;
                float t = elapsed * ascendSpeed;
                
                Vector3 pos = transform.position;
                pos.y = Mathf.Lerp(startHeight, targetHeight, t);
                transform.position = pos;
                
                yield return null;
            }
            
            currentFlyHeight = targetHeight;
            
            // 목적지로 비행
            float flightTimeout = 0f;
            float maxFlightTime = 30f; // 최대 비행 시간 제한
            
            while (Vector3.Distance(new Vector3(transform.position.x, 0, transform.position.z), 
                                   new Vector3(flyDestination.x, 0, flyDestination.z)) > 1f)
            {
                // 비행 시간 초과 체크
                flightTimeout += Time.deltaTime;
                if (flightTimeout > maxFlightTime)
                {
                    Debug.LogWarning($"[LegendaryPet] {petName}: 비행 시간 초과, 강제 착륙합니다.");
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
                
                // 부드러운 상하 움직임
                newPos.y = currentFlyHeight + Mathf.Sin(Time.time * bobSpeed) * bobAmount;
                
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
            
            // 착륙
            yield return StartCoroutine(Land());
        }
        
        // 착륙 코루틴
        private IEnumerator Land()
        {
            // 착륙 위치를 NavMesh 상에서 먼저 찾기
            Vector3 targetLandingPos = transform.position;
            targetLandingPos.y = 0; // 기본 높이로 초기화
            
            // 현재 위치 아래에서 NavMesh 위치 찾기
            NavMeshHit navHit;
            bool foundValidPosition = false;
            
            // 1차 시도: 현재 위치 바로 아래에서 NavMesh 찾기
            if (NavMesh.SamplePosition(new Vector3(transform.position.x, 0, transform.position.z), out navHit, 20f, NavMesh.AllAreas))
            {
                targetLandingPos = navHit.position;
                foundValidPosition = true;
                Debug.Log($"[LegendaryPet] {petName}: 착륙 위치 확보 - {targetLandingPos}");
            }
            // 2차 시도: 더 넓은 범위에서 찾기
            else if (NavMesh.SamplePosition(transform.position, out navHit, 50f, NavMesh.AllAreas))
            {
                targetLandingPos = navHit.position;
                foundValidPosition = true;
                Debug.LogWarning($"[LegendaryPet] {petName}: 대체 착륙 위치 확보 - {targetLandingPos}");
            }
            // 3차 시도: 스폰 위치 근처에서 찾기
            else
            {
                Vector3 spawnPos = GameObject.Find("SpawnPoint")?.transform.position ?? Vector3.zero;
                if (NavMesh.SamplePosition(spawnPos, out navHit, 100f, NavMesh.AllAreas))
                {
                    targetLandingPos = navHit.position;
                    foundValidPosition = true;
                    Debug.LogWarning($"[LegendaryPet] {petName}: 스폰 위치 근처로 착륙 - {targetLandingPos}");
                }
            }
            
            if (!foundValidPosition)
            {
                Debug.LogError($"[LegendaryPet] {petName}: 유효한 착륙 위치를 찾을 수 없습니다!");
                // 비상 착륙: 원점으로
                targetLandingPos = Vector3.zero;
            }
            
            // 지면 높이 확인을 위한 Raycast
            RaycastHit groundHit;
            if (Physics.Raycast(new Vector3(targetLandingPos.x, 50f, targetLandingPos.z), Vector3.down, out groundHit, 100f))
            {
                // NavMesh 높이와 실제 지면 높이 중 더 높은 것 선택 (땅 아래로 떨어지는 것 방지)
                targetLandingPos.y = Mathf.Max(targetLandingPos.y, groundHit.point.y);
            }
            
            // 하강 애니메이션
            float startHeight = transform.position.y;
            float elapsed = 0f;
            Vector3 startXZ = new Vector3(transform.position.x, 0, transform.position.z);
            Vector3 targetXZ = new Vector3(targetLandingPos.x, 0, targetLandingPos.z);
            
            while (elapsed < 1f / descendSpeed)
            {
                elapsed += Time.deltaTime;
                float t = elapsed * descendSpeed;
                
                // 높이는 부드럽게 하강
                float currentHeight = Mathf.Lerp(startHeight, targetLandingPos.y, t);
                
                // XZ 위치도 부드럽게 이동 (착륙 위치가 다른 경우)
                Vector3 currentXZ = Vector3.Lerp(startXZ, targetXZ, t);
                
                transform.position = new Vector3(currentXZ.x, currentHeight, currentXZ.z);
                
                yield return null;
            }
            
            // 최종 위치 설정
            transform.position = targetLandingPos;
            
            // NavMeshAgent 재활성화
            if (agent != null)
            {
                agent.enabled = true;
                
                // agent가 완전히 활성화되도록 프레임 대기
                yield return null;
                
                // 최종 위치에서 다시 한번 NavMesh 확인
                if (NavMesh.SamplePosition(transform.position, out navHit, 10f, NavMesh.AllAreas))
                {
                    transform.position = navHit.position;
                    
                    if (agent.isOnNavMesh)
                    {
                        agent.Warp(navHit.position);
                        agent.isStopped = false;
                        Debug.Log($"[LegendaryPet] {petName}: 성공적으로 착륙했습니다.");
                    }
                    else
                    {
                        Debug.LogError($"[LegendaryPet] {petName}: NavMesh에 배치 실패!");
                        // 비상 처리: 에이전트 비활성화 상태 유지
                        agent.enabled = false;
                    }
                }
                else
                {
                    Debug.LogError($"[LegendaryPet] {petName}: 착륙 후 NavMesh를 찾을 수 없음!");
                    agent.enabled = false;
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