using System;
using UnityEngine;

/// <summary>
/// 펫의 기본 프로필 정보를 담는 클래스
/// 이름, 타입, 성격, 식성, 서식지 등 펫의 고유 특성을 그룹화
/// </summary>
[Serializable]
public class PetProfile
{
    [Header("기본 정보")]
    [Tooltip("펫의 이름")]
    public string name = "Buddy";
    
    [Tooltip("펫의 생일")]
    public DateTime birthday = DateTime.Now;
    
    [Header("펫 특성")]
    [Tooltip("펫의 종류")]
    public PetType type = PetType.Dog;
    
    [Tooltip("펫의 성격")]
    public PetTraits.Personality personality = PetTraits.Personality.Shy;
    
    [Tooltip("펫이 먹는 음식의 종류를 중복 선택할 수 있습니다")]
    public PetTraits.DietaryFlags diet = PetTraits.DietaryFlags.None;
    
    [Tooltip("펫의 주요 서식지")]
    public PetTraits.Habitat habitat = PetTraits.Habitat.Forest;
    
    [Header("펫 크기")]
    [Tooltip("펫의 크기 분류 (CapsuleCollider radius 기준)")]
    public PetTraits.Size size = PetTraits.Size.Medium;
    
    [Header("특수 행동 설정")]
    [Tooltip("서식지가 'Tree'인 펫이 평소에 나무에 오를 확률 (0.0 ~ 1.0)")]
    [Range(0f, 1f)]
    public float treeClimbChance = 0.1f;
    
    [Tooltip("펫이 물에 잠기는 깊이 (값이 클수록 더 깊이 잠김)")]
    [Range(0f, 5f)]
    public float waterSinkDepth = 1.0f;
    
    /// <summary>
    /// 프로필 정보를 문자열로 반환
    /// </summary>
    public override string ToString()
    {
        return $"{name} ({type}) - {personality}, Diet: {diet}, Habitat: {habitat}";
    }
    
    /// <summary>
    /// 특정 음식을 먹을 수 있는지 확인
    /// </summary>
    public bool CanEat(PetTraits.DietaryFlags foodType)
    {
        return (diet & foodType) != 0;
    }
    
    /// <summary>
    /// 물 서식지 펫인지 확인
    /// </summary>
    public bool IsAquatic => habitat == PetTraits.Habitat.Water;
    
    /// <summary>
    /// 나무 서식지 펫인지 확인
    /// </summary>
    public bool IsArboreal => habitat == PetTraits.Habitat.Tree;
    
    /// <summary>
    /// 크기에 따른 상호작용 거리 배율 반환
    /// </summary>
    public float GetInteractionDistanceMultiplier()
    {
        switch (size)
        {
            case PetTraits.Size.Small:
                return 0.6f;  // 기본 거리의 60%
            case PetTraits.Size.Medium:
                return 1.0f;  // 기본 거리 100%
            case PetTraits.Size.Large:
                return 1.5f;  // 기본 거리의 150%
            default:
                return 1.0f;
        }
    }
}

// PetType enum은 PetInteractionManager.cs에 이미 정의되어 있음