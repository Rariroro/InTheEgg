using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 상호작용 타입에 따른 토스트 알림 포맷팅 헬퍼
/// </summary>
public static class InteractionToastFormatter
{
    // 상호작용 타입별 한글 이름
    private static readonly Dictionary<InteractionType, string> interactionNames = new Dictionary<InteractionType, string>
    {
        { InteractionType.Fight, "싸움" },
        { InteractionType.WalkTogether, "함께 산책" },
        { InteractionType.RestTogether, "함께 휴식" },
        { InteractionType.Race, "달리기 시합" },
        { InteractionType.ChaseAndRun, "추격전" },
        { InteractionType.SleepTogether, "함께 잠들기" },
        { InteractionType.RideAndWalk, "태우고 걷기" },
        { InteractionType.SlothKoalaRace, "느림보 경주" },
        { InteractionType.ChameleonCamouflage, "위장 놀이" }
    };

    // 상호작용 타입별 이모지/아이콘 텍스트
    private static readonly Dictionary<InteractionType, string> interactionEmojis = new Dictionary<InteractionType, string>
    {
        { InteractionType.Fight, "⚔️" },
        { InteractionType.WalkTogether, "🚶" },
        { InteractionType.RestTogether, "🛋️" },
        { InteractionType.Race, "🏃" },
        { InteractionType.ChaseAndRun, "🏃‍♂️" },
        { InteractionType.SleepTogether, "😴" },
        { InteractionType.RideAndWalk, "🏇" },
        { InteractionType.SlothKoalaRace, "🐌" },
        { InteractionType.ChameleonCamouflage, "🎨" }
    };

    // 상호작용 타입별 색상
    private static readonly Dictionary<InteractionType, Color> interactionColors = new Dictionary<InteractionType, Color>
    {
        { InteractionType.Fight, new Color(1f, 0.3f, 0.3f) },          // 빨강
        { InteractionType.WalkTogether, new Color(0.5f, 0.8f, 0.5f) }, // 초록
        { InteractionType.RestTogether, new Color(0.6f, 0.6f, 0.9f) }, // 파랑
        { InteractionType.Race, new Color(1f, 0.6f, 0.2f) },           // 주황
        { InteractionType.ChaseAndRun, new Color(1f, 0.8f, 0.3f) },    // 노랑
        { InteractionType.SleepTogether, new Color(0.5f, 0.5f, 0.8f) },// 보라
        { InteractionType.RideAndWalk, new Color(0.8f, 0.5f, 0.3f) },  // 갈색
        { InteractionType.SlothKoalaRace, new Color(0.6f, 0.7f, 0.6f) },// 회록색
        { InteractionType.ChameleonCamouflage, new Color(0.7f, 0.5f, 0.8f) } // 연보라
    };

    // 활동별 메시지
    private static readonly Dictionary<string, string> activityMessages = new Dictionary<string, string>
    {
        { "Eat", "먹이 먹기" },
        { "Sleep", "잠들기" },
        { "Wander", "산책하기" },
        { "ClimbTree", "나무 오르기" },
        { "Diving", "다이빙" },
        { "TreasureHunt", "보물 찾기" },
        { "TreasureFound", "보물 발견!" },
        { "Butterfly", "나비와 놀기" },
        { "BeeEscape", "벌 피하기" },
        { "Gather", "모이기" },
        { "EnvironmentGather", "탐색하기" }
    };

    /// <summary>
    /// 상호작용 타입의 한글 이름 가져오기
    /// </summary>
    public static string GetInteractionName(InteractionType type)
    {
        return interactionNames.TryGetValue(type, out string name) ? name : type.ToString();
    }

    /// <summary>
    /// 상호작용 타입의 이모지 가져오기
    /// </summary>
    public static string GetInteractionEmoji(InteractionType type)
    {
        return interactionEmojis.TryGetValue(type, out string emoji) ? emoji : "🎮";
    }

    /// <summary>
    /// 상호작용 타입의 색상 가져오기
    /// </summary>
    public static Color GetInteractionColor(InteractionType type)
    {
        return interactionColors.TryGetValue(type, out Color color) ? color : Color.white;
    }

    /// <summary>
    /// 활동 이름의 한글 메시지 가져오기
    /// </summary>
    public static string GetActivityMessage(string activityName)
    {
        return activityMessages.TryGetValue(activityName, out string message) ? message : activityName;
    }

    /// <summary>
    /// 포맷팅된 상호작용 메시지 생성
    /// </summary>
    public static string FormatInteractionMessage(string pet1Name, string pet2Name, InteractionType type, bool includeEmoji = false)
    {
        string interactionName = GetInteractionName(type);

        if (includeEmoji)
        {
            string emoji = GetInteractionEmoji(type);
            return $"{emoji} {pet1Name} & {pet2Name} - {interactionName}";
        }
        else
        {
            return $"{pet1Name} & {pet2Name} - {interactionName}";
        }
    }

    /// <summary>
    /// 짧은 포맷 메시지 (토스트용)
    /// </summary>
    public static string FormatShortMessage(string pet1Name, string pet2Name, InteractionType type)
    {
        // 이름 길이 제한
        pet1Name = TruncateName(pet1Name, 6);
        pet2Name = TruncateName(pet2Name, 6);

        return $"{pet1Name} ↔ {pet2Name}";
    }

