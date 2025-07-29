using System;
using UnityEngine;

/// <summary>
/// 펫의 이동 관련 설정을 담는 클래스
/// 속도, 가속도, 회전 등 이동과 관련된 모든 설정을 그룹화
/// </summary>
[Serializable]
public class MovementSettings
{
    [Header("기본 이동 설정")]
    [Tooltip("걷기 속도")]
    [Range(0.5f, 10f)]
    public float walkSpeed = 3.5f;
    
    [Tooltip("달리기 속도 배율 (walkSpeed * runMultiplier)")]
    [Range(1.5f, 3f)]
    public float runMultiplier = 2f;
    
    [Tooltip("회전 속도")]
    [Range(30f, 360f)]
    public float angularSpeed = 120f;
    
    [Tooltip("가속도")]
    [Range(1f, 20f)]
    public float acceleration = 8f;
    
    [Tooltip("정지 거리")]
    [Range(0.1f, 2f)]
    public float stoppingDistance = 0.5f;
    
    [Header("부드러운 움직임 설정")]
    [Tooltip("회전 보간 속도")]
    [Range(1f, 10f)]
    public float rotationSmoothness = 5f;
    
    [Tooltip("애니메이션 전환 시간")]
    [Range(0.1f, 1f)]
    public float animationSmoothTime = 0.3f;
    
    [Header("특수 이동 설정")]
    [Tooltip("물 속에서의 속도 감소율 (0.0 ~ 1.0)")]
    [Range(0f, 1f)]
    public float waterSpeedReduction = 0.5f;
    
    [Tooltip("나무 오르기 속도")]
    [Range(0.5f, 5f)]
    public float climbSpeed = 2f;
    
    [Tooltip("나무에서 내려오는 속도")]
    [Range(0.5f, 5f)]
    public float descendSpeed = 3f;
    
    /// <summary>
    /// 달리기 속도 계산
    /// </summary>
    public float RunSpeed => walkSpeed * runMultiplier;
    
    /// <summary>
    /// 물 속에서의 이동 속도 계산
    /// </summary>
    public float WaterSpeed => walkSpeed * (1f - waterSpeedReduction);
    
    /// <summary>
    /// 설정값 검증
    /// </summary>
    public void Validate()
    {
        walkSpeed = Mathf.Max(0.5f, walkSpeed);
        runMultiplier = Mathf.Max(1.1f, runMultiplier);
        angularSpeed = Mathf.Max(30f, angularSpeed);
        acceleration = Mathf.Max(1f, acceleration);
        stoppingDistance = Mathf.Max(0.1f, stoppingDistance);
        rotationSmoothness = Mathf.Max(1f, rotationSmoothness);
        animationSmoothTime = Mathf.Clamp(animationSmoothTime, 0.1f, 1f);
        waterSpeedReduction = Mathf.Clamp01(waterSpeedReduction);
        climbSpeed = Mathf.Max(0.5f, climbSpeed);
        descendSpeed = Mathf.Max(0.5f, descendSpeed);
    }
    
    /// <summary>
    /// 기본값으로 초기화
    /// </summary>
    public void Reset()
    {
        walkSpeed = 3.5f;
        runMultiplier = 2f;
        angularSpeed = 120f;
        acceleration = 8f;
        stoppingDistance = 0.5f;
        rotationSmoothness = 5f;
        animationSmoothTime = 0.3f;
        waterSpeedReduction = 0.5f;
        climbSpeed = 2f;
        descendSpeed = 3f;
    }
}