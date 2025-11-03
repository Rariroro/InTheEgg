using UnityEngine;

namespace InTheEgg.Constants
{
    /// <summary>
    /// 나비 놀이 활동 관련 상수 정의
    /// </summary>
    public static class ButterflyConstants
    {
        // ===== 거리 및 범위 설정 =====

        /// <summary>
        /// 나비를 감지할 수 있는 최대 거리 (미터)
        /// </summary>
        public const float DETECTION_RANGE = 20f;

        /// <summary>
        /// 나비와 놀이를 시작하는 거리 (미터)
        /// 이 거리보다 가까워지면 놀이 행동 시작
        /// </summary>
        public const float PLAY_DISTANCE = 3f;

        // ===== 시간 설정 =====

        /// <summary>
        /// 나비와 놀이하는 총 지속 시간 (초)
        /// </summary>
        public const float PLAY_DURATION = 45f;

        /// <summary>
        /// 나비 놀이 후 쿨다운 시간 (초)
        /// 다시 나비에게 관심을 가질 때까지 대기 시간
        /// </summary>
        public const float PLAY_COOLDOWN = 120f;

        /// <summary>
        /// 나비 주변에서 놀이하는 시간 (초)
        /// </summary>
        public const float PLAY_AROUND_DURATION = 10f;

        /// <summary>
        /// 나비에게 접근할 때 목표 위치 갱신 간격 (초)
        /// </summary>
        public const float CHASE_UPDATE_INTERVAL = 0.5f;

        /// <summary>
        /// 나비 주변을 도는 시간 (초)
        /// </summary>
        public const float CIRCLE_DURATION = 3f;

        /// <summary>
        /// 나비를 관찰하는 시간 (초)
        /// </summary>
        public const float OBSERVE_DURATION = 1.5f;

        /// <summary>
        /// 점프 애니메이션 지속 시간 (초)
        /// </summary>
        public const float JUMP_DURATION = 1.5f;

        /// <summary>
        /// 부드러운 회전 애니메이션 시간 (초)
        /// </summary>
        public const float SMOOTH_ROTATION_DURATION = 0.5f;

        // ===== 이동 및 행동 설정 =====

        /// <summary>
        /// 나비를 쫓을 때 이동 속도 배수
        /// </summary>
        public const float CHASE_SPEED_MULTIPLIER = 1.2f;

        /// <summary>
        /// 나비 주변을 돌 때 반경 (미터)
        /// </summary>
        public const float CIRCLE_RADIUS = 2f;

        /// <summary>
        /// 나비 주변을 돌 때 회전 속도 (도/초)
        /// </summary>
        public const float CIRCLE_ANGULAR_SPEED = 60f;

        // ===== 확률 설정 =====

        /// <summary>
        /// 나비를 발견했을 때 관심을 가질 확률 (0.0 ~ 1.0)
        /// </summary>
        public const float PLAY_INTEREST_CHANCE = 0.5f;

        // ===== 우선순위 설정 =====

        /// <summary>
        /// 나비와 놀이 기본 우선순위
        /// </summary>
        public const float BASE_PRIORITY = 15f;

        /// <summary>
        /// Playful 성격 보너스 우선순위
        /// </summary>
        public const float PLAYFUL_BONUS = 5f;

        /// <summary>
        /// 나비가 가까울 때 최대 우선순위
        /// </summary>
        public const float MAX_PRIORITY_NEARBY = 20f;

        /// <summary>
        /// 나비가 멀 때 최소 우선순위
        /// </summary>
        public const float MIN_PRIORITY_DISTANT = 10f;

        // ===== 욕구 임계값 =====

        /// <summary>
        /// 배고픔 임계값 - 이 값을 초과하면 나비 놀이를 하지 않음
        /// </summary>
        public const float HUNGER_THRESHOLD = 70f;

        /// <summary>
        /// 졸림 임계값 - 이 값을 초과하면 나비 놀이를 하지 않음
        /// </summary>
        public const float SLEEPINESS_THRESHOLD = 80f;
    }
}
