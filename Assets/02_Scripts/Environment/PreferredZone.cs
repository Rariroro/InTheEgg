using UnityEngine;
using PetAIProperties = PetTraits;

/// <summary>
/// 특정 속성의 펫들이 선호하는 구역을 정의하는 컴포넌트
/// 펫들은 자신의 서식지에 맞는 구역에서 더 오래 머물고자 합니다
/// </summary>
public class PreferredZone : MonoBehaviour
{
    /// <summary>
    /// 구역의 모양 타입
    /// </summary>
    public enum ZoneShape 
    { 
        Sphere,  // 원형 구역 (반경 기반)
        Box      // 사각형 구역 (Box Collider 기반)
    }
    
    [Header("구역 모양")]
    [Tooltip("구역의 모양을 선택하세요. Box를 선택하면 Box Collider가 필요합니다.")]
    public ZoneShape zoneShape = ZoneShape.Sphere;
    
    [Header("구역 설정")]
    [Tooltip("이 구역을 선호하는 펫의 서식지 타입")]
    public PetAIProperties.Habitat habitatType;
    
    [Header("Sphere 구역 설정")]
    [Tooltip("구역의 반경 (미터) - Sphere 모양일 때만 사용")]
    [Range(5f, 50f)]
    public float zoneRadius = 15f;
    
    [Header("선택적 설정")]
    [Tooltip("특정 성격의 펫들도 이 구역을 선호하게 하려면 체크")]
    public bool usePersonalityPreference = false;
    
    [Tooltip("이 구역을 선호하는 펫의 성격 타입들")]
    public PetAIProperties.Personality[] preferredPersonalities;
    
    [Tooltip("구역 내에서 행동 지속 시간 배율")]
    [Range(1f, 3f)]
    public float behaviorDurationMultiplier = 1.5f;
    
    void Awake()
    {
        // PreferredZone 오브젝트를 자동으로 Ignore Raycast 레이어로 설정
        // 이렇게 하면 펫 선택 등의 Raycast가 이 오브젝트에 막히지 않습니다
        gameObject.layer = LayerMask.NameToLayer("Ignore Raycast");
        
        // Box Collider가 있는 경우 Trigger로 설정
        BoxCollider boxCollider = GetComponent<BoxCollider>();
        if (boxCollider != null)
        {
            boxCollider.isTrigger = true;
        }
    }
    
    /// <summary>
    /// 주어진 펫이 이 구역을 선호하는지 확인
    /// </summary>
    public bool IsPetPreferred(PetController pet)
    {
        if (pet == null) return false;
        
        // 서식지가 일치하는지 확인
        if (pet.habitat == habitatType)
            return true;
        
        // 성격 기반 선호도 확인 (옵션)
        if (usePersonalityPreference && preferredPersonalities != null)
        {
            foreach (var personality in preferredPersonalities)
            {
                if (pet.personality == personality)
                    return true;
            }
        }
        
        return false;
    }
    
    /// <summary>
    /// 주어진 위치가 구역 내에 있는지 확인
    /// </summary>
    public bool IsInZone(Vector3 position)
    {
        switch (zoneShape)
        {
            case ZoneShape.Box:
                // Box Collider를 사용한 구역 체크
                BoxCollider boxCollider = GetComponent<BoxCollider>();
                if (boxCollider != null)
                {
                    // Collider의 bounds를 사용하여 위치 확인
                    return boxCollider.bounds.Contains(position);
                }
                else
                {
                    Debug.LogWarning($"{gameObject.name}: Box 모양이 선택되었지만 Box Collider가 없습니다!");
                    return false;
                }
                
            case ZoneShape.Sphere:
            default:
                // 기존 원형 구역 체크
                float distance = Vector3.Distance(transform.position, position);
                return distance <= zoneRadius;
        }
    }
    
    /// <summary>
    /// 구역 중심으로부터 주어진 위치까지의 거리
    /// </summary>
    public float GetDistanceFrom(Vector3 position)
    {
        switch (zoneShape)
        {
            case ZoneShape.Box:
                // Box의 경우 가장 가까운 점까지의 거리
                BoxCollider boxCollider = GetComponent<BoxCollider>();
                if (boxCollider != null)
                {
                    Vector3 closestPoint = boxCollider.ClosestPoint(position);
                    return Vector3.Distance(closestPoint, position);
                }
                break;
        }
        
        // Sphere 또는 Box Collider가 없는 경우 중심점까지의 거리
        return Vector3.Distance(transform.position, position);
    }
    
