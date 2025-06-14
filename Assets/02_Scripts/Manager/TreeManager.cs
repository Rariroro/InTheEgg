// TreeManager.cs

using UnityEngine;
using System.Collections.Generic;

public class TreeManager : MonoBehaviour
{
    public static TreeManager Instance { get; private set; }
    [SerializeField] private float gridSize = 50f;
    private Dictionary<Vector2Int, List<Transform>> treeGrid = new Dictionary<Vector2Int, List<Transform>>();
    private Vector3 worldOrigin;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
        }
        else
        {
            Instance = this;
        }
        worldOrigin = Vector3.zero;
        // Awake에서 BuildTreeGrid() 호출 제거!
    }

    /// <summary>
    /// 모든 'Tree' 태그를 가진 오브젝트를 찾아 그리드를 다시 빌드합니다.
    /// 이 메서드는 환경 생성이 완료된 후 외부에서 호출해야 합니다.
    /// </summary>
    public void RebuildTreeGrid()
    {
        treeGrid.Clear(); // 기존 그리드 데이터 초기화
        GameObject[] trees = GameObject.FindGameObjectsWithTag("Tree");
        
        foreach (var tree in trees)
        {
            RegisterTree(tree.transform);
        }
        
        Debug.Log($"[TreeManager] 그리드 재생성 완료. {trees.Length}개의 나무를 등록했습니다.");
    }

    /// <summary>
    /// 특정 나무를 그리드에 등록합니다.
    /// </summary>
    public void RegisterTree(Transform treeTransform)
    {
        Vector2Int gridPos = GetGridCellFromPosition(treeTransform.position);

        if (!treeGrid.ContainsKey(gridPos))
        {
            treeGrid[gridPos] = new List<Transform>();
        }
        // 중복 등록 방지
        if (!treeGrid[gridPos].Contains(treeTransform))
        {
            treeGrid[gridPos].Add(treeTransform);
        }
    }


    /// <summary>
    /// 특정 위치에서 가장 가까운 나무를 찾습니다.
    /// </summary>
    public Transform FindNearestTree(Vector3 position)
    {
        Vector2Int centerCell = GetGridCellFromPosition(position);
        Transform nearestTree = null;
        float minSqrDistance = float.MaxValue;

        // 현재 셀과 주변 8개 셀을 탐색
        for (int x = -1; x <= 1; x++)
        {
            for (int y = -1; y <= 1; y++)
            {
                Vector2Int cellToCheck = new Vector2Int(centerCell.x + x, centerCell.y + y);

                if (treeGrid.ContainsKey(cellToCheck))
                {
                    foreach (Transform tree in treeGrid[cellToCheck])
                    {
                        float sqrDist = (tree.position - position).sqrMagnitude;
                        if (sqrDist < minSqrDistance)
                        {
                            minSqrDistance = sqrDist;
                            nearestTree = tree;
                        }
                    }
                }
            }
        }

        return nearestTree;
    }

    /// <summary>
    /// 월드 좌표를 그리드 셀 좌표로 변환합니다.
    /// </summary>
    private Vector2Int GetGridCellFromPosition(Vector3 position)
    {
        int x = Mathf.FloorToInt((position.x - worldOrigin.x) / gridSize);
        int y = Mathf.FloorToInt((position.z - worldOrigin.z) / gridSize);
        return new Vector2Int(x, y);
    }

    // 디버깅을 위해 에디터에서 그리드를 시각화합니다.
    private void OnDrawGizmos()
    {
        if (treeGrid == null) return;

        Gizmos.color = Color.cyan;
        foreach (var cell in treeGrid.Keys)
        {
            Vector3 center = new Vector3(
                (cell.x * gridSize) + gridSize / 2f + worldOrigin.x,
                worldOrigin.y,
                (cell.y * gridSize) + gridSize / 2f + worldOrigin.z
            );
            Gizmos.DrawWireCube(center, new Vector3(gridSize, 1f, gridSize));
        }
    }
}