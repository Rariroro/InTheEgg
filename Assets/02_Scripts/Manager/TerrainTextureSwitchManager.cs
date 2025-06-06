using UnityEngine;
using System.Collections.Generic;

// 플레인 그룹을 정의하는 클래스
[System.Serializable]
public class PlaneGroup
{
    public string groupName = "New Group";
    [Tooltip("이 그룹의 텍스처를 변경할지 여부")]
    public bool useAlternativeTexture = false;
    [Tooltip("이 그룹에 속한 플레인 오브젝트들")]
    public List<GameObject> planes = new List<GameObject>();
    
    // 이전 토글 상태 (변경 감지용)
    [HideInInspector]
    public bool previousToggleState = false;
    
    // 그룹 내 플레인에 영향받는 지형 포인트 및 알파맵 좌표
    [HideInInspector]
    public List<Vector3> affectedTerrainPoints = new List<Vector3>();
    [HideInInspector]
    public List<Vector2Int> affectedAlphamapPoints = new List<Vector2Int>();
    
    // 각 플레인별 영향 포인트 (디버깅용)
    [HideInInspector]
    public Dictionary<GameObject, List<Vector3>> planeTerrainPoints = new Dictionary<GameObject, List<Vector3>>();
    
    // 원본 텍스처 데이터
    [HideInInspector]
    public float[,,] savedSplatmapData;
}

public class TerrainTextureSwitchManager : MonoBehaviour
{
    public Terrain targetTerrain;
    [Tooltip("플레인 그룹들. 각 그룹은 독립적으로 텍스처를 변경할 수 있습니다.")]
    public List<PlaneGroup> planeGroups = new List<PlaneGroup>();

    [Header("디버깅 옵션")]
    public bool showDebugVisuals = false;
    public int samplesPerUnit = 1; // 단위 면적당 샘플 수 (높을수록 정확하지만 성능이 저하됨)

    private int alphamapWidth;
    private int alphamapHeight;

    // 모든 영향받는 알파맵 좌표를 추적하는 딕셔너리
    // 키: 알파맵 좌표, 값: 해당 좌표에 영향을 주는 그룹들의 인덱스 목록
    private Dictionary<Vector2Int, List<int>> affectedCoordinatesMap = new Dictionary<Vector2Int, List<int>>();
    [Header("초기화 설정")]
    [Tooltip("시작 시 모든 토글을 자동으로 켤 것인지")]
    public bool autoEnableAllTogglesOnStart = true;


    // 런타임용 임시 TerrainData 추가
    private TerrainData runtimeTerrainData;
    private TerrainData originalTerrainData;
    void Start()
    {
        if (targetTerrain == null)
        {
            Debug.LogError("대상 지형이 할당되지 않았습니다!");
            return;
        }

        // 원본 TerrainData 보존
        originalTerrainData = targetTerrain.terrainData;
        // 런타임용 TerrainData 복사본 생성 (에디터에서 저장하지 않도록 플래그 설정)
        runtimeTerrainData = Instantiate(originalTerrainData);
        runtimeTerrainData.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild; // 추가
        targetTerrain.terrainData = runtimeTerrainData;
        // 런타임용 TerrainData 복사본 생성
        runtimeTerrainData = Instantiate(originalTerrainData);
        targetTerrain.terrainData = runtimeTerrainData;

        if (planeGroups.Count == 0)
        {
            Debug.LogWarning("플레인 그룹이 정의되지 않았습니다!");
            return;
        }

        // 지형 데이터 가져오기
        TerrainData terrainData = targetTerrain.terrainData; // 이제 복사본 사용
        alphamapWidth = terrainData.alphamapWidth;
        alphamapHeight = terrainData.alphamapHeight;

        // 모든 그룹별로 영향받는 지형 영역 계산
        CalculateAllGroupsAffectedAreas();

        // 중첩 영역 맵 구축
        BuildOverlappingAreasMap();

        // 각 그룹별로 초기 상태 저장 (현재 지형 상태를 원본으로 저장)
        for (int i = 0; i < planeGroups.Count; i++)
        {
            SaveOriginalTextureForGroup(i);
        }

        // 시작 시 모든 토글을 켜서 환경을 얻지 못한 상태로 만들기
        if (autoEnableAllTogglesOnStart)
        {
            for (int i = 0; i < planeGroups.Count; i++)
            {
                planeGroups[i].useAlternativeTexture = true;
                planeGroups[i].previousToggleState = true;
            }

            // 모든 변경사항 적용
            ApplyAllGroupChanges();
        }
    }

