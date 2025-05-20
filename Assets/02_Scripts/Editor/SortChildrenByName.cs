using UnityEngine;
using UnityEditor;

public class SortChildrenByName : EditorWindow
{
    [MenuItem("Tools/Sort Children By Name")]
    static void SortSelectedGameObjectChildren()
    {
        // 현재 선택한 게임오브젝트들에 대해 처리
        foreach (GameObject go in Selection.gameObjects)
        {
            SortChildren(go);
        }
    }

    static void SortChildren(GameObject parent)
    {
        int childCount = parent.transform.childCount;
        // 자식 오브젝트들을 배열에 담음
        GameObject[] children = new GameObject[childCount];
        for (int i = 0; i < childCount; i++)
        {
            children[i] = parent.transform.GetChild(i).gameObject;
        }

        // 이름을 기준으로 오름차순 정렬
        System.Array.Sort(children, (a, b) => a.name.CompareTo(b.name));

        // 정렬된 순서대로 자식의 sibling index를 재설정
        for (int i = 0; i < children.Length; i++)
        {
            children[i].transform.SetSiblingIndex(i);
        }
    }
}
