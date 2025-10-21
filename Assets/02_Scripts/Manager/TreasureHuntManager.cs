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
    [Tooltip("중요 안내 메시지 텍스트 (화면 중앙 고정)")]
    public TMP_Text feedbackText;

    [Tooltip("코인 획득 피드백 텍스트 (보물 위에 동적 표시)")]
    public TMP_Text coinPopupText;

    [Tooltip("전체 코인 표시 UI")]
    public TMP_Text totalCoinsText;

    [Header("성공 팝업")]
    [Tooltip("보물찾기 성공 시 표시할 팝업 프리팹")]
    public GameObject successPopupPrefab;
    
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
    
    // 펫이 찾아서 내려놓은 보물들 추적
    [SerializeField] private List<TreasureController> droppedTreasures = new List<TreasureController>();
    
    // 동시성 제어를 위한 필드들
    private readonly object countLock = new object();  // 카운팅 동기화용 lock
    private HashSet<TreasureSpot> countedSpots = new HashSet<TreasureSpot>();  // 이미 카운팅된 스팟들
    
    // 프로퍼티
    public bool IsTreasureHuntActive => isTreasureHuntActive;
    public List<TreasureSpot> ActiveTreasureSpots => activeTreasureSpots;
    public int TotalCoins => totalCoins;
    public float RemainingTime => remainingTime;
    public int TotalTreasureCount => totalTreasureCount;
    public int FoundTreasureCount => foundTreasureCount;
    
    // UI에서 접근할 수 있도록 읽기 전용 프로퍼티 제공
    public List<TreasureController> DroppedTreasures => new List<TreasureController>(droppedTreasures);
    
    // 내려놓은 보물 개수 반환
    public int GetDroppedTreasureCount() => droppedTreasures.Count;
    
    // 내려놓은 보물 리스트 반환 (GameObject로)
    public List<GameObject> GetDroppedTreasureList()
    {
        List<GameObject> treasures = new List<GameObject>();
        foreach (var treasure in droppedTreasures)
        {
            if (treasure != null && treasure.gameObject != null)
                treasures.Add(treasure.gameObject);
        }
        return treasures;
    }
    
    // 이벤트
    public System.Action<int> OnCoinsCollected;
    public System.Action OnTreasureHuntStarted;
    public System.Action OnTreasureHuntEnded;
    public System.Action<int, int> OnTreasureFound;  // 찾은 개수, 전체 개수
    public System.Action<float> OnTimeUpdate;  // 남은 시간
    public System.Action<int> OnDroppedTreasureUpdate;  // 내려놓은 보물 개수 업데이트
    
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
        // Debug.Log($"TreasureHuntManager: {allTreasureSpots.Count}개의 보물 스팟을 찾았습니다.");
    }
    
    /// <summary>
    /// 보물찾기 시작
    /// </summary>
    public void StartTreasureHunt()
    {
        if (isTreasureHuntActive)
        {
        // Debug.Log("이미 보물찾기가 진행 중입니다.");
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
        // Debug.Log($"친밀도 {requiredAffection} 이상인 펫이 없어 보물찾기를 시작할 수 없습니다.");
            ShowFeedback($"친밀도 {requiredAffection} 이상인 펫이 필요합니다!");
            return;
        }
        
        // 보물 생성
        SpawnTreasures();
        
        // 상태 초기화 (완전한 초기화)
        foundTreasureCount = 0;
        totalTreasureCount = activeTreasureSpots.Count;
        remainingTime = treasureHuntDuration;
        droppedTreasures.Clear();  // 내려놓은 보물 리스트 초기화
        countedSpots.Clear();  // 카운팅된 스팟 초기화
        
        // 모든 스팟의 카운팅 플래그 리셋
        foreach (var spot in activeTreasureSpots)
        {
            if (spot != null)
            {
                spot.ResetCountingFlag();
            }
        }
        
        // Debug.Log($"[TreasureHuntManager] 초기화 완료: foundCount={foundTreasureCount}, totalCount={totalTreasureCount}, countedSpots={countedSpots.Count}");
        
        isTreasureHuntActive = true;
        OnTreasureHuntStarted?.Invoke();
        OnTreasureFound?.Invoke(foundTreasureCount, totalTreasureCount);
        
        // 타이머 시작
        if (timerCoroutine != null)
        {
            StopCoroutine(timerCoroutine);
        }
        timerCoroutine = StartCoroutine(TreasureHuntTimer());
        
        // Debug.Log($"보물찾기 시작! {participatingPets.Count}마리 참여, {totalTreasureCount}개 보물 생성, 제한시간 {treasureHuntDuration}초");
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
            countedSpots.Clear();  // 카운팅된 스팟 목록 초기화
            
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
            // 일반 종료 - 보물찾기 상태는 해제하고 모든 펫을 정상 상태로 전환
            
            // 남아있는 미발견 보물만 정리 (펫이 찾아서 내려놓은 보물은 유지)
            foreach (var spot in activeTreasureSpots)
            {
                if (spot != null)
                {
                    spot.Clear();  // 스팟에 남아있는 미발견 보물만 제거
                }
            }
            
            // 펫이 아직 들고 있는 보물만 제거 (내려놓은 보물은 유지)
            TreasureController[] allTreasures = FindObjectsOfType<TreasureController>();
            foreach (var treasure in allTreasures)
            {
                if (treasure != null && treasure.gameObject != null)
                {
                    // 수집 가능한 상태(내려놓은 보물)는 유지
                    if (treasure.IsDropped)
                    {
        // Debug.Log($"[TreasureHuntManager] 수집 가능한 보물 유지: {treasure.gameObject.name}");
                        continue;
                    }
                    
                    // 아직 펫이 들고 있는 보물은 제거
                    if (treasure.IsCarried)
                    {
        // Debug.Log($"[TreasureHuntManager] 펫이 들고 있는 보물 제거: {treasure.gameObject.name}");
                        Destroy(treasure.gameObject);
                    }
                }
            }
            
            activeTreasureSpots.Clear();  // 활성 스팟 목록 비우기
            countedSpots.Clear();  // 카운팅된 스팟 목록 초기화
            
            // 펫 상태 초기화
            foreach (var pet in participatingPets)
            {
                if (pet != null)
                {
                    // 보물찾기 활성 상태 해제
                    pet.State.SetTreasureHuntingState(false);
                    
                    // TreasureHunting 상태인 펫들만 Idle로 전환 (아직 못 찾은 펫들)
                    if (pet.State.CurrentStatus == PetStatus.TreasureHunting)
                    {
                        // 현재 활동 강제 중단
                        if (pet.AI != null)
                        {
                            pet.AI.ForceStopCurrentActivity();
                        }
                        
                        pet.State.TrySetStatus(PetStatus.Idle);
        // Debug.Log($"[TreasureHuntManager] {pet.petName}: 보물찾기 종료, Idle로 전환");
                        
                        // AI 완전 리셋
                        if (pet.AI != null)
                        {
                            pet.AI.InterruptAndResetAI();
                        }
                    }
                    // TreasureFound 상태인 펫들은 그대로 유지 - 보물을 내려놓고 축하 점프를 계속하도록
                    else if (pet.State.CurrentStatus == PetStatus.TreasureFound)
                    {
        // Debug.Log($"[TreasureHuntManager] {pet.petName}: 보물 찾은 펫은 계속 축하 중 (유지)");
                        // 아무것도 하지 않음 - 펫이 자연스럽게 보물을 내려놓고 축하 점프를 계속함
                        // 유저가 보물을 수집하면 그때 Idle로 전환됨
                    }
                }
            }
        }
        
        isTreasureHuntActive = false;
        OnTreasureHuntEnded?.Invoke();
        
        // 보물찾기 종료 시 내려놓은 보물 리스트는 유지 (유저가 수집할 수 있도록)
        // droppedTreasures.Clear(); // 이 부분은 제거
        
        string endMessage = foundTreasureCount == totalTreasureCount ?
            $"모든 보물을 찾았습니다! ({foundTreasureCount}/{totalTreasureCount})" :
            $"시간이 종료되었습니다! ({foundTreasureCount}/{totalTreasureCount} 찾음)";

        // Debug.Log($"보물찾기 종료! {endMessage}");
        ShowFeedback(endMessage);

        // 시간 종료로 인한 종료일 때만 팝업 표시 (모든 보물을 찾아서 종료하는 경우는 이미 팝업을 표시했음)
        // foundTreasureCount < totalTreasureCount 조건 추가하여 중복 방지
        if (!clearAll && foundTreasureCount > 0 && foundTreasureCount < totalTreasureCount)
        {
            ShowSuccessPopup();
        }
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
            // IsAvailable 체크: 보물이 있고 아직 차지되지 않은 경우만
            if (spot == null || !spot.IsAvailable)
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
    /// 펫이 보물을 찾았을 때 (카운팅만 처리, 보물은 유지)
    /// </summary>
    public void OnPetFoundTreasure(TreasureSpot spot, PetController pet)
    {
        if (!isTreasureHuntActive) return;
        if (spot == null) return;
        
        bool shouldCount = false;
        
        // 동기화 블록 - 중복 카운팅 완벽 방지
        lock (countLock)
        {
            // HashSet과 플래그 이중 체크
            if (!countedSpots.Contains(spot) && !spot.HasBeenCounted)
            {
                // 카운팅 처리
                countedSpots.Add(spot);
                spot.SetCounted();
                foundTreasureCount++;
                shouldCount = true;
                
        // Debug.Log($"[TreasureHuntManager] {pet?.petName}이(가) 보물 카운팅 성공! (스팟: {spot.name}, 현재: {foundTreasureCount}/{totalTreasureCount})");
            }
            else
            {
        // Debug.Log($"[TreasureHuntManager] {pet?.petName}이(가) 이미 카운팅된 보물 접근 - 중복 방지! (스팟: {spot.name}, HashSet: {countedSpots.Contains(spot)}, Flag: {spot.HasBeenCounted})");
                return;
            }
        }
        
        // lock 밖에서 처리 (데드락 방지)
        if (!shouldCount) return;
        
        // 보물은 제거하지 않음! 펫이 물고 가서 내려놓을 것임
        // spot.CollectTreasure() 호출하지 않음
        // activeTreasureSpots에서도 제거하지 않음
        
        // UI 이벤트 발생 (보물 개수 업데이트)
        OnTreasureFound?.Invoke(foundTreasureCount, totalTreasureCount);
        
        // Debug.Log($"{pet?.petName}이(가) 보물을 찾았습니다! ({foundTreasureCount}/{totalTreasureCount})");
        
        // 모든 보물을 찾았는지 확인
        if (foundTreasureCount >= totalTreasureCount && isTreasureHuntActive)
        {
            ShowFeedback("모든 보물을 찾았습니다!");
            // 성공 팝업 표시
            ShowSuccessPopup();
            // 잠시 후 종료 (펫과 보물은 유지)
            StartCoroutine(EndAfterDelay(2f));
        }
    }
    
    /// <summary>
    /// 펫이 보물을 내려놓았을 때 리스트에 추가
    /// </summary>
    public void RegisterDroppedTreasure(TreasureController treasure)
    {
        if (treasure != null && !droppedTreasures.Contains(treasure))
        {
            droppedTreasures.Add(treasure);
        // Debug.Log($"[TreasureHuntManager] 보물이 내려놓아짐. 현재 개수: {droppedTreasures.Count}");
            OnDroppedTreasureUpdate?.Invoke(droppedTreasures.Count);
        }
    }
    
    /// <summary>
    /// 유저가 보물을 수집할 때 리스트에서 제거
    /// </summary>
    public void UnregisterDroppedTreasure(TreasureController treasure)
    {
        if (treasure != null && droppedTreasures.Contains(treasure))
        {
            droppedTreasures.Remove(treasure);
        // Debug.Log($"[TreasureHuntManager] 보물이 수집됨. 남은 개수: {droppedTreasures.Count}");
            OnDroppedTreasureUpdate?.Invoke(droppedTreasures.Count);
        }
    }
    
    /// <summary>
    /// 유저가 보물을 수집할 때 (코인 보상만 처리)
    /// </summary>
    public void CollectTreasure(Vector3 treasurePosition, PetController pet)
    {
        // 코인 보상
        int coins = Random.Range(minCoinReward, maxCoinReward + 1);
        totalCoins += coins;

        // UI 업데이트
        UpdateCoinUI();
        ShowCoinFeedback(coins, treasurePosition);
        
        // 코인 이벤트 발생
        OnCoinsCollected?.Invoke(coins);
        
        // 이 보물을 찾은 펫의 상태 처리
        if (pet != null)
        {
            pet.ShowEmotion(EmotionType.Happy);
            
            // 보물찾기가 아직 진행 중이면 다시 보물 찾기
            if (isTreasureHuntActive)
            {
        // Debug.Log($"{pet.petName}: 다른 보물을 찾으러 갑니다!");
                
                // 보물찾기 상태 유지
                pet.State.SetTreasureHuntingState(true);
                pet.State.TrySetStatus(PetStatus.TreasureHunting);
                
                // AI 리셋하여 새로운 보물 찾기 시작
                if (pet.AI != null)
                {
                    pet.AI.InterruptAndResetAI();
                }
                
                // 참여 펫 목록에는 계속 유지
            }
            else
            {
                // 보물찾기가 종료되었으면 일상으로 복귀
        // Debug.Log($"{pet.petName}: 보물찾기 종료, 일상으로 복귀");
                
                pet.State.SetTreasureHuntingState(false);
                pet.State.TrySetStatus(PetStatus.Idle);
                
                // AI 리셋
                if (pet.AI != null)
                {
                    pet.AI.InterruptAndResetAI();
                }
                
                // 참여 펫 목록에서 제거
                participatingPets.Remove(pet);
            }
        }
        
        // Debug.Log($"유저가 {pet?.petName}이(가) 찾은 보물을 수집했습니다! +{coins} 코인");
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
            totalCoinsText.text = $"{totalCoins}";
        }
    }
    
    /// <summary>
    /// 코인 획득 피드백 표시
    /// </summary>
    private void ShowCoinFeedback(int coins, Vector3 worldPosition)
    {
        if (coinPopupText != null)
        {
            coinPopupText.text = $"+{coins}";
            coinPopupText.gameObject.SetActive(true);

            // 월드 좌표를 스크린 좌표로 변환
            if (Camera.main != null)
            {
                Vector3 screenPos = Camera.main.WorldToScreenPoint(worldPosition + Vector3.up * 2f);
                coinPopupText.transform.position = screenPos;
            }

            // 페이드 아웃 애니메이션
            StartCoroutine(FadeOutText(coinPopupText, 2f));
        }
    }
    
    /// <summary>
    /// 일반 피드백 표시
    /// </summary>
    private void ShowFeedback(string message)
    {
        if (feedbackText != null)
        {
            feedbackText.text = message;
            feedbackText.gameObject.SetActive(true);
            StartCoroutine(HideTextAfterDelay(feedbackText, 3f));
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
    /// 보물찾기 성공 팝업 표시
    /// </summary>
    private void ShowSuccessPopup()
    {
        if (successPopupPrefab != null)
        {
            // Canvas 찾기
            GameObject canvasObject = GameObject.Find("Canvas");
            if (canvasObject == null)
            {
                Debug.LogError("Canvas를 찾을 수 없습니다!");
                return;
            }

            // 팝업 생성
            var popup = Instantiate(successPopupPrefab);
            popup.SetActive(true);
            popup.transform.localScale = Vector3.zero;
            popup.transform.SetParent(canvasObject.transform, false);

            // Ricimi.Popup 컴포넌트가 있으면 Open() 호출
            var popupComponent = popup.GetComponent<Ricimi.Popup>();
            if (popupComponent != null)
            {
                popupComponent.Open();
            }

            // 결과 텍스트 업데이트 (TMP_Text 컴포넌트들을 찾아서 업데이트)
            var textComponents = popup.GetComponentsInChildren<TMPro.TMP_Text>();
            foreach (var text in textComponents)
            {
                // 제목 텍스트 찾아서 업데이트
                if (text.name.Contains("Title") || text.name.Contains("Headline"))
                {
                    text.text = "보물찾기 완료!";
                }
                // 결과 텍스트 찾아서 업데이트
                else if (text.name.Contains("Description") || text.name.Contains("Content"))
                {
                    text.text = $"{totalTreasureCount}개 중 {foundTreasureCount}개 발견!\n+{totalCoins} 코인 획득!";
                }
            }

            Debug.Log($"보물찾기 성공 팝업 표시: {foundTreasureCount}/{totalTreasureCount} 발견, +{totalCoins} 코인");
        }
        else
        {
            Debug.LogWarning("successPopupPrefab이 설정되지 않았습니다!");
        }
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