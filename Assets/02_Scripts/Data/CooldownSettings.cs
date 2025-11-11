using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 쿨타임 설정을 저장하는 ScriptableObject
/// 에디터에서 모든 쿨타임 값을 편리하게 조정 가능
/// </summary>
[CreateAssetMenu(fileName = "CooldownSettings", menuName = "InTheEgg/Cooldown Settings", order = 1)]
public class CooldownSettings : ScriptableObject
{
    [Header("펫 상호작용 쿨타임")]
    [Tooltip("펫 간 일반 상호작용 쿨타임 - 상호작용 후 해당 펫의 체력 회복 시간 (초)")]
    [Range(0f, 120f)]
    public float petInteractionCooldown = 60f;

    [Header("활동별 쿨타임")]
    [Tooltip("다이빙 성공 후 쿨타임 (초)")]
    [Range(0f, 120f)]
    public float divingCooldown = 30f;

    [Tooltip("다이빙 실패 후 쿨타임 (초)")]
    [Range(0f, 180f)]
    public float divingFailedCooldown = 60f;

    [Tooltip("나무 오르기 탐색 쿨타임 (초)")]
    [Range(0f, 60f)]
    public float treeClimbingCooldown = 10f;

    [Tooltip("나비 놀이 쿨타임 (초)")]
    [Range(0f, 60f)]
    public float butterflyPlayCooldown = 0f;

    [Tooltip("보물 찾기 쿨타임 (초)")]
    [Range(0f, 120f)]
    public float treasureHuntCooldown = 60f;

    [Header("환경 상호작용 쿨타임")]
    [Tooltip("환경 터치 입력 쿨타임 (초)")]
    [Range(0f, 1f)]
    public float environmentTouchCooldown = 0.1f;

    [Tooltip("선물 터치 쿨타임 (초)")]
    [Range(0f, 1f)]
    public float giftTouchCooldown = 0.1f;

    [Header("먹이 관련 쿨타임")]
    [Tooltip("먹이 먹기 쿨타임 (초)")]
    [Range(0f, 60f)]
    public float feedingCooldown = 10f;

    [Tooltip("먹이 탐색 쿨타임 (초)")]
    [Range(0f, 30f)]
    public float foodSearchCooldown = 5f;

    [Header("쿨타임 수정자")]
    [Tooltip("전역 쿨타임 배율 (0.5 = 50% 감소, 2.0 = 200% 증가)")]
    [Range(0.1f, 3f)]
    public float globalCooldownMultiplier = 1f;

    [Tooltip("성격별 쿨타임 배율")]
    public PersonalityCooldownModifier[] personalityModifiers = new PersonalityCooldownModifier[]
    {
        new PersonalityCooldownModifier { personality = PetTraits.Personality.Playful, multiplier = 0.8f },
        new PersonalityCooldownModifier { personality = PetTraits.Personality.Lazy, multiplier = 1.2f },
        new PersonalityCooldownModifier { personality = PetTraits.Personality.Brave, multiplier = 0.9f },
        new PersonalityCooldownModifier { personality = PetTraits.Personality.Shy, multiplier = 1.1f }
    };

    [Header("디버그 설정")]
    [Tooltip("쿨타임을 무시하는 디버그 모드")]
    public bool debugIgnoreCooldowns = false;

    [Tooltip("모든 쿨타임을 이 값으로 고정 (디버그용, 0이면 사용 안 함)")]
    [Range(0f, 120f)]
    public float debugFixedCooldown = 0f;

    /// <summary>
    /// 성격별 쿨타임 수정자
    /// </summary>
    [System.Serializable]
    public class PersonalityCooldownModifier
    {
        public PetTraits.Personality personality;
        [Range(0.1f, 2f)]
        public float multiplier = 1f;
    }

    /// <summary>
    /// 쿨타임 타입에 따른 지속시간 가져오기
    /// </summary>
    public float GetCooldownDuration(CooldownManager.CooldownType type)
    {
        // ✅ 우선순위 1: 고정 쿨타임 (명시적 테스트 값)
        if (debugFixedCooldown > 0)
            return debugFixedCooldown;

        // ✅ 우선순위 2: 쿨타임 무시 (완전 비활성화)
        if (debugIgnoreCooldowns)
            return 0f;

        // ✅ 우선순위 3: 정상 쿨타임 (기본값)
        // 타입별 기본 쿨타임 가져오기
        float baseDuration = GetBaseDuration(type);

        // 전역 배율 적용
        return baseDuration * globalCooldownMultiplier;
    }

    /// <summary>
    /// 펫의 성격을 고려한 쿨타임 가져오기
    /// </summary>
    public float GetCooldownDurationWithPersonality(CooldownManager.CooldownType type, PetTraits.Personality personality)
    {
        float baseDuration = GetCooldownDuration(type);

        // 성격별 수정자 적용
        foreach (var modifier in personalityModifiers)
        {
            if (modifier.personality == personality)
            {
                return baseDuration * modifier.multiplier;
            }
        }

        return baseDuration;
    }

