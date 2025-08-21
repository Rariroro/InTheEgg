using System;
using UnityEngine;

/// <summary>
/// 펫의 특성과 관련된 열거형들을 모아놓은 클래스
/// 기존 PetAIProperties를 더 직관적인 이름으로 변경
/// </summary>
public static class PetTraits
{
    /// <summary>
    /// 펫의 성격 타입
    /// </summary>
    public enum Personality 
    { 
        Shy,      // 소심한
        Brave,    // 용감한
        Lazy,     // 게으른
        Playful   // 놀기 좋아하는
    }

    /// <summary>
    /// 펫의 식성 (복수 선택 가능)
    /// </summary>
    [Flags]
    public enum DietaryFlags
    {
        None = 0,                    // 아무것도 먹지 않음
        SeedsAndGrains = 1 << 0,     // 씨앗 및 곡물 (값: 1)
        FruitsAndVegetables = 1 << 1, // 과일 및 채소 (값: 2)
        Grass = 1 << 2,              // 풀(초목) (값: 4)
        Honey = 1 << 3,              // 꿀 (값: 8)
        Meat = 1 << 4,               // 고기(육류) (값: 16)
        Fish = 1 << 5,               // 생선(어류) (값: 32)

        // 조합 예시
        Omnivore = SeedsAndGrains | FruitsAndVegetables | Meat | Fish,  // 일반적인 잡식
        Herbivore = FruitsAndVegetables | Grass | SeedsAndGrains,       // 일반적인 초식
        Carnivore = Meat | Fish                                         // 일반적인 육식
    }

    /// <summary>
    /// 펫의 주요 서식지
    /// </summary>
    public enum Habitat 
    { 
        Water,   // 물속
        Forest,  // 숲
        Field,   // 들판
        Fence,   // 울타리 근처
        Tree     // 나무 위
    }
    
    /// <summary>
    /// 펫의 크기 분류
    /// CapsuleCollider의 radius 값 기준
    /// </summary>
    public enum Size
    {
        Small,   // 소형 (radius < 1.5)
        Medium,  // 중형 (1.5 <= radius < 3.0)
        Large    // 대형 (radius >= 3.0)
    }
    
    
    /// <summary>
    /// 식성 타입을 읽기 쉬운 문자열로 변환
    /// </summary>
    public static string GetDietaryDescription(DietaryFlags diet)
    {
        if (diet == DietaryFlags.None) return "없음";
        if (diet == DietaryFlags.Omnivore) return "잡식성";
        if (diet == DietaryFlags.Herbivore) return "초식성";
        if (diet == DietaryFlags.Carnivore) return "육식성";

        // 개별 플래그 조합
        var parts = new System.Collections.Generic.List<string>();
        if ((diet & DietaryFlags.SeedsAndGrains) != 0) parts.Add("씨앗/곡물");
        if ((diet & DietaryFlags.FruitsAndVegetables) != 0) parts.Add("과일/채소");
        if ((diet & DietaryFlags.Grass) != 0) parts.Add("풀");
        if ((diet & DietaryFlags.Honey) != 0) parts.Add("꿀");
        if ((diet & DietaryFlags.Meat) != 0) parts.Add("고기");
        if ((diet & DietaryFlags.Fish) != 0) parts.Add("생선");

        return string.Join(", ", parts);
    }
}