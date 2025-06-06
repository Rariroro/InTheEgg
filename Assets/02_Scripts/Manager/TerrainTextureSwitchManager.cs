using UnityEngine;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;

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

    // 원본 텍스처 데이터 - Dictionary로 최적화
    [HideInInspector]
    public Dictionary<Vector2Int, float[]> savedSplatmapDict;
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
    private Dictionary<Vector2Int, List<int>> affectedCoordinatesMap = new Dictionary<Vector2Int, List<int>>();
    
    [Header("초기화 설정")]
    [Tooltip("시작 시 모든 토글을 자동으로 켤 것인지")]
    public bool autoEnableAllTogglesOnStart = true;

    // 런타임용 임시 TerrainData
    private TerrainData runtimeTerrainData;
    private TerrainData originalTerrainData;

    // 레이캐스트 결과 캐싱
    private Dictionary<GameObject, List<Vector3>> cachedPlanePoints = new Dictionary<GameObject, List<Vector3>>();

    // 최적화된 변경 추적
    private HashSet<Vector2Int> dirtyCoordinates = new HashSet<Vector2Int>();
    private bool hasChanges = false;

    void Start()
    {
        if (targetTerrain == null)
        {
            Debug.LogError("대상 지형이 할당되지 않았습니다!");
            return;
        }

        // 원본 TerrainData 보존
        originalTerrainData = targetTerrain.terrainData;
        // 런타임용 TerrainData 복사본 생성
        runtimeTerrainData = Instantiate(originalTerrainData);
        runtimeTerrainData.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
        targetTerrain.terrainData = runtimeTerrainData;

        if (planeGroups.Count == 0)
        {
            Debug.LogWarning("플레인 그룹이 정의되지 않았습니다!");
            return;
        }

        // 지형 데이터 가져오기
        TerrainData terrainData = targetTerrain.terrainData;
        alphamapWidth = terrainData.alphamapWidth;
        alphamapHeight = terrainData.alphamapHeight;

        // 모든 그룹별로 영향받는 지형 영역 계산
        CalculateAllGroupsAffectedAreas();

        // 중첩 영역 맵 구축
        BuildOverlappingAreasMap();

        // 각 그룹별로 초기 상태 저장
        for (int i = 0; i < planeGroups.Count; i++)
        {
            SaveOriginalTextureForGroup(i);
        }

        // 시작 시 모든 토글 켜기
        if (autoEnableAllTogglesOnStart)
        {
            for (int i = 0; i < planeGroups.Count; i++)
            {
                planeGroups[i].useAlternativeTexture = true;
                planeGroups[i].previousToggleState = true;
            }
            ApplyAllGroupChanges();
        }
    }

    void OnApplicationQuit()
    {
        RestoreAllOriginalTextures();
    }

    void OnDestroy()
    {
        if (targetTerrain != null && originalTerrainData != null)
        {
            targetTerrain.terrainData = originalTerrainData;
        }

        if (runtimeTerrainData != null)
        {
#if UNITY_EDITOR
            DestroyImmediate(runtimeTerrainData);
#else
            Destroy(runtimeTerrainData);
#endif
        }

        // 캐시 정리
        cachedPlanePoints.Clear();
    }

    // 이벤트 기반으로 변경된 토글 설정 메서드
    public void SetGroupToggle(int groupIndex, bool enabled)
    {
        if (groupIndex < 0 || groupIndex >= planeGroups.Count) return;
        
        PlaneGroup group = planeGroups[groupIndex];
        if (group.useAlternativeTexture == enabled) return; // 변경 없으면 무시
        
        group.useAlternativeTexture = enabled;
        group.previousToggleState = enabled;
        
        // 변경된 그룹만 처리
        ApplyGroupChangeOptimized(groupIndex);
    }

    // 최적화된 단일 그룹 변경 적용
    private void ApplyGroupChangeOptimized(int changedGroupIndex)
    {
        TerrainData terrainData = targetTerrain.terrainData;
        PlaneGroup changedGroup = planeGroups[changedGroupIndex];
        
        // 이 그룹이 영향을 미치는 좌표만 처리
        HashSet<Vector2Int> coordsToUpdate = new HashSet<Vector2Int>(changedGroup.affectedAlphamapPoints);
        
        // 중첩 영역 추가
        foreach (var coord in changedGroup.affectedAlphamapPoints)
        {
            if (affectedCoordinatesMap.ContainsKey(coord))
            {
                foreach (int otherGroupIndex in affectedCoordinatesMap[coord])
                {
                    coordsToUpdate.Add(coord);
                }
            }
        }
        
        // 최소 영역 계산
        int minX = int.MaxValue, minY = int.MaxValue;
        int maxX = int.MinValue, maxY = int.MinValue;
        
        foreach (var coord in coordsToUpdate)
        {
            minX = Mathf.Min(minX, coord.x);
            minY = Mathf.Min(minY, coord.y);
            maxX = Mathf.Max(maxX, coord.x);
            maxY = Mathf.Max(maxY, coord.y);
        }
        
        if (minX > maxX || minY > maxY) return;
        
        int width = maxX - minX + 1;
        int height = maxY - minY + 1;
        
        // 최소 영역만 GetAlphamaps
        float[,,] splatmapData = terrainData.GetAlphamaps(minX, minY, width, height);
        
        // 변경 적용
        foreach (var coord in coordsToUpdate)
        {
            int localX = coord.x - minX;
            int localY = coord.y - minY;
            
            ApplyTextureAtCoordinate(splatmapData, localX, localY, coord, terrainData.alphamapLayers);
        }
        
        // 최소 영역만 SetAlphamaps
        terrainData.SetAlphamaps(minX, minY, splatmapData);
    }

    // 특정 좌표에 텍스처 적용
    private void ApplyTextureAtCoordinate(float[,,] splatmapData, int localX, int localY, Vector2Int worldCoord, int layerCount)
    {
        List<int> affectingGroups = affectedCoordinatesMap[worldCoord];
        
        // 활성화된 그룹 수 계산
        int activeGroupsCount = 0;
        foreach (int groupIndex in affectingGroups)
        {
            if (planeGroups[groupIndex].useAlternativeTexture)
            {
                activeGroupsCount++;
            }
        }
        
        if (activeGroupsCount == 0)
        {
            // 원본 복원
            int firstGroupIndex = affectingGroups[0];
            PlaneGroup firstGroup = planeGroups[firstGroupIndex];
            
            if (firstGroup.savedSplatmapDict != null && firstGroup.savedSplatmapDict.ContainsKey(worldCoord))
            {
                float[] originalData = firstGroup.savedSplatmapDict[worldCoord];
                for (int layer = 0; layer < layerCount; layer++)
                {
                    splatmapData[localY, localX, layer] = originalData[layer];
                }
            }
        }
        else
        {
            // 첫 번째 레이어 적용
            for (int layer = 0; layer < layerCount; layer++)
            {
                splatmapData[localY, localX, layer] = (layer == 0) ? 1f : 0f;
            }
        }
    }

    // 모든 그룹의 변경사항을 한 번에 적용
    private void ApplyAllGroupChanges()
    {
        TerrainData terrainData = targetTerrain.terrainData;
        float[,,] splatmapData = terrainData.GetAlphamaps(0, 0, alphamapWidth, alphamapHeight);
        bool anyChanges = false;

        foreach (var kvp in affectedCoordinatesMap)
        {
            Vector2Int alphaCoord = kvp.Key;
            List<int> affectingGroups = kvp.Value;

            int activeGroupsCount = 0;
            foreach (int groupIndex in affectingGroups)
            {
                if (planeGroups[groupIndex].useAlternativeTexture)
                {
                    activeGroupsCount++;
                }
            }

            if (activeGroupsCount == 0)
            {
                int firstGroupIndex = affectingGroups[0];
                PlaneGroup firstGroup = planeGroups[firstGroupIndex];

                if (firstGroup.savedSplatmapDict != null && firstGroup.savedSplatmapDict.ContainsKey(alphaCoord))
                {
                    float[] originalData = firstGroup.savedSplatmapDict[alphaCoord];
                    for (int layer = 0; layer < terrainData.alphamapLayers; layer++)
                    {
                        splatmapData[alphaCoord.y, alphaCoord.x, layer] = originalData[layer];
                    }
                    anyChanges = true;
                }
            }
            else if (activeGroupsCount > 0)
            {
                for (int layer = 0; layer < terrainData.alphamapLayers; layer++)
                {
                    splatmapData[alphaCoord.y, alphaCoord.x, layer] = (layer == 0) ? 1f : 0f;
                }
                anyChanges = true;
            }
        }

        if (anyChanges)
        {
            terrainData.SetAlphamaps(0, 0, splatmapData);
        }
    }

    // 최적화된 레이캐스트를 사용한 영향 영역 계산
    List<Vector3> CalculateAffectedTerrainArea(GameObject plane)
    {
        // 캐시 확인
        if (cachedPlanePoints.ContainsKey(plane))
        {
            return new List<Vector3>(cachedPlanePoints[plane]);
        }
        
        List<Vector3> affectedPoints = new List<Vector3>();
        Bounds planeWorldBounds = GetPlaneWorldBounds(plane);
        
        // 동적 샘플링 - 작은 플레인은 적은 샘플
        float planeArea = planeWorldBounds.size.x * planeWorldBounds.size.z;
        int totalSamples = Mathf.Clamp(Mathf.RoundToInt(planeArea * samplesPerUnit), 20, 100);
        
        // 배치 레이캐스트 준비
        NativeArray<RaycastCommand> commands = new NativeArray<RaycastCommand>(totalSamples, Allocator.TempJob);
        NativeArray<RaycastHit> results = new NativeArray<RaycastHit>(totalSamples, Allocator.TempJob);
        
        List<Vector3> samplePoints = GenerateSamplePoints(plane, totalSamples);
        
        for (int i = 0; i < totalSamples; i++)
        {
            Vector3 origin = samplePoints[i] + Vector3.up * 0.5f;
            commands[i] = new RaycastCommand(origin, Vector3.down, 1000f, LayerMask.GetMask("Terrain"));
        }
        
        // 배치 레이캐스트 실행
        JobHandle handle = RaycastCommand.ScheduleBatch(commands, results, 1);
        handle.Complete();
        
        for (int i = 0; i < results.Length; i++)
        {
            if (results[i].collider != null)
            {
                affectedPoints.Add(results[i].point);
            }
        }
        
        commands.Dispose();
        results.Dispose();
        
        // 결과 캐싱
        cachedPlanePoints[plane] = new List<Vector3>(affectedPoints);
        
        return affectedPoints;
    }

    // 최적화된 원본 텍스처 저장
    void SaveOriginalTextureForGroup(int groupIndex)
    {
        PlaneGroup group = planeGroups[groupIndex];
        if (group.affectedAlphamapPoints.Count == 0) return;
        
        TerrainData terrainData = targetTerrain.terrainData;
        
        // Dictionary로 필요한 좌표만 저장
        if (group.savedSplatmapDict == null)
            group.savedSplatmapDict = new Dictionary<Vector2Int, float[]>();
        
        foreach (Vector2Int coord in group.affectedAlphamapPoints)
        {
            float[] layerData = new float[terrainData.alphamapLayers];
            float[,,] currentData = terrainData.GetAlphamaps(coord.x, coord.y, 1, 1);
            
            for (int layer = 0; layer < terrainData.alphamapLayers; layer++)
            {
                layerData[layer] = currentData[0, 0, layer];
            }
            
            group.savedSplatmapDict[coord] = layerData;
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

   private void CalculateGroupAffectedTerrainArea(int groupIndex)
{
    PlaneGroup group = planeGroups[groupIndex];
    group.affectedAlphamapPoints.Clear();

    foreach (GameObject plane in group.planes)
    {
        Bounds planeBounds = GetPlaneWorldBounds(plane);
        TerrainData terrainData = targetTerrain.terrainData;

        // 플레인 바운드에 해당하는 알파맵 좌표 범위 계산
        Vector3 terrainPos = targetTerrain.transform.position;
        float relativeMinX = (planeBounds.min.x - terrainPos.x) / terrainData.size.x;
        float relativeMaxX = (planeBounds.max.x - terrainPos.x) / terrainData.size.x;
        float relativeMinZ = (planeBounds.min.z - terrainPos.z) / terrainData.size.z;
        float relativeMaxZ = (planeBounds.max.z - terrainPos.z) / terrainData.size.z;

        int alphaMinX = Mathf.Clamp(Mathf.FloorToInt(relativeMinX * alphamapWidth), 0, alphamapWidth - 1);
        int alphaMaxX = Mathf.Clamp(Mathf.CeilToInt(relativeMaxX * alphamapWidth), 0, alphamapWidth - 1);
        int alphaMinY = Mathf.Clamp(Mathf.FloorToInt(relativeMinZ * alphamapHeight), 0, alphamapHeight - 1);
        int alphaMaxY = Mathf.Clamp(Mathf.CeilToInt(relativeMaxZ * alphamapHeight), 0, alphamapHeight - 1);

        // 영역 전체를 affectedAlphamapPoints에 넣기
        for (int y = alphaMinY; y <= alphaMaxY; y++)
        {
            for (int x = alphaMinX; x <= alphaMaxX; x++)
            {
                Vector2Int coord = new Vector2Int(x, y);
                if (!group.affectedAlphamapPoints.Contains(coord))
                    group.affectedAlphamapPoints.Add(coord);
            }
        }
    }
}

    // 중첩 영역 맵 구축
    void BuildOverlappingAreasMap()
    {
        affectedCoordinatesMap.Clear();

        for (int groupIndex = 0; groupIndex < planeGroups.Count; groupIndex++)
        {
            PlaneGroup group = planeGroups[groupIndex];

            foreach (Vector2Int coord in group.affectedAlphamapPoints)
            {
                if (!affectedCoordinatesMap.ContainsKey(coord))
                {
                    affectedCoordinatesMap[coord] = new List<int>();
                }
                affectedCoordinatesMap[coord].Add(groupIndex);
            }
        }

        int overlappingCount = 0;
        foreach (var kvp in affectedCoordinatesMap)
        {
            if (kvp.Value.Count > 1) overlappingCount++;
        }
        Debug.Log($"발견된 중첩 영역: {overlappingCount} 알파맵 좌표");
    }

    // 유틸리티 메서드들
    private Bounds GetPlaneWorldBounds(GameObject plane)
    {
        Renderer planeRenderer = plane.GetComponent<Renderer>();
        if (planeRenderer == null) return new Bounds();
        return planeRenderer.bounds;
    }

    private List<Vector3> GenerateSamplePoints(GameObject plane, int numSamples)
    {
        List<Vector3> samplePoints = new List<Vector3>();
        Bounds bounds = GetPlaneWorldBounds(plane);
        int gridSize = Mathf.CeilToInt(Mathf.Sqrt(numSamples));

        for (int x = 0; x < gridSize; x++)
        {
            for (int z = 0; z < gridSize; z++)
            {
                float normalizedX = (float)x / (gridSize - 1);
                float normalizedZ = (float)z / (gridSize - 1);

                Vector3 worldPos = new Vector3(
                    Mathf.Lerp(bounds.min.x, bounds.max.x, normalizedX),
                    bounds.center.y,
                    Mathf.Lerp(bounds.min.z, bounds.max.z, normalizedZ)
                );

                samplePoints.Add(worldPos);
            }
        }

        return samplePoints;
    }

    private Vector2Int GetAlphamapCoord(Vector3 worldPos)
    {
        TerrainData terrainData = targetTerrain.terrainData;
        Vector3 terrainPos = targetTerrain.transform.position;

        float relativeX = (worldPos.x - terrainPos.x) / terrainData.size.x;
        float relativeZ = (worldPos.z - terrainPos.z) / terrainData.size.z;

        int alphaX = Mathf.Clamp(Mathf.FloorToInt(relativeX * alphamapWidth), 0, alphamapWidth - 1);
        int alphaY = Mathf.Clamp(Mathf.FloorToInt(relativeZ * alphamapHeight), 0, alphamapHeight - 1);

        return new Vector2Int(alphaX, alphaY);
    }

    private Vector3 GetWorldPosFromAlphamapCoord(Vector2Int alphaCoord)
    {
        TerrainData terrainData = targetTerrain.terrainData;
        Vector3 terrainPos = targetTerrain.transform.position;

        float relativeX = (float)alphaCoord.x / (alphamapWidth - 1);
        float relativeZ = (float)alphaCoord.y / (alphamapHeight - 1);

        Vector3 worldPos = new Vector3(
            terrainPos.x + relativeX * terrainData.size.x,
            terrainPos.y,
            terrainPos.z + relativeZ * terrainData.size.z
        );

        worldPos.y = targetTerrain.SampleHeight(worldPos) + terrainPos.y;
        return worldPos;
    }

    // 모든 텍스처를 원본으로 복원
    private void RestoreAllOriginalTextures()
    {
        if (targetTerrain == null) return;

        for (int i = 0; i < planeGroups.Count; i++)
        {
            planeGroups[i].useAlternativeTexture = false;
            planeGroups[i].previousToggleState = false;
        }

        ApplyAllGroupChanges();
        Debug.Log("모든 지형 텍스처가 원본으로 복원되었습니다.");
    }

    // 외부에서 호출하는 인터페이스 메서드들
    public void ResetAllTogglesToOff()
    {
        Debug.Log("모든 환경 토글을 끕니다.");

        for (int i = 0; i < planeGroups.Count; i++)
        {
            SetGroupToggle(i, false);
        }

        Debug.Log("모든 환경이 원본 상태로 복원되었습니다.");
    }

    public void DisableGroupByEnvironmentId(string environmentId)
    {
        string groupName = ConvertEnvironmentIdToGroupName(environmentId);
        
        for (int i = 0; i < planeGroups.Count; i++)
        {
            if (planeGroups[i].groupName == groupName)
            {
                SetGroupToggle(i, false);
                Debug.Log($"환경 {environmentId}에 해당하는 그룹 '{groupName}' 토글을 껐습니다.");
                break;
            }
        }
    }

    private string ConvertEnvironmentIdToGroupName(string environmentId)
    {
        if (environmentId.StartsWith("env_"))
        {
            string baseName = environmentId.Substring(4);
            return baseName + "Environment";
        }
        return environmentId;
    }

    public static TerrainTextureSwitchManager GetInstance()
    {
        return FindObjectOfType<TerrainTextureSwitchManager>();
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        if (targetTerrain == null || !showDebugVisuals) return;

        if (!Application.isPlaying)
        {
            for (int i = 0; i < planeGroups.Count; i++)
            {
                PlaneGroup group = planeGroups[i];
                Color[] debugColors = new Color[] {
                    Color.red, Color.green, Color.blue, Color.magenta, Color.cyan, Color.yellow
                };
                Gizmos.color = debugColors[i % debugColors.Length];

                foreach (GameObject plane in group.planes)
                {
                    if (plane == null) continue;
                    Bounds bounds = GetPlaneWorldBounds(plane);
                    Gizmos.DrawWireCube(bounds.center, bounds.size);
                }
            }
        }
        else
        {
            for (int i = 0; i < planeGroups.Count; i++)
            {
                PlaneGroup group = planeGroups[i];
                Color[] debugColors = new Color[] {
                    Color.red, Color.green, Color.blue, Color.magenta, Color.cyan, Color.yellow
                };
                Gizmos.color = debugColors[i % debugColors.Length];

                if (!group.useAlternativeTexture)
                {
                    Gizmos.color = Color.Lerp(Gizmos.color, Color.black, 0.5f);
                }

                foreach (var planePair in group.planeTerrainPoints)
                {
                    foreach (Vector3 point in planePair.Value)
                    {
                        Gizmos.DrawSphere(point, 0.2f);
                    }
                }
            }

            Gizmos.color = Color.white;
            foreach (var kvp in affectedCoordinatesMap)
            {
                if (kvp.Value.Count > 1)
                {
                    Vector3 worldPos = GetWorldPosFromAlphamapCoord(kvp.Key);
                    Gizmos.DrawWireSphere(worldPos, 0.3f);
                }
            }
        }
    }
#endif
}