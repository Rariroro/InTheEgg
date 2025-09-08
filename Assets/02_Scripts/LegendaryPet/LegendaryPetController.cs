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
    [RequireComponent(typeof(Animator))]
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
            InitializeComponents();
            InitializeTraits();
        }
        
        private void Start()
        {
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
            
            // Animator 설정
            animator = GetComponent<Animator>();
            if (animator == null)
            {
                Debug.LogWarning($"[LegendaryPet] {gameObject.name}에 Animator 컴포넌트가 없습니다");
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
            
            // 애니메이션 업데이트
            if (animator != null)
            {
                animator.SetBool("isWalking", moving);
                animator.SetFloat("moveSpeed", moving ? agent.velocity.magnitude : 0f);
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
            if (animator != null)
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
            
            flyDestination = destination;
            
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
                agent.isStopped = true;
                agent.enabled = false;
            }
            
            // 비행 애니메이션 시작
            if (animator != null && traits.flyAnimIndex > 0)
            {
                animator.SetInteger("State", traits.flyAnimIndex);
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
            while (Vector3.Distance(new Vector3(transform.position.x, 0, transform.position.z), 
                                   new Vector3(flyDestination.x, 0, flyDestination.z)) > 1f)
            {
                // XZ 평면에서 이동
                Vector3 direction = (flyDestination - transform.position).normalized;
                direction.y = 0;
                
                Vector3 newPos = transform.position + direction * flySpeed * Time.deltaTime;
                
                // 부드러운 상하 움직임
                newPos.y = currentFlyHeight + Mathf.Sin(Time.time * bobSpeed) * bobAmount;
                
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
            // 착륙 위치 찾기
            RaycastHit hit;
            Vector3 landingPos = transform.position;
            
            if (Physics.Raycast(transform.position, Vector3.down, out hit, 50f))
            {
                landingPos = hit.point;
            }
            else
            {
                landingPos.y = 0; // 기본 높이
            }
            
            // 하강
            float startHeight = transform.position.y;
            float elapsed = 0f;
            
            while (elapsed < 1f / descendSpeed)
            {
                elapsed += Time.deltaTime;
                float t = elapsed * descendSpeed;
                
                Vector3 pos = transform.position;
                pos.y = Mathf.Lerp(startHeight, landingPos.y, t);
                transform.position = pos;
                
                yield return null;
            }
            
            transform.position = new Vector3(transform.position.x, landingPos.y, transform.position.z);
            
            // NavMeshAgent 재활성화
            if (agent != null)
            {
                agent.enabled = true;
                agent.Warp(transform.position);
                agent.isStopped = false;
            }
            
            // 걷기 애니메이션으로 전환
            if (animator != null && traits.walkAnimIndex > 0)
            {
                animator.SetInteger("State", traits.walkAnimIndex);
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
                // 이동 상태 업데이트
                if (agent != null && agent.enabled && agent.isOnNavMesh)
                {
                    bool shouldBeMoving = agent.hasPath && !agent.isStopped && agent.remainingDistance > agent.stoppingDistance;
                    if (shouldBeMoving != isMoving)
                    {
                        SetMoving(shouldBeMoving);
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