    /// <summary>
    /// 활동 메시지 포맷팅
    /// </summary>
    public static string FormatActivityMessage(string petName, string activityName, bool includeEmoji = false)
    {
        string activity = GetActivityMessage(activityName);
        petName = TruncateName(petName, 8);

        if (includeEmoji)
        {
            string emoji = GetActivityEmoji(activityName);
            return $"{emoji} {petName} - {activity}";
        }
        else
        {
            return $"{petName} - {activity}";
        }
    }

    /// <summary>
    /// 활동별 이모지 가져오기
    /// </summary>
    private static string GetActivityEmoji(string activityName)
    {
        switch (activityName)
        {
            case "Eat": return "🍖";
            case "Sleep": return "💤";
            case "Wander": return "🚶";
            case "ClimbTree": return "🌳";
            case "Diving": return "🌊";
            case "TreasureHunt": return "🔍";
            case "TreasureFound": return "💎";
            case "Butterfly": return "🦋";
            case "BeeEscape": return "🐝";
            case "Gather": return "📢";
            case "EnvironmentGather": return "🔎";
            default: return "🎯";
        }
    }

    /// <summary>
    /// 이름 자르기
    /// </summary>
    private static string TruncateName(string name, int maxLength)
    {
        if (string.IsNullOrEmpty(name)) return "";
        if (name.Length <= maxLength) return name;
        return name.Substring(0, maxLength - 1) + "…";
    }

    /// <summary>
    /// 우선순위별 메시지 접두사
    /// </summary>
    public static string GetPriorityPrefix(NotificationPriority priority)
    {
        switch (priority)
        {
            case NotificationPriority.Critical:
                return "[특별] ";
            case NotificationPriority.High:
                return "[중요] ";
            default:
                return "";
        }
    }

    /// <summary>
    /// 시간 포맷팅 (방금, 1분 전 등)
    /// </summary>
    public static string FormatTimeAgo(System.DateTime timestamp)
    {
        var timeSpan = System.DateTime.Now - timestamp;

        if (timeSpan.TotalSeconds < 5)
            return "방금";
        else if (timeSpan.TotalSeconds < 60)
            return $"{(int)timeSpan.TotalSeconds}초 전";
        else if (timeSpan.TotalMinutes < 60)
            return $"{(int)timeSpan.TotalMinutes}분 전";
        else if (timeSpan.TotalHours < 24)
            return $"{(int)timeSpan.TotalHours}시간 전";
        else
            return $"{(int)timeSpan.TotalDays}일 전";
    }

    /// <summary>
    /// 거리 포맷팅
    /// </summary>
    public static string FormatDistance(float distance)
    {
        if (distance < 10f)
            return "매우 가까움";
        else if (distance < 25f)
            return "가까움";
        else if (distance < 50f)
            return "보통";
        else if (distance < 100f)
            return "멀리";
        else
            return "매우 멀리";
    }

    /// <summary>
    /// 집계 메시지 포맷팅
    /// </summary>
    public static string FormatAggregatedMessage(int count, InteractionType? type = null)
    {
        if (type.HasValue)
        {
            string typeName = GetInteractionName(type.Value);
            return $"{count}개의 {typeName} 진행 중";
        }
        else
        {
            return $"{count}개의 상호작용 진행 중";
        }
    }

    /// <summary>
    /// 펫 타입별 아이콘 텍스트 (이모지)
    /// </summary>
    public static string GetPetTypeEmoji(PetType petType)
    {
        // 대표적인 동물들만 이모지 매핑
        switch (petType)
        {
            case PetType.Cat: return "🐱";
            case PetType.Dog: return "🐶";
            case PetType.Rabbit: return "🐰";
            case PetType.Fox: return "🦊";
            case PetType.Bear: return "🐻";
            case PetType.Panda: return "🐼";
            case PetType.Monkey: return "🐵";
            case PetType.Tiger: return "🐯";
            case PetType.Lion: return "🦁";
            case PetType.Elephant: return "🐘";
            case PetType.Giraffe: return "🦒";
            case PetType.Zebra: return "🦓";
            case PetType.Kangaroo: return "🦘";
            case PetType.Koala: return "🐨";
            case PetType.Owl: return "🦉";
            case PetType.Chicken: return "🐔";
            case PetType.Duck: return "🦆";
            case PetType.Peacock: return "🦚";
            case PetType.Turtle: return "🐢";
            case PetType.Crocodile: return "🐊";
            case PetType.Hippo: return "🦛";
            case PetType.Rhino: return "🦏";
            case PetType.Pig: return "🐷";
            case PetType.Cow: return "🐄";
            case PetType.Horse: return "🐴";
            case PetType.Sheep: return "🐑";
            case PetType.Goat: return "🐐";
            case PetType.Camel: return "🐪";
            case PetType.Sloth: return "🦥";
            case PetType.Otter: return "🦦";
            case PetType.Skunk: return "🦨";
            case PetType.Raccoon: return "🦝";
            default: return "🐾";
        }
    }
}