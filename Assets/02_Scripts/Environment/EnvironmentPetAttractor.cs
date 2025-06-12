using System.Collections;
using System.Collections.Generic;
using UnityEngine;

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
            // 이미 다른 환경으로 가고 있거나 상호작용 중인 펫은 제외
            if (pet.isGathering || pet.isInteracting)
                continue;

            bool shouldAttract = false;

            switch (environmentId)
            {
                case "env_fence":
                    shouldAttract = pet.habitat == PetAIProperties.Habitat.Fence;
                    break;

                case "env_berryfield":
                    shouldAttract = pet.dietType == PetAIProperties.DietType.Herbivore ||
                                   pet.dietType == PetAIProperties.DietType.Omnivore;
                    break;

                case "env_sunflower":
                    shouldAttract = pet.personality == PetAIProperties.Personality.Playful;
                    break;

                case "env_honeypot":


                case "env_forest":
                    shouldAttract = pet.habitat == PetAIProperties.Habitat.Forest ||
                    pet.habitat == PetAIProperties.Habitat.Tree;
                    break;

                case "env_pond":
                    shouldAttract = pet.habitat == PetAIProperties.Habitat.Water;
                    break;

                case "env_ricefield":
                    shouldAttract = pet.dietType == PetAIProperties.DietType.Herbivore;
                    break;

                case "env_cucumber":
                    shouldAttract = pet.dietType == PetAIProperties.DietType.Herbivore;
                    break;


                case "env_watermelon":
                    shouldAttract = pet.dietType == PetAIProperties.DietType.Herbivore ||
                                   pet.dietType == PetAIProperties.DietType.Omnivore;
                    break;

                case "env_foodstore":
                    shouldAttract = pet.dietType == PetAIProperties.DietType.Carnivore ||
                                   pet.dietType == PetAIProperties.DietType.Omnivore;
                    break;

                case "env_flowers":
                    shouldAttract = pet.personality == PetAIProperties.Personality.Shy ||
                                   pet.personality == PetAIProperties.Personality.Playful;
                    break;

                case "env_cornfield":
                    shouldAttract = pet.dietType == PetAIProperties.DietType.Herbivore ||
                                   pet.dietType == PetAIProperties.DietType.Omnivore;
                    break;

                case "env_orchard":
                    shouldAttract = pet.dietType == PetAIProperties.DietType.Herbivore ||
                                   pet.dietType == PetAIProperties.DietType.Omnivore;
                    break;
            }

            // 거리 체크 - 너무 멀리 있는 펫은 제외
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

        // 각 펫에게 목적지 설정
        foreach (PetController pet in pets)
        {
            // 랜덤한 위치 (환경 주변)
            Vector2 randomCircle = Random.insideUnitCircle * gatherRadius;
            Vector3 targetPosition = centerPosition + new Vector3(randomCircle.x, 0, randomCircle.y);

            // 펫을 빠르게 이동시키기
            StartCoroutine(MovePetToEnvironment(pet, targetPosition, environmentId));
        }

        yield return null;
    }

    private IEnumerator MovePetToEnvironment(PetController pet, Vector3 targetPosition, string environmentId)
    {
        // 기존 속도 저장
        float originalSpeed = pet.agent.speed;
        float originalAngularSpeed = pet.agent.angularSpeed;
        float originalAcceleration = pet.agent.acceleration;

        // 빠른 이동 설정
        pet.isGathering = true;
        pet.agent.speed = originalSpeed * 3f; // 3배 속도
        pet.agent.angularSpeed = originalAngularSpeed * 2f;
        pet.agent.acceleration = originalAcceleration * 3f;
        pet.agent.isStopped = false;

        // 달리기 애니메이션
        if (pet.animator != null)
        {
            pet.animator.SetInteger("animation", 2); // Run animation
        }

        // 목적지 설정
        pet.agent.SetDestination(targetPosition);

        // 도착할 때까지 대기
        while (pet.agent.pathPending || pet.agent.remainingDistance > 2f)
        {
            yield return new WaitForSeconds(0.1f);

            // 펫이 삭제되었거나 agent가 비활성화된 경우 중단
            if (pet == null || !pet.agent.enabled || !pet.agent.isOnNavMesh)
            {
                yield break;
            }
        }

        // 도착 후 속도 복원
        pet.agent.speed = originalSpeed;
        pet.agent.angularSpeed = originalAngularSpeed;
        pet.agent.acceleration = originalAcceleration;
        pet.agent.isStopped = true;

        // 환경을 바라보도록 회전
        Vector3 lookDirection = (targetPosition - pet.transform.position).normalized;
        lookDirection.y = 0;
        if (lookDirection != Vector3.zero)
        {
            pet.transform.rotation = Quaternion.LookRotation(lookDirection);
        }

        // 기쁨 표현 (점프 및 감정 표현)
        yield return StartCoroutine(CelebratePet(pet, environmentId));

        // 상태 복원
        pet.isGathering = false;
        pet.agent.isStopped = false;

        // 이동 재개
        if (pet != null)
        {
            pet.SetRandomDestination();
        }
    }

    private IEnumerator CelebratePet(PetController pet, string environmentId)
    {
        // 기쁨 감정 표현
        pet.ShowEmotion(GetEmotionForEnvironment(environmentId), celebrationDuration);

        float celebrationTime = 0f;

        while (celebrationTime < celebrationDuration)
        {
            // 점프 애니메이션
            if (pet.animator != null)
            {
                // 랜덤하게 점프 또는 다른 기쁨 표현
                int randomAnimation = Random.Range(0, 3);

                switch (randomAnimation)
                {
                    case 0: // 점프
                        pet.animator.SetInteger("animation", 3);
                        yield return new WaitForSeconds(1f);
                        break;

                    case 1: // 제자리 뛰기
                        pet.animator.SetInteger("animation", 2);
                        yield return new WaitForSeconds(0.5f);
                        pet.animator.SetInteger("animation", 0);
                        yield return new WaitForSeconds(0.3f);
                        break;

                    case 2: // 빙글빙글 돌기
                        float rotateTime = 0f;
                        while (rotateTime < 1f)
                        {
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

        // 감정 숨기기
        pet.HideEmotion();

        // 기본 애니메이션으로 복귀
        if (pet.animator != null)
        {
            pet.animator.SetInteger("animation", 0);
        }
    }

    private EmotionType GetEmotionForEnvironment(string environmentId)
    {
        // 환경별로 적절한 감정 반환
        switch (environmentId)
        {
            case "env_foodstore":
            case "env_berryfield":
            case "env_honeypot":
                return EmotionType.Happy; // 음식 관련은 행복

            case "env_sunflower":
            case "env_flowers":
                return EmotionType.Love; // 꽃 관련은 사랑

            case "env_pond":
            case "env_ricefield":
                return EmotionType.Happy; // 물 관련은 시원함

            default:
                return EmotionType.Happy; // 기본은 행복
        }
    }
}