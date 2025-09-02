using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 보물이 나타날 수 있는 위치를 정의하는 컴포넌트
/// 씬에 빈 GameObject로 배치하여 사용
/// </summary>
public class TreasureSpot : MonoBehaviour
{
    [Header("보물 설정")]
    [Range(0f, 100f)]
    [Tooltip("이 위치에 보물이 나타날 확률 (0-100%)")]
    public float treasureProbability = 50f;
    
    [Header("대기 위치")]
    [Tooltip("펫이 보물을 찾은 후 대기할 위치 (자식 오브젝트)")]
    public Transform waitingPoint;
    
    [Header("상태")]
    [SerializeField] private bool hasTreasure = false;
    
    [Tooltip("런타임에 자동 관리됨 - 이 스팟에 생성된 보물 오브젝트 참조\n" +
             "보물이 생성되면 자동으로 할당되고, 수집되면 null이 됩니다.\n" +
             "인스펙터에서 수동 설정 불필요")]
    [SerializeField] private GameObject currentTreasure;  // 현재 이 위치에 있는 보물 인스턴스
    
    [Tooltip("런타임에 자동 관리됨 - 이 보물을 차지한(발견한) 펫\n" +
             "여러 펫이 동시에 같은 보물에 접근하는 것을 방지합니다.\n" +
             "먼저 도착한 펫만 보물을 가질 수 있도록 소유권을 관리합니다.\n" +
             "인스펙터에서 수동 설정 불필요")]
    [SerializeField] private PetController occupyingPet;  // 이 보물 스팟을 점유한 펫
    
    // 경쟁 시스템을 위한 추가 필드
    [SerializeField] private List<PetController> competingPets = new List<PetController>();  // 이 보물을 향해 오고 있는 펫들
    
    // 프로퍼티
    public bool HasTreasure => hasTreasure && currentTreasure != null;
    public bool IsOccupied => occupyingPet != null;
    public bool IsAvailable => HasTreasure && !IsOccupied;  // 보물이 있고 아직 차지되지 않은 경우만 true
    public GameObject CurrentTreasure => currentTreasure;
    public Vector3 WaitingPosition => waitingPoint != null ? waitingPoint.position : transform.position + Vector3.forward * 2f;
    public List<PetController> CompetingPets => competingPets;
    
    private void Awake()
    {
        // WaitingPoint가 설정되지 않았다면 자식에서 찾기
        if (waitingPoint == null)
        {
            Transform child = transform.Find("WaitingPoint");
            if (child != null)
            {
                waitingPoint = child;
            }
            else
            {
                // 자식이 없으면 자동 생성
                GameObject waitingPointObj = new GameObject("WaitingPoint");
                waitingPointObj.transform.SetParent(transform);
                waitingPointObj.transform.localPosition = Vector3.forward * 2f + Vector3.up * 0.5f;
                waitingPoint = waitingPointObj.transform;
            }
        }
    }
    
    /// <summary>
    /// 이 위치에 보물을 생성할지 결정
    /// </summary>
    public bool TrySpawnTreasure(GameObject treasurePrefab)
    {
        if (hasTreasure || currentTreasure != null) 
            return false;
            
        // 확률 체크
        float roll = Random.Range(0f, 100f);
        if (roll > treasureProbability)
            return false;
            
        // 보물 생성 (땅 위에 자연스럽게 놓이도록 높이 조정)
        Vector3 spawnPosition = transform.position + Vector3.up * 1f;
        currentTreasure = Instantiate(treasurePrefab, spawnPosition, Quaternion.identity);
        currentTreasure.transform.SetParent(transform);
        hasTreasure = true;
        
        // 보물 컨트롤러에 이 스팟 연결 및 초기 상태 설정
        TreasureController treasureController = currentTreasure.GetComponent<TreasureController>();
        if (treasureController != null)
        {
            treasureController.SetSpot(this);
            treasureController.SetInitialState(false);  // 회전하지 않도록 설정
        }
        
        return true;
    }
    
    /// <summary>
    /// 펫이 이 위치를 점유 (경쟁 시스템에서는 사용 안함)
    /// </summary>
    public bool TryOccupy(PetController pet)
    {
        if (IsOccupied && occupyingPet != pet)
            return false;
            
        occupyingPet = pet;
        return true;
    }
    