    // OnApplicationQuit 또는 OnDestroy에서 원본 텍스처로 복원
    void OnApplicationQuit()
    {
        RestoreAllOriginalTextures();
    }

    void OnDestroy()
    {
        // 원본 TerrainData로 복원 (선택사항)
        if (targetTerrain != null && originalTerrainData != null)
        {
            targetTerrain.terrainData = originalTerrainData;
        }

        // 런타임 데이터 정리
        if (runtimeTerrainData != null)
        {
#if UNITY_EDITOR
            DestroyImmediate(runtimeTerrainData);
#else
            Destroy(runtimeTerrainData);
#endif
        }
    }

    // 모든 텍스처를 원본으로 복원하는 메서드 추가
    private void RestoreAllOriginalTextures()
    {
        if (targetTerrain == null) return;

        TerrainData terrainData = targetTerrain.terrainData;
        float[,,] splatmapData = terrainData.GetAlphamaps(0, 0, alphamapWidth, alphamapHeight);

        // 모든 그룹의 토글을 끄고 원본 복원
        for (int i = 0; i < planeGroups.Count; i++)
        {
            planeGroups[i].useAlternativeTexture = false;
            planeGroups[i].previousToggleState = false;
        }

        // 모든 영향받는 좌표를 원본으로 복원
        foreach (var kvp in affectedCoordinatesMap)
        {
            Vector2Int alphaCoord = kvp.Key;

            // 첫 번째 그룹의 저장된 원본 데이터 사용
            if (kvp.Value.Count > 0 && planeGroups.Count > kvp.Value[0])
            {
                PlaneGroup firstGroup = planeGroups[kvp.Value[0]];
                if (firstGroup.savedSplatmapData != null)
                {
                    for (int layer = 0; layer < terrainData.alphamapLayers; layer++)
                    {
                        splatmapData[alphaCoord.y, alphaCoord.x, layer] =
                            firstGroup.savedSplatmapData[alphaCoord.y, alphaCoord.x, layer];
                    }
                }
            }
        }

        // 변경사항 적용
        terrainData.SetAlphamaps(0, 0, splatmapData);

        Debug.Log("모든 지형 텍스처가 원본으로 복원되었습니다.");
    }

