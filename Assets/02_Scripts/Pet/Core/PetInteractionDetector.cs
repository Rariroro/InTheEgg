using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 콜라이더 기반 펫 상호작용 감지 시스템
/// 각 펫에 부착되어 근처 펫을 실시간으로 감지하고 상호작용을 트리거합니다
/// </summary>
public class PetInteractionDetector : MonoBehaviour
{
    [Header("감지 설정")]
    [SerializeField] private float detectionRadius = 10f;
    [SerializeField] private float interactionCheckInterval = 1f; // 상호작용 체크 간격
    
    private PetController myPet;
    private SphereCollider detectionCollider;
    private HashSet<PetController> nearbyPets = new HashSet<PetController>();
    private float lastInteractionCheckTime;
    
    // 디버그용
    [Header("디버그")]
    [SerializeField] private bool enableDebugLogs = false;
    [SerializeField] private bool showDetectionRadius = false;
    [SerializeField] private List<string> nearbyPetNames = new List<string>(); // 인스펙터에서 확인용
    
    public void Init(PetController pet)
    {
        myPet = pet;
        SetupDetectionCollider();
        lastInteractionCheckTime = Time.time;
    }
    
    private void SetupDetectionCollider()
    {
        // 기존 SphereCollider들 찾기
        SphereCollider[] colliders = GetComponents<SphereCollider>();
        
        // Trigger용 콜라이더 찾기 (상호작용 감지용)
        foreach (var col in colliders)
        {
            // 이미 Trigger로 설정된 콜라이더가 있으면 그것을 사용
            if (col.isTrigger)
            {
                detectionCollider = col;
                // 감지 반경 설정
                detectionCollider.radius = detectionRadius;
                detectionCollider.center = Vector3.zero;
                return;
            }
        }
        
        // Trigger 콜라이더가 없는 경우에만 새로 생성
        // (기존 물리 충돌용 콜라이더는 유지)
        detectionCollider = gameObject.AddComponent<SphereCollider>();
        detectionCollider.isTrigger = true;
        detectionCollider.radius = detectionRadius;
        detectionCollider.center = Vector3.zero;
    }
    
    private void OnTriggerEnter(Collider other)
    {
        // 스피어 콜라이더인 경우만 처리 (펫 간 상호작용 전용)
        if (!(other is SphereCollider) || !other.isTrigger)
            return;
            
        // 다른 펫이 감지 범위에 들어옴
        PetController otherPet = other.GetComponent<PetController>();
        if (otherPet != null && otherPet != myPet)
        {
            nearbyPets.Add(otherPet);
            UpdateDebugList();
            
            // 즉시 상호작용 체크
            CheckInteractionWithPet(otherPet);
        }
    }
    
    private void OnTriggerExit(Collider other)
    {
        // 스피어 콜라이더인 경우만 처리 (펫 간 상호작용 전용)
        if (!(other is SphereCollider) || !other.isTrigger)
            return;
            
        // 다른 펫이 감지 범위를 벗어남
        PetController otherPet = other.GetComponent<PetController>();
        if (otherPet != null)
        {
            nearbyPets.Remove(otherPet);
            UpdateDebugList();
        }
    }
    
    private void OnTriggerStay(Collider other)
    {
        // 스피어 콜라이더인 경우만 처리 (펫 간 상호작용 전용)
        if (!(other is SphereCollider) || !other.isTrigger)
            return;
            
        // 주기적으로 상호작용 가능성 체크
        if (Time.time - lastInteractionCheckTime < interactionCheckInterval)
            return;
            
        PetController otherPet = other.GetComponent<PetController>();
        if (otherPet != null && otherPet != myPet)
        {
            CheckInteractionWithPet(otherPet);
        }
        
        lastInteractionCheckTime = Time.time;
    }
    
