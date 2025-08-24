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
        
        // 감지 레이어 설정 (옵션)
        gameObject.layer = LayerMask.NameToLayer("Default");
    }
    
    private void OnTriggerEnter(Collider other)
    {
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
        // null 체크
        if (myPet == null || otherPet == null)
            return;
            
        // 이미 상호작용 중인지 체크
        if (myPet.State.IsInteracting || otherPet.State.IsInteracting)
            return;
            
        // 홀딩 상태 체크
        if (myPet.State.IsHolding || otherPet.State.IsHolding)
            return;
            
        // 모이기 상태 체크
        if (IsGathering(myPet) || IsGathering(otherPet))
            return;
            
        // 욕구 체크 (배고픔 70 이상, 졸림 70 이상이면 상호작용 안함)
        if (myPet.Needs.Hunger >= 70f || myPet.Needs.Sleepiness >= 70f ||
            otherPet.Needs.Hunger >= 70f || otherPet.Needs.Sleepiness >= 70f)
            return;
            
        // PetInteractionManager에 상호작용 요청
        if (PetInteractionManager.Instance != null)
        {
            PetInteractionManager.Instance.RequestInteraction(myPet, otherPet);
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
        if (!showDetectionRadius)
            return;
            
        Gizmos.color = new Color(0, 1, 0, 0.3f);
        Gizmos.DrawWireSphere(transform.position, detectionRadius);
        
        // 근처 펫들과의 연결선 표시
        Gizmos.color = new Color(1, 1, 0, 0.5f);
        foreach (var pet in nearbyPets)
        {
            if (pet != null)
            {
                Gizmos.DrawLine(transform.position, pet.transform.position);
            }
        }
    }
    
    private void OnDestroy()
    {
        nearbyPets.Clear();
    }
}