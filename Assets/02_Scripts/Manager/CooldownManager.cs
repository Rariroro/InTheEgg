using System.Collections.Generic;
using UnityEngine;
using System;
using System.Linq;

/// <summary>
/// 게임 내 모든 쿨타임을 중앙에서 관리하는 매니저
/// 각 시스템의 쿨타임을 통합 관리하여 일관성과 유지보수성 향상
/// </summary>
public class CooldownManager : MonoBehaviour
{
    // 싱글톤 인스턴스
    public static CooldownManager Instance { get; private set; }

    [Header("쿨타임 설정")]
    [SerializeField] private CooldownSettings settings;

    [Header("디버그")]
    [SerializeField] private bool enableDebugLog = false;
    [SerializeField] private bool showActiveCooldowns = false;

    /// <summary>
    /// 쿨타임 타입 정의
    /// </summary>
    public enum CooldownType
    {
        // === 펫 상호작용 ===
        PetInteraction,         // 펫 간 일반 상호작용

        // === 활동별 쿨타임 ===
        Diving,                 // 다이빙 성공 후
        DivingFailed,          // 다이빙 실패 후
        TreeClimbing,          // 나무 오르기 탐색
        ButterflyPlay,         // 나비 놀이
        TreasureHunt,          // 보물 찾기

        // === 환경 상호작용 ===
        EnvironmentTouch,      // 환경 터치 입력
        GiftTouch,             // 선물 터치

        // === 먹이 관련 ===
        Feeding,               // 먹이 먹기
        FoodSearch,            // 먹이 탐색

        // === 특수 상호작용 ===
        ChaseAndRun,           // 추격전
        WalkTogether,          // 함께 걷기
        Race,                  // 경주
        Fight,                 // 싸움
        SleepTogether,         // 함께 자기

        // === 기타 ===
        Custom                 // 커스텀 쿨타임 (동적 생성용)
    }

    /// <summary>
    /// 쿨타임 정보를 저장하는 구조체
    /// </summary>
    [System.Serializable]
    private class CooldownInfo
    {
        public string id;               // 쿨타임 고유 ID (타입_엔티티ID 형식)
        public CooldownType type;       // 쿨타임 타입
        public float startTime;         // 쿨타임 시작 시간
        public float duration;          // 쿨타임 지속 시간
        public string entityId;         // 엔티티 ID (펫 이름 등)
        public Action onComplete;       // 쿨타임 완료 시 콜백

        public float RemainingTime => Mathf.Max(0, duration - (Time.time - startTime));
        public bool IsActive => Time.time < startTime + duration;
        public float Progress => Mathf.Clamp01((Time.time - startTime) / duration);
    }

    // 활성 쿨타임 저장소 (키: 쿨타임ID)
    private Dictionary<string, CooldownInfo> activeCooldowns = new Dictionary<string, CooldownInfo>();

    // 쿨타임 완료 이벤트
    public event Action<CooldownType, string> OnCooldownComplete;
    public event Action<CooldownType, string> OnCooldownStarted;

