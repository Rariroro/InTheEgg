using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using FlutterIntegration;
using LegendaryPet;

/// <summary>
/// Flutter와의 양방향 통신을 관리하는 브릿지 클래스
/// PetVillage 씬에 배치하여 사용
/// </summary>
public class FlutterBridge : MonoBehaviour
{
    public static FlutterBridge Instance { get; private set; }

    [Header("동기화 설정")]
    [Tooltip("친밀도 동기화 주기 (초)")]
    [SerializeField] private float intimacySyncInterval = 30f;

    [Header("에디터 테스트 설정")]
    [Tooltip("Flutter 데이터 대기 시간 (초). 이 시간 안에 데이터가 안 오면 기존 모드로 진행")]
    [SerializeField] private float flutterDataTimeout = 3f;

    [Header("재시도 설정")]
    [SerializeField] private float[] retryDelays = { 3f, 5f, 10f, 20f, 30f };

    private FlutterMessageQueue messageQueue;
    private Coroutine intimacySyncCoroutine;
    private Coroutine messageQueueCoroutine;
    private bool isUnityReady = false;

    // 스폰된 펫 ID -> PetController 매핑 (친밀도 동기화용)
    private Dictionary<string, PetController> spawnedPets = new Dictionary<string, PetController>();

    // 마지막 동기화 시점의 친밀도 (변경된 것만 전송하기 위함)
    private Dictionary<string, int> lastSyncedIntimacy = new Dictionary<string, int>();

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            transform.SetParent(null);
            DontDestroyOnLoad(gameObject);
            messageQueue = new FlutterMessageQueue(retryDelays);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        // Unity 준비 완료 알림
        SendUnityReady();
        isUnityReady = true;

