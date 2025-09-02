using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System.Collections;
using TMPro;

/// <summary>
/// 보물찾기 시스템을 중앙에서 관리하는 매니저
/// </summary>
public class TreasureHuntManager : MonoBehaviour
{
    private static TreasureHuntManager instance;
    public static TreasureHuntManager Instance => instance;
    
    [Header("보물 설정")]
    [Tooltip("보물로 사용할 프리팹")]
    public GameObject treasurePrefab;
    
    [Tooltip("최소 보물 개수")]
    [Range(1, 10)]
    public int minTreasureCount = 2;
    
    [Tooltip("최대 보물 개수")]
    [Range(1, 10)]
    public int maxTreasureCount = 5;
    
    [Header("보상 설정")]
    [Tooltip("보물 하나당 최소 코인")]
    public int minCoinReward = 10;
    
    [Tooltip("보물 하나당 최대 코인")]
    public int maxCoinReward = 50;
    
    [Header("시간 설정")]
    [Tooltip("보물찾기 제한 시간 (초)")]
    [Range(30, 300)]
    public float treasureHuntDuration = 120f;  // 기본 2분
    
    [Header("친밀도 설정")]
    [Tooltip("보물찾기 참여에 필요한 최소 친밀도")]
    public float requiredAffection = 75f;
    
    [Header("UI")]
    [Tooltip("코인 획득 시 표시할 UI 텍스트")]
    public TMP_Text coinFeedbackText;
    
    [Tooltip("전체 코인 표시 UI")]
    public TMP_Text totalCoinsText;
    
    [Header("상태")]
    [SerializeField] private bool isTreasureHuntActive = false;
    [SerializeField] private int totalCoins = 0;
    [SerializeField] private List<TreasureSpot> allTreasureSpots = new List<TreasureSpot>();
    [SerializeField] private List<TreasureSpot> activeTreasureSpots = new List<TreasureSpot>();
    [SerializeField] private List<PetController> participatingPets = new List<PetController>();
    [SerializeField] private float remainingTime = 0f;
    [SerializeField] private int totalTreasureCount = 0;  // 전체 보물 개수
    [SerializeField] private int foundTreasureCount = 0;  // 찾은 보물 개수
    private Coroutine timerCoroutine;
    
    // 프로퍼티
    public bool IsTreasureHuntActive => isTreasureHuntActive;
    public List<TreasureSpot> ActiveTreasureSpots => activeTreasureSpots;
    public int TotalCoins => totalCoins;
    public float RemainingTime => remainingTime;
    public int TotalTreasureCount => totalTreasureCount;
    public int FoundTreasureCount => foundTreasureCount;
    
    // 이벤트
    public System.Action<int> OnCoinsCollected;
    public System.Action OnTreasureHuntStarted;
    public System.Action OnTreasureHuntEnded;
    public System.Action<int, int> OnTreasureFound;  // 찾은 개수, 전체 개수
    public System.Action<float> OnTimeUpdate;  // 남은 시간
    
    private void Awake()
    {
        // 싱글톤 설정
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        
        // 씬의 모든 TreasureSpot 수집
        RefreshTreasureSpots();
        
        // 저장된 코인 로드 (선택사항)
        LoadCoins();
        UpdateCoinUI();
    }
    
    private void Start()
    {
        // 보물 프리팹 체크
        if (treasurePrefab == null)
        {
            Debug.LogWarning("TreasureHuntManager: 보물 프리팹이 설정되지 않았습니다!");
        }
    }
    
    /// <summary>
    /// 씬의 모든 TreasureSpot 다시 수집
    /// </summary>
    public void RefreshTreasureSpots()
    {
        allTreasureSpots.Clear();
        allTreasureSpots.AddRange(FindObjectsByType<TreasureSpot>(FindObjectsSortMode.None));
        Debug.Log($"TreasureHuntManager: {allTreasureSpots.Count}개의 보물 스팟을 찾았습니다.");
    }
    
