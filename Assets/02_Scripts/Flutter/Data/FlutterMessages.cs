using System;
using System.Collections.Generic;
using UnityEngine;

namespace FlutterIntegration
{
    /// <summary>
    /// Unity -> Flutter 메시지 베이스 클래스
    /// </summary>
    [Serializable]
    public abstract class FlutterOutboundMessage
    {
        public string type;

        public virtual string ToJson()
        {
            return JsonUtility.ToJson(this);
        }
    }

    #region UNITY_READY - Unity 준비 완료 알림

    /// <summary>
    /// Unity 준비 완료 메시지
    /// </summary>
    [Serializable]
    public class UnityReadyMessage : FlutterOutboundMessage
    {
        public UnityReadyMessage()
        {
            type = "READY";
        }
    }

    #endregion

    #region PET_SPAWNED - 일반 펫 Egg 터치 시

    [Serializable]
    public class PetSpawnedMessage : FlutterOutboundMessage
    {
        public PetSpawnedData data;

        public PetSpawnedMessage(string petCardId)
        {
            type = "PET_SPAWNED";
            data = new PetSpawnedData { petCardId = petCardId, isSpawned = true };
        }
    }

    [Serializable]
    public class PetSpawnedData
    {
        public string petCardId;
        public bool isSpawned;
    }

    #endregion

    #region LEGEND_PET_SPAWNED - 레전드 펫 Gift 터치 시

    [Serializable]
    public class LegendPetSpawnedMessage : FlutterOutboundMessage
    {
        public LegendPetSpawnedData data;

        public LegendPetSpawnedMessage(string petCardId)
        {
            type = "LEGEND_PET_SPAWNED";
            data = new LegendPetSpawnedData { petCardId = petCardId, isSpawned = true };
        }
    }

    [Serializable]
    public class LegendPetSpawnedData
    {
        public string petCardId;
        public bool isSpawned;
    }

    #endregion

    #region ENV_ITEM_SPAWNED - 환경 아이템 선물상자 터치 시

    [Serializable]
    public class EnvItemSpawnedMessage : FlutterOutboundMessage
    {
        public EnvItemSpawnedData data;

        public EnvItemSpawnedMessage(string envId)
        {
            type = "ENV_ITEM_SPAWNED";
            data = new EnvItemSpawnedData { id = envId, isSpawned = true };
        }
    }

    [Serializable]
    public class EnvItemSpawnedData
    {
        public string id;
        public bool isSpawned;
    }

    #endregion

    #region FOOD_USED - 음식 사용 시

    [Serializable]
    public class FoodUsedMessage : FlutterOutboundMessage
    {
        public FoodUsedData data;

        public FoodUsedMessage(string foodId, int usedQuantity)
        {
            type = "FOOD_USED";
            data = new FoodUsedData { id = foodId, usedQuantity = usedQuantity };
        }
    }

    [Serializable]
    public class FoodUsedData
    {
        public string id;
        public int usedQuantity;
    }

    #endregion

    #region COIN_EARNED - 코인 획득 시

    [Serializable]
    public class CoinEarnedMessage : FlutterOutboundMessage
    {
        public CoinEarnedData data;

        public CoinEarnedMessage(int amount, int totalCoins)
        {
            type = "COIN_EARNED";
            data = new CoinEarnedData { amount = amount, totalCoins = totalCoins };
        }
    }

    [Serializable]
    public class CoinEarnedData
    {
        public int amount;      // 이번에 획득한 코인
        public int totalCoins;  // 획득 후 총 코인
    }

    #endregion

    #region SYNC_INTIMACY / GAME_EXIT - 친밀도 동기화

    [Serializable]
    public class SyncIntimacyMessage : FlutterOutboundMessage
    {
        public SyncIntimacyData data;

        public SyncIntimacyMessage(List<PetIntimacyData> pets, bool isGameExit = false)
        {
            type = isGameExit ? "GAME_EXIT" : "SYNC_INTIMACY";
            data = new SyncIntimacyData { pets = pets };
        }

        public override string ToJson()
        {
            // List를 포함하므로 수동 직렬화
            var petsJson = new List<string>();
            if (data?.pets != null)
            {
                foreach (var pet in data.pets)
                {
                    petsJson.Add($"{{\"petCardId\":\"{pet.petCardId}\",\"petIntimacy\":{pet.petIntimacy}}}");
                }
            }
            return $"{{\"type\":\"{type}\",\"data\":{{\"pets\":[{string.Join(",", petsJson)}]}}}}";
        }
    }

    [Serializable]
    public class SyncIntimacyData
    {
        public List<PetIntimacyData> pets;
    }

    [Serializable]
    public class PetIntimacyData
    {
        public string petCardId;
        public int petIntimacy;
    }

    #endregion
}