    private void Awake()
    {
        // 싱글톤 설정
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Initialize();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Initialize()
    {
        // Settings가 없으면 기본값으로 생성 시도
        if (settings == null)
        {
            settings = Resources.Load<CooldownSettings>("CooldownSettings");
            if (settings == null)
            {
                Debug.LogWarning("[CooldownManager] CooldownSettings를 찾을 수 없습니다. 기본값을 사용합니다.");
                Debug.LogWarning("[CooldownManager] Unity 에디터에서 우클릭 → Create → InTheEgg → Cooldown Settings로 생성하세요.");
                Debug.LogWarning("[CooldownManager] 또는 메뉴 → InTheEgg → Create Cooldown Settings Asset을 선택하세요.");
            }
            else
            {
                Debug.Log("[CooldownManager] CooldownSettings 로드 완료!");
            }
        }
    }

    private void Update()
    {
        // 완료된 쿨타임 체크 및 제거
        CheckCompletedCooldowns();

        // 디버그 표시
        if (showActiveCooldowns && activeCooldowns.Count > 0)
        {
            ShowDebugInfo();
        }
    }

    /// <summary>
    /// 특정 쿨타임이 활성 상태인지 확인
    /// </summary>
    /// <param name="type">쿨타임 타입</param>
    /// <param name="entityId">엔티티 ID (선택사항, 펫 이름 등)</param>
    /// <returns>쿨타임 중이면 true</returns>
    public bool IsOnCooldown(CooldownType type, string entityId = null)
    {
        string cooldownId = GenerateCooldownId(type, entityId);

        if (activeCooldowns.TryGetValue(cooldownId, out CooldownInfo info))
        {
            return info.IsActive;
        }

        return false;
    }

    /// <summary>
    /// 쿨타임 시작
    /// </summary>
    /// <param name="type">쿨타임 타입</param>
    /// <param name="entityId">엔티티 ID (선택사항)</param>
    /// <param name="customDuration">커스텀 지속 시간 (0이면 설정값 사용)</param>
    /// <param name="onComplete">완료 시 콜백</param>
    public void StartCooldown(CooldownType type, string entityId = null, float customDuration = 0f, Action onComplete = null)
    {
        string cooldownId = GenerateCooldownId(type, entityId);
        float duration = customDuration > 0 ? customDuration : GetDefaultDuration(type);

        if (duration <= 0)
        {
            Debug.LogWarning($"[CooldownManager] 잘못된 쿨타임 지속시간: {type} - {duration}");
            return;
        }

        // 기존 쿨타임 덮어쓰기
        CooldownInfo info = new CooldownInfo
        {
            id = cooldownId,
            type = type,
            startTime = Time.time,
            duration = duration,
            entityId = entityId,
            onComplete = onComplete
        };

        activeCooldowns[cooldownId] = info;

        if (enableDebugLog)
        {
            Debug.Log($"[CooldownManager] 쿨타임 시작: {type} ({entityId ?? "Global"}) - {duration}초");
        }

        // 이벤트 발생
        OnCooldownStarted?.Invoke(type, entityId);
    }

    /// <summary>
    /// 남은 쿨타임 시간 확인
    /// </summary>
    /// <param name="type">쿨타임 타입</param>
    /// <param name="entityId">엔티티 ID (선택사항)</param>
    /// <returns>남은 시간 (초), 쿨타임이 없으면 0</returns>
    public float GetRemainingTime(CooldownType type, string entityId = null)
    {
        string cooldownId = GenerateCooldownId(type, entityId);

        if (activeCooldowns.TryGetValue(cooldownId, out CooldownInfo info))
        {
            return info.RemainingTime;
        }

        return 0f;
    }

    /// <summary>
    /// 쿨타임 진행률 확인 (0~1)
    /// </summary>
    public float GetProgress(CooldownType type, string entityId = null)
    {
        string cooldownId = GenerateCooldownId(type, entityId);

        if (activeCooldowns.TryGetValue(cooldownId, out CooldownInfo info))
        {
            return info.Progress;
        }

        return 1f; // 쿨타임이 없으면 완료 상태
    }

    /// <summary>
    /// 특정 쿨타임 강제 리셋
    /// </summary>
    /// <param name="type">쿨타임 타입</param>
    /// <param name="entityId">엔티티 ID (선택사항)</param>
    public void ResetCooldown(CooldownType type, string entityId = null)
    {
        string cooldownId = GenerateCooldownId(type, entityId);

        if (activeCooldowns.Remove(cooldownId))
        {
            if (enableDebugLog)
            {
                Debug.Log($"[CooldownManager] 쿨타임 리셋: {type} ({entityId ?? "Global"})");
            }
        }
    }

    /// <summary>
    /// 특정 엔티티의 모든 쿨타임 리셋
    /// </summary>
    /// <param name="entityId">엔티티 ID</param>
    public void ResetEntityCooldowns(string entityId)
    {
        if (string.IsNullOrEmpty(entityId)) return;

        var keysToRemove = activeCooldowns
            .Where(kvp => kvp.Value.entityId == entityId)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in keysToRemove)
        {
            activeCooldowns.Remove(key);
        }

        if (enableDebugLog && keysToRemove.Count > 0)
        {
            Debug.Log($"[CooldownManager] {entityId}의 모든 쿨타임 리셋 ({keysToRemove.Count}개)");
        }
    }

    /// <summary>
    /// 모든 쿨타임 리셋
    /// </summary>
    public void ResetAllCooldowns()
    {
        int count = activeCooldowns.Count;
        activeCooldowns.Clear();

        if (enableDebugLog)
        {
            Debug.Log($"[CooldownManager] 모든 쿨타임 리셋 ({count}개)");
        }
    }

    /// <summary>
    /// 특정 타입의 모든 쿨타임 리셋
    /// </summary>
    public void ResetTypeCooldowns(CooldownType type)
    {
        var keysToRemove = activeCooldowns
            .Where(kvp => kvp.Value.type == type)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in keysToRemove)
        {
            activeCooldowns.Remove(key);
        }