    /// <summary>
    /// 모든 그룹의 토글을 끄고 원본 텍스처로 복원하는 메서드
    /// 나중에 삭제
    /// </summary>
    public void ResetAllTogglesToOff()
    {
        Debug.Log("모든 환경 토글을 끕니다.");

        // 모든 그룹의 토글을 끄기
        for (int i = 0; i < planeGroups.Count; i++)
        {
            planeGroups[i].useAlternativeTexture = false;
            planeGroups[i].previousToggleState = false;
        }

        // 모든 변경사항 적용 (원본 텍스처로 복원됨)
        ApplyAllGroupChanges();

        Debug.Log("모든 환경이 원본 상태로 복원되었습니다.");
    }
    void Update()
    {
        bool anyGroupChanged = false;

        // 각 그룹별로 토글 상태 변경 확인
        for (int i = 0; i < planeGroups.Count; i++)
        {
            PlaneGroup group = planeGroups[i];

            // 토글 상태 변경 확인
            if (group.useAlternativeTexture != group.previousToggleState)
            {
                anyGroupChanged = true;
                group.previousToggleState = group.useAlternativeTexture;
            }
        }

        // 어떤 그룹이라도 변경되었으면 모든 변경사항 한 번에 적용
        if (anyGroupChanged)
        {
            ApplyAllGroupChanges();
        }

        // 디버그 시각화
        if (showDebugVisuals)
        {
            for (int i = 0; i < planeGroups.Count; i++)
            {
                PlaneGroup group = planeGroups[i];

                // 그룹별로 다른 색상 사용
                Color[] debugColors = new Color[] {
                    Color.red, Color.green, Color.blue, Color.magenta, Color.cyan, Color.yellow
                };
                Color groupColor = debugColors[i % debugColors.Length];

                // 밝기를 토글 상태에 따라 조정
                if (!group.useAlternativeTexture)
                {
                    groupColor = Color.Lerp(groupColor, Color.black, 0.5f);
                }

                foreach (var planePair in group.planeTerrainPoints)
                {
                    foreach (Vector3 point in planePair.Value)
                    {
                        Debug.DrawRay(point, Vector3.up * 2f, groupColor, Time.deltaTime);
                    }
                }
            }
        }
    }

    // 모든 그룹의 변경사항을 한 번에 적용하는 최적화된 함수
    private void ApplyAllGroupChanges()
    {
        TerrainData terrainData = targetTerrain.terrainData;
        float[,,] splatmapData = terrainData.GetAlphamaps(0, 0, alphamapWidth, alphamapHeight);
        bool anyChanges = false;

        // 모든 영향받는 좌표에 대해 처리
        foreach (var kvp in affectedCoordinatesMap)
        {
            Vector2Int alphaCoord = kvp.Key;
            List<int> affectingGroups = kvp.Value;

            // 활성화된 그룹 수 계산
            int activeGroupsCount = 0;
            foreach (int groupIndex in affectingGroups)
            {
                if (planeGroups[groupIndex].useAlternativeTexture)
                {
                    activeGroupsCount++;
                }
            }

            // 모든 그룹이 비활성화되었으면 원본으로 복원
            if (activeGroupsCount == 0)
            {
                // 어떤 그룹이든 저장된 원본 데이터를 사용 (모두 동일한 원본을 저장함)
                int firstGroupIndex = affectingGroups[0];
                PlaneGroup firstGroup = planeGroups[firstGroupIndex];

                for (int layer = 0; layer < terrainData.alphamapLayers; layer++)
                {
                    splatmapData[alphaCoord.y, alphaCoord.x, layer] = firstGroup.savedSplatmapData[alphaCoord.y, alphaCoord.x, layer];
                }
                anyChanges = true;
            }
            // 하나 이상 활성화되었으면 첫 번째 레이어 적용
            else if (activeGroupsCount > 0)
            {
                // 모든 레이어를 0으로 설정
                for (int layer = 0; layer < terrainData.alphamapLayers; layer++)
                {
                    splatmapData[alphaCoord.y, alphaCoord.x, layer] = 0;
                }

                // 첫 번째 레이어만 1로 설정
                splatmapData[alphaCoord.y, alphaCoord.x, 0] = 1;
                anyChanges = true;
            }
        }

        // 변경사항이 있는 경우에만 SetAlphamaps 호출
        if (anyChanges)
        {
            terrainData.SetAlphamaps(0, 0, splatmapData);
            Debug.Log("모든 그룹의 텍스처 변경사항이 적용되었습니다.");
        }
    }

    // 모든 그룹의 영향 영역 계산
    void CalculateAllGroupsAffectedAreas()
    {
        for (int i = 0; i < planeGroups.Count; i++)
        {
            PlaneGroup group = planeGroups[i];
            CalculateGroupAffectedTerrainArea(i);
            Debug.Log($"그룹 '{group.groupName}': {group.affectedAlphamapPoints.Count} 알파맵 좌표에 영향");
        }
    }

