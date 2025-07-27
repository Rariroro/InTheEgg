using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 펫의 감지 기능만을 담당하는 클래스
/// 주변 환경, 다른 펫, 아이템 등을 감지
/// </summary>
public class PetSensor : MonoBehaviour
{
    [Header("Detection Settings")]
    [SerializeField] private float defaultDetectionRadius = 10f;
    [SerializeField] private float interactionDetectionRadius = 5f;
    [SerializeField] private float foodDetectionRadius = 15f;
    [SerializeField] private float environmentDetectionRadius = 20f;
    
    [Header("Layer Settings")]
    [SerializeField] private LayerMask petLayer;
    [SerializeField] private LayerMask foodLayer;
    [SerializeField] private LayerMask environmentLayer;
    [SerializeField] private LayerMask itemLayer;
    
    private PetController petController;
    private bool isInitialized = false;
    
    // 캐시된 감지 결과
    private List<PetController> nearbyPets = new List<PetController>();
    private List<GameObject> nearbyFoods = new List<GameObject>();
    private List<GameObject> nearbyEnvironments = new List<GameObject>();
    private List<GameObject> nearbyItems = new List<GameObject>();
    
    // 프로퍼티
    public IReadOnlyList<PetController> NearbyPets => nearbyPets;
    public IReadOnlyList<GameObject> NearbyFoods => nearbyFoods;
    public IReadOnlyList<GameObject> NearbyEnvironments => nearbyEnvironments;
    public IReadOnlyList<GameObject> NearbyItems => nearbyItems;
    
    /// <summary>
    /// PetSensor 초기화
    /// </summary>
    public void Init(PetController controller)
    {
        petController = controller;
        
        // 레이어 설정
        if (petLayer == 0) petLayer = LayerMask.GetMask("Pet");
        if (foodLayer == 0) foodLayer = LayerMask.GetMask("Food");
        if (environmentLayer == 0) environmentLayer = LayerMask.GetMask("Environment");
        if (itemLayer == 0) itemLayer = LayerMask.GetMask("Item");
        
        isInitialized = true;
        Debug.Log($"[PetSensor] {petController.petName}: 감지 시스템 초기화 완료");
    }
    
    /// <summary>
    /// 주변 펫 감지
    /// </summary>
    public List<PetController> DetectNearbyPets(float radius = -1f)
    {
        if (!isInitialized) return new List<PetController>();
        
        float detectionRadius = radius > 0 ? radius : interactionDetectionRadius;
        nearbyPets.Clear();
        
        Collider[] colliders = Physics.OverlapSphere(transform.position, detectionRadius, petLayer);
        
        foreach (var collider in colliders)
        {
            PetController otherPet = collider.GetComponent<PetController>();
            if (otherPet != null && otherPet != petController && otherPet.isActive)
            {
                nearbyPets.Add(otherPet);
            }
        }
        
        // 거리순으로 정렬
        nearbyPets.Sort((a, b) => 
            Vector3.Distance(transform.position, a.transform.position)
            .CompareTo(Vector3.Distance(transform.position, b.transform.position))
        );
        
        return nearbyPets;
    }
    
    /// <summary>
    /// 가장 가까운 펫 찾기
    /// </summary>
    public PetController FindNearestPet(float maxDistance = -1f)
    {
        List<PetController> pets = DetectNearbyPets(maxDistance);
        return pets.Count > 0 ? pets[0] : null;
    }
    
    /// <summary>
    /// 특정 조건을 만족하는 펫 찾기
    /// </summary>
    public PetController FindPetWithCondition(System.Func<PetController, bool> condition, float radius = -1f)
    {
        List<PetController> pets = DetectNearbyPets(radius);
        
        foreach (var pet in pets)
        {
            if (condition(pet))
                return pet;
        }
        
        return null;
    }
    
