using System.Collections.Generic;
using UnityEngine;

namespace LegendaryPet
{
    /// <summary>
    /// 레전드 펫을 관리하는 싱글톤 매니저
    /// 일반 펫 시스템과 완전히 독립적으로 작동
    /// </summary>
    public class LegendaryPetManager : MonoBehaviour
    {
        private static LegendaryPetManager instance;
        public static LegendaryPetManager Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindObjectOfType<LegendaryPetManager>();
                    if (instance == null)
                    {
                        GameObject managerObject = new GameObject("LegendaryPetManager");
                        instance = managerObject.AddComponent<LegendaryPetManager>();
                        DontDestroyOnLoad(managerObject);
                    }
                }
                return instance;
            }
        }
        
        [Header("레전드 펫 관리")]
        [SerializeField] private List<LegendaryPetController> legendaryPets = new List<LegendaryPetController>();
        [SerializeField] private int maxLegendaryPets = 3; // 동시에 존재할 수 있는 최대 레전드 펫 수
        
        [Header("스폰 설정")]
        [SerializeField] private bool autoSpawn = false;
        [SerializeField] private float spawnRadius = 50f;
        [SerializeField] private GameObject[] legendaryPetPrefabs; // 레전드 펫 프리팹 배열
        
        [Header("특수 효과 설정")]
        [SerializeField] private bool globalEffectsEnabled = true;
        [SerializeField] private float effectIntensityMultiplier = 1f;
        
        // 이벤트
        public delegate void LegendaryPetEvent(LegendaryPetController pet);
        public event LegendaryPetEvent OnLegendaryPetSpawned;
        public event LegendaryPetEvent OnLegendaryPetRemoved;
        
        // 프로퍼티
        public List<LegendaryPetController> LegendaryPets => new List<LegendaryPetController>(legendaryPets);
        public int CurrentLegendaryPetCount => legendaryPets.Count;
        public bool CanSpawnMore => legendaryPets.Count < maxLegendaryPets;
        
        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }
            
            instance = this;
            DontDestroyOnLoad(gameObject);
            
            Debug.Log("[LegendaryPetManager] 매니저 초기화 완료");
        }
        
        private void Start()
        {
            // 씬에 이미 있는 레전드 펫 찾기
            FindExistingLegendaryPets();
            
            // 자동 스폰이 활성화되어 있으면 스폰 시작
            if (autoSpawn && legendaryPetPrefabs != null && legendaryPetPrefabs.Length > 0)
            {
                SpawnRandomLegendaryPet();
            }
        }
        
        private void FindExistingLegendaryPets()
        {
            LegendaryPetController[] existingPets = FindObjectsOfType<LegendaryPetController>();
            foreach (var pet in existingPets)
            {
                if (!legendaryPets.Contains(pet))
                {
                    RegisterLegendaryPet(pet);
                }
            }
            
            Debug.Log($"[LegendaryPetManager] {existingPets.Length}개의 레전드 펫 발견");
        }
        
        public void RegisterLegendaryPet(LegendaryPetController pet)
        {
            if (pet == null || legendaryPets.Contains(pet)) return;
            
            legendaryPets.Add(pet);
            Debug.Log($"[LegendaryPetManager] {pet.PetName} ({pet.PetType}) 등록 완료");
            
            OnLegendaryPetSpawned?.Invoke(pet);
        }
        
        public void UnregisterLegendaryPet(LegendaryPetController pet)
        {
            if (pet == null || !legendaryPets.Contains(pet)) return;
            
            legendaryPets.Remove(pet);
            Debug.Log($"[LegendaryPetManager] {pet.PetName} ({pet.PetType}) 등록 해제");
            
            OnLegendaryPetRemoved?.Invoke(pet);
        }
        
        public LegendaryPetController SpawnLegendaryPet(GameObject prefab, Vector3 position, Quaternion rotation)
        {
            if (!CanSpawnMore)
            {
                Debug.LogWarning($"[LegendaryPetManager] 최대 레전드 펫 수({maxLegendaryPets})에 도달했습니다");
                return null;
            }
            
            if (prefab == null)
            {
                Debug.LogError("[LegendaryPetManager] 스폰할 프리팹이 null입니다");
                return null;
            }
            
            GameObject petObject = Instantiate(prefab, position, rotation);
            LegendaryPetController controller = petObject.GetComponent<LegendaryPetController>();
            
            if (controller == null)
            {
                Debug.LogError("[LegendaryPetManager] 프리팹에 LegendaryPetController가 없습니다");
                Destroy(petObject);
                return null;
            }
            
            // 자동으로 RegisterLegendaryPet이 호출됨 (LegendaryPetController.Start에서)
            
            return controller;
        }
        
        public LegendaryPetController SpawnRandomLegendaryPet()
        {
            if (legendaryPetPrefabs == null || legendaryPetPrefabs.Length == 0)
            {
                Debug.LogWarning("[LegendaryPetManager] 스폰 가능한 레전드 펫 프리팹이 없습니다");
                return null;
            }
            
            GameObject randomPrefab = legendaryPetPrefabs[Random.Range(0, legendaryPetPrefabs.Length)];
            Vector3 randomPosition = GetRandomSpawnPosition();
            
            return SpawnLegendaryPet(randomPrefab, randomPosition, Quaternion.identity);
        }
        
        public void RemoveLegendaryPet(LegendaryPetController pet)
        {
            if (pet == null) return;
            
            UnregisterLegendaryPet(pet);
            Destroy(pet.gameObject);
        }
        
        public void RemoveAllLegendaryPets()
        {
            List<LegendaryPetController> petsToRemove = new List<LegendaryPetController>(legendaryPets);
            foreach (var pet in petsToRemove)
            {
                RemoveLegendaryPet(pet);
            }
        }
        
        private Vector3 GetRandomSpawnPosition()
        {
            Vector3 randomDirection = Random.insideUnitSphere * spawnRadius;
            randomDirection.y = 0;
            
            Vector3 spawnPosition = transform.position + randomDirection;
            
            // 지면 높이 조정
            if (Physics.Raycast(spawnPosition + Vector3.up * 10f, Vector3.down, out RaycastHit hit, 20f))
            {
                spawnPosition.y = hit.point.y;
            }
            
            return spawnPosition;
        }
        
        public void SetGlobalEffects(bool enabled)
        {
            globalEffectsEnabled = enabled;
            
            foreach (var pet in legendaryPets)
            {
                if (pet != null)
                {
                    pet.SetActive(enabled);
                }
            }
            
            Debug.Log($"[LegendaryPetManager] 글로벌 효과: {(enabled ? "활성화" : "비활성화")}");
        }
        
        public void SetAllPetsMovementPattern(LegendaryPetAI.MovementPattern pattern)
        {
            foreach (var pet in legendaryPets)
            {
                if (pet != null)
                {
                    LegendaryPetAI ai = pet.GetComponent<LegendaryPetAI>();
                    if (ai != null)
                    {
                        ai.SetMovementPattern(pattern);
                    }
                }
            }
            
            Debug.Log($"[LegendaryPetManager] 모든 레전드 펫 움직임 패턴 변경: {pattern}");
        }
        
        public LegendaryPetController GetLegendaryPetByType(LegendaryPetType type)
        {
            return legendaryPets.Find(pet => pet.PetType == type);
        }
        
        public List<LegendaryPetController> GetLegendaryPetsByType(LegendaryPetType type)
        {
            return legendaryPets.FindAll(pet => pet.PetType == type);
        }
        
        // 디버그 명령어
        [ContextMenu("Spawn Random Legendary Pet")]
        private void DebugSpawnRandom()
        {
            SpawnRandomLegendaryPet();
        }
        
        [ContextMenu("Remove All Legendary Pets")]
        private void DebugRemoveAll()
        {
            RemoveAllLegendaryPets();
        }
        
        [ContextMenu("Toggle Global Effects")]
        private void DebugToggleEffects()
        {
            SetGlobalEffects(!globalEffectsEnabled);
        }
        
        private void OnDrawGizmosSelected()
        {
            // 스폰 반경 표시
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.3f);
            Gizmos.DrawWireSphere(transform.position, spawnRadius);
            
            // 현재 레전드 펫 위치 표시
            Gizmos.color = Color.yellow;
            foreach (var pet in legendaryPets)
            {
                if (pet != null)
                {
                    Gizmos.DrawWireCube(pet.transform.position + Vector3.up * 2f, Vector3.one * 0.5f);
                    Gizmos.DrawLine(transform.position, pet.transform.position);
                }
            }
        }
    }
}