    // 중첩 영역 맵 구축 - 어떤 영역이 어떤 그룹들에 의해 영향받는지 추적
    void BuildOverlappingAreasMap()
    {
        affectedCoordinatesMap.Clear();

        // 각 그룹의 모든 영향받는 좌표를 맵에 추가
        for (int groupIndex = 0; groupIndex < planeGroups.Count; groupIndex++)
        {
            PlaneGroup group = planeGroups[groupIndex];

            foreach (Vector2Int coord in group.affectedAlphamapPoints)
            {
                if (!affectedCoordinatesMap.ContainsKey(coord))
                {
                    affectedCoordinatesMap[coord] = new List<int>();
                }

                // 이 좌표에 영향을 주는 그룹 인덱스 추가
                affectedCoordinatesMap[coord].Add(groupIndex);
            }
        }

        // 중첩 영역 로깅
        int overlappingCount = 0;
        foreach (var kvp in affectedCoordinatesMap)
        {
            if (kvp.Value.Count > 1)
            {
                overlappingCount++;
            }
        }
        Debug.Log($"발견된 중첩 영역: {overlappingCount} 알파맵 좌표");
    }

    // 특정 그룹의 영향 영역 계산
    void CalculateGroupAffectedTerrainArea(int groupIndex)
    {
        PlaneGroup group = planeGroups[groupIndex];
        group.affectedTerrainPoints.Clear();
        group.affectedAlphamapPoints.Clear();
        group.planeTerrainPoints.Clear();

        foreach (GameObject plane in group.planes)
        {
            if (plane == null)
            {
                Debug.LogWarning($"그룹 '{group.groupName}'에 null 플레인 오브젝트가 있습니다. 건너뜁니다.");
                continue;
            }

            List<Vector3> terrainPoints = CalculateAffectedTerrainArea(plane);

            // 이 플레인에 대한 영향 지점 저장 (디버깅용)
            group.planeTerrainPoints[plane] = new List<Vector3>(terrainPoints);

            // 그룹의 통합 목록에 추가
            group.affectedTerrainPoints.AddRange(terrainPoints);

            // 알파맵 좌표 추가
            foreach (Vector3 point in terrainPoints)
            {
                Vector2Int alphamapCoord = GetAlphamapCoord(point);
                if (!group.affectedAlphamapPoints.Contains(alphamapCoord))
                {
                    group.affectedAlphamapPoints.Add(alphamapCoord);
                }
            }
        }
    }

    // 단일 플레인에 대한 영향 지역 계산
    List<Vector3> CalculateAffectedTerrainArea(GameObject plane)
    {
        List<Vector3> affectedPoints = new List<Vector3>();

        // 플레인 면적 계산
        Bounds planeWorldBounds = GetPlaneWorldBounds(plane);
        float planeArea = planeWorldBounds.size.x * planeWorldBounds.size.z;

        // 샘플 수 계산 (면적에 비례하여 샘플 생성)
        int totalSamples = Mathf.Max(50, Mathf.RoundToInt(planeArea * samplesPerUnit));

        // 플레인에서 균등하게 분포된 샘플 포인트 생성
        List<Vector3> samplePoints = GenerateSamplePoints(plane, totalSamples);

        // 각 샘플 포인트에서 레이캐스트하여 지형 위치 찾기
        foreach (Vector3 samplePoint in samplePoints)
        {
            // 플레인 위치에서 아래로 레이 발사
            RaycastHit hit;
            Ray ray = new Ray(samplePoint + Vector3.up * 0.5f, Vector3.down);

            // 지형 레이어와의 교차 확인
            if (Physics.Raycast(ray, out hit, 1000f, LayerMask.GetMask("Terrain")))
            {
                Vector3 hitPoint = hit.point;
                affectedPoints.Add(hitPoint);
            }
        }

        Debug.Log($"플레인 '{plane.name}': {affectedPoints.Count} 지점에 영향");
        return affectedPoints;
    }

