using UnityEngine;

/// <summary>
/// 물 영역 Trigger 감지를 위한 헬퍼 컴포넌트
/// 물 Plane 오브젝트에 부착하여 펫의 물 진입/탈출을 감지
/// </summary>
[RequireComponent(typeof(Collider))]
public class WaterZoneTrigger : MonoBehaviour
{
    // 물 표면의 Y 위치를 저장
    private float waterSurfaceY;
    
    /// <summary>
    /// 물 표면의 월드 Y 좌표
    /// </summary>
    public float WaterSurfaceY => waterSurfaceY;
    private void Awake()
    {
        // 물 표면 Y 위치 저장
        waterSurfaceY = transform.position.y;
        // Debug.Log($"[WaterZoneTrigger] 물 표면 높이 설정: Y = {waterSurfaceY}");
        
        // Collider가 Trigger로 설정되어 있는지 확인
        Collider col = GetComponent<Collider>();
        if (!col.isTrigger)
        {
            Debug.LogWarning($"[WaterZoneTrigger] {gameObject.name}의 Collider가 Trigger로 설정되지 않았습니다. 자동으로 설정합니다.");
            col.isTrigger = true;
        }
        
        // Tag 설정 확인
        if (!CompareTag("Water"))
        {
            Debug.LogWarning($"[WaterZoneTrigger] {gameObject.name}의 Tag가 'Water'가 아닙니다. 자동으로 설정합니다.");
            tag = "Water";
        }
    }


    private void OnTriggerEnter(Collider other)
    {
        // 쾔슐 콜라이더인 경우만 처리 (기본 물리 충돌용 콜라이더)
        if (!(other is CapsuleCollider) || other.isTrigger)
            return;
            
        // 펫인지 확인
        PetController pet = other.GetComponent<PetController>();
        if (pet != null)
        {
            // PetWaterBehaviorController에 진입 알림 (물 표면 높이 전달)
            PetWaterBehaviorController waterController = pet.GetComponent<PetWaterBehaviorController>();
            if (waterController != null)
            {
                waterController.OnWaterEnter(waterSurfaceY);
            }
        }
    }
    
    private void OnTriggerExit(Collider other)
    {
        // 콉슐 콜라이더인 경우만 처리 (기본 물리 충돌용 콜라이더)
        if (!(other is CapsuleCollider) || other.isTrigger)
            return;
            
        // 펫인지 확인
        PetController pet = other.GetComponent<PetController>();
        if (pet != null)
        {
            // PetWaterBehaviorController에 탈출 알림
            PetWaterBehaviorController waterController = pet.GetComponent<PetWaterBehaviorController>();
            if (waterController != null)
            {
                waterController.OnWaterExit();
            }
        }
    }
    
    private void OnDrawGizmos()
    {
        // 에디터에서 물 영역 시각화
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            Gizmos.color = new Color(0, 0.5f, 1f, 0.3f); // 반투명 파란색
            
            if (col is BoxCollider box)
            {
                Matrix4x4 oldMatrix = Gizmos.matrix;
                Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, transform.localScale);
                Gizmos.DrawCube(box.center, box.size);
                Gizmos.matrix = oldMatrix;
            }
            else if (col is MeshCollider)
            {
                Gizmos.DrawMesh(GetComponent<MeshFilter>()?.sharedMesh, transform.position, transform.rotation, transform.localScale);
            }
        }
    }
}