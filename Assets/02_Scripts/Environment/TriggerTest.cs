using UnityEngine;

/// <summary>
/// Trigger 충돌 테스트용 임시 스크립트
/// </summary>
public class TriggerTest : MonoBehaviour
{
    void OnTriggerEnter(Collider other)
    {
        Debug.Log($"[TriggerTest] 무언가 들어옴: {other.name}, Layer: {other.gameObject.layer}");
    }
    
    void OnTriggerStay(Collider other)
    {
        Debug.Log($"[TriggerTest] 무언가 머물고 있음: {other.name}");
    }
    
    void OnTriggerExit(Collider other)
    {
        Debug.Log($"[TriggerTest] 무언가 나감: {other.name}");
    }
}