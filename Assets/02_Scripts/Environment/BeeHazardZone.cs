using UnityEngine;
using System.Collections;

/// <summary>
/// 꿀 지역에서 벌떼가 펫을 공격하는 위험 지역을 관리하는 컴포넌트
/// FeedingArea와 함께 사용되어 꿀을 먹으려는 펫에게 벌 공격 이벤트를 발생시킵니다.
/// </summary>
[RequireComponent(typeof(Collider))]
public class BeeHazardZone : MonoBehaviour
{
    [Header("벌 공격 설정")]
    [Tooltip("벌떼 파티클 시스템 프리팹")]
    public GameObject beeSwarmParticlePrefab;
    
    [Tooltip("벌 공격 사운드")]
    public AudioClip beeAttackSound;
    
    [Tooltip("벌 공격이 시작되기까지의 지연 시간")]
    [Range(0.5f, 3f)]
    public float attackDelay = 1.5f;
    
    [Tooltip("벌 공격 지속 시간")]
    [Range(5f, 30f)]
    public float attackDuration = 15f;  // 15초로 증가
    
    [Tooltip("벌이 펫을 쫓아가는 최대 거리")]
    [Range(10f, 50f)]
    public float maxChaseDistance = 30f;  // 30m로 증가
    
    [Tooltip("벌이 포기하고 돌아가는 거리")]
    [Range(30f, 100f)]
    public float giveUpDistance = 50f;  // 50m로 증가
    
    [Tooltip("벌떼 파티클이 펫을 따라다니는 오프셋")]
    public Vector3 swarmOffset = new Vector3(0, 2f, 0);
    
    private FeedingArea feedingArea;
    private AudioSource audioSource;
    private System.Collections.Generic.HashSet<GameObject> loggedObjects = new System.Collections.Generic.HashSet<GameObject>();
    
    // 중복 실행 방지를 위한 추가 필드
    private System.Collections.Generic.HashSet<PetController> petsBeingAttacked = new System.Collections.Generic.HashSet<PetController>();
    
    private void Awake()
    {
        Debug.Log($"[BeeHazardZone] Awake 호출됨 - {gameObject.name}");
        
        // FeedingArea 컴포넌트 확인
        feedingArea = GetComponent<FeedingArea>();
        if (feedingArea == null)
        {
            Debug.LogError($"[BeeHazardZone] {gameObject.name}에 FeedingArea 컴포넌트가 없습니다!");
            return;
        }
        
        Debug.Log($"[BeeHazardZone] FeedingArea 발견. foodType: {feedingArea.foodType}");
        
        // 꿀 타입인지 확인
        if ((feedingArea.foodType & PetAIProperties.DietaryFlags.Honey) == 0)
        {
            Debug.LogWarning($"[BeeHazardZone] {gameObject.name}의 FeedingArea가 Honey 타입이 아닙니다. 현재 타입: {feedingArea.foodType}");
            // enabled = false;  // 일단 비활성화하지 않고 진행
            // return;
        }
        else
        {
            Debug.Log($"[BeeHazardZone] Honey 타입 확인됨!");
        }
        
        // AudioSource 설정
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
        audioSource.spatialBlend = 1f; // 3D 사운드
        audioSource.maxDistance = 20f;
        
        // Collider가 Trigger인지 확인
        Collider col = GetComponent<Collider>();
        if (col == null)
        {
            Debug.LogError($"[BeeHazardZone] {gameObject.name}에 Collider가 없습니다!");
            return;
        }
        
        if (!col.isTrigger)
        {
            Debug.Log($"[BeeHazardZone] Collider를 Trigger로 설정합니다.");
            col.isTrigger = true;
        }
        
        Debug.Log($"[BeeHazardZone] 초기화 완료! Collider 타입: {col.GetType().Name}, Bounds: {col.bounds.size}");
    }
    
