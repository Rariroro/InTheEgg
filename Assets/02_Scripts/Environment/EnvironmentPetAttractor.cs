using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI; // NavMeshAgent 사용을 위해 추가

public class EnvironmentPetAttractor : MonoBehaviour
{
    [Header("환경별 펫 유인 설정")]
    [SerializeField] private float attractionRadius = 100f; // 펫을 끌어들이는 반경
    [SerializeField] private float gatherRadius = 15f; // 환경 주변 모임 반경
    [SerializeField] private float celebrationDuration = 5f; // 축하 애니메이션 지속 시간

    private static EnvironmentPetAttractor instance;
    public static EnvironmentPetAttractor Instance
    {
        get
        {
            if (instance == null)
            {
                GameObject go = new GameObject("EnvironmentPetAttractor");
                instance = go.AddComponent<EnvironmentPetAttractor>();
            }
            return instance;
        }
    }

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (instance != this)
        {
            Destroy(gameObject);
        }
    }

    // 환경이 스폰될 때 호출
    public void OnEnvironmentSpawned(string environmentId, Vector3 position)
    {
        List<PetController> attractedPets = GetPetsForEnvironment(environmentId, position);

        if (attractedPets.Count > 0)
        {
            StartCoroutine(AttractPetsToEnvironment(attractedPets, position, environmentId));
        }
    }

    // 환경별로 끌어들일 펫 찾기
    private List<PetController> GetPetsForEnvironment(string environmentId, Vector3 position)
    {
        List<PetController> matchingPets = new List<PetController>();
        PetController[] allPets = FindObjectsOfType<PetController>();

        foreach (PetController pet in allPets)
        {
            if (pet.isGathering || pet.isInteracting)
                continue;

            bool shouldAttract = false;

            switch (environmentId)
            {
                case "env_fence":
                    shouldAttract = pet.habitat == PetAIProperties.Habitat.Fence;
                    break;
                case "env_berryfield":
                    shouldAttract = (pet.diet & PetAIProperties.DietaryFlags.FruitsAndVegetables) != 0;
                    break;
                case "env_sunflower":
                    shouldAttract = pet.personality == PetAIProperties.Personality.Playful;
                    break;
                case "env_honeypot":
                    shouldAttract = (pet.diet & PetAIProperties.DietaryFlags.Honey) != 0;
                    break;
                case "env_forest":
                    shouldAttract = pet.habitat == PetAIProperties.Habitat.Forest || pet.habitat == PetAIProperties.Habitat.Tree;
                    break;
                case "env_pond":
                    shouldAttract = pet.habitat == PetAIProperties.Habitat.Water;
                    break;
                case "env_ricefield":
                    shouldAttract = (pet.diet & PetAIProperties.DietaryFlags.SeedsAndGrains) != 0;
                    break;
                case "env_cucumber":
                    shouldAttract = (pet.diet & PetAIProperties.DietaryFlags.FruitsAndVegetables) != 0;
                    break;
                case "env_watermelon":
                    shouldAttract = (pet.diet & PetAIProperties.DietaryFlags.FruitsAndVegetables) != 0;
                    break;
                case "env_foodstore":
                    shouldAttract = (pet.diet & (PetAIProperties.DietaryFlags.Meat | PetAIProperties.DietaryFlags.Fish)) != 0;
                    break;
                case "env_flowers":
                    shouldAttract = pet.personality == PetAIProperties.Personality.Shy || pet.personality == PetAIProperties.Personality.Playful;
                    break;
                case "env_cornfield":
                    shouldAttract = (pet.diet & PetAIProperties.DietaryFlags.SeedsAndGrains) != 0;
                    break;
                case "env_orchard":
                    shouldAttract = (pet.diet & PetAIProperties.DietaryFlags.FruitsAndVegetables) != 0;
                    break;
            }

            if (shouldAttract && Vector3.Distance(pet.transform.position, position) <= attractionRadius)
            {
                matchingPets.Add(pet);
            }
        }
        return matchingPets;
    }

    // 펫들을 환경으로 끌어들이고 축하 애니메이션 실행
    private IEnumerator AttractPetsToEnvironment(List<PetController> pets, Vector3 centerPosition, string environmentId)
    {
        Debug.Log($"{environmentId} 환경에 {pets.Count}마리의 펫이 반응합니다!");

        foreach (PetController pet in pets)
        {
            Vector2 randomCircle = Random.insideUnitCircle * gatherRadius;
            Vector3 targetPosition = centerPosition + new Vector3(randomCircle.x, 0, randomCircle.y);
            
            // ▼▼▼ [수정된 부분] centerPosition을 전달해줍니다. ▼▼▼
            StartCoroutine(MovePetToEnvironment(pet, targetPosition, centerPosition, environmentId));
        }

        yield return null;
    }

    // ▼▼▼ [수정된 부분] centerPosition 파라미터를 받도록 변경합니다. ▼▼▼
    private IEnumerator MovePetToEnvironment(PetController pet, Vector3 targetPosition, Vector3 centerPosition, string environmentId)
    {
        if (pet.isClimbingTree)
        {
            Debug.Log($"{pet.petName}이(가) 나무에서 내려옵니다.");
            var movementController = pet.GetComponent<PetMovementController>();
            if (movementController != null)
            {
                movementController.ForceCancelClimbing();
            }
            float waitTime = 0f;
            while (waitTime < 2f) 
            {
                if (pet.agent != null && pet.agent.enabled && pet.agent.isOnNavMesh)
                {
                    break;
                }
                yield return null; 
                waitTime += Time.deltaTime;
            }
        }
        
        if (pet == null || pet.agent == null || !pet.agent.enabled || !pet.agent.isOnNavMesh)
        {
            Debug.LogWarning($"{pet?.petName ?? "Unknown"}: NavMeshAgent가 준비되지 않아 환경 이동을 취소합니다.");
            yield break;
        }

        float originalSpeed = pet.baseSpeed;
        float originalAngularSpeed = pet.baseAngularSpeed;
        float originalAcceleration = pet.baseAcceleration;

        pet.isGathering = true;
        pet.agent.speed = originalSpeed * 3f;
        pet.agent.angularSpeed = originalAngularSpeed * 2f;
        pet.agent.acceleration = originalAcceleration * 3f;
        pet.agent.isStopped = false;

        if (pet.animator != null)
        {
            pet.animator.SetInteger("animation", 2);
        }

        pet.agent.SetDestination(targetPosition);

        while (pet != null && pet.agent.enabled && (pet.agent.pathPending || pet.agent.remainingDistance > 2f))
        {
            yield return new WaitForSeconds(0.1f);
        }
        
        if (pet == null || !pet.agent.enabled) yield break;

        pet.agent.speed = originalSpeed;
        pet.agent.angularSpeed = originalAngularSpeed;
        pet.agent.acceleration = originalAcceleration;
        pet.agent.isStopped = true;

        // ▼▼▼ [수정된 부분] 전달받은 centerPosition을 사용합니다. ▼▼▼
        Vector3 lookDirection = (centerPosition - pet.transform.position).normalized;
        lookDirection.y = 0;
        if (lookDirection != Vector3.zero)
        {
            pet.transform.rotation = Quaternion.LookRotation(lookDirection);
        }

        yield return StartCoroutine(CelebratePet(pet, environmentId));
        
        if (pet != null)
        {
            pet.isGathering = false;
            if(pet.agent.enabled) pet.agent.isStopped = false;
            pet.SetRandomDestination();
        }
    }

    private IEnumerator CelebratePet(PetController pet, string environmentId)
    {
        pet.ShowEmotion(GetEmotionForEnvironment(environmentId), celebrationDuration);
        float celebrationTime = 0f;

        while (celebrationTime < celebrationDuration)
        {
            if (pet == null) yield break;

            if (pet.animator != null)
            {
                int randomAnimation = Random.Range(0, 3);
                switch (randomAnimation)
                {
                    case 0:
                        pet.animator.SetInteger("animation", 3);
                        yield return new WaitForSeconds(1f);
                        break;
                    case 1:
                        pet.animator.SetInteger("animation", 2);
                        yield return new WaitForSeconds(0.5f);
                        if(pet.animator != null) pet.animator.SetInteger("animation", 0);
                        yield return new WaitForSeconds(0.3f);
                        break;
                    case 2:
                        float rotateTime = 0f;
                        while (rotateTime < 1f)
                        {
                            if (pet == null) yield break;
                            pet.transform.Rotate(0, 360 * Time.deltaTime, 0);
                            rotateTime += Time.deltaTime;
                            yield return null;
                        }
                        break;
                }
            }
            celebrationTime += 1.5f;
            yield return new WaitForSeconds(0.5f);
        }
        
        if (pet == null) yield break;
        pet.HideEmotion();

        if (pet.animator != null)
        {
            pet.animator.SetInteger("animation", 0);
        }
    }

    private EmotionType GetEmotionForEnvironment(string environmentId)
    {
        switch (environmentId)
        {
            case "env_foodstore": case "env_berryfield": case "env_honeypot":
            case "env_ricefield": case "env_cucumber": case "env_watermelon":
            case "env_cornfield": case "env_orchard":
                return EmotionType.Happy;
            case "env_sunflower": case "env_flowers":
                return EmotionType.Love;
            case "env_pond":
                return EmotionType.Happy; 
            default:
                return EmotionType.Happy;
        }
    }
}