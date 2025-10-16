using UnityEngine;

/// <summary>
/// 펫이 스폰될 위치를 나타내는 컴포넌트
/// 씬 뷰와 게임 뷰에서 시각적으로 표시됨
/// </summary>
public class PetSpawnSpot : MonoBehaviour
{
    [Header("스폰 스팟 설정")]
    [SerializeField] private int spotIndex = 0; // 스폰 스팟 번호 (0부터 시작)
    [SerializeField] private float spotRadius = 1f; // 스폰 스팟 반경
    [SerializeField] private Color spotColor = new Color(0.2f, 0.8f, 0.2f, 0.5f); // 스폰 스팟 색상

    [Header("시각화 설정")]
    [SerializeField] private bool showInSceneView = true; // 씬 뷰에서 표시 여부
    [SerializeField] private bool showLabel = true; // 번호 라벨 표시 여부
    [SerializeField] private float labelOffset = 2f; // 라벨 높이 오프셋

    // 현재 이 스팟에 스폰된 펫 수 (런타임 정보)
    private int currentPetCount = 0;

    /// <summary>
    /// 스폰 스팟 인덱스 반환
    /// </summary>
    public int SpotIndex => spotIndex;

    /// <summary>
    /// 스폰 스팟 반경 반환
    /// </summary>
    public float SpotRadius => spotRadius;

    /// <summary>
    /// 현재 이 스팟에 스폰된 펫 수 반환
    /// </summary>
    public int CurrentPetCount => currentPetCount;

    /// <summary>
    /// 이 스팟에 펫이 스폰될 때 호출
    /// </summary>
    public void OnPetSpawned()
    {
        currentPetCount++;
    }

    /// <summary>
    /// 펫 카운트 리셋
    /// </summary>
    public void ResetPetCount()
    {
        currentPetCount = 0;
    }

    /// <summary>
    /// 이 스폰 스팟에서 랜덤 위치 반환 (오프셋 적용)
    /// </summary>
    /// <param name="offsetMultiplier">오프셋 배수 (같은 스팟에 여러 펫이 스폰될 때 사용)</param>
    public Vector3 GetSpawnPosition(int offsetMultiplier = 0)
    {
        Vector3 basePosition = transform.position;

        // 첫 번째 펫은 정중앙에 스폰
        if (offsetMultiplier == 0)
        {
            return basePosition;
        }

        // 추가 펫들은 원형으로 배치
        float angle = (offsetMultiplier - 1) * (360f / 8f); // 최대 8개까지 원형 배치
        float distance = spotRadius + (offsetMultiplier * 0.5f); // 점진적으로 거리 증가

        Vector3 offset = new Vector3(
            Mathf.Cos(angle * Mathf.Deg2Rad) * distance,
            0,
            Mathf.Sin(angle * Mathf.Deg2Rad) * distance
        );

        return basePosition + offset;
    }

    /// <summary>
    /// 인덱스 설정 (에디터에서 자동 번호 매기기용)
    /// </summary>
    public void SetIndex(int index)
    {
        spotIndex = index;
    }

    private void OnDrawGizmos()
    {
        if (!showInSceneView) return;

        DrawSpawnSpotGizmo(false);
    }

    private void OnDrawGizmosSelected()
    {
        if (!showInSceneView) return;

        DrawSpawnSpotGizmo(true);
    }

    private void DrawSpawnSpotGizmo(bool isSelected)
    {
        // 스폰 스팟 색상 설정
        Color gizmoColor = spotColor;
        if (isSelected)
        {
            gizmoColor.a = 0.8f; // 선택되면 더 진하게
        }

        // 원형 영역 그리기
        Gizmos.color = gizmoColor;
        Vector3 position = transform.position;

        // 와이어프레임 구체
        Gizmos.DrawWireSphere(position, spotRadius);

        // 반투명 구체
        gizmoColor.a *= 0.3f;
        Gizmos.color = gizmoColor;
        Gizmos.DrawSphere(position, spotRadius);

        // 중심 마커
        Gizmos.color = Color.white;
        Gizmos.DrawWireCube(position, Vector3.one * 0.3f);

        // 수직 선
        Gizmos.color = new Color(1, 1, 1, 0.5f);
        Gizmos.DrawLine(position, position + Vector3.up * labelOffset);

        // 라벨 표시 (UnityEditor 사용)
        #if UNITY_EDITOR
        if (showLabel)
        {
            string labelText = $"Spawn #{spotIndex + 1}";
            if (Application.isPlaying && currentPetCount > 0)
            {
                labelText += $"\n[{currentPetCount} pets]";
            }

            UnityEditor.Handles.Label(
                position + Vector3.up * labelOffset,
                labelText,
                new GUIStyle()
                {
                    alignment = TextAnchor.MiddleCenter,
                    normal = new GUIStyleState()
                    {
                        textColor = Color.white
                    },
                    fontStyle = FontStyle.Bold,
                    fontSize = 12
                }
            );
        }
        #endif
    }

    /// <summary>
    /// 컴포넌트 검증
    /// </summary>
    private void OnValidate()
    {
        // 반경은 최소 0.5
        if (spotRadius < 0.5f)
            spotRadius = 0.5f;

        // 라벨 오프셋은 최소 1
        if (labelOffset < 1f)
            labelOffset = 1f;

        // 인덱스는 0 이상
        if (spotIndex < 0)
            spotIndex = 0;
    }
}