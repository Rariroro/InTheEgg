#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

// 상호작용 추가를 위한 편집기 확장
public class PetInteractionManagerEditor : Editor
{
    [MenuItem("Tools/Pet System/Add WalkTogether Interaction")]
    private static void AddWalkTogetherInteraction()
    {
        AddInteractionComponent<WalkTogetherInteraction>("WalkTogether");
    }
    
    [MenuItem("Tools/Pet System/Add Fight Interaction")]
    private static void AddFightInteraction()
    {
        AddInteractionComponent<FightInteraction>("Fight");
    }
    
    [MenuItem("Tools/Pet System/Add RestTogether Interaction")]
    private static void AddRestTogetherInteraction()
    {
        AddInteractionComponent<RestTogetherInteraction>("RestTogether");
    }
    
    [MenuItem("Tools/Pet System/Add Race Interaction")]
    private static void AddRaceInteraction()
    {
        AddInteractionComponent<RaceInteraction>("Race");
    }
    
    [MenuItem("Tools/Pet System/Add ChaseAndRun Interaction")]
    private static void AddChaseAndRunInteraction()
    {
        AddInteractionComponent<ChaseAndRunInteraction>("ChaseAndRun");
    }
    
    [MenuItem("Tools/Pet System/Add SleepTogether Interaction")]
    private static void AddSleepTogetherInteraction()
    {
        AddInteractionComponent<SleepTogetherInteraction>("SleepTogether");
    }
    
    [MenuItem("Tools/Pet System/Add RideAndWalk Interaction")]
    private static void AddRideAndWalkInteraction()
    {
        AddInteractionComponent<RideAndWalkInteraction>("RideAndWalk");
    }
    
    [MenuItem("Tools/Pet System/Add SlothKoalaRace Interaction")]
    private static void AddSlothKoalaRaceInteraction()
    {
        AddInteractionComponent<SlothKoalaRaceInteraction>("SlothKoalaRace");
    }
    
    [MenuItem("Tools/Pet System/Add All Interactions")]
    private static void AddAllInteractions()
    {
        AddWalkTogetherInteraction();
        AddFightInteraction();
        AddRestTogetherInteraction();
        AddRaceInteraction();
        AddChaseAndRunInteraction();
        AddSleepTogetherInteraction();
        AddRideAndWalkInteraction();
        AddSlothKoalaRaceInteraction();
        
        Debug.Log("모든 상호작용이 추가되었습니다.");
    }
    
    // 재사용 가능한 컴포넌트 추가 메서드
    private static void AddInteractionComponent<T>(string interactionName) where T : BasePetInteraction
    {
        // PetInteractionManager를 찾아 필요한 상호작용 컴포넌트 추가
        PetInteractionManager manager = Object.FindObjectOfType<PetInteractionManager>();
        if (manager != null)
        {
            // 이미 존재하는지 확인
            if (manager.GetComponent<T>() == null)
            {
                manager.gameObject.AddComponent<T>();
                Debug.Log($"{interactionName} 상호작용이 추가되었습니다.");
            }
            else
            {
                Debug.Log($"{interactionName} 상호작용이 이미 존재합니다.");
            }
        }
        else
        {
            Debug.LogError("PetInteractionManager를 찾을 수 없습니다.");
        }
    }
}
#endif