    // 플레인의 월드 공간 경계(Bounds)를 가져오는 함수
    private Bounds GetPlaneWorldBounds(GameObject plane)
    {
        Renderer planeRenderer = plane.GetComponent<Renderer>();
        if (planeRenderer == null)
        {
            return new Bounds(); // 빈 경계 반환
        }
        return planeRenderer.bounds;
    }

    // 플레인에서 균등하게 분포된 샘플 포인트 생성
    private List<Vector3> GenerateSamplePoints(GameObject plane, int numSamples)
    {
        List<Vector3> samplePoints = new List<Vector3>();
        Bounds bounds = GetPlaneWorldBounds(plane);

        // 샘플 수의 제곱근을 구해서 그리드 샘플 수 계산
        int gridSize = Mathf.CeilToInt(Mathf.Sqrt(numSamples));

        for (int x = 0; x < gridSize; x++)
        {
            for (int z = 0; z < gridSize; z++)
            {
                // 월드 공간에서 정규화된 위치 계산
                float normalizedX = (float)x / (gridSize - 1);
                float normalizedZ = (float)z / (gridSize - 1);

                // 월드 공간에서의 위치 계산
                Vector3 worldPos = new Vector3(
                    Mathf.Lerp(bounds.min.x, bounds.max.x, normalizedX),
                    bounds.center.y, // Plane의 높이를 사용
                    Mathf.Lerp(bounds.min.z, bounds.max.z, normalizedZ)
                );

                samplePoints.Add(worldPos);
            }
        }

        return samplePoints;
    }

    // 월드 좌표를 알파맵 좌표로 변환
    private Vector2Int GetAlphamapCoord(Vector3 worldPos)
    {
        TerrainData terrainData = targetTerrain.terrainData;
        Vector3 terrainPos = targetTerrain.transform.position;

        // 월드 좌표를 지형 상대 좌표로 변환
        float relativeX = (worldPos.x - terrainPos.x) / terrainData.size.x;
        float relativeZ = (worldPos.z - terrainPos.z) / terrainData.size.z;

        // 알파맵 인덱스 계산
        int alphaX = Mathf.Clamp(Mathf.FloorToInt(relativeX * alphamapWidth), 0, alphamapWidth - 1);
        int alphaY = Mathf.Clamp(Mathf.FloorToInt(relativeZ * alphamapHeight), 0, alphamapHeight - 1);

        return new Vector2Int(alphaX, alphaY);
    }

    // 특정 그룹의 원본 텍스처 저장
    void SaveOriginalTextureForGroup(int groupIndex)
    {
        PlaneGroup group = planeGroups[groupIndex];

        if (group.affectedAlphamapPoints.Count == 0)
        {
            Debug.LogWarning($"그룹 '{group.groupName}'의 플레인 아래 지형 영역이 계산되지 않았습니다!");
            return;
        }

        TerrainData terrainData = targetTerrain.terrainData;

        // 현재 스플랫맵 가져오기
        float[,,] splatmapData = terrainData.GetAlphamaps(0, 0, alphamapWidth, alphamapHeight);

        // 원본 데이터를 저장할 복사본 생성
        group.savedSplatmapData = new float[alphamapHeight, alphamapWidth, terrainData.alphamapLayers];

        // 영향 받는 지점만 저장
        foreach (Vector2Int alphaCoord in group.affectedAlphamapPoints)
        {
            for (int layer = 0; layer < terrainData.alphamapLayers; layer++)
            {
                group.savedSplatmapData[alphaCoord.y, alphaCoord.x, layer] = splatmapData[alphaCoord.y, alphaCoord.x, layer];
            }
        }

        Debug.Log($"그룹 '{group.groupName}'의 원본 텍스처가 저장되었습니다.");
    }