    /// <summary>
    /// 주변 음식 감지
    /// </summary>
    public List<GameObject> DetectNearbyFood(float radius = -1f)
    {
        if (!isInitialized) return new List<GameObject>();
        
        float detectionRadius = radius > 0 ? radius : foodDetectionRadius;
        nearbyFoods.Clear();
        
        Collider[] colliders = Physics.OverlapSphere(transform.position, detectionRadius, foodLayer);
        
        foreach (var collider in colliders)
        {
            if (collider.gameObject.activeInHierarchy)
            {
                nearbyFoods.Add(collider.gameObject);
            }
        }
        
        // 거리순으로 정렬
        nearbyFoods.Sort((a, b) => 
            Vector3.Distance(transform.position, a.transform.position)
            .CompareTo(Vector3.Distance(transform.position, b.transform.position))
        );
        
        return nearbyFoods;
    }
    
    /// <summary>
    /// 가장 가까운 음식 찾기
    /// </summary>
    public GameObject FindNearestFood(float maxDistance = -1f)
    {
        List<GameObject> foods = DetectNearbyFood(maxDistance);
        return foods.Count > 0 ? foods[0] : null;
    }
    
    /// <summary>
    /// 특정 타입의 음식 찾기
    /// </summary>
    public GameObject FindFoodOfType(PetAIProperties.DietaryFlags foodType, float radius = -1f)
    {
        List<GameObject> foods = DetectNearbyFood(radius);
        
        foreach (var food in foods)
        {
            // Food 컴포넌트나 태그로 음식 타입 확인
            Food foodComponent = food.GetComponent<Food>();
            if (foodComponent != null && (foodComponent.FoodType & foodType) != 0)
            {
                return food;
            }
        }
        
        return null;
    }
    
    /// <summary>
    /// 주변 환경 객체 감지
    /// </summary>
    public List<GameObject> DetectNearbyEnvironment(float radius = -1f)
    {
        if (!isInitialized) return new List<GameObject>();
        
        float detectionRadius = radius > 0 ? radius : environmentDetectionRadius;
        nearbyEnvironments.Clear();
        
        Collider[] colliders = Physics.OverlapSphere(transform.position, detectionRadius, environmentLayer);
        
        foreach (var collider in colliders)
        {
            if (collider.gameObject.activeInHierarchy)
            {
                nearbyEnvironments.Add(collider.gameObject);
            }
        }
        
        return nearbyEnvironments;
    }
    
    /// <summary>
    /// 특정 태그를 가진 환경 객체 찾기
    /// </summary>
    public GameObject FindEnvironmentWithTag(string tag, float radius = -1f)
    {
        List<GameObject> environments = DetectNearbyEnvironment(radius);
        
        foreach (var env in environments)
        {
            if (env.CompareTag(tag))
                return env;
        }
        
        return null;
    }
    
    /// <summary>
    /// 시야 내에 있는지 확인
    /// </summary>
    public bool IsInLineOfSight(Vector3 targetPosition, float maxDistance = 50f)
    {
        Vector3 direction = targetPosition - transform.position;
        float distance = direction.magnitude;
        
        if (distance > maxDistance)
            return false;
            
        // 레이캐스트로 장애물 확인
        RaycastHit hit;
        if (Physics.Raycast(transform.position + Vector3.up * 0.5f, direction.normalized, out hit, distance))
        {
            // 타겟 위치에 도달했는지 확인
            return Vector3.Distance(hit.point, targetPosition) < 1f;
        }
        
        return true;
    }
    
    /// <summary>
    /// 특정 방향의 장애물 감지
    /// </summary>
    public bool DetectObstacle(Vector3 direction, float distance = 2f)
    {
        return Physics.Raycast(transform.position + Vector3.up * 0.5f, direction, distance);
    }
    
    /// <summary>
    /// 지면 감지
    /// </summary>
    public bool DetectGround(out RaycastHit hit, float maxDistance = 2f)
    {
        return Physics.Raycast(transform.position + Vector3.up * 0.1f, Vector3.down, out hit, maxDistance);
    }
    
    /// <summary>
    /// 디버그 정보 그리기
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        if (!isInitialized)
            return;
            
        // 감지 반경 표시
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, interactionDetectionRadius);
        
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, foodDetectionRadius);
        
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, environmentDetectionRadius);
        
        // 감지된 객체들 표시
        Gizmos.color = Color.red;
        foreach (var pet in nearbyPets)
        {
            if (pet != null)
                Gizmos.DrawLine(transform.position, pet.transform.position);
        }
    }
}