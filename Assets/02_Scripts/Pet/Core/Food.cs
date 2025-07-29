using UnityEngine;
using PetAIProperties = PetTraits;
/// <summary>
/// Food 컴포넌트 인터페이스
/// </summary>
public interface Food
{
    PetAIProperties.DietaryFlags FoodType { get; }
}