    /// <summary>
    /// 기본 쿨타임 값 가져오기 (수정자 적용 전)
    /// </summary>
    private float GetBaseDuration(CooldownManager.CooldownType type)
    {
        switch (type)
        {
            // 펫 상호작용
            case CooldownManager.CooldownType.PetInteraction:
                return petInteractionCooldown;

            // 활동별 쿨타임
            case CooldownManager.CooldownType.Diving:
                return divingCooldown;
            case CooldownManager.CooldownType.DivingFailed:
                return divingFailedCooldown;
            case CooldownManager.CooldownType.TreeClimbing:
                return treeClimbingCooldown;
            case CooldownManager.CooldownType.ButterflyPlay:
                return butterflyPlayCooldown;
            case CooldownManager.CooldownType.TreasureHunt:
                return treasureHuntCooldown;

            // 환경 상호작용
            case CooldownManager.CooldownType.EnvironmentTouch:
                return environmentTouchCooldown;
            case CooldownManager.CooldownType.GiftTouch:
                return giftTouchCooldown;

            // 먹이 관련
            case CooldownManager.CooldownType.Feeding:
                return feedingCooldown;
            case CooldownManager.CooldownType.FoodSearch:
                return foodSearchCooldown;

            // 기타
            case CooldownManager.CooldownType.Custom:
            default:
                return 10f; // 기본값
        }
    }

    /// <summary>
    /// 런타임에 특정 쿨타임 값 변경
    /// </summary>
    public void SetCooldownDuration(CooldownManager.CooldownType type, float duration)
    {
        switch (type)
        {
            case CooldownManager.CooldownType.PetInteraction:
                petInteractionCooldown = duration;
                break;
            case CooldownManager.CooldownType.Diving:
                divingCooldown = duration;
                break;
            case CooldownManager.CooldownType.DivingFailed:
                divingFailedCooldown = duration;
                break;
            case CooldownManager.CooldownType.TreeClimbing:
                treeClimbingCooldown = duration;
                break;
            case CooldownManager.CooldownType.ButterflyPlay:
                butterflyPlayCooldown = duration;
                break;
            // ... 필요한 경우 추가
        }
    }

    /// <summary>
    /// 모든 쿨타임을 비율로 조정
    /// </summary>
    /// <param name="multiplier">배율 (0.5 = 50%, 2.0 = 200%)</param>
    public void AdjustAllCooldowns(float multiplier)
    {
        petInteractionCooldown *= multiplier;
        divingCooldown *= multiplier;
        divingFailedCooldown *= multiplier;
        treeClimbingCooldown *= multiplier;
        butterflyPlayCooldown *= multiplier;
        treasureHuntCooldown *= multiplier;
        environmentTouchCooldown *= multiplier;
        giftTouchCooldown *= multiplier;
        feedingCooldown *= multiplier;
        foodSearchCooldown *= multiplier;
    }

    /// <summary>
    /// 기본값으로 리셋
    /// </summary>
    [ContextMenu("Reset to Default")]
    public void ResetToDefault()
    {
        petInteractionCooldown = 60f;
        divingCooldown = 30f;
        divingFailedCooldown = 60f;
        treeClimbingCooldown = 10f;
        butterflyPlayCooldown = 0f;
        treasureHuntCooldown = 60f;
        environmentTouchCooldown = 0.1f;
        giftTouchCooldown = 0.1f;
        feedingCooldown = 10f;
        foodSearchCooldown = 5f;
        globalCooldownMultiplier = 1f;
        debugIgnoreCooldowns = false;
        debugFixedCooldown = 0f;
    }

    /// <summary>
    /// 쿨타임 정보를 문자열로 출력
    /// </summary>
    public string GetCooldownInfo()
    {
        string info = "=== Cooldown Settings ===\n";
        info += $"Pet Interaction: {petInteractionCooldown}s\n";
        info += $"Diving: {divingCooldown}s (Failed: {divingFailedCooldown}s)\n";
        info += $"Tree Climbing: {treeClimbingCooldown}s\n";
        info += $"Butterfly Play: {butterflyPlayCooldown}s\n";
        info += $"Treasure Hunt: {treasureHuntCooldown}s\n";
        info += $"Environment Touch: {environmentTouchCooldown}s\n";
        info += $"Feeding: {feedingCooldown}s\n";
        info += $"Global Multiplier: {globalCooldownMultiplier:F1}x\n";

        if (debugIgnoreCooldowns)
            info += "[DEBUG MODE: All cooldowns disabled]\n";
        else if (debugFixedCooldown > 0)
            info += $"[DEBUG MODE: Fixed cooldown {debugFixedCooldown}s]\n";

        return info;
    }
}