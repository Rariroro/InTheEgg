// FoodItem.cs - 새로운 스크립트
using UnityEngine;

/// <summary>
/// 드롭된 음식 아이템을 관리하는 컴포넌트
/// 아이템이 삭제될 때 PetFeedingController 캐시에서도 제거합니다.
/// </summary>
public class FoodItem : MonoBehaviour
{
     [Tooltip("이 음식이 어떤 종류에 속하는지 선택하세요. (단일 선택)")]
    public PetAIProperties.DietaryFlags foodType;
    public void Initialize()
    {
        // 음식 아이템이 생성되었음을 로그로 확인
        Debug.Log($"음식 아이템 생성됨: {gameObject.name}");
    }
    
    private void OnDestroy()
    {
        // 아이템이 삭제될 때 캐시에서도 제거
        PetFeedingController.UnregisterFoodItem(gameObject);
        Debug.Log($"음식 아이템 삭제됨: {gameObject.name}");
    }
}