    // 특정 그룹에 첫 번째 레이어 텍스처 적용 (개별 그룹 직접 적용 시 사용)
    void ApplyFirstLayerTextureForGroup(int groupIndex)
    {
        PlaneGroup group = planeGroups[groupIndex];

        if (group.affectedAlphamapPoints.Count == 0)
        {
            Debug.LogWarning($"그룹 '{group.groupName}'의 플레인 아래 지형 영역이 계산되지 않았습니다!");
            return;
        }

        TerrainData terrainData = targetTerrain.terrainData;

        // 레이어 확인
        if (terrainData.terrainLayers == null || terrainData.terrainLayers.Length == 0)
        {
            Debug.LogError("지형에 텍스처 레이어가 없습니다!");
            return;
        }

        // 현재 스플랫맵 가져오기
        float[,,] splatmapData = terrainData.GetAlphamaps(0, 0, alphamapWidth, alphamapHeight);

        // 영향 받는 지점에만 첫 번째 레이어 적용 (중첩 영역 고려)
        foreach (Vector2Int alphaCoord in group.affectedAlphamapPoints)
        {
            // 이 좌표가 여러 그룹에 의해 영향받는지 확인
            if (affectedCoordinatesMap.ContainsKey(alphaCoord) && affectedCoordinatesMap[alphaCoord].Count > 1)
            {
                // 중첩 영역 처리
                List<int> affectingGroups = affectedCoordinatesMap[alphaCoord];

                // 활성화된 그룹 수 계산
                int activeGroupsCount = 0;
                foreach (int affectingGroupIndex in affectingGroups)
                {
                    if (planeGroups[affectingGroupIndex].useAlternativeTexture)
                    {
                        activeGroupsCount++;
                    }
                }

                // 모든 그룹이 비활성화되었으면 원본으로 복원
                if (activeGroupsCount == 0)
                {
                    for (int layer = 0; layer < terrainData.alphamapLayers; layer++)
                    {
                        splatmapData[alphaCoord.y, alphaCoord.x, layer] = group.savedSplatmapData[alphaCoord.y, alphaCoord.x, layer];
                    }
                }
                // 하나 이상 활성화되었으면 첫 번째 레이어 적용
                else
                {
                    // 모든 레이어를 0으로 설정
                    for (int layer = 0; layer < terrainData.alphamapLayers; layer++)
                    {
                        splatmapData[alphaCoord.y, alphaCoord.x, layer] = 0;
                    }

                    // 첫 번째 레이어만 1로 설정
                    splatmapData[alphaCoord.y, alphaCoord.x, 0] = 1;
                }
            }
            // 중첩 영역이 아닌 경우 - 단순히 첫 번째 레이어 적용
            else
            {
                // 모든 레이어를 0으로 설정
                for (int layer = 0; layer < terrainData.alphamapLayers; layer++)
                {
                    splatmapData[alphaCoord.y, alphaCoord.x, layer] = 0;
                }

                // 첫 번째 레이어만 1로 설정
                splatmapData[alphaCoord.y, alphaCoord.x, 0] = 1;
            }
        }

        // 변경된 스플랫맵 적용
        terrainData.SetAlphamaps(0, 0, splatmapData);
        Debug.Log($"그룹 '{group.groupName}'에 첫 번째 레이어 텍스처가 적용되었습니다.");
    }

