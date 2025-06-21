// FeedingArea.cs - 새로운 스크립트
using UnityEngine;

/// <summary>
/// 환경에 고정된 먹이 장소를 관리하는 컴포넌트입니다.
/// 이 장소가 어떤 종류의 음식을 제공하는지 정의합니다.
/// </summary>
public class FeedingArea : MonoBehaviour
{
    [Tooltip("이 장소에서 제공하는 음식의 종류를 선택하세요. (중복 가능)")]
    public PetAIProperties.DietaryFlags foodType;
}