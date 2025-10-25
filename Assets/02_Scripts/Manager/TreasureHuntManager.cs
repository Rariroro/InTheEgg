using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System.Collections;
using TMPro;
using UnityEngine.UI;

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
    [Range(1, 17)]
    public int minTreasureCount = 2;
    
    [Tooltip("최대 보물 개수")]
    [Range(1, 17)]
    public int maxTreasureCount = 5;
    
    [Header("보상 설정")]
    [Tooltip("보물 하나당 최소 코인")]
    public int minCoinReward = 10;

    [Tooltip("보물 하나당 최대 코인")]
    public int maxCoinReward = 50;

    [Tooltip("모든 보물을 찾았을 때 기본 보너스")]
    public int baseBonusCoins = 100;

    [Tooltip("남은 시간 1초당 보너스 코인")]
    public int bonusCoinsPerSecond = 2;
    
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

    [Tooltip("팝업이 차지할 화면 너비 비율 (0.6 = 화면의 60%)")]
    [Range(0.2f, 0.9f)]
    public float popupScreenRatio = 0.6f;
    
    [Header("상태")]
    [SerializeField] private bool isTreasureHuntActive = false;
    [SerializeField] private int totalCoins = 0;
    [SerializeField] private int missionBonusCoins = 0;  // 이번 미션의 보너스 코인
    private bool bonusApplied = false;  // 보너스 중복 적용 방지
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
    
    // 코인 카운트 애니메이션 관련 필드
    private int displayedCoins = 0;  // 현재 표시되는 코인 수
    private int targetCoins = 0;     // 목표 코인 수
    private Coroutine countCoroutine;  // 카운트 코루틴
    private float countSpeed = 0.02f;  // 카운트 속도 (초당 50개)

    // WaitForSeconds 캐싱 (성능 최적화)
    private readonly WaitForSeconds waitForPopupCleanup = new WaitForSeconds(0.1f);

    // 프로퍼티
    public bool IsTreasureHuntActive => isTreasureHuntActive;
    public List<TreasureSpot> ActiveTreasureSpots => activeTreasureSpots;
    public int TotalCoins => totalCoins;
    public int MissionBonusCoins => missionBonusCoins;  // 보너스 코인 프로퍼티
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

        // 코인 카운트 애니메이션 초기화
        displayedCoins = totalCoins;
        targetCoins = totalCoins;

        // 초기 텍스트 즉시 설정 (게임 시작 시 표시 보장)
        if (totalCoinsText != null)
        {
            totalCoinsText.text = $"{totalCoins}";
        }

        UpdateCoinUI();

        // 게임 시작 시 남아있는 PopupBackground 정리
        StartCoroutine(CleanupPopupBackgroundsOnStart());
    }

    /// <summary>
    /// 게임 시작 시 남아있는 PopupBackground 정리
    /// </summary>
    private IEnumerator CleanupPopupBackgroundsOnStart()
    {
        // Canvas가 생성될 때까지 대기
        yield return null;

        GameObject canvasObject = GameObject.Find("Canvas");
        if (canvasObject != null)
        {
            CleanupExistingPopupBackgrounds(canvasObject);
        }
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
    }
    
    /// <summary>
    /// 보물찾기 시작
    /// </summary>
    public void StartTreasureHunt()
    {
        if (isTreasureHuntActive)
        {
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

        // 이전 보물찾기에서 남은 보물들 삭제
        if (droppedTreasures.Count > 0)
        {
            foreach (var treasure in droppedTreasures)
            {
                if (treasure != null && treasure.gameObject != null)
                {
                    Destroy(treasure.gameObject);
                }
            }
            droppedTreasures.Clear();
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
            ShowFeedback($"친밀도 {requiredAffection} 이상인 펫이 필요합니다!");
            return;
        }
        
        // 보물 생성
        SpawnTreasures();
        
        // 상태 초기화 (완전한 초기화)
        foundTreasureCount = 0;
        totalTreasureCount = activeTreasureSpots.Count;
        remainingTime = treasureHuntDuration;
        missionBonusCoins = 0;  // 보너스 코인 초기화
        bonusApplied = false;  // 보너스 적용 플래그 초기화
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
        
        
        isTreasureHuntActive = true;
        OnTreasureHuntStarted?.Invoke();
        OnTreasureFound?.Invoke(foundTreasureCount, totalTreasureCount);
        
        // 타이머 시작
        if (timerCoroutine != null)
        {
            StopCoroutine(timerCoroutine);
        }
        timerCoroutine = StartCoroutine(TreasureHuntTimer());
        
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
            
            // 펫이 찾은 보물들은 모두 유지 (들고 있거나 내려놓은 상태 모두)
            TreasureController[] allTreasures = FindObjectsOfType<TreasureController>();
            foreach (var treasure in allTreasures)
            {
                if (treasure != null && treasure.gameObject != null)
                {
                    // 내려놓은 보물 유지
                    if (treasure.IsDropped)
                    {
                        continue;
                    }

                    // 펫이 들고 있는 보물도 유지 (펫이 자연스럽게 내려놓을 수 있도록)
                    if (treasure.IsCarried)
                    {
                        continue;
                    }

                    // 아무도 들고 있지 않고, 내려놓지도 않은 보물만 제거 (미발견 보물)
                    Destroy(treasure.gameObject);
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
                        
                        // AI 완전 리셋
                        if (pet.AI != null)
                        {
                            pet.AI.InterruptAndResetAI();
                        }
                    }
                    // TreasureFound 상태인 펫들은 그대로 유지 - 보물을 내려놓고 축하 점프를 계속하도록
                    else if (pet.State.CurrentStatus == PetStatus.TreasureFound)
                    {
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

        // 모든 보물을 찾은 경우는 성공 팝업이 이미 표시되었으므로 feedbackText 표시 안 함
        if (foundTreasureCount < totalTreasureCount)
        {
            string endMessage = $"시간이 종료되었습니다! ({foundTreasureCount}/{totalTreasureCount} 찾음)";
            ShowFeedback(endMessage);
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
                
            }
            else
            {
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
        
        
        // 모든 보물을 찾았는지 확인
        if (foundTreasureCount >= totalTreasureCount && isTreasureHuntActive)
        {
            // 보너스 코인 계산 (모든 보물 찾았을 때)
            CalculateBonusCoins();

            // 성공 팝업 표시 (feedbackText 대신 팝업으로 알림)
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

        // 코인 날아가는 애니메이션 재생
        if (CoinFlyAnimation.Instance != null)
        {
            // 획득 코인 텍스트도 함께 표시
            ShowCoinFeedback(coins, treasurePosition);

            CoinFlyAnimation.Instance.PlayCoinAnimation(treasurePosition, coins, () => {
                // 애니메이션 완료 후 실제 코인 증가
                totalCoins += coins;
                UpdateCoinUI();

                // 코인 이벤트 발생
                OnCoinsCollected?.Invoke(coins);
            });
        }
        else
        {
            // 애니메이션이 없으면 즉시 증가
            totalCoins += coins;
            UpdateCoinUI();
            ShowCoinFeedback(coins, treasurePosition);

            // 코인 이벤트 발생
            OnCoinsCollected?.Invoke(coins);
        }
        
        // 이 보물을 찾은 펫의 상태 처리
        if (pet != null)
        {
            pet.ShowEmotion(EmotionType.Happy);
            
            // 보물찾기가 아직 진행 중이면 다시 보물 찾기
            if (isTreasureHuntActive)
            {
                
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
    /// 코인 UI 업데이트 (애니메이션 적용)
    /// </summary>
    private void UpdateCoinUI()
    {
        targetCoins = totalCoins;

        // 즉시 표시 (처음이거나 같을 때)
        if (displayedCoins == targetCoins)
        {
            if (totalCoinsText != null)
            {
                totalCoinsText.text = $"{displayedCoins}";
            }
            return;  // 애니메이션 필요 없음
        }

        // 차이가 있을 때만 애니메이션
        countCoroutine ??= StartCoroutine(AnimateCoinCount());
    }

    /// <summary>
    /// 코인 카운트 애니메이션
    /// </summary>
    private IEnumerator AnimateCoinCount()
    {
        while (displayedCoins != targetCoins)
        {
            // 차이가 큰 경우 더 빠르게 증가
            int difference = targetCoins - displayedCoins;
            int increment = 1;

            if (Mathf.Abs(difference) > 50)
                increment = 5;  // 50 이상 차이나면 5씩
            else if (Mathf.Abs(difference) > 20)
                increment = 2;  // 20 이상 차이나면 2씩

            // 증가 또는 감소
            if (displayedCoins < targetCoins)
                displayedCoins = Mathf.Min(displayedCoins + increment, targetCoins);
            else if (displayedCoins > targetCoins)
                displayedCoins = Mathf.Max(displayedCoins - increment, targetCoins);

            // UI 업데이트
            if (totalCoinsText != null)
            {
                totalCoinsText.text = $"{displayedCoins}";
            }

            yield return new WaitForSeconds(countSpeed);
        }

        countCoroutine = null;
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
    /// 보너스 코인 계산
    /// </summary>
    private void CalculateBonusCoins()
    {
        if (foundTreasureCount >= totalTreasureCount)
        {
            // 기본 보너스
            missionBonusCoins = baseBonusCoins;

            // 시간 보너스 (남은 시간 초 단위로 계산)
            int timeBonus = Mathf.RoundToInt(remainingTime) * bonusCoinsPerSecond;
            missionBonusCoins += timeBonus;

        }
        else
        {
            // 모든 보물을 찾지 못한 경우 부분 보너스 (선택사항)
            missionBonusCoins = 0;
        }
    }

    /// <summary>
    /// 보너스 코인을 전체 코인에 적용
    /// </summary>
    private void ApplyBonusCoins()
    {
        if (!bonusApplied && missionBonusCoins > 0)
        {
            bonusApplied = true;

            // 성공 팝업의 중앙 위치에서 코인 애니메이션 시작
            if (CoinFlyAnimation.Instance != null)
            {
                // 화면 중앙 위치 계산
                Vector3 screenCenter = new Vector3(Screen.width / 2f, Screen.height / 2f, 0);

                CoinFlyAnimation.Instance.PlayCoinAnimationFromScreen(screenCenter, missionBonusCoins, () => {
                    // 애니메이션 완료 후 실제 코인 증가
                    totalCoins += missionBonusCoins;

                    // 코인 저장 및 UI 업데이트
                    SaveCoins();
                    UpdateCoinUI();

                    Debug.Log($"보너스 코인 {missionBonusCoins}이(가) 적용되었습니다. 총 코인: {totalCoins}");
                });
            }
            else
            {
                // 애니메이션이 없으면 즉시 증가
                totalCoins += missionBonusCoins;

                // 코인 저장 및 UI 업데이트
                SaveCoins();
                UpdateCoinUI();

                Debug.Log($"보너스 코인 {missionBonusCoins}이(가) 적용되었습니다. 총 코인: {totalCoins}");
            }
        }
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

            // 기존 PopupBackground가 있으면 먼저 제거 (이전 팝업의 잔재 정리)
            CleanupExistingPopupBackgrounds(canvasObject);

            // 팝업을 감쌀 래퍼 오브젝트 생성 (애니메이션과 scale 충돌 방지)
            GameObject wrapper = new("PopupWrapper");
            wrapper.transform.SetParent(canvasObject.transform, false);
            wrapper.AddComponent<RectTransform>();

            // 팝업 생성
            var popup = Instantiate(successPopupPrefab);
            popup.SetActive(true);
            popup.transform.SetParent(wrapper.transform, false);

            // Canvas 크기 기반 동적 scale 계산 (모든 기기에서 일관된 화면 비율 유지)
            var canvasRect = canvasObject.GetComponent<RectTransform>();
            if (canvasRect != null)
            {
                // 팝업의 원본 크기 가져오기
                var popupRect = popup.GetComponent<RectTransform>();
                if (popupRect != null)
                {
                    float popupWidth = popupRect.sizeDelta.x;

                    // 목표: 팝업이 Canvas 너비의 X%를 차지하도록 scale 계산
                    float targetWidth = canvasRect.sizeDelta.x * popupScreenRatio;
                    float scale = targetWidth / popupWidth;

                    // wrapper의 scale 조절 (애니메이션이 건드리지 않음)
                    wrapper.transform.localScale = Vector3.one * scale;
                }
            }

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
                    text.text = "SUCCESS!";
                }
                // 결과 텍스트 찾아서 업데이트
                else if (text.name.Contains("Description") || text.name.Contains("Content"))
                {
                    text.text = $"모든 보물을 찾았습니다!\n 성공 보너스 코인 획득!!";
                }
                // Amount Text 오브젝트 찾아서 업데이트
                else if (text.name.Contains("Amount"))
                {
                    text.text = $"{missionBonusCoins}";
                }
            }

            // Close 버튼 찾아서 리스너 추가
            var buttons = popup.GetComponentsInChildren<Button>();
            foreach (var button in buttons)
            {
                // Close 버튼 찾기 (버튼 이름이나 부모 오브젝트 이름으로 판단)
                if (button.name.Contains("Close") || button.name.Contains("Button"))
                {
                    // Popup 컴포넌트의 Close 이벤트를 감지하는 방식으로 변경
                    var popupComp = popup.GetComponent<Ricimi.Popup>();
                    if (popupComp != null)
                    {
                        // Popup이 닫힐 때 실행될 코루틴 시작
                        StartCoroutine(WaitForPopupCloseAndApplyBonus(popup, wrapper, button));
                    }
                    else
                    {
                        // Popup 컴포넌트가 없으면 기존 방식 사용
                        button.onClick.AddListener(() => {
                            ApplyBonusCoins();
                        });
                    }
                    break;  // 첫 번째 Close 버튼만 처리
                }
            }

            Debug.Log($"보물찾기 성공 팝업 표시: {foundTreasureCount}/{totalTreasureCount} 발견, 보너스 +{missionBonusCoins} 코인");
        }
        else
        {
            Debug.LogWarning("successPopupPrefab이 설정되지 않았습니다!");
        }
    }

    /// <summary>
    /// 기존 PopupBackground 오브젝트 정리
    /// </summary>
    private void CleanupExistingPopupBackgrounds(GameObject canvasObject)
    {
        if (canvasObject == null) return;

        Transform[] children = canvasObject.GetComponentsInChildren<Transform>(true);
        foreach (Transform child in children)
        {
            if (child != null && (child.name == "PopupBackground" || child.name.Contains("PopupBackground")))
            {
                Debug.Log($"[TreasureHunt] 기존 PopupBackground 제거: {child.name}");
                Destroy(child.gameObject);
            }
        }
    }

    /// <summary>
    /// 팝업이 닫힐 때까지 대기 후 보너스 적용
    /// </summary>
    private IEnumerator WaitForPopupCloseAndApplyBonus(GameObject popup, GameObject wrapper, Button closeButton)
    {
        bool buttonClicked = false;

        // 버튼 클릭 이벤트 감지
        UnityEngine.Events.UnityAction onClickAction = () => {
            buttonClicked = true;
        };

        closeButton.onClick.AddListener(onClickAction);

        // 버튼이 클릭되거나 팝업이 삭제될 때까지 대기
        while (!buttonClicked && popup != null)
        {
            yield return null;
        }

        // 리스너 제거
        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(onClickAction);
        }

        // 보너스 코인 적용 (한 번만 실행)
        if (!bonusApplied && missionBonusCoins > 0)
        {
            ApplyBonusCoins();
        }

        // PopupWrapper 제거
        if (wrapper != null)
        {
            Destroy(wrapper);
        }

        // PopupBackground 제거 (Ricimi Popup이 생성한 배경 오브젝트)
        yield return waitForPopupCleanup;  // 팝업 애니메이션 완료 대기

        GameObject canvasObject = GameObject.Find("Canvas");
        if (canvasObject != null)
        {
            // Canvas 하위의 모든 PopupBackground 찾아서 제거
            Transform[] children = canvasObject.GetComponentsInChildren<Transform>(true);
            foreach (Transform child in children)
            {
                if (child.name == "PopupBackground" || child.name.Contains("PopupBackground"))
                {
                    Debug.Log($"[TreasureHunt] PopupBackground 제거: {child.name}");
                    Destroy(child.gameObject);
                }
            }
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

        // 카운트 코루틴 정리
        if (countCoroutine != null)
        {
            StopCoroutine(countCoroutine);
            countCoroutine = null;
        }

        if (instance == this)
        {
            instance = null;
        }
    }
}