    // 특정 그룹의 원본 텍스처 복원 (개별 그룹 직접 적용 시 사용)
    void RestoreOriginalTextureForGroup(int groupIndex)
    {
        PlaneGroup group = planeGroups[groupIndex];

        if (group.savedSplatmapData == null)
        {
            Debug.LogError($"그룹 '{group.groupName}'의 저장된 텍스처 데이터가 없습니다!");
            return;
        }

        if (group.affectedAlphamapPoints.Count == 0)
        {
            Debug.LogWarning($"그룹 '{group.groupName}'의 플레인 아래 지형 영역이 계산되지 않았습니다!");
            return;
        }

        TerrainData terrainData = targetTerrain.terrainData;

        // 현재 스플랫맵 가져오기
        float[,,] splatmapData = terrainData.GetAlphamaps(0, 0, alphamapWidth, alphamapHeight);

        // 영향 받는 지점만 복원 (중첩 영역 고려)
        foreach (Vector2Int alphaCoord in group.affectedAlphamapPoints)
        {
            // 이 좌표가 여러 그룹에 의해 영향받는지 확인
            if (affectedCoordinatesMap.ContainsKey(alphaCoord) && affectedCoordinatesMap[alphaCoord].Count > 1)
            {
                // 중첩 영역 처리
                List<int> affectingGroups = affectedCoordinatesMap[alphaCoord];

                // 아직 활성화된 그룹이 있는지 확인
                bool anyGroupActive = false;
                foreach (int affectingGroupIndex in affectingGroups)
                {
                    // 자기 자신은 이미 비활성화로 간주
                    if (affectingGroupIndex != groupIndex && planeGroups[affectingGroupIndex].useAlternativeTexture)
                    {
                        anyGroupActive = true;
                        break;
                    }
                }

                // 다른 활성화된 그룹이 있으면 첫 번째 레이어 유지
                if (anyGroupActive)
                {
                    // 첫 번째 레이어 계속 사용 (변경 없음)
                    continue;
                }
                // 모든 그룹이 비활성화되었으면 원본으로 복원
                else
                {
                    for (int layer = 0; layer < terrainData.alphamapLayers; layer++)
                    {
                        splatmapData[alphaCoord.y, alphaCoord.x, layer] = group.savedSplatmapData[alphaCoord.y, alphaCoord.x, layer];
                    }
                }
            }
            // 중첩 영역이 아닌 경우 - 단순히 원본 복원
            else
            {
                for (int layer = 0; layer < terrainData.alphamapLayers; layer++)
                {
                    splatmapData[alphaCoord.y, alphaCoord.x, layer] = group.savedSplatmapData[alphaCoord.y, alphaCoord.x, layer];
                }
            }
        }

        // 변경된 스플랫맵 적용
        terrainData.SetAlphamaps(0, 0, splatmapData);
        Debug.Log($"그룹 '{group.groupName}'의 원본 텍스처가 복원되었습니다.");
    }

    // 에디터에서 기즈모 그리기
    void OnDrawGizmosSelected()
    {
        if (targetTerrain == null) return;

        // 애플리케이션이 실행 중이 아닌 경우 (에디터에서만)
        if (!Application.isPlaying)
        {
            // 각 그룹별로 플레인 시각화
            for (int i = 0; i < planeGroups.Count; i++)
            {
                PlaneGroup group = planeGroups[i];

                // 그룹별로 다른 색상 사용
                Color[] debugColors = new Color[] {
                    Color.red, Color.green, Color.blue, Color.magenta, Color.cyan, Color.yellow
                };
                Gizmos.color = debugColors[i % debugColors.Length];

                // 각 플레인 영역 시각화
                foreach (GameObject plane in group.planes)
                {
                    if (plane == null) continue;

                    Bounds bounds = GetPlaneWorldBounds(plane);
                    Gizmos.DrawWireCube(bounds.center, bounds.size);
                }
            }
        }
        // 애플리케이션이 실행 중인 경우
        else
        {
            // 각 그룹별로 영향 지점 시각화
            for (int i = 0; i < planeGroups.Count; i++)
            {
                PlaneGroup group = planeGroups[i];

                // 그룹별로 다른 색상 사용
                Color[] debugColors = new Color[] {
                    Color.red, Color.green, Color.blue, Color.magenta, Color.cyan, Color.yellow
                };
                Gizmos.color = debugColors[i % debugColors.Length];

                // 밝기를 토글 상태에 따라 조정
                if (!group.useAlternativeTexture)
                {
                    Gizmos.color = Color.Lerp(Gizmos.color, Color.black, 0.5f);
                }

                // 각 플레인의 영향 지점 시각화
                foreach (var planePair in group.planeTerrainPoints)
                {
                    foreach (Vector3 point in planePair.Value)
                    {
                        Gizmos.DrawSphere(point, 0.2f);
                    }
                }
            }

            // 중첩 영역 시각화 (흰색으로 표시)
            Gizmos.color = Color.white;
            foreach (var kvp in affectedCoordinatesMap)
            {
                if (kvp.Value.Count > 1)
                {
                    // 알파맵 좌표를 월드 좌표로 변환
                    Vector3 worldPos = GetWorldPosFromAlphamapCoord(kvp.Key);
                    Gizmos.DrawWireSphere(worldPos, 0.3f);
                }
            }
        }
    }