    /// <summary>
    /// 보물찾기 시작
    /// </summary>
    public void StartTreasureHunt()
    {
        if (isTreasureHuntActive)
        {
            Debug.Log("이미 보물찾기가 진행 중입니다.");
            return;
        }
        
        if (treasurePrefab == null)
        {
            Debug.LogError("보물 프리팹이 설정되지 않았습니다!");
            return;
        }
        
        if (allTreasureSpots.Count == 0)
        {
            RefreshTreasureSpots();
            if (allTreasureSpots.Count == 0)
            {
                Debug.LogError("보물 스팟이 씬에 없습니다!");
                return;
            }
        }
        
        // 참여 가능한 펫 찾기
        PetController[] allPets = FindObjectsByType<PetController>(FindObjectsSortMode.None);
        participatingPets.Clear();
        
        foreach (var pet in allPets)
        {
            if (pet != null && pet.Needs != null && pet.Needs.Affection >= requiredAffection)
            {
                participatingPets.Add(pet);
                // 펫을 보물찾기 상태로 전환
                pet.State.SetTreasureHuntingState(true);
                
                // AI 즉시 업데이트
                if (pet.AI != null)
                {
                    pet.AI.InterruptAndResetAI();
                }
            }
        }
        
        if (participatingPets.Count == 0)
        {
            Debug.Log($"친밀도 {requiredAffection} 이상인 펫이 없어 보물찾기를 시작할 수 없습니다.");
            ShowFeedback($"친밀도 {requiredAffection} 이상인 펫이 필요합니다!");
            return;
        }
        
        // 보물 생성
        SpawnTreasures();
        
        // 상태 초기화
        foundTreasureCount = 0;
        totalTreasureCount = activeTreasureSpots.Count;
        remainingTime = treasureHuntDuration;
        
        isTreasureHuntActive = true;
        OnTreasureHuntStarted?.Invoke();
        OnTreasureFound?.Invoke(foundTreasureCount, totalTreasureCount);
        
        // 타이머 시작
        if (timerCoroutine != null)
        {
            StopCoroutine(timerCoroutine);
        }
        timerCoroutine = StartCoroutine(TreasureHuntTimer());
        
        Debug.Log($"보물찾기 시작! {participatingPets.Count}마리 참여, {totalTreasureCount}개 보물 생성, 제한시간 {treasureHuntDuration}초");
        ShowFeedback($"{participatingPets.Count}마리가 보물찾기를 시작합니다!");
    }
    
    /// <summary>
    /// 보물찾기 종료 (완료 상태로 - 펫과 보물 유지)
    /// </summary>
    public void EndTreasureHunt(bool clearAll = false)
    {
        if (!isTreasureHuntActive) return;
        
        // 타이머 중지
        if (timerCoroutine != null)
        {
            StopCoroutine(timerCoroutine);
            timerCoroutine = null;
        }
        
        if (clearAll)
        {
            // 강제 종료 시에만 모든 것을 정리 (테스트용)
            foreach (var spot in activeTreasureSpots)
            {
                if (spot != null)
                {
                    spot.Clear();
                }
            }
            activeTreasureSpots.Clear();
            
            // 모든 펫 상태 초기화
            foreach (var pet in participatingPets)
            {
                if (pet != null)
                {
                    pet.State.SetTreasureHuntingState(false);
                    pet.State.TrySetStatus(PetStatus.Idle);
                    
                    // AI 리셋
                    if (pet.AI != null)
                    {
                        pet.AI.InterruptAndResetAI();
                    }
                }
            }
            participatingPets.Clear();
        }
        else
        {
            // 일반 종료 - 펫은 보물 앞에서 계속 대기
            // IsTreasureHuntActive만 false로 설정
            // 펫들은 TreasureFound 상태 유지
        }
        
        isTreasureHuntActive = false;
        OnTreasureHuntEnded?.Invoke();
        
        string endMessage = foundTreasureCount == totalTreasureCount ? 
            $"모든 보물을 찾았습니다! ({foundTreasureCount}/{totalTreasureCount})" : 
            $"시간이 종료되었습니다! ({foundTreasureCount}/{totalTreasureCount} 찾음)";
        
        Debug.Log($"보물찾기 종료! {endMessage}");
        ShowFeedback(endMessage);
    }
    
