using System;
using System.Collections.Generic;

namespace FlutterIntegration
{
    /// <summary>
    /// Flutter에서 수신하는 메시지의 루트 구조
    /// </summary>
    [Serializable]
    public class FlutterMessage
    {
        public string type;
        public FlutterGameData data;
    }

    /// <summary>
    /// INIT_GAME 메시지의 데이터 구조
    /// </summary>
    [Serializable]
    public class FlutterGameData
    {
        public int coins;  // v3.0: 유저의 초기 코인
        public List<FlutterPetData> pets;
        public List<FlutterLegendaryPetData> legendaryPets;
        public List<FlutterEnvironmentData> environmentItems;
        public List<FlutterFoodData> foodItems;
    }

    /// <summary>
    /// 일반 펫 데이터 (pet_001 ~ pet_060)
    /// </summary>
    [Serializable]
    public class FlutterPetData
    {
        public string petCardId;      // "pet_001" ~ "pet_060"
        public string petName;        // 한글 이름 (선택)
        public int petIntimacy;       // 0 ~ 100
        public bool isSpawned;        // true: 직접 스폰, false: Egg로 스폰

        /// <summary>
        /// petCardId에서 배열 인덱스 추출 (pet_001 -> 0)
        /// </summary>
        public int GetPrefabIndex()
        {
            if (string.IsNullOrEmpty(petCardId)) return -1;

            if (petCardId.StartsWith("pet_") && petCardId.Length >= 7)
            {
                string numberPart = petCardId.Substring(4);
                if (int.TryParse(numberPart, out int number))
                {
                    return number - 1;
                }
            }
            return -1;
        }
    }

    /// <summary>
    /// 레전드 펫 데이터 (pet_legend_001 ~ pet_legend_021)
    /// v3.5: petCardId 제거, legendaryPetId만 사용
    /// </summary>
    [Serializable]
    public class FlutterLegendaryPetData
    {
        public string legendaryPetId; // "pet_legend_001_1734567890123" (전체 ID)
        public string petName;        // 영문 이름
        public bool isSpawned;        // true: 직접 스폰, false: Gift로 스폰

        /// <summary>
        /// legendaryPetId에서 프리팹 매칭용 기본 ID 추출
        /// 예: "pet_legend_011_1734567890123" → "pet_legend_011"
        /// </summary>
        public string GetBasePetCardId()
        {
            if (string.IsNullOrEmpty(legendaryPetId)) return null;

            // pet_legend_XXX_timestamp 형식에서 앞부분만 추출
            // pet_legend_ (11자) + 숫자 3자 = 14자
            if (legendaryPetId.StartsWith("pet_legend_") && legendaryPetId.Length >= 14)
            {
                return legendaryPetId.Substring(0, 14);
            }
            return legendaryPetId;
        }

        /// <summary>
        /// legendaryPetId에서 배열 인덱스 추출 (pet_legend_011_xxx -> 10)
        /// </summary>
        public int GetPrefabIndex()
        {
            string basePetCardId = GetBasePetCardId();
            if (string.IsNullOrEmpty(basePetCardId)) return -1;

            if (basePetCardId.StartsWith("pet_legend_") && basePetCardId.Length >= 14)
            {
                string numberPart = basePetCardId.Substring(11, 3);
                if (int.TryParse(numberPart, out int number))
                {
                    return number - 1;
                }
            }
            return -1;
        }
    }

    /// <summary>
    /// 환경 아이템 데이터 (env_foodstore, env_pond 등)
    /// </summary>
    [Serializable]
    public class FlutterEnvironmentData
    {
        public string id;             // "env_foodstore", "env_pond" 등
        public string name;           // 한글 이름 (선택)
        public bool isSpawned;        // true: 직접 배치, false: 선물상자
    }

    /// <summary>
    /// 음식 아이템 데이터 (food_001 ~ food_007)
    /// </summary>
    [Serializable]
    public class FlutterFoodData
    {
        public string id;             // "food_001" ~ "food_007"
        public string name;           // 한글 이름 (선택)
        public int quantity;          // 보유 수량

        /// <summary>
        /// id에서 음식 타입 문자열 반환
        /// food_001=meat, food_002=fish, food_003=fruit,
        /// food_004=vegetable, food_005=Grain, food_006=hay, food_007=Grass
        /// </summary>
        public string GetFoodType()
        {
            if (string.IsNullOrEmpty(id)) return null;

            switch (id)
            {
                case "food_001": return "meat";
                case "food_002": return "fish";
                case "food_003": return "fruit";
                case "food_004": return "vegetable";
                case "food_005": return "Grain";
                case "food_006": return "hay";
                case "food_007": return "Grass";
                default: return null;
            }
        }
    }
}
