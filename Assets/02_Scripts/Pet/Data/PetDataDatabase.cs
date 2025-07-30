using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 모든 펫의 프로필 데이터를 저장하는 ScriptableObject 데이터베이스
/// </summary>
[CreateAssetMenu(fileName = "PetDataDatabase", menuName = "Pet/Pet Data Database")]
public class PetDataDatabase : ScriptableObject
{
    [SerializeField]
    private List<PetData> petDataList = new List<PetData>();
    
    private Dictionary<PetType, PetData> petDataDictionary;
    
    /// <summary>
    /// 데이터베이스 초기화 및 딕셔너리 생성
    /// </summary>
    private void InitializeDictionary()
    {
        if (petDataDictionary == null)
        {
            petDataDictionary = new Dictionary<PetType, PetData>();
            foreach (var data in petDataList)
            {
                if (!petDataDictionary.ContainsKey(data.petType))
                {
                    petDataDictionary.Add(data.petType, data);
                }
            }
        }
    }
    
    /// <summary>
    /// 특정 펫 타입의 데이터 가져오기
    /// </summary>
    public PetData GetPetData(PetType petType)
    {
        InitializeDictionary();
        
        if (petDataDictionary.TryGetValue(petType, out PetData data))
        {
            return data;
        }
        
        Debug.LogWarning($"PetData for {petType} not found in database!");
        return null;
    }
    
