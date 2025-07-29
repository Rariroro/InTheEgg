using System;
using UnityEngine;

/// <summary>
/// 펫의 이동 관련 설정을 관리하는 클래스
/// </summary>
[Serializable]
public class MovementSettings
{
    [Header("기본 이동 속도")]
    [Tooltip("걷기 속도")]
    [Range(0.5f, 10f)]
    public float walkSpeed = 3.5f;
    
    [Tooltip("달리기 속도 배율 (걷기 속도에 곱해짐)")]
    [Range(1.5f, 3f)]
    public float runMultiplier = 2f;
    
    [Header("NavMeshAgent 설정")]
    [Tooltip("회전 속도")]
    [Range(100f, 1000f)]
    public float angularSpeed = 360f;
    
    [Tooltip("가속도")]
    [Range(1f, 20f)]
    public float acceleration = 8f;
    
    [Tooltip("정지 거리")]
    [Range(0.1f, 5f)]
    public float stoppingDistance = 1f;
    
    [Header("회전 설정")]
    [Tooltip("회전 부드러움 (값이 클수록 빠른 회전)")]
    [Range(1f, 20f)]
    public float rotationSmoothness = 10f;
    
    [Header("특수 이동 설정")]
    [Tooltip("나무 오르기 높이")]
    [Range(1f, 10f)]
    public float climbHeight = 5f;
    
    [Tooltip("물 속 이동 속도 감소율")]
    [Range(0.1f, 1f)]
    public float waterSpeedMultiplier = 0.3f;
    
    [Tooltip("물 속성 펫의 물 속 속도 감소율")]
    [Range(0.3f, 1f)]
    public float aquaticWaterSpeedMultiplier = 0.7f;
    
    [Header("성격별 속도 조정")]
    [Tooltip("소심한 성격 속도 배율")]
    [Range(0.5f, 1.5f)]
    public float shySpeedMultiplier = 0.8f;
    
    [Tooltip("용감한 성격 속도 배율")]
    [Range(0.5f, 1.5f)]
    public float braveSpeedMultiplier = 1.1f;
    
    [Tooltip("게으른 성격 속도 배율")]
    [Range(0.5f, 1.5f)]
    public float lazySpeedMultiplier = 0.7f;
    
    [Tooltip("놀기 좋아하는 성격 속도 배율")]
    [Range(0.5f, 1.5f)]
    public float playfulSpeedMultiplier = 1.2f;
    
    /// <summary>
    /// 달리기 속도 계산
    /// </summary>
    public float RunSpeed => walkSpeed * runMultiplier;
    
    /// <summary>
    /// 성격에 따른 속도 조정값 가져오기
    /// </summary>
    public float GetPersonalitySpeedMultiplier(PetTraits.Personality personality)
    {
        switch (personality)
        {
            case PetTraits.Personality.Shy:
                return shySpeedMultiplier;
            case PetTraits.Personality.Brave:
                return braveSpeedMultiplier;
            case PetTraits.Personality.Lazy:
                return lazySpeedMultiplier;
            case PetTraits.Personality.Playful:
                return playfulSpeedMultiplier;
            default:
                return 1f;
        }
    }
    
    /// <summary>
    /// 성격이 적용된 이동 속도 계산
    /// </summary>
    public float GetAdjustedWalkSpeed(PetTraits.Personality personality)
    {
        return walkSpeed * GetPersonalitySpeedMultiplier(personality);
    }
    
    /// <summary>
    /// 물 속에서의 속도 계산
    /// </summary>
    public float GetWaterSpeed(bool isAquatic, PetTraits.Personality personality)
    {
        float baseSpeed = GetAdjustedWalkSpeed(personality);
        float waterMultiplier = isAquatic ? aquaticWaterSpeedMultiplier : waterSpeedMultiplier;
        return baseSpeed * waterMultiplier;
    }
    
    /// <summary>
    /// 설정값 검증
    /// </summary>
    public void Validate()
    {
        walkSpeed = Mathf.Clamp(walkSpeed, 0.5f, 10f);
        runMultiplier = Mathf.Clamp(runMultiplier, 1.5f, 3f);
        angularSpeed = Mathf.Clamp(angularSpeed, 100f, 1000f);
        acceleration = Mathf.Clamp(acceleration, 1f, 20f);
        stoppingDistance = Mathf.Clamp(stoppingDistance, 0.1f, 5f);
        rotationSmoothness = Mathf.Clamp(rotationSmoothness, 1f, 20f);
        climbHeight = Mathf.Clamp(climbHeight, 1f, 10f);
        waterSpeedMultiplier = Mathf.Clamp(waterSpeedMultiplier, 0.1f, 1f);
        aquaticWaterSpeedMultiplier = Mathf.Clamp(aquaticWaterSpeedMultiplier, 0.3f, 1f);
        shySpeedMultiplier = Mathf.Clamp(shySpeedMultiplier, 0.5f, 1.5f);
        braveSpeedMultiplier = Mathf.Clamp(braveSpeedMultiplier, 0.5f, 1.5f);
        lazySpeedMultiplier = Mathf.Clamp(lazySpeedMultiplier, 0.5f, 1.5f);
        playfulSpeedMultiplier = Mathf.Clamp(playfulSpeedMultiplier, 0.5f, 1.5f);
    }
    
    /// <summary>
    /// 기본값으로 초기화
    /// </summary>
    public void Reset()
    {
        walkSpeed = 3.5f;
        runMultiplier = 2f;
        angularSpeed = 360f;
        acceleration = 8f;
        stoppingDistance = 1f;
        rotationSmoothness = 10f;
        climbHeight = 5f;
        waterSpeedMultiplier = 0.3f;
        aquaticWaterSpeedMultiplier = 0.7f;
        shySpeedMultiplier = 0.8f;
        braveSpeedMultiplier = 1.1f;
        lazySpeedMultiplier = 0.7f;
        playfulSpeedMultiplier = 1.2f;
    }
}