    /// <summary>
    /// 보물 생성
    /// </summary>
    private void SpawnTreasures()
    {
        activeTreasureSpots.Clear();
        
        // 보물 개수 결정
        int treasureCount = Random.Range(minTreasureCount, maxTreasureCount + 1);
        treasureCount = Mathf.Min(treasureCount, allTreasureSpots.Count);
        
        // 랜덤하게 스팟 선택
        List<TreasureSpot> shuffledSpots = new List<TreasureSpot>(allTreasureSpots);
        for (int i = 0; i < shuffledSpots.Count; i++)
        {
            int randomIndex = Random.Range(i, shuffledSpots.Count);
            var temp = shuffledSpots[i];
            shuffledSpots[i] = shuffledSpots[randomIndex];
            shuffledSpots[randomIndex] = temp;
        }
        
        // 보물 생성
        int spawned = 0;
        foreach (var spot in shuffledSpots)
        {
            if (spawned >= treasureCount) break;
            
            if (spot.TrySpawnTreasure(treasurePrefab))
            {
                activeTreasureSpots.Add(spot);
                spawned++;
            }
        }
    }
    
    /// <summary>
    /// 가장 가까운 보물 스팟 찾기
    /// </summary>
    public TreasureSpot FindNearestAvailableSpot(Vector3 position, float maxDistance = float.MaxValue)
    {
        TreasureSpot nearestSpot = null;
        float nearestDistance = float.MaxValue;
        
        foreach (var spot in activeTreasureSpots)
        {
            if (spot == null || !spot.HasTreasure)
                continue;
                
            float distance = Vector3.Distance(position, spot.transform.position);
            
            // 최대 거리 제한 체크
            if (distance > maxDistance)
                continue;
                
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearestSpot = spot;
            }
        }
        