    private void Start()
    {
        Debug.Log($"[BeeHazardZone] Start 호출됨 - {gameObject.name}, 활성화 상태: {enabled}");
        Debug.Log($"[BeeHazardZone] 내 Layer: {LayerMask.LayerToName(gameObject.layer)}, Tag: {gameObject.tag}");
        
        // Collider 정보 출력
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            Debug.Log($"[BeeHazardZone] Collider 정보 - Type: {col.GetType().Name}, IsTrigger: {col.isTrigger}, Enabled: {col.enabled}");
        }
    }
    
    private void OnTriggerEnter(Collider other)
    {
        // 펫이 아니면 무시
        if (!other.TryGetComponent<PetController>(out var pet))
            return;
            
        Debug.Log($"[BeeHazardZone] 펫 감지: {pet.petName}");
        
        // 이미 벌 공격을 받고 있거나 처리 중이면 무시
        if (pet.isBeingAttackedByBees || petsBeingAttacked.Contains(pet))
            return;
        
        // 트리거에 들어온 펫을 추적 시작
        StartCoroutine(MonitorPetInHoneyZone(pet));
    }
    
    // OnTriggerStay는 제거하고 MonitorPetInHoneyZone에서 처리
    
    // PetFeedingController에서 직접 호출하는 메서드
    public void OnPetStartedEating(PetController pet)
    {
        if (pet == null || pet.isBeingAttackedByBees || petsBeingAttacked.Contains(pet))
            return;
            
        Debug.Log($"[BeeHazardZone] {pet.petName}이(가) 꿀을 먹기 시작함! 즉시 벌 공격!");
        
        // 중복 방지 체크 후 즉시 벌 공격 시작
        if (!petsBeingAttacked.Contains(pet))
        {
            petsBeingAttacked.Add(pet);
            StartCoroutine(TriggerBeeAttack(pet));
        }
    }
    
    private IEnumerator MonitorPetInHoneyZone(PetController pet)
    {
        float monitorTime = 0f;
        PetFeedingController feedingController = pet.GetComponent<PetFeedingController>();
        
        while (pet != null && !pet.isBeingAttackedByBees && monitorTime < 30f)
        {
            if (feedingController != null && feedingController.IsEatingOrSeeking())
            {
                Debug.Log($"[BeeHazardZone] {pet.petName}이(가) 꿀을 먹기 시작함! 벌들이 화났다!");
                
                // 중복 방지 체크 후 벌 공격 시작
                if (!petsBeingAttacked.Contains(pet))
                {
                    petsBeingAttacked.Add(pet);
                    StartCoroutine(TriggerBeeAttack(pet));
                }
                yield break;
            }
            
            monitorTime += 0.2f;
            yield return new WaitForSeconds(0.2f);
        }
    }
    
    private IEnumerator TriggerBeeAttack(PetController pet)
    {
        // 공격 지연
        yield return new WaitForSeconds(attackDelay);
        
        // 펫이 여전히 범위 내에 있는지 확인
        if (pet == null || Vector3.Distance(transform.position, pet.transform.position) > 10f)
            yield break;
            
        Debug.Log($"[BeeHazardZone] {pet.petName}에게 벌 공격 시작!");
        
        // ★ [Phase 4] PetState를 통한 상태 업데이트
        pet.State.SetBeeAttackState(true, transform.position);
        
        // 벌떼 파티클 생성
        GameObject beeSwarm = null;
        if (beeSwarmParticlePrefab != null)
        {
            beeSwarm = Instantiate(beeSwarmParticlePrefab, pet.transform.position + swarmOffset, Quaternion.identity);
            Debug.Log($"[BeeHazardZone] 벌떼 파티클 생성됨: {beeSwarm.name} at {beeSwarm.transform.position}");
        }
        else
        {
            Debug.LogWarning("[BeeHazardZone] beeSwarmParticlePrefab이 할당되지 않음!");
        }
        
        // 벌 공격 사운드 재생
        if (beeAttackSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(beeAttackSound);
        }
        
        // 벌이 펫을 쫓아가면서 공격
        float elapsedTime = 0f;
        Vector3 hivePosition = transform.position; // 벌집 위치
        
        while (pet != null && pet.isBeingAttackedByBees)
        {
            // 펫과 벌집 사이의 거리 계산
            float distanceFromHive = Vector3.Distance(pet.transform.position, hivePosition);
            
            // 벌떼 위치 업데이트
            if (beeSwarm != null)
            {
                Vector3 targetPos = pet.transform.position + swarmOffset;
                beeSwarm.transform.position = targetPos;
                
                // 디버그: 5초마다 상태 로그
                if (Mathf.FloorToInt(elapsedTime / 5f) != Mathf.FloorToInt((elapsedTime - Time.deltaTime) / 5f))
                {
                    Debug.Log($"[BeeHazardZone] 벌 공격 진행중 - 경과시간: {elapsedTime:F1}초, 거리: {distanceFromHive:F1}m");
                }
            }
            else if (elapsedTime < 0.1f) // 처음에만 경고
            {
                Debug.LogWarning("[BeeHazardZone] beeSwarm이 null입니다!");
            }
            
            // 종료 조건 체크
            if (elapsedTime >= attackDuration) // 시간 초과
            {
                Debug.Log($"[BeeHazardZone] 시간 초과로 벌 공격 종료 (지속시간: {elapsedTime:F1}초)");
                break;
            }
            
            if (distanceFromHive > giveUpDistance) // 너무 멀리 갔음
            {
                Debug.Log($"[BeeHazardZone] 펫이 너무 멀리 도망감. 벌이 포기 (거리: {distanceFromHive:F1}m)");
                break;
            }
            
            // 펫이 멈추고 충분히 멀리 있을 때만 종료
            if (pet.agent != null && pet.agent.velocity.magnitude < 0.1f && distanceFromHive > maxChaseDistance)
            {
                Debug.Log($"[BeeHazardZone] 펫이 충분히 멀리서 멈춤. 벌 공격 종료 (거리: {distanceFromHive:F1}m)");
                break;
            }
            
            elapsedTime += Time.deltaTime;
            yield return null;
        }
        
        // 벌 공격 종료
        if (pet != null)
        {
            // ★ [Phase 4] PetState를 통한 상태 업데이트
            pet.State.SetBeeAttackState(false);
            // beeAttackSource는 아직 유지 (도망 방향 계산용)
            petsBeingAttacked.Remove(pet);  // 중복 방지 리스트에서 제거
            Debug.Log($"[BeeHazardZone] {pet.petName}의 벌 공격 종료");
        }
        
        // 벌떼가 벌집으로 돌아가는 애니메이션과 동시에 펫 도망 타이머 시작
        if (beeSwarm != null)
        {
            Debug.Log($"[BeeHazardZone] 벌떼가 벌집으로 돌아갑니다");
            StartCoroutine(ReturnBeesToHive(beeSwarm, hivePosition));
        }
        
        // 2초 후 펫의 도망 종료 (벌이 돌아가는 동안)
        yield return new WaitForSeconds(5f);
        if (pet != null)
        {
            // ★ [Phase 4] 벌 공격 소스 초기화
            pet.State.SetBeeAttackState(false, Vector3.zero);
            Debug.Log($"[BeeHazardZone] {pet.petName}의 도망 시간 종료");
        }
    }
    
    private IEnumerator ReturnBeesToHive(GameObject beeSwarm, Vector3 hivePosition)
    {
        float returnDuration = 5f; // 5초 동안 천천히 돌아감
        float elapsedTime = 0f;
        Vector3 startPosition = beeSwarm.transform.position;
        
        // 파티클 방출 중지
        ParticleSystem ps = beeSwarm.GetComponent<ParticleSystem>();
        if (ps != null)
        {
            ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
        
        // 벌집으로 부드럽게 이동
        while (elapsedTime < returnDuration)
        {
            float t = elapsedTime / returnDuration;
            
            // 처음엔 빠르게, 나중엔 천천히 (ease-out 효과)
            float easedT = 1f - Mathf.Pow(1f - t, 3f);
            
            // 포물선 경로로 이동
            Vector3 currentPos = Vector3.Lerp(startPosition, hivePosition + swarmOffset, easedT);
            currentPos.y += Mathf.Sin(t * Mathf.PI) * 5f; // 더 높이 올라갔다가 내려오는 효과
            
            beeSwarm.transform.position = currentPos;
            
            elapsedTime += Time.deltaTime;
            yield return null;
        }
        
        // 벌집 도착 후 파티클 완전히 사라질 때까지 대기
        if (ps != null)
        {
            yield return new WaitForSeconds(ps.main.startLifetime.constantMax);
        }
        
        // 오브젝트 제거
        Destroy(beeSwarm);
    }
    
    private void OnDrawGizmos()
    {
        // 에디터에서 벌 공격 범위 시각화
        Collider col = GetComponent<Collider>();
        if (col != null && col.isTrigger)
        {
            Gizmos.color = new Color(1f, 0.8f, 0f, 0.3f); // 노란색 반투명
            
            if (col is SphereCollider sphere)
            {
                Gizmos.DrawSphere(transform.position + sphere.center, sphere.radius);
            }
            else if (col is BoxCollider box)
            {
                Matrix4x4 oldMatrix = Gizmos.matrix;
                Gizmos.matrix = Matrix4x4.TRS(transform.position + box.center, transform.rotation, transform.lossyScale);
                Gizmos.DrawCube(Vector3.zero, box.size);
                Gizmos.matrix = oldMatrix;
            }
            
            // 벌 아이콘 표시
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position + Vector3.up * 2f, 0.5f);
        }
    }
}