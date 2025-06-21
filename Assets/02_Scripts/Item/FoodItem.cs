// FoodItem.cs - 레이어 시스템에 맞게 수정된 버전
using UnityEngine;

/// <summary>
/// 드롭된 음식 아이템을 관리하는 컴포넌트입니다.
/// 이제 아이템의 음식 종류 데이터만 가지고 있으며, 레이어를 통해 탐지됩니다.
/// </summary>
public class FoodItem : MonoBehaviour
{
    [Tooltip("이 음식이 어떤 종류에 속하는지 선택하세요.")]
    public PetAIProperties.DietaryFlags foodType; // 펫의 식성과 비교할 음식 종류

    // ★★★ Start()와 OnDestroy() 메서드 전체를 삭제합니다. ★★★
    // 더 이상 중앙 컨트롤러에 스스로를 등록/제거할 필요가 없습니다.
}