    private void CheckInteractionWithPet(PetController otherPet)
    {
        if (enableDebugLogs)
            Debug.Log($"[Detector] {myPet?.petName ?? "null"} → {otherPet?.petName ?? "null"} 체크 시작");

        // null 체크
        if (myPet == null || otherPet == null)
        {
            if (enableDebugLogs)
                Debug.Log($"[Detector] ❌ null 체크 실패: myPet={myPet != null}, otherPet={otherPet != null}");
            return;
        }

        // 이미 상호작용 중인지 체크
        if (myPet.State.IsInteracting || otherPet.State.IsInteracting)
        {
            if (enableDebugLogs)
                Debug.Log($"[Detector] ❌ 이미 상호작용 중: {myPet.petName}={myPet.State.IsInteracting}, {otherPet.petName}={otherPet.State.IsInteracting}");
            return;
        }

        // 홀딩 상태 체크
        if (myPet.State.IsHolding || otherPet.State.IsHolding)
        {
            if (enableDebugLogs)
                Debug.Log($"[Detector] ❌ 홀딩 중: {myPet.petName}={myPet.State.IsHolding}, {otherPet.petName}={otherPet.State.IsHolding}");
            return;
        }

        // 모이기 상태 체크
        if (IsGathering(myPet) || IsGathering(otherPet))
        {
            if (enableDebugLogs)
                Debug.Log($"[Detector] ❌ 모이기 중: {myPet.petName}={IsGathering(myPet)}, {otherPet.petName}={IsGathering(otherPet)}");
            return;
        }

        // 욕구 체크 (배고픔 70 이상, 졸림 70 이상이면 상호작용 안함)
        if (myPet.Needs.Hunger >= 70f || myPet.Needs.Sleepiness >= 70f ||
            otherPet.Needs.Hunger >= 70f || otherPet.Needs.Sleepiness >= 70f)
        {
            if (enableDebugLogs)
                Debug.Log($"[Detector] ❌ 욕구 임계값 초과: " +
                    $"{myPet.petName}(배고픔:{myPet.Needs.Hunger:F1}, 졸림:{myPet.Needs.Sleepiness:F1}), " +
                    $"{otherPet.petName}(배고픔:{otherPet.Needs.Hunger:F1}, 졸림:{otherPet.Needs.Sleepiness:F1})");
            return;
        }

        // PetInteractionManager에 상호작용 요청
        if (PetInteractionManager.Instance != null)
        {
            if (enableDebugLogs)
                Debug.Log($"[Detector] ✅ 상호작용 요청: {myPet.petName} ↔ {otherPet.petName}");

            PetInteractionManager.Instance.RequestInteraction(myPet, otherPet);
        }
        else
        {
            if (enableDebugLogs)
                Debug.LogWarning($"[Detector] ❌ PetInteractionManager.Instance가 null입니다!");
        }
    }
    
    private bool IsGathering(PetController pet)
    {
        return pet.State.CurrentStatus == PetStatus.GatheringInProgress || 
               pet.State.CurrentStatus == PetStatus.GatheredWaiting;
    }
    
    private void UpdateDebugList()
    {
        nearbyPetNames.Clear();
        foreach (var pet in nearbyPets)
        {
            if (pet != null)
                nearbyPetNames.Add(pet.petName);
        }
    }
    
    public HashSet<PetController> GetNearbyPets()
    {
        // null인 펫 제거
        nearbyPets.RemoveWhere(p => p == null);
        return new HashSet<PetController>(nearbyPets);
    }
    
    public void SetDetectionRadius(float radius)
    {
        detectionRadius = radius;
        if (detectionCollider != null)
        {
            detectionCollider.radius = radius;
        }
    }
    
    private void OnDrawGizmosSelected()
    {
        if (!showDetectionRadius || myPet == null)
            return;

        // 감지 범위 표시 (펫 상태에 따라 색상 변경)
        if (myPet.State.IsInteracting)
        {
            Gizmos.color = new Color(1, 1, 0, 0.3f); // 노란색: 상호작용 중
        }
        else if (myPet.State.IsHolding)
        {
            Gizmos.color = new Color(1, 0.5f, 0, 0.3f); // 주황색: 홀딩 중
        }
        else if (IsGathering(myPet))
        {
            Gizmos.color = new Color(0, 0.5f, 1, 0.3f); // 하늘색: 모이기 중
        }
        else
        {
            Gizmos.color = new Color(0, 1, 0, 0.3f); // 초록색: 정상
        }
        Gizmos.DrawWireSphere(transform.position, detectionRadius);

        // 근처 펫들과의 연결선 표시 (상호작용 가능 여부에 따라 색상 구분)
        foreach (var pet in nearbyPets)
        {
            if (pet == null) continue;

            // 상호작용 가능 여부 체크
            bool canInteract = !myPet.State.IsInteracting && !pet.State.IsInteracting &&
                              !myPet.State.IsHolding && !pet.State.IsHolding &&
                              !IsGathering(myPet) && !IsGathering(pet) &&
                              myPet.Needs.Hunger < 70f && myPet.Needs.Sleepiness < 70f &&
                              pet.Needs.Hunger < 70f && pet.Needs.Sleepiness < 70f;

            if (canInteract)
            {
                Gizmos.color = new Color(0, 1, 0, 0.7f); // 초록색: 상호작용 가능
            }
            else if (pet.State.IsInteracting)
            {
                Gizmos.color = new Color(1, 1, 0, 0.5f); // 노란색: 다른 펫과 상호작용 중
            }
            else
            {
                Gizmos.color = new Color(1, 0, 0, 0.5f); // 빨간색: 상호작용 불가
            }

            Gizmos.DrawLine(transform.position, pet.transform.position);
        }
    }
    
    private void OnDestroy()
    {
        nearbyPets.Clear();
    }
}