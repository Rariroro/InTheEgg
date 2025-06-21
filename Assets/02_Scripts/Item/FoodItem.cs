// FoodItem.cs - 새로운 스크립트
using UnityEngine;

/// <summary>
/// 드롭된 음식 아이템을 관리하는 컴포넌트입니다.
/// 아이템의 음식 종류를 정의하고, 파괴될 때 PetFeedingController의 캐시에서 스스로를 제거합니다.
/// </summary>
public class FoodItem : MonoBehaviour
{
    [Tooltip("이 음식이 어떤 종류에 속하는지 선택하세요.")]
    public PetAIProperties.DietaryFlags foodType; // 펫의 식성과 비교할 음식 종류

    private void Start()
    {
        // 생성 시점에 스스로를 정적 캐시에 등록합니다.
        PetFeedingController.RegisterFoodItem(gameObject);
    }

    private void OnDestroy()
    {
        // 아이템이 파괴될 때 캐시에서도 제거하도록 요청합니다.
        PetFeedingController.UnregisterFoodItem(gameObject);
        // Debug.Log($"음식 아이템 삭제됨: {gameObject.name}");
    }
}