    // 알파맵 좌표를 월드 좌표로 변환 (기즈모 시각화용)
    private Vector3 GetWorldPosFromAlphamapCoord(Vector2Int alphaCoord)
    {
        TerrainData terrainData = targetTerrain.terrainData;
        Vector3 terrainPos = targetTerrain.transform.position;

        // 알파맵 좌표를 상대 좌표로 변환
        float relativeX = (float)alphaCoord.x / (alphamapWidth - 1);
        float relativeZ = (float)alphaCoord.y / (alphamapHeight - 1);

        // 월드 좌표 계산
        Vector3 worldPos = new Vector3(
            terrainPos.x + relativeX * terrainData.size.x,
            terrainPos.y,
            terrainPos.z + relativeZ * terrainData.size.z
        );

        // 지형 높이 가져오기
        worldPos.y = targetTerrain.SampleHeight(worldPos) + terrainPos.y;

        return worldPos;
    }
    
public void DisableGroupByEnvironmentId(string environmentId)
{
    string groupName = ConvertEnvironmentIdToGroupName(environmentId);
    Debug.Log($"환경 ID '{environmentId}'를 그룹명 '{groupName}'으로 변환");
    
    bool groupFound = false;
    for (int i = 0; i < planeGroups.Count; i++)
    {
        Debug.Log($"그룹 {i}: '{planeGroups[i].groupName}' vs '{groupName}'");
        if (planeGroups[i].groupName == groupName)
        {
            planeGroups[i].useAlternativeTexture = false;
            planeGroups[i].previousToggleState = false;
            groupFound = true;
            
            // 즉시 변경사항 적용
            ApplyAllGroupChanges();
            
            Debug.Log($"환경 {environmentId}에 해당하는 그룹 '{groupName}' 토글을 껐습니다.");
            break;
        }
    }
    
    if (!groupFound)
    {
        Debug.LogError($"환경 {environmentId}에 해당하는 그룹 '{groupName}'을 찾을 수 없습니다!");
    }
}

/// <summary>
/// 환경 ID를 플레인 그룹 이름으로 변환
/// </summary>
private string ConvertEnvironmentIdToGroupName(string environmentId)
{
    // env_foodstore -> foodstoreEnvironment
    if (environmentId.StartsWith("env_"))
    {
        string baseName = environmentId.Substring(4); // "env_" 제거
        return baseName + "Environment";
    }
    return environmentId;
}

/// <summary>
/// TerrainTextureSwitchManager 인스턴스를 가져오는 정적 메서드
/// </summary>
public static TerrainTextureSwitchManager GetInstance()
{
    return FindObjectOfType<TerrainTextureSwitchManager>();
}

/// <summary>
/// 선택된 모든 환경에 대해 토글을 끄는 메서드
/// </summary>
// public void DisableSelectedEnvironmentGroups()
// {
//     if (EnvironmentSelectionManager.Instance != null)
//     {
//         foreach (string environmentId in EnvironmentSelectionManager.Instance.selectedEnvironmentIds)
//         {
//             DisableGroupByEnvironmentId(environmentId);
//         }
        
//         // 변경사항 즉시 적용
//         ApplyAllGroupChanges();
        
//         Debug.Log($"선택된 {EnvironmentSelectionManager.Instance.selectedEnvironmentIds.Count}개 환경의 토글을 껐습니다.");
//     }
// }
}