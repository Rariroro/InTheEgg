// 음식 탐지, 접근, 먹기 동작 및 배고픔 수치 조절 전담
using System.Collections;
using UnityEngine;

public class PetFeedingController : MonoBehaviour
{
    private PetController petController;
    private GameObject targetFood;
    private float detectionRadius = 100f;
    private float eatingDistance = 4f;
    private bool isEating = false;

    public void Init(PetController controller)
    {
        petController = controller;
    }

    public void UpdateFeeding()
    {
        // 시간에 따라 배고픔 증가
        petController.hunger += Time.deltaTime * 0.1f;
        petController.hunger = Mathf.Clamp(petController.hunger, 0f, 100f);

        // 배고픔이 일정 수준 이상이고 아직 음식 목표가 없으며 먹고 있지 않은 경우 음식 탐지
        if (petController.hunger > 50 && targetFood == null && !isEating)
        {
            DetectFood();
        }

        if (targetFood != null && !isEating)
        {
            petController.agent.SetDestination(targetFood.transform.position);
            if (Vector3.Distance(petController.transform.position, targetFood.transform.position) <= eatingDistance)
            {
                StartCoroutine(EatFood());
            }
        }
    }

    private void DetectFood()
    {
        GameObject[] foods = GameObject.FindGameObjectsWithTag("Food");
        GameObject nearestFood = null;
        float nearestDistance = float.MaxValue;
        foreach (GameObject food in foods)
        {
            float distance = Vector3.Distance(petController.transform.position, food.transform.position);
            if (distance < nearestDistance && distance <= detectionRadius)
            {
                nearestFood = food;
                nearestDistance = distance;
            }
        }
        if (nearestFood != null)
        {
            targetFood = nearestFood;
            petController.agent.SetDestination(targetFood.transform.position);
            Debug.Log($"{petController.petName}이(가) 음식을 발견하고 접근 중입니다.");
        }
    }

    private IEnumerator EatFood()
    {
        isEating = true;
        petController.StopMovement();
        // 배고픔 감소
        petController.hunger -= 30;
        petController.hunger = Mathf.Clamp(petController.hunger, 0f, 100f);
        Debug.Log($"{petController.petName}이(가) 음식을 먹기 시작했습니다. 현재 배고픔: {petController.hunger}");

        yield return StartCoroutine(petController.GetComponent<PetAnimationController>().PlaySpecialAnimation(4));

        if (targetFood != null)
        {
            Destroy(targetFood);
            targetFood = null;
            Debug.Log($"{petController.petName}이(가) 음식을 다 먹었습니다.");
        }
        petController.GetComponent<PetMovementController>().SetRandomDestination();
        isEating = false;
    }
}