    /// <summary>
    /// 구역 내의 랜덤한 위치 반환
    /// </summary>
    public Vector3 GetRandomPositionInZone()
    {
        switch (zoneShape)
        {
            case ZoneShape.Box:
                // Box 내의 랜덤 위치
                BoxCollider boxCollider = GetComponent<BoxCollider>();
                if (boxCollider != null)
                {
                    Bounds bounds = boxCollider.bounds;
                    return new Vector3(
                        Random.Range(bounds.min.x, bounds.max.x),
                        bounds.center.y, // Y는 중심값 유지 (지면 레벨)
                        Random.Range(bounds.min.z, bounds.max.z)
                    );
                }
                break;
                
            case ZoneShape.Sphere:
                // 원형 내의 랜덤 위치
                float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
                float distance = Random.Range(0f, zoneRadius);
                
                Vector3 offset = new Vector3(
                    Mathf.Cos(angle) * distance,
                    0f,
                    Mathf.Sin(angle) * distance
                );
                
                return transform.position + offset;
        }
        
        // 기본값은 현재 위치
        return transform.position;
    }
    
    // Unity 에디터에서 구역 범위를 시각적으로 표시
    void OnDrawGizmos()
    {
        switch (zoneShape)
        {
            case ZoneShape.Box:
                DrawBoxGizmo(0.1f, 0.3f);
                break;
                
            case ZoneShape.Sphere:
                DrawSphereGizmo(0.1f, 0.3f);
                break;
        }
    }
    
    void OnDrawGizmosSelected()
    {
        switch (zoneShape)
        {
            case ZoneShape.Box:
                DrawBoxGizmo(0.2f, 0.5f);
                break;
                
            case ZoneShape.Sphere:
                DrawSphereGizmo(0.2f, 0.5f);
                break;
        }
        
        // 서식지 타입 라벨 표시
        #if UNITY_EDITOR
        Vector3 labelPosition = transform.position;
        
        if (zoneShape == ZoneShape.Sphere)
        {
            labelPosition += Vector3.up * (zoneRadius + 2f);
        }
        else if (zoneShape == ZoneShape.Box)
        {
            BoxCollider boxCollider = GetComponent<BoxCollider>();
            if (boxCollider != null)
            {
                labelPosition = boxCollider.bounds.center + Vector3.up * (boxCollider.bounds.extents.y + 2f);
            }
        }
        
        UnityEditor.Handles.Label(
            labelPosition,
            $"Preferred Zone: {habitatType} ({zoneShape})"
        );
        #endif
    }
    
    // Box Gizmo 그리기
    private void DrawBoxGizmo(float fillAlpha, float wireAlpha)
    {
        BoxCollider boxCollider = GetComponent<BoxCollider>();
        if (boxCollider != null)
        {
            // 채워진 박스
            Gizmos.color = new Color(0f, 0.5f, 0f, fillAlpha);
            Gizmos.DrawCube(boxCollider.bounds.center, boxCollider.bounds.size);
            
            // 와이어프레임 박스
            Gizmos.color = new Color(0f, 0.7f, 0f, wireAlpha);
            Gizmos.DrawWireCube(boxCollider.bounds.center, boxCollider.bounds.size);
        }
        else
        {
            // Box Collider가 없을 때 경고 표시
            Gizmos.color = new Color(1f, 0f, 0f, 0.5f);
            Gizmos.DrawWireCube(transform.position, Vector3.one * 2f);
        }
    }
    
    // Sphere Gizmo 그리기
    private void DrawSphereGizmo(float fillAlpha, float wireAlpha)
    {
        // 채워진 구체
        Gizmos.color = new Color(0f, 0.5f, 0f, fillAlpha);
        Gizmos.DrawSphere(transform.position, zoneRadius);
        
        // 와이어프레임 구체
        Gizmos.color = new Color(0f, 0.7f, 0f, wireAlpha);
        Gizmos.DrawWireSphere(transform.position, zoneRadius);
    }
    
    // Inspector에서 값이 변경될 때 검증
    void OnValidate()
    {
        // Box 모양이 선택되었을 때 Box Collider 확인
        if (zoneShape == ZoneShape.Box)
        {
            BoxCollider boxCollider = GetComponent<BoxCollider>();
            if (boxCollider == null)
            {
                Debug.LogWarning($"{gameObject.name}: Box 모양이 선택되었지만 Box Collider가 없습니다. Box Collider를 추가해주세요.");
            }
            else
            {
                // Box Collider가 Trigger인지 확인
                if (!boxCollider.isTrigger)
                {
                    boxCollider.isTrigger = true;
        // Debug.Log($"{gameObject.name}: Box Collider의 Is Trigger를 자동으로 활성화했습니다.");
                }
            }
        }
    }
}