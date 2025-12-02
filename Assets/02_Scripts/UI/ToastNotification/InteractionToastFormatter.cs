using System.Collections.Generic;

/// <summary>
/// 상호작용 타입에 따른 토스트 알림 포맷팅 헬퍼
///
/// ✅ Single Source of Truth: 모든 상호작용의 한글 이름은 여기서 정의됩니다.
/// </summary>
public static class InteractionToastFormatter
{
    /// <summary>
    /// 상호작용 타입별 한글 이름 - 프로젝트 전체에서 사용되는 유일한 정의
    /// </summary>
    private static readonly Dictionary<InteractionType, string> interactionNames = new Dictionary<InteractionType, string>
    {
        { InteractionType.Fight, "싸움" },
        { InteractionType.Headbutt, "박치기" },
        { InteractionType.CamelAlpacaSpitFight, "침 뱉기 대결" },
        { InteractionType.WalkTogether, "함께 산책" },
        { InteractionType.RestTogether, "함께 휴식" },
        { InteractionType.SleepTogether, "함께 잠들기" },
        { InteractionType.TurtleRabbitRace, "토끼와 거북이 경주" },
        { InteractionType.ChaseAndRun, "추격전" },
        { InteractionType.RideAndWalk, "태우고 걷기" },
        { InteractionType.SlowRace, "느림보 경주" },
        { InteractionType.PredatorMoleHunt, "두더지 사냥" },
        { InteractionType.PredatorPossumPrank, "주머니쥐 장난" },
        { InteractionType.ChameleonCamouflage, "위장 놀이" },
        { InteractionType.PersonalityReaction, "성격 반응" },
        { InteractionType.SkunkDefense, "스컹크 방어" }
    };

    /// <summary>
    /// 상호작용 타입의 한글 이름 가져오기
    /// </summary>
    public static string GetInteractionName(InteractionType type)
    {
        return interactionNames.TryGetValue(type, out string name) ? name : type.ToString();
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
}
