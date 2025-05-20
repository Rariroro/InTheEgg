using UnityEngine;
using UnityEditor;
using System;

public class AttachObjectsToCollider : EditorWindow
{
    [MenuItem("Tools/선택 오브젝트를 Collider에 붙이기")]
    static void AttachSelectedObjectsToCollider()
    {
        foreach (GameObject obj in Selection.gameObjects)
        {
            Ray ray = new Ray(obj.transform.position + Vector3.up * 1000, Vector3.down);
            // 모든 충돌 결과를 가져옴
            RaycastHit[] hits = Physics.RaycastAll(ray, Mathf.Infinity);
            
            if (hits.Length > 0)
            {
                // 거리순으로 정렬
                Array.Sort(hits, (x, y) => x.distance.CompareTo(y.distance));
                bool foundValidHit = false;
                foreach (RaycastHit hit in hits)
                {
                    // 자신의 Collider는 제외하고 처리
                    if (hit.collider.gameObject != obj)
                    {
                        Vector3 pos = obj.transform.position;
                        pos.y = hit.point.y;
                        obj.transform.position = pos;
                        foundValidHit = true;
                        break;
                    }
                }
                if (!foundValidHit)
                {
                    Debug.LogWarning($"오브젝트 {obj.name} 아래에서 유효한 Collider를 찾지 못했습니다.");
                }
            }
            else
            {
                Debug.LogWarning($"오브젝트 {obj.name} 아래에서 Collider를 찾지 못했습니다.");
            }
        }
    }
}
