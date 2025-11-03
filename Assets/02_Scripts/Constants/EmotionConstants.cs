using UnityEngine;

namespace InTheEgg.Constants
{
    /// <summary>
    /// 감정 시스템 관련 상수 정의
    /// </summary>
    public static class EmotionConstants
    {
        // ===== Duration 관련 상수 =====

        /// <summary>
        /// 활동이 지속되는 동안 계속 표시되는 감정 (999f = 사실상 무한)
        /// Activity가 Stop()될 때까지 유지
        /// </summary>
        public const float DURATION_PERSISTENT = 999f;

        /// <summary>
        /// 매우 짧은 감정 표현 (1초)
        /// 예: 놀람, 발견 등의 순간적 반응
        /// </summary>
        public const float DURATION_VERY_SHORT = 1f;

        /// <summary>
        /// 짧은 감정 표현 (3초)
        /// 예: 기쁨, 만족 등의 일시적 반응
        /// </summary>
        public const float DURATION_SHORT = 3f;

        /// <summary>
        /// 중간 길이 감정 표현 (5초)
        /// 예: 배고픔 알림, 피곤함 표시
        /// </summary>
        public const float DURATION_MEDIUM = 5f;

        /// <summary>
        /// 긴 감정 표현 (10초)
        /// 기본값으로 사용
        /// </summary>
        public const float DURATION_LONG = 10f;

        // ===== Priority 관련 상수 =====

        /// <summary>
        /// 긴급 우선순위 (50+)
        /// 예: 생존 관련 활동, 보물 발견
        /// </summary>
        public const float PRIORITY_EMERGENCY_MIN = 50f;

        /// <summary>
        /// 높은 우선순위 (20-49)
        /// 예: 중요한 욕구 충족, 특별 이벤트
        /// </summary>
        public const float PRIORITY_HIGH_MIN = 20f;
        public const float PRIORITY_HIGH_MAX = 49f;

        /// <summary>
        /// 중간 우선순위 (10-19)
        /// 예: 일반적인 활동, 환경 상호작용
        /// </summary>
        public const float PRIORITY_MEDIUM_MIN = 10f;
        public const float PRIORITY_MEDIUM_MAX = 19f;

        /// <summary>
        /// 낮은 우선순위 (1-9)
        /// 예: 배회, 기본 행동
        /// </summary>
        public const float PRIORITY_LOW_MIN = 1f;
        public const float PRIORITY_LOW_MAX = 9f;

        // ===== 배고픔 감정 관련 상수 =====

        /// <summary>
        /// 배고픔 감정이 자동으로 표시되기 시작하는 기준값
        /// </summary>
        public const float HUNGER_EMOTION_THRESHOLD = 70f;

        /// <summary>
        /// 배고픔 감정 변경 최소 간격 (초)
        /// </summary>
        public const float HUNGER_EMOTION_MIN_INTERVAL = 7f;

        /// <summary>
        /// 배고픔 감정 변경 최대 간격 (초)
        /// </summary>
        public const float HUNGER_EMOTION_MAX_INTERVAL = 25f;

        // ===== 특정 Activity 우선순위 상수 =====

        /// <summary>
        /// EatActivity: 매우 배고플 때 우선순위
        /// </summary>
        public const float PRIORITY_EATING_VERY_HUNGRY = 60f;

        /// <summary>
        /// EatActivity: 적당히 배고플 때 우선순위
        /// </summary>
        public const float PRIORITY_EATING_MODERATELY_HUNGRY = 20f;

        /// <summary>
        /// EatActivity: 먹는 중 우선순위
        /// </summary>
        public const float PRIORITY_EATING_IN_PROGRESS = 35f;

        /// <summary>
        /// SleepActivity: 매우 피곤할 때 우선순위
        /// </summary>
        public const float PRIORITY_SLEEPING_VERY_TIRED = 50f;

        /// <summary>
        /// SleepActivity: 적당히 피곤할 때 우선순위
        /// </summary>
        public const float PRIORITY_SLEEPING_MODERATELY_TIRED = 20f;

        /// <summary>
        /// SleepActivity: 자는 중 우선순위
        /// </summary>
        public const float PRIORITY_SLEEPING_IN_PROGRESS = 40f;

        /// <summary>
        /// TreasureHuntActivity: 탐색 중 우선순위
        /// </summary>
        public const float PRIORITY_TREASURE_HUNTING = 15f;

        /// <summary>
        /// TreasureFoundActivity: 보물 발견 후 우선순위
        /// </summary>
        public const float PRIORITY_TREASURE_FOUND = 20f;

        /// <summary>
        /// ButterflyPlayActivity: 나비와 놀기 우선순위
        /// </summary>
        public const float PRIORITY_BUTTERFLY_PLAY = 12f;

        /// <summary>
        /// ClimbTreeActivity: 나무 타기 우선순위
        /// </summary>
        public const float PRIORITY_CLIMB_TREE = 5f;

        /// <summary>
        /// DivingActivity: 다이빙 우선순위
        /// </summary>
        public const float PRIORITY_DIVING = 8f;

        /// <summary>
        /// WanderActivity: 배회 우선순위
        /// </summary>
        public const float PRIORITY_WANDER = 1f;
    }
}