    /// <summary>
    /// 에디터에서 데이터베이스 초기화
    /// </summary>
    public void InitializeDatabase()
    {
        petDataList.Clear();
        
        // 제공된 데이터에 따라 모든 펫 데이터 추가
        petDataList.Add(new PetData(PetType.Turtle, PetTraits.Personality.Lazy, PetTraits.DietaryFlags.FruitsAndVegetables | PetTraits.DietaryFlags.Fish, PetTraits.Habitat.Water));
        petDataList.Add(new PetData(PetType.Flamingo, PetTraits.Personality.Shy, PetTraits.DietaryFlags.SeedsAndGrains | PetTraits.DietaryFlags.Fish, PetTraits.Habitat.Water));
        petDataList.Add(new PetData(PetType.Chick, PetTraits.Personality.Playful, PetTraits.DietaryFlags.SeedsAndGrains, PetTraits.Habitat.Fence));
        petDataList.Add(new PetData(PetType.Chicken, PetTraits.Personality.Shy, PetTraits.DietaryFlags.SeedsAndGrains, PetTraits.Habitat.Fence));
        petDataList.Add(new PetData(PetType.Pig, PetTraits.Personality.Playful, PetTraits.DietaryFlags.FruitsAndVegetables | PetTraits.DietaryFlags.SeedsAndGrains, PetTraits.Habitat.Fence));
        petDataList.Add(new PetData(PetType.Cow, PetTraits.Personality.Lazy, PetTraits.DietaryFlags.Grass, PetTraits.Habitat.Fence));
        petDataList.Add(new PetData(PetType.Cat, PetTraits.Personality.Playful, PetTraits.DietaryFlags.Meat | PetTraits.DietaryFlags.Fish, PetTraits.Habitat.Field));
        petDataList.Add(new PetData(PetType.Dog, PetTraits.Personality.Brave, PetTraits.DietaryFlags.Meat, PetTraits.Habitat.Field));
        petDataList.Add(new PetData(PetType.Duck, PetTraits.Personality.Playful, PetTraits.DietaryFlags.SeedsAndGrains | PetTraits.DietaryFlags.Grass, PetTraits.Habitat.Water));
        petDataList.Add(new PetData(PetType.Elk, PetTraits.Personality.Brave, PetTraits.DietaryFlags.Grass, PetTraits.Habitat.Forest));
        petDataList.Add(new PetData(PetType.Boar, PetTraits.Personality.Brave, PetTraits.DietaryFlags.FruitsAndVegetables | PetTraits.DietaryFlags.Meat, PetTraits.Habitat.Forest));
        petDataList.Add(new PetData(PetType.Wolf, PetTraits.Personality.Brave, PetTraits.DietaryFlags.Meat, PetTraits.Habitat.Forest));
        petDataList.Add(new PetData(PetType.Rabbit, PetTraits.Personality.Shy, PetTraits.DietaryFlags.FruitsAndVegetables | PetTraits.DietaryFlags.Grass, PetTraits.Habitat.Field));
        petDataList.Add(new PetData(PetType.Skunk, PetTraits.Personality.Shy, PetTraits.DietaryFlags.FruitsAndVegetables | PetTraits.DietaryFlags.Meat, PetTraits.Habitat.Forest));
        petDataList.Add(new PetData(PetType.Deer, PetTraits.Personality.Shy, PetTraits.DietaryFlags.Grass | PetTraits.DietaryFlags.FruitsAndVegetables, PetTraits.Habitat.Forest));
        petDataList.Add(new PetData(PetType.Raccoon, PetTraits.Personality.Playful, PetTraits.DietaryFlags.FruitsAndVegetables | PetTraits.DietaryFlags.Fish | PetTraits.DietaryFlags.Meat, PetTraits.Habitat.Forest));
        petDataList.Add(new PetData(PetType.Owl, PetTraits.Personality.Shy, PetTraits.DietaryFlags.Meat, PetTraits.Habitat.Tree));
        petDataList.Add(new PetData(PetType.Fox, PetTraits.Personality.Playful, PetTraits.DietaryFlags.Meat | PetTraits.DietaryFlags.FruitsAndVegetables, PetTraits.Habitat.Forest));
        petDataList.Add(new PetData(PetType.Squirrel, PetTraits.Personality.Playful, PetTraits.DietaryFlags.SeedsAndGrains | PetTraits.DietaryFlags.FruitsAndVegetables, PetTraits.Habitat.Tree));
        petDataList.Add(new PetData(PetType.Mole, PetTraits.Personality.Shy, PetTraits.DietaryFlags.Meat, PetTraits.Habitat.Field));
        petDataList.Add(new PetData(PetType.Porcupine, PetTraits.Personality.Shy, PetTraits.DietaryFlags.FruitsAndVegetables, PetTraits.Habitat.Forest));
        petDataList.Add(new PetData(PetType.Camel, PetTraits.Personality.Lazy, PetTraits.DietaryFlags.Grass, PetTraits.Habitat.Field));
        petDataList.Add(new PetData(PetType.Goat, PetTraits.Personality.Brave, PetTraits.DietaryFlags.Grass, PetTraits.Habitat.Fence));
        petDataList.Add(new PetData(PetType.Anteater, PetTraits.Personality.Lazy, PetTraits.DietaryFlags.Meat, PetTraits.Habitat.Forest));
        petDataList.Add(new PetData(PetType.Iguana, PetTraits.Personality.Lazy, PetTraits.DietaryFlags.FruitsAndVegetables, PetTraits.Habitat.Tree));
        petDataList.Add(new PetData(PetType.Pangolin, PetTraits.Personality.Shy, PetTraits.DietaryFlags.Meat, PetTraits.Habitat.Forest));
        petDataList.Add(new PetData(PetType.Alpaca, PetTraits.Personality.Shy, PetTraits.DietaryFlags.Grass, PetTraits.Habitat.Field));
        petDataList.Add(new PetData(PetType.Kangaroo, PetTraits.Personality.Playful, PetTraits.DietaryFlags.Grass, PetTraits.Habitat.Field));
        petDataList.Add(new PetData(PetType.Meerkat, PetTraits.Personality.Playful, PetTraits.DietaryFlags.Meat, PetTraits.Habitat.Field));
        petDataList.Add(new PetData(PetType.Mule, PetTraits.Personality.Lazy, PetTraits.DietaryFlags.Grass, PetTraits.Habitat.Fence));
        petDataList.Add(new PetData(PetType.Bison, PetTraits.Personality.Brave, PetTraits.DietaryFlags.Grass, PetTraits.Habitat.Field));
        petDataList.Add(new PetData(PetType.Ostrich, PetTraits.Personality.Brave, PetTraits.DietaryFlags.SeedsAndGrains | PetTraits.DietaryFlags.Grass, PetTraits.Habitat.Field));
        petDataList.Add(new PetData(PetType.Horse, PetTraits.Personality.Brave, PetTraits.DietaryFlags.Grass, PetTraits.Habitat.Field));
        petDataList.Add(new PetData(PetType.Zebra, PetTraits.Personality.Shy, PetTraits.DietaryFlags.Grass, PetTraits.Habitat.Field));
        petDataList.Add(new PetData(PetType.Bull, PetTraits.Personality.Brave, PetTraits.DietaryFlags.Grass, PetTraits.Habitat.Fence));
        petDataList.Add(new PetData(PetType.Lioness, PetTraits.Personality.Brave, PetTraits.DietaryFlags.Meat, PetTraits.Habitat.Field));
        petDataList.Add(new PetData(PetType.Giraffe, PetTraits.Personality.Shy, PetTraits.DietaryFlags.Grass | PetTraits.DietaryFlags.FruitsAndVegetables, PetTraits.Habitat.Field));
        petDataList.Add(new PetData(PetType.Lion, PetTraits.Personality.Brave, PetTraits.DietaryFlags.Meat, PetTraits.Habitat.Field));
        petDataList.Add(new PetData(PetType.Rhino, PetTraits.Personality.Brave, PetTraits.DietaryFlags.Grass, PetTraits.Habitat.Field));
        petDataList.Add(new PetData(PetType.Elephant, PetTraits.Personality.Brave, PetTraits.DietaryFlags.FruitsAndVegetables | PetTraits.DietaryFlags.Grass, PetTraits.Habitat.Field));
        petDataList.Add(new PetData(PetType.Sheep, PetTraits.Personality.Shy, PetTraits.DietaryFlags.Grass, PetTraits.Habitat.Fence));
        petDataList.Add(new PetData(PetType.Gorilla, PetTraits.Personality.Brave, PetTraits.DietaryFlags.FruitsAndVegetables, PetTraits.Habitat.Forest));
        petDataList.Add(new PetData(PetType.Possum, PetTraits.Personality.Shy, PetTraits.DietaryFlags.FruitsAndVegetables | PetTraits.DietaryFlags.Meat, PetTraits.Habitat.Tree));
        petDataList.Add(new PetData(PetType.Leopard, PetTraits.Personality.Brave, PetTraits.DietaryFlags.Meat, PetTraits.Habitat.Forest));
        petDataList.Add(new PetData(PetType.Bear, PetTraits.Personality.Brave, PetTraits.DietaryFlags.Meat | PetTraits.DietaryFlags.Fish | PetTraits.DietaryFlags.FruitsAndVegetables | PetTraits.DietaryFlags.Honey, PetTraits.Habitat.Forest));
        petDataList.Add(new PetData(PetType.Peacock, PetTraits.Personality.Shy, PetTraits.DietaryFlags.SeedsAndGrains | PetTraits.DietaryFlags.FruitsAndVegetables, PetTraits.Habitat.Field));
        petDataList.Add(new PetData(PetType.Tiger, PetTraits.Personality.Brave, PetTraits.DietaryFlags.Meat, PetTraits.Habitat.Forest));
        petDataList.Add(new PetData(PetType.Panda, PetTraits.Personality.Lazy, PetTraits.DietaryFlags.FruitsAndVegetables | PetTraits.DietaryFlags.Grass, PetTraits.Habitat.Forest));
        petDataList.Add(new PetData(PetType.Monkey, PetTraits.Personality.Playful, PetTraits.DietaryFlags.FruitsAndVegetables, PetTraits.Habitat.Tree));
        petDataList.Add(new PetData(PetType.Sloth, PetTraits.Personality.Lazy, PetTraits.DietaryFlags.FruitsAndVegetables, PetTraits.Habitat.Tree));
        petDataList.Add(new PetData(PetType.RedPanda, PetTraits.Personality.Playful, PetTraits.DietaryFlags.FruitsAndVegetables, PetTraits.Habitat.Tree));
        petDataList.Add(new PetData(PetType.Koala, PetTraits.Personality.Lazy, PetTraits.DietaryFlags.Grass, PetTraits.Habitat.Tree));
        petDataList.Add(new PetData(PetType.Malayan, PetTraits.Personality.Brave, PetTraits.DietaryFlags.Meat, PetTraits.Habitat.Forest));
        petDataList.Add(new PetData(PetType.Chameleon, PetTraits.Personality.Shy, PetTraits.DietaryFlags.Meat, PetTraits.Habitat.Tree));
        petDataList.Add(new PetData(PetType.Buffalo, PetTraits.Personality.Brave, PetTraits.DietaryFlags.Grass, PetTraits.Habitat.Field));
        petDataList.Add(new PetData(PetType.Hippo, PetTraits.Personality.Brave, PetTraits.DietaryFlags.Grass, PetTraits.Habitat.Water));
        petDataList.Add(new PetData(PetType.Armadillo, PetTraits.Personality.Shy, PetTraits.DietaryFlags.Meat, PetTraits.Habitat.Field));
        petDataList.Add(new PetData(PetType.Crocodile, PetTraits.Personality.Brave, PetTraits.DietaryFlags.Meat | PetTraits.DietaryFlags.Fish, PetTraits.Habitat.Water));
        petDataList.Add(new PetData(PetType.Platypus, PetTraits.Personality.Shy, PetTraits.DietaryFlags.Meat | PetTraits.DietaryFlags.Fish, PetTraits.Habitat.Water));
        petDataList.Add(new PetData(PetType.Otter, PetTraits.Personality.Playful, PetTraits.DietaryFlags.Fish | PetTraits.DietaryFlags.Meat, PetTraits.Habitat.Water));
    }
}