        return nearestSpot;
    }
    
    /// <summary>
    /// 보물 수집 처리
    /// </summary>
    public void CollectTreasure(TreasureSpot spot, PetController pet)
    {
        // spot이 null이어도 처리 가능 (펫이 놓은 보물)
        
        // 코인 보상
        int coins = Random.Range(minCoinReward, maxCoinReward + 1);
        totalCoins += coins;
        
        // 찾은 보물 개수 증가
        foundTreasureCount++;
        
        // UI 업데이트
        UpdateCoinUI();
        if (spot != null)
        {
            ShowCoinFeedback(coins, spot.transform.position);
        }
        else if (pet != null)
        {
            ShowCoinFeedback(coins, pet.transform.position);
        }
        
        // 이벤트 발생
        OnCoinsCollected?.Invoke(coins);
        OnTreasureFound?.Invoke(foundTreasureCount, totalTreasureCount);
        
        // 스팟에서 보물 제거
        if (spot != null)
        {
            if (spot.HasTreasure)
            {
                spot.CollectTreasure();
            }
            activeTreasureSpots.Remove(spot);
        }
        
        // 이 보물을 찾은 펫만 상태 초기화
        if (pet != null)
        {
            pet.State.SetTreasureHuntingState(false);
            pet.State.TrySetStatus(PetStatus.Idle);
            pet.ShowEmotion(EmotionType.Happy);
            
            // AI 리셋
            if (pet.AI != null)
            {
                pet.AI.InterruptAndResetAI();
            }
            
            // 참여 펫 목록에서 제거
            participatingPets.Remove(pet);
        }
        
        Debug.Log($"{pet?.petName}이(가) 보물을 찾았습니다! +{coins} 코인 ({foundTreasureCount}/{totalTreasureCount})");
        
        // 모든 보물을 찾았는지 확인
        if (foundTreasureCount >= totalTreasureCount && isTreasureHuntActive)
        {
            ShowFeedback("모든 보물을 찾았습니다!");
            // 잠시 후 종료 (펫과 보물은 유지)
            StartCoroutine(EndAfterDelay(2f));
        }
    }
    
    private IEnumerator EndAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        EndTreasureHunt(false);  // 일반 종료 - 펫과 보물 유지
    }
    
    /// <summary>
    /// 보물찾기 타이머
    /// </summary>
    private IEnumerator TreasureHuntTimer()
    {
        while (remainingTime > 0 && isTreasureHuntActive)
        {
            remainingTime -= Time.deltaTime;
            OnTimeUpdate?.Invoke(remainingTime);
            
            // 마지막 10초 경고
            if (remainingTime <= 10f && remainingTime > 9f)
            {
                ShowFeedback("10초 남았습니다!");
            }
            
            yield return null;
        }
        
        if (isTreasureHuntActive)
        {
            // 시간 종료로 인한 종료
            EndTreasureHunt(false);
        }
    }
    
    /// <summary>
    /// 코인 UI 업데이트
    /// </summary>
    private void UpdateCoinUI()
    {
        if (totalCoinsText != null)
        {
            totalCoinsText.text = $"코인: {totalCoins}";
        }
    }
    
    /// <summary>
    /// 코인 획득 피드백 표시
    /// </summary>
    private void ShowCoinFeedback(int coins, Vector3 worldPosition)
    {
        if (coinFeedbackText != null)
        {
            coinFeedbackText.text = $"+{coins}";
            coinFeedbackText.gameObject.SetActive(true);
            
            // 월드 좌표를 스크린 좌표로 변환
            if (Camera.main != null)
            {
                Vector3 screenPos = Camera.main.WorldToScreenPoint(worldPosition + Vector3.up * 2f);
                coinFeedbackText.transform.position = screenPos;
            }
            
            // 페이드 아웃 애니메이션
            StartCoroutine(FadeOutText(coinFeedbackText, 2f));
        }
    }
    
    /// <summary>
    /// 일반 피드백 표시
    /// </summary>
    private void ShowFeedback(string message)
    {
        if (coinFeedbackText != null)
        {
            coinFeedbackText.text = message;
            coinFeedbackText.gameObject.SetActive(true);
            StartCoroutine(HideTextAfterDelay(coinFeedbackText, 3f));
        }
    }
    
    private IEnumerator FadeOutText(TMP_Text text, float duration)
    {
        float elapsed = 0f;
        Color originalColor = text.color;
        Vector3 startPos = text.transform.position;
        
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            
            // 위로 이동하면서 페이드 아웃
            text.color = new Color(originalColor.r, originalColor.g, originalColor.b, 1f - t);
            text.transform.position = startPos + Vector3.up * (t * 50f);
            
            yield return null;
        }
        
        text.gameObject.SetActive(false);
        text.color = originalColor;
    }
    
    private IEnumerator HideTextAfterDelay(TMP_Text text, float delay)
    {
        yield return new WaitForSeconds(delay);
        text.gameObject.SetActive(false);
    }
    
    /// <summary>
    /// 코인 저장
    /// </summary>
    private void SaveCoins()
    {
        PlayerPrefs.SetInt("TotalCoins", totalCoins);
        PlayerPrefs.Save();
    }
    
    /// <summary>
    /// 코인 로드
    /// </summary>
    private void LoadCoins()
    {
        totalCoins = PlayerPrefs.GetInt("TotalCoins", 0);
    }
    
    private void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus) SaveCoins();
    }
    
    private void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus) SaveCoins();
    }
    
    private void OnDestroy()
    {
        SaveCoins();
        
        if (instance == this)
        {
            instance = null;
        }
    }
}