        // 메시지 큐 처리 시작
        messageQueueCoroutine = StartCoroutine(ProcessMessageQueueLoop());
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus)
        {
            // 백그라운드 전환 시 친밀도 동기화
            if (FlutterModeManager.Instance?.IsFlutterMode == true)
            {
                SendSyncIntimacy(false);
            }

            // 나갈 때 모든 오브젝트 정리
            CleanupForExit();
        }
        else
        {
            // ★ 앱 재개 시 (재진입) - READY 재전송
            Debug.Log("[FlutterBridge] 앱 재개 감지 - READY 재전송");
            ResetForNewSession();
            SendUnityReady();
            isUnityReady = true;
        }
    }

    /// <summary>
    /// Unity 나갈 때 모든 오브젝트 정리 (코루틴 중지 후 파괴)
    /// </summary>
    private void CleanupForExit()
    {
        Debug.Log("[FlutterBridge] Unity 나가기 - 오브젝트 정리 시작");

        // 1. 모든 상호작용 중지 및 제거
        var interactions = FindObjectsOfType<BasePetInteraction>();
        foreach (var interaction in interactions)
        {
            if (interaction != null)
            {
                interaction.StopAllCoroutines();
                Destroy(interaction.gameObject);
            }
        }

        // 2. 모든 펫 코루틴 중지 및 제거
        var pets = FindObjectsOfType<PetController>();
        foreach (var pet in pets)
        {
            if (pet != null)
            {
                pet.StopAllCoroutines();
                Destroy(pet.gameObject);
            }
        }

        // 3. 모든 레전드 펫 제거
        var legendaryPets = FindObjectsOfType<LegendaryPetController>();
        foreach (var pet in legendaryPets)
        {
            if (pet != null)
            {
                pet.StopAllCoroutines();
                Destroy(pet.gameObject);
            }
        }

        Debug.Log("[FlutterBridge] Unity 나가기 - 오브젝트 정리 완료");
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (hasFocus && !isUnityReady)
        {
            // 포커스 획득 시 Unity가 준비되지 않았으면 READY 전송
            Debug.Log("[FlutterBridge] 포커스 획득 - READY 전송");
            SendUnityReady();
            isUnityReady = true;
        }
    }

    private void OnApplicationQuit()
    {
        if (FlutterModeManager.Instance?.IsFlutterMode == true)
        {
            // 게임 종료 시 GAME_EXIT 전송
            SendSyncIntimacy(true);
        }
    }

    private void OnDestroy()
    {
        if (intimacySyncCoroutine != null)
        {
            StopCoroutine(intimacySyncCoroutine);
        }
        if (messageQueueCoroutine != null)
        {
            StopCoroutine(messageQueueCoroutine);
        }
    }

    #region 메시지 수신 (Flutter -> Unity)

    /// <summary>
    /// Unity 준비 완료 메시지 전송
    /// </summary>
    private void SendUnityReady()
    {
        var message = new UnityReadyMessage();
        string json = message.ToJson();

        Debug.Log($"[FlutterBridge] READY 전송: {json}");
        SendToFlutter.Send(json);
    }

    /// <summary>
    /// Flutter 화면이 (재)진입했을 때 호출되는 메서드
    /// flutter_embed_unity가 이 메서드를 호출함
    /// 사용법: sendToUnity("FlutterManager", "OnScreenEntered", jsonString)
    /// </summary>
    public void OnScreenEntered(string jsonMessage)
    {
        Debug.Log($"[FlutterBridge] SCREEN_ENTERED 수신: {jsonMessage}");

        // 새 세션을 위해 상태 리셋
        ResetForNewSession();

        // READY 재전송 → Flutter가 INIT_GAME을 보내도록 함
        SendUnityReady();
        isUnityReady = true;
    }

    /// <summary>
    /// 새 세션을 위해 상태 리셋
    /// </summary>
    private void ResetForNewSession()
    {
        Debug.Log("[FlutterBridge] 새 세션을 위해 상태 리셋");

        // 상태 리셋
        isUnityReady = false;
        spawnedPets.Clear();
        lastSyncedIntimacy.Clear();
        messageQueue?.Clear();

        // FlutterModeManager도 리셋
        FlutterModeManager.Instance?.ResetForNewSession();

        // 친밀도 동기화 코루틴 정리 (새 INIT_GAME 후 다시 시작됨)
        if (intimacySyncCoroutine != null)
        {
            StopCoroutine(intimacySyncCoroutine);
            intimacySyncCoroutine = null;
        }
    }

    /// <summary>
    /// Flutter에서 INIT_GAME 메시지를 받을 때 호출되는 메서드
    /// flutter_embed_unity가 이 메서드를 호출함
    /// 사용법: sendToUnity("FlutterManager", "OnInitGame", jsonString)
    /// </summary>
    public void OnInitGame(string jsonMessage)
    {
        Debug.Log($"[FlutterBridge] 메시지 수신: {jsonMessage}");

        try
        {
            // JSON 파싱
            var message = JsonUtility.FromJson<FlutterMessage>(jsonMessage);

            if (message == null || string.IsNullOrEmpty(message.type))
            {
                Debug.LogError("[FlutterBridge] 잘못된 메시지 형식입니다.");
                return;
            }

            switch (message.type)
            {
                case "INIT_GAME":
                    HandleInitGame(message.data);
                    break;

                default:
                    Debug.LogWarning($"[FlutterBridge] 알 수 없는 메시지 타입: {message.type}");
                    break;
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[FlutterBridge] JSON 파싱 오류: {e.Message}\n{jsonMessage}");
        }
    }

    /// <summary>
    /// INIT_GAME 메시지 처리
    /// </summary>
    private void HandleInitGame(FlutterGameData data)
    {
        if (FlutterModeManager.Instance != null)
        {
            FlutterModeManager.Instance.InitializeWithFlutterData(data);
            StartIntimacySync();
        }
        else
        {
            Debug.LogError("[FlutterBridge] FlutterModeManager가 없습니다.");
        }
    }

    #endregion

    #region 메시지 송신 (Unity -> Flutter)

    /// <summary>
    /// 일반 펫 스폰 완료 알림 (Egg 터치 시)
    /// </summary>
    public void SendPetSpawned(string petCardId, PetController controller = null)
    {
        if (!FlutterModeManager.Instance?.IsFlutterMode ?? true) return;

        var message = new PetSpawnedMessage(petCardId);
        SendMessageWithRetry(message, true);

        // 펫 컨트롤러 등록 (친밀도 동기화용)
        if (controller != null && !string.IsNullOrEmpty(petCardId))
        {
            RegisterSpawnedPet(petCardId, controller);
        }

        Debug.Log($"[FlutterBridge] PET_SPAWNED 전송: {petCardId}");
    }

    /// <summary>
    /// 레전드 펫 스폰 완료 알림 (Gift 터치 시)
    /// </summary>
    public void SendLegendPetSpawned(string petCardId)
    {
        if (!FlutterModeManager.Instance?.IsFlutterMode ?? true) return;

        var message = new LegendPetSpawnedMessage(petCardId);
        SendMessageWithRetry(message, true);

        Debug.Log($"[FlutterBridge] LEGEND_PET_SPAWNED 전송: {petCardId}");
    }

    /// <summary>
    /// 환경 아이템 스폰 완료 알림 (선물상자 터치 시)
    /// </summary>
    public void SendEnvItemSpawned(string envId)
    {
        if (!FlutterModeManager.Instance?.IsFlutterMode ?? true) return;

        var message = new EnvItemSpawnedMessage(envId);
        SendMessageWithRetry(message, true);

        Debug.Log($"[FlutterBridge] ENV_ITEM_SPAWNED 전송: {envId}");
    }

    /// <summary>
    /// 음식 사용 알림
    /// </summary>
    public void SendFoodUsed(string foodId, int usedQuantity = 1)
    {
        if (!FlutterModeManager.Instance?.IsFlutterMode ?? true) return;

        var message = new FoodUsedMessage(foodId, usedQuantity);
        SendMessageWithRetry(message, true);

        Debug.Log($"[FlutterBridge] FOOD_USED 전송: {foodId} x{usedQuantity}");
    }

    /// <summary>
    /// 음식 타입으로 음식 사용 알림 (편의 메서드)
    /// </summary>
    public void SendFoodUsedByType(string foodType, int usedQuantity = 1)
    {
        string foodId = FlutterModeManager.GetFoodIdFromType(foodType);
        if (!string.IsNullOrEmpty(foodId))
        {
            SendFoodUsed(foodId, usedQuantity);
        }
    }

    /// <summary>
    /// 코인 획득 알림 (v3.0)
    /// </summary>
    /// <param name="amount">이번에 획득한 코인</param>
    /// <param name="totalCoins">획득 후 총 코인</param>
    public void SendCoinEarned(int amount, int totalCoins)
    {
        if (!FlutterModeManager.Instance?.IsFlutterMode ?? true) return;

        var message = new CoinEarnedMessage(amount, totalCoins);
        SendMessageWithRetry(message, true);  // 재시도 포함

        Debug.Log($"[FlutterBridge] COIN_EARNED 전송: +{amount}, 총 {totalCoins}");
    }

    /// <summary>
    /// 로딩 완료 알림 - Flutter에 게임 시작 가능 알림
    /// </summary>
    public void SendLoadingComplete()
    {
        var message = new LoadingCompleteMessage();
        string json = message.ToJson();

        Debug.Log($"[FlutterBridge] LOADING_COMPLETE 전송: {json}");
        SendToFlutter.Send(json);
    }

    /// <summary>
    /// 친밀도 동기화 전송 (변경된 펫만 전송하여 성능 최적화)
    /// </summary>
    public void SendSyncIntimacy(bool isGameExit = false)
    {
        if (!FlutterModeManager.Instance?.IsFlutterMode ?? true) return;

        // 변경된 펫들의 친밀도만 수집
        var changedPets = new List<PetIntimacyData>();

        foreach (var kvp in spawnedPets)
        {
            if (kvp.Value != null && kvp.Value.Needs != null)
            {
                int currentIntimacy = Mathf.RoundToInt(kvp.Value.Needs.Affection);

                // 이전 동기화 값과 비교하여 변경된 것만 추가
                bool hasChanged = !lastSyncedIntimacy.TryGetValue(kvp.Key, out int lastIntimacy)
                                  || currentIntimacy != lastIntimacy;

                if (hasChanged || isGameExit)
                {
                    // FlutterModeManager 캐시도 업데이트
                    FlutterModeManager.Instance?.UpdatePetIntimacy(kvp.Key, currentIntimacy);

                    changedPets.Add(new PetIntimacyData
                    {
                        petCardId = kvp.Key,
                        petIntimacy = currentIntimacy
                    });

                    // 마지막 동기화 값 업데이트
                    lastSyncedIntimacy[kvp.Key] = currentIntimacy;
                }
            }
        }

        // 변경된 게 있거나 게임 종료 시에만 전송
        if (changedPets.Count > 0)
        {
            var message = new SyncIntimacyMessage(changedPets, isGameExit);
            SendMessageWithRetry(message, false); // 친밀도는 재시도 없음

            string messageType = isGameExit ? "GAME_EXIT" : "SYNC_INTIMACY";
            Debug.Log($"[FlutterBridge] {messageType} 전송: {changedPets.Count}마리 (변경분만)");
        }
        else if (!isGameExit)
        {
            Debug.Log("[FlutterBridge] 친밀도 변경 없음 - 전송 스킵");
        }
    }

    #endregion

    #region 펫 등록/해제

    /// <summary>
    /// 펫 등록 (스폰 시 호출)
    /// </summary>
    public void RegisterSpawnedPet(string petCardId, PetController controller)
    {
        if (string.IsNullOrEmpty(petCardId) || controller == null) return;

        if (!spawnedPets.ContainsKey(petCardId))
        {
            spawnedPets[petCardId] = controller;
            Debug.Log($"[FlutterBridge] 펫 등록: {petCardId}");
        }
    }

    /// <summary>
    /// 펫 해제 (파괴 시 호출)
    /// </summary>
    public void UnregisterPet(string petCardId)
    {
        if (spawnedPets.Remove(petCardId))
        {
            Debug.Log($"[FlutterBridge] 펫 해제: {petCardId}");
        }
    }

    #endregion

    #region 내부 유틸리티

    private void SendMessageWithRetry(FlutterOutboundMessage message, bool useRetry)
    {
        string json = message.ToJson();

        if (useRetry)
        {
            // 먼저 즉시 전송 시도
            bool success = SendToFlutterInternal(json);
            if (!success)
            {
                // 실패 시 큐에 추가
                messageQueue.Enqueue(json, () => SendToFlutterInternal(json));
            }
        }
        else
        {
            SendToFlutterInternal(json);
        }
    }

    private bool SendToFlutterInternal(string json)
    {
        try
        {
            SendToFlutter.Send(json);
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"[FlutterBridge] 전송 실패: {e.Message}");
            return false;
        }
    }

    private IEnumerator ProcessMessageQueueLoop()
    {
        while (true)
        {
            if (messageQueue.HasPendingMessages())
            {
                var pending = messageQueue.GetNextReadyMessage();
                if (pending != null)
                {
                    bool success = pending.SendAction();
                    if (success)
                    {
                        messageQueue.MarkComplete(pending);
                    }
                    else
                    {
                        messageQueue.ScheduleRetry(pending);
                    }
                }
            }

            yield return new WaitForSeconds(1f); // 1초마다 체크
        }
    }

    private void StartIntimacySync()
    {
        if (intimacySyncCoroutine != null)
        {
            StopCoroutine(intimacySyncCoroutine);
        }
        intimacySyncCoroutine = StartCoroutine(IntimacySyncLoop());
    }

    private IEnumerator IntimacySyncLoop()
    {
        while (true)
        {
            yield return new WaitForSeconds(intimacySyncInterval);
            SendSyncIntimacy(false);
        }
    }

    #endregion

    #region 대기 유틸리티

    /// <summary>
    /// Flutter 데이터 수신 대기 코루틴
    /// EnvironmentManager에서 사용
    /// </summary>
    public IEnumerator WaitForFlutterDataOrTimeout()
    {
        float elapsed = 0f;

        while (elapsed < flutterDataTimeout)
        {
            // Flutter 데이터가 도착했으면 즉시 반환
            if (FlutterModeManager.Instance != null && FlutterModeManager.Instance.IsFlutterMode)
            {
                Debug.Log("[FlutterBridge] Flutter 데이터 수신 완료");
                yield break;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        Debug.Log("[FlutterBridge] Flutter 데이터 대기 타임아웃 - 기존 모드로 진행");
    }

    /// <summary>
    /// Flutter 데이터가 수신되었는지 확인
    /// </summary>
    public bool HasReceivedFlutterData()
    {
        return FlutterModeManager.Instance != null && FlutterModeManager.Instance.IsFlutterMode;
    }

    #endregion
}