    /// <summary>
    /// 펫이 이 보물을 목표로 설정 (경쟁 추적)
    /// </summary>
    public void AddCompetingPet(PetController pet)
    {
        if (!competingPets.Contains(pet))
        {
            competingPets.Add(pet);
            Debug.Log($"{pet.petName}이(가) {name} 보물을 목표로 설정했습니다.");
        }
    }
    
    /// <summary>
    /// 펫이 보물에 도착해서 획듍 시도
    /// </summary>
    public bool TryCollect(PetController pet)
    {
        // 이미 누군가 가져간 경우
        if (!HasTreasure || occupyingPet != null)
        {
            Debug.Log($"{pet.petName}: 보물이 이미 다른 펫에게 가져가졌습니다.");
            
            // 경쟁에서 패배한 펫들에게 알림
            NotifyLosingPets(pet);
            return false;
        }
        
        // 성공적으로 획듍
        occupyingPet = pet;
        Debug.Log($"{pet.petName}이(가) {name} 보물을 획듍했습니다!");
        
        // 경쟁에서 패배한 다른 펫들에게 알림
        NotifyLosingPets(pet);
        
        return true;
    }
    
    /// <summary>
    /// 경쟁에서 패배한 펫들에게 알림
    /// </summary>
    private void NotifyLosingPets(PetController winner)
    {
        foreach (var loser in competingPets)
        {
            if (loser != winner && loser != null)
            {
                Debug.Log($"[TreasureSpot] {loser.petName}은(는) {winner.petName}에게 보물을 빼앗겼습니다. 즉시 알림 전송");
                
                // TreasureHuntActivity의 OnTargetLost 메서드 호출
                // PetAI를 통해 현재 Activity 접근
                if (loser.AI != null)
                {
                    var currentActivity = loser.AI.GetCurrentActivity();
                    if (currentActivity is TreasureHuntActivity treasureHuntActivity)
                    {
                        // 즉시 다른 보물을 찾도록 알림
                        treasureHuntActivity.OnTargetLost();
                        Debug.Log($"[TreasureSpot] {loser.petName}에게 OnTargetLost 알림 전송 완료");
                    }
                    else
                    {
                        Debug.LogWarning($"[TreasureSpot] {loser.petName}의 CurrentActivity가 TreasureHuntActivity가 아님 (현재: {currentActivity?.Name ?? "None"})");
                    }
                }
            }
        }
    }
    
    /// <summary>
    /// 펫이 이 위치 점유 해제
    /// </summary>
    public void Release(PetController pet)
    {
        if (occupyingPet == pet)
        {
            occupyingPet = null;
        }
        
        // 경쟁 리스트에서도 제거
        competingPets.Remove(pet);
    }
    
    /// <summary>
    /// 보물 획득 처리
    /// </summary>
    public void CollectTreasure()
    {
        if (currentTreasure != null)
        {
            // 파티클 효과나 사운드는 TreasureController에서 처리
            Destroy(currentTreasure);
            currentTreasure = null;
        }
        
        hasTreasure = false;
        occupyingPet = null;
        competingPets.Clear();
    }
    
    /// <summary>
    /// 보물찾기 종료 시 정리
    /// </summary>
    public void Clear()
    {
        if (currentTreasure != null)
        {
            Destroy(currentTreasure);
            currentTreasure = null;
        }
        
        hasTreasure = false;
        occupyingPet = null;
        competingPets.Clear();
    }
    
    // 에디터에서 시각화
    private void OnDrawGizmos()
    {
        // 보물 스팟 위치
        Gizmos.color = hasTreasure ? Color.yellow : Color.cyan;
        Gizmos.DrawWireSphere(transform.position, 0.5f);
        
        // 대기 위치
        if (waitingPoint != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(waitingPoint.position, 0.3f);
            Gizmos.DrawLine(transform.position, waitingPoint.position);
        }
        else
        {
            Vector3 defaultWaitingPos = transform.position + Vector3.forward * 2f + Vector3.up * 0.5f;
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(defaultWaitingPos, 0.3f);
            Gizmos.DrawLine(transform.position, defaultWaitingPos);
        }
        
        // 확률 표시
        #if UNITY_EDITOR
        UnityEditor.Handles.Label(transform.position + Vector3.up * 1f, 
            $"보물 확률: {treasureProbability}%");
        #endif
    }
    
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, 1f);
    }
}