        if (enableDebugLog && keysToRemove.Count > 0)
        {
            Debug.Log($"[CooldownManager] {type} 타입의 모든 쿨타임 리셋 ({keysToRemove.Count}개)");
        }
    }

    /// <summary>
    /// 쿨타임 지속시간 수정 (런타임)
    /// </summary>
    public void ModifyCooldown(CooldownType type, string entityId, float newDuration)
    {
        string cooldownId = GenerateCooldownId(type, entityId);

        if (activeCooldowns.TryGetValue(cooldownId, out CooldownInfo info))
        {
            info.duration = newDuration;

            if (enableDebugLog)
            {
                Debug.Log($"[CooldownManager] 쿨타임 수정: {type} ({entityId ?? "Global"}) - 새 지속시간: {newDuration}초");
            }
        }
    }

    /// <summary>
    /// 활성 쿨타임 목록 가져오기
    /// </summary>
    public List<string> GetActiveCooldowns()
    {
        return activeCooldowns
            .Where(kvp => kvp.Value.IsActive)
            .Select(kvp => $"{kvp.Value.type} ({kvp.Value.entityId ?? "Global"}): {kvp.Value.RemainingTime:F1}초")
            .ToList();
    }

    /// <summary>
    /// 특정 엔티티의 활성 쿨타임 목록 가져오기
    /// </summary>
    public List<CooldownType> GetEntityCooldowns(string entityId)
    {
        return activeCooldowns
            .Where(kvp => kvp.Value.entityId == entityId && kvp.Value.IsActive)
            .Select(kvp => kvp.Value.type)
            .ToList();
    }

    // === Private Methods ===

    /// <summary>
    /// 쿨타임 고유 ID 생성
    /// </summary>
    private string GenerateCooldownId(CooldownType type, string entityId)
    {
        return string.IsNullOrEmpty(entityId) ?
            $"{type}" :
            $"{type}_{entityId}";
    }

    /// <summary>
    /// 기본 쿨타임 지속시간 가져오기
    /// </summary>
    private float GetDefaultDuration(CooldownType type)
    {
        if (settings != null)
        {
            return settings.GetCooldownDuration(type);
        }

        // Settings가 없으면 하드코딩된 기본값 사용
        switch (type)
        {
            case CooldownType.PetInteraction: return 30f;
            case CooldownType.Diving: return 30f;
            case CooldownType.DivingFailed: return 60f;
            case CooldownType.TreeClimbing: return 10f;
            case CooldownType.ButterflyPlay: return 0f;
            case CooldownType.EnvironmentTouch: return 0.1f;
            case CooldownType.ChaseAndRun: return 30f;
            case CooldownType.WalkTogether: return 30f;
            default: return 10f;
        }
    }

    /// <summary>
    /// 완료된 쿨타임 체크 및 제거
    /// </summary>
    private void CheckCompletedCooldowns()
    {
        var completedCooldowns = activeCooldowns
            .Where(kvp => !kvp.Value.IsActive)
            .ToList();

        foreach (var kvp in completedCooldowns)
        {
            CooldownInfo info = kvp.Value;

            // 콜백 실행
            info.onComplete?.Invoke();

            // 이벤트 발생
            OnCooldownComplete?.Invoke(info.type, info.entityId);

            // 딕셔너리에서 제거
            activeCooldowns.Remove(kvp.Key);

            if (enableDebugLog)
            {
                Debug.Log($"[CooldownManager] 쿨타임 완료: {info.type} ({info.entityId ?? "Global"})");
            }
        }
    }

    /// <summary>
    /// 디버그 정보 표시
    /// </summary>
    private void ShowDebugInfo()
    {
        if (Time.frameCount % 60 == 0) // 1초마다 업데이트
        {
            string debugInfo = "[활성 쿨타임]\n";
            foreach (var kvp in activeCooldowns.Where(x => x.Value.IsActive))
            {
                debugInfo += $"- {kvp.Value.type} ({kvp.Value.entityId ?? "Global"}): {kvp.Value.RemainingTime:F1}초 남음\n";
            }
            Debug.Log(debugInfo);
        }
    }

    // === Editor Methods ===

    [ContextMenu("모든 쿨타임 상태 출력")]
    private void PrintAllCooldowns()
    {
        if (activeCooldowns.Count == 0)
        {
            Debug.Log("[CooldownManager] 활성 쿨타임 없음");
            return;
        }

        string output = $"[CooldownManager] 활성 쿨타임 ({activeCooldowns.Count}개)\n";
        foreach (var kvp in activeCooldowns)
        {
            var info = kvp.Value;
            output += $"- {info.type} ({info.entityId ?? "Global"}): ";
            output += info.IsActive ?
                $"{info.RemainingTime:F1}초 남음 ({info.Progress:P0})\n" :
                "완료됨\n";
        }
        Debug.Log(output);
    }

    [ContextMenu("모든 쿨타임 강제 리셋")]
    private void ForceResetAll()
    {
        ResetAllCooldowns();
    }

    [ContextMenu("테스트 쿨타임 추가")]
    private void AddTestCooldowns()
    {
        StartCooldown(CooldownType.PetInteraction, "고양이", 10f);
        StartCooldown(CooldownType.Diving, "펭귄", 15f);
        StartCooldown(CooldownType.TreeClimbing, null, 5f);
        Debug.Log("[CooldownManager] 테스트 쿨타임 3개 추가됨");
    }
}