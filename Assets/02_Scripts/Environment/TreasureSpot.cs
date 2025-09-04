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
    [SerializeField] private bool hasBeenCounted = false;  // 이미 카운팅되었는지 추적
    
    [Tooltip("런타임에 자동 관리됨 - 이 스팟에 생성된 보물 오브젝트 참조\n" +
             "보물이 생성되면 자동으로 할당되고, 수집되면 null이 됩니다.\n" +
             "인스펙터에서 수동 설정 불필요")]
    [SerializeField] private GameObject currentTreasure;  // 현재 이 위치에 있는 보물 인스턴스
    
    [Tooltip("런타임에 자동 관리됨 - 이 보물을 차지한(발견한) 펫\n" +
             "여러 펫이 동시에 같은 보물에 접근하는 것을 방지합니다.\n" +
             "먼저 도착한 펫만 보물을 가질 수 있도록 소유권을 관리합니다.\n" +
             "인스펙터에서 수동 설정 불필요")]
    [SerializeField] private PetController occupyingPet;  // 이 보물 스팟을 점유한 펫
    
    
    // 프로퍼티
    public bool HasTreasure => hasTreasure && currentTreasure != null;
    public bool IsOccupied => occupyingPet != null;
    public bool IsAvailable => HasTreasure && !IsOccupied;  // 보물이 있고 아직 차지되지 않은 경우만 true
    public GameObject CurrentTreasure => currentTreasure;
    public Vector3 WaitingPosition => waitingPoint != null ? waitingPoint.position : transform.position + Vector3.forward * 2f;
    public bool HasBeenCounted => hasBeenCounted;
    
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
        hasBeenCounted = false;  // 새 보물은 아직 카운팅 안됨
        
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
    /// 펫이 보물에 도착해서 획득 시도
    /// </summary>
    public bool TryCollect(PetController pet)
    {
        // 보물이 없거나, 다른 펫이 이미 예약한 경우 실패
        // (자신이 예약한 경우는 성공)
        if (!HasTreasure || (occupyingPet != null && occupyingPet != pet))
        {
            Debug.Log($"{pet.petName}: 보물이 없거나 다른 펫이 이미 예약했습니다.");
            return false;
        }
        
        // 성공적으로 획득
        occupyingPet = pet;  // 이미 설정되어 있겠지만 안전하게 재설정
        // hasTreasure는 아직 true로 유지 - TreasureFoundActivity가 보물을 찾을 수 있도록
        Debug.Log($"{pet.petName}이(가) {name} 보물을 획득했습니다!");
        
        return true;
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
    }
    
    /// <summary>
    /// 이 보물이 카운팅되었음을 표시
    /// </summary>
    public void SetCounted()
    {
        hasBeenCounted = true;
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
        hasBeenCounted = false;
    }
    
    /// <summary>
    /// 보물찾기 종료 시 정리
    /// </summary>
    public void Clear()
    {
        if (currentTreasure != null)
        {
            // 보물 컨트롤러 확인
            TreasureController treasureController = currentTreasure.GetComponent<TreasureController>();
            
            // 펫이 내려놓은 보물은 삭제하지 않음
            if (treasureController != null && treasureController.IsDropped)
            {
                Debug.Log($"[TreasureSpot] 펫이 내려놓은 보물은 유지: {currentTreasure.name}");
                // currentTreasure 참조만 해제 (오브젝트는 유지)
                currentTreasure = null;
            }
            // 아직 스팟에 있거나 펫이 들고 있는 보물만 삭제
            else
            {
                Debug.Log($"[TreasureSpot] 미발견 보물 제거: {currentTreasure.name}");
                Destroy(currentTreasure);
                currentTreasure = null;
            }
        }
        
        hasTreasure = false;
        occupyingPet = null;
        hasBeenCounted = false;
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