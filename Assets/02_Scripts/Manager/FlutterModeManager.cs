using System;
using System.Collections.Generic;
using UnityEngine;
using FlutterIntegration;
using LegendaryPet;

/// <summary>
/// Flutter 연동 모드 상태를 관리하는 싱글톤
/// 게임 시작 전 Flutter에서 INIT_GAME 메시지를 받으면 Flutter 모드로 전환
/// </summary>
public class FlutterModeManager : MonoBehaviour
{
    public static FlutterModeManager Instance { get; private set; }

    [Header("모드 상태")]
    [SerializeField] private bool isFlutterMode = false;
    [SerializeField] private bool isInitialized = false;

    [Header("수신된 게임 데이터")]
    private FlutterGameData gameData;

    // 펫 ID -> 친밀도 매핑 (빠른 조회용)
    private Dictionary<string, int> petIntimacyCache = new Dictionary<string, int>();

    // 프로퍼티
    public bool IsFlutterMode => isFlutterMode;
    public bool IsInitialized => isInitialized;
    public FlutterGameData GameData => gameData;

    // 이벤트
    public event Action OnFlutterDataReceived;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            transform.SetParent(null);
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// Flutter에서 INIT_GAME 메시지 수신 시 호출
    /// </summary>
    public void InitializeWithFlutterData(FlutterGameData data)
    {
        if (data == null)
        {
            Debug.LogError("[FlutterModeManager] 수신된 데이터가 null입니다.");
            return;
        }

        gameData = data;
        isFlutterMode = true;
        isInitialized = true;

        // 친밀도 캐시 구축
        BuildIntimacyCache();

        // SelectionManager들에 데이터 설정
        PopulateSelectionManagers();

        Debug.Log($"[FlutterModeManager] Flutter 모드 초기화 완료. " +
                  $"펫: {data.pets?.Count ?? 0}, " +
                  $"레전드: {data.legendaryPets?.Count ?? 0}, " +
                  $"환경: {data.environmentItems?.Count ?? 0}, " +
                  $"음식: {data.foodItems?.Count ?? 0}");

        OnFlutterDataReceived?.Invoke();

        // EnvironmentManager에게 Flutter 데이터로 스폰 시작 요청
        if (EnvironmentManager.Instance != null)
        {
            EnvironmentManager.Instance.StartSpawnWithFlutterData();
        }
    }

    /// <summary>
    /// 친밀도 캐시 구축
    /// </summary>
    private void BuildIntimacyCache()
    {
        petIntimacyCache.Clear();

        if (gameData?.pets == null) return;

        foreach (var pet in gameData.pets)
        {
            if (!string.IsNullOrEmpty(pet.petCardId))
            {
                petIntimacyCache[pet.petCardId] = pet.petIntimacy;
            }
        }
    }

    /// <summary>
    /// SelectionManager들에 Flutter 데이터 적용
    /// </summary>
    private void PopulateSelectionManagers()
    {
        // PetSelectionManager
        EnsureManagerExists<PetSelectionManager>("PetSelectionManager");
        if (PetSelectionManager.Instance != null && gameData.pets != null)
        {
            PetSelectionManager.Instance.ClearSelectedPets();
            foreach (var pet in gameData.pets)
            {
                PetSelectionManager.Instance.AddPet(pet.petCardId);
                if (!pet.isSpawned)
                {
                    PetSelectionManager.Instance.SetPetFirstAppearance(pet.petCardId, true);
                }
            }
            Debug.Log($"[FlutterModeManager] PetSelectionManager 설정 완료: {gameData.pets.Count}마리");
        }

        // LegendaryPetSelectionManager
        EnsureManagerExists<LegendaryPetSelectionManager>("LegendaryPetSelectionManager");
        if (LegendaryPetSelectionManager.Instance != null && gameData.legendaryPets != null)
        {
            LegendaryPetSelectionManager.Instance.ClearSelectedLegendaryPets();
            foreach (var legendPet in gameData.legendaryPets)
            {
                LegendaryPetSelectionManager.Instance.AddLegendaryPet(legendPet.petCardId);
                if (!legendPet.isSpawned)
                {
                    LegendaryPetSelectionManager.Instance.SetLegendaryPetFirstAppearance(legendPet.petCardId, true);
                }
            }
            Debug.Log($"[FlutterModeManager] LegendaryPetSelectionManager 설정 완료: {gameData.legendaryPets.Count}마리");
        }

        // EnvironmentSelectionManager
        EnsureManagerExists<EnvironmentSelectionManager>("EnvironmentSelectionManager");
        if (EnvironmentSelectionManager.Instance != null && gameData.environmentItems != null)
        {
            EnvironmentSelectionManager.Instance.ClearSelectedEnvironments();
            foreach (var env in gameData.environmentItems)
            {
                EnvironmentSelectionManager.Instance.AddEnvironment(env.id);
                if (!env.isSpawned)
                {
                    EnvironmentSelectionManager.Instance.SetEnvironmentFirstAppearance(env.id, true);
                }
            }
            Debug.Log($"[FlutterModeManager] EnvironmentSelectionManager 설정 완료: {gameData.environmentItems.Count}개");
        }

        // ItemSelectionManager (음식)
        EnsureManagerExists<ItemSelectionManager>("ItemSelectionManager");
        if (ItemSelectionManager.Instance != null && gameData.foodItems != null)
        {
            ItemSelectionManager.Instance.ClearSelectedItems();
            foreach (var food in gameData.foodItems)
            {
                string foodType = food.GetFoodType();
                if (foodType != null)
                {
                    ItemSelectionManager.Instance.SetItemCount(foodType, food.quantity);
                }
            }
            Debug.Log($"[FlutterModeManager] ItemSelectionManager 설정 완료: {gameData.foodItems.Count}종류");
        }
    }

    /// <summary>
    /// 싱글톤 매니저가 없으면 생성
    /// </summary>
    private void EnsureManagerExists<T>(string name) where T : MonoBehaviour
    {
        if (FindObjectOfType<T>() == null)
        {
            GameObject obj = new GameObject(name);
            obj.AddComponent<T>();
            DontDestroyOnLoad(obj);
        }
    }

    /// <summary>
    /// 펫 친밀도 데이터 조회 (petCardId -> intimacy)
    /// </summary>
    public int GetPetIntimacy(string petCardId)
    {
        if (string.IsNullOrEmpty(petCardId)) return 50; // 기본값

        if (petIntimacyCache.TryGetValue(petCardId, out int intimacy))
        {
            return intimacy;
        }

        return 50; // 기본값
    }

    /// <summary>
    /// 친밀도 업데이트 (내부 캐시)
    /// </summary>
    public void UpdatePetIntimacy(string petCardId, int newIntimacy)
    {
        if (string.IsNullOrEmpty(petCardId)) return;

        int clampedValue = Mathf.Clamp(newIntimacy, 0, 100);
        petIntimacyCache[petCardId] = clampedValue;

        // gameData도 업데이트
        if (gameData?.pets != null)
        {
            var pet = gameData.pets.Find(p => p.petCardId == petCardId);
            if (pet != null)
            {
                pet.petIntimacy = clampedValue;
            }
        }
    }

    /// <summary>
    /// 모든 펫의 현재 친밀도 목록 반환 (SYNC_INTIMACY용)
    /// </summary>
    public List<PetIntimacyData> GetAllPetIntimacies()
    {
        var result = new List<PetIntimacyData>();

        foreach (var kvp in petIntimacyCache)
        {
            result.Add(new PetIntimacyData
            {
                petCardId = kvp.Key,
                petIntimacy = kvp.Value
            });
        }

        return result;
    }

    /// <summary>
    /// 새 세션을 위해 상태 리셋 (FlutterBridge에서 호출)
    /// </summary>
    public void ResetForNewSession()
    {
        isFlutterMode = false;
        isInitialized = false;
        gameData = null;
        petIntimacyCache.Clear();

        // PetManager도 리셋 (기존 펫/Egg 제거 및 플래그 초기화)
        var petManager = FindObjectOfType<PetManager>();
        if (petManager != null)
        {
            petManager.ResetForNewSession();
        }

        // LegendaryPetManager도 리셋
        var legendaryPetManager = FindObjectOfType<LegendaryPetManager>();
        if (legendaryPetManager != null)
        {
            legendaryPetManager.ResetForNewSession();
        }

        // EnvironmentManager도 리셋
        if (EnvironmentManager.Instance != null)
        {
            EnvironmentManager.Instance.ResetForNewSession();
        }

        Debug.Log("[FlutterModeManager] 새 세션을 위해 상태 리셋됨");
    }

    /// <summary>
    /// Flutter ID로 음식 타입 변환 헬퍼
    /// </summary>
    public static string GetFoodIdFromType(string foodType)
    {
        switch (foodType)
        {
            case "meat": return "food_001";
            case "fish": return "food_002";
            case "fruit": return "food_003";
            case "vegetable": return "food_004";
            case "Grain": return "food_005";
            case "hay": return "food_006";
            case "Grass": return "food_007";
            default: return null;
        }
    }
}
