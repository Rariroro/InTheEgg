using System;
using UnityEngine;

/// <summary>
/// 개별 펫의 프로필 데이터를 정의하는 클래스
/// ScriptableObject의 데이터베이스에서 사용
/// </summary>
[Serializable]
public class PetData
{
    [Header("펫 정보")]
    public PetType petType;
    public PetTraits.Personality personality;
    public PetTraits.DietaryFlags diet;
    public PetTraits.Habitat habitat;
    
    public PetData(PetType type, PetTraits.Personality p, PetTraits.DietaryFlags d, PetTraits.Habitat h)
    {
        petType = type;
        personality = p;
        diet = d;
        habitat = h;
    }
}