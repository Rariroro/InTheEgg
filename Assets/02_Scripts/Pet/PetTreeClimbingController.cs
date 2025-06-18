using System.Collections;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 펫의 나무 타기 행동을 전담하는 컨트롤러
/// </summary>
public class PetTreeClimbingController : MonoBehaviour
{
    private PetController petController;
    
    // 나무 탐지 설정
    private float treeDetectionRadius = 50f;
    private float treeClimbChance = 0.9f;
    private bool isSearchingForTree = false;
    [SerializeField] private LayerMask treeLayerMask = -1;
    private float lastTreeSearchTime = 0f;
    private float treeSearchCooldown = 2f;
    
    private Coroutine climbTreeCoroutine = null;

    public void Init(PetController controller)
    {
        petController = controller;
    }

    public void CheckForTreeClimbing()
    {
        // Tree habitat이 아니면 즉시 리턴
        if (petController.habitat != PetAIProperties.Habitat.Tree) return;

        // 이미 상태가 정해진 경우 즉시 리턴
        if (petController.isClimbingTree || petController.isInWater || isSearchingForTree) return;

        // 쿨다운 체크를 먼저 (불필요한 Random.value 호출 방지)
        if (Time.time - lastTreeSearchTime < treeSearchCooldown) return;

        // 확률 체크
        if (Random.value < 0.1f)
        {
            lastTreeSearchTime = Time.time;
            StartCoroutine(SearchAndClimbTree());
        }
    }

    public void ForceCancelClimbing()
    {
        // 1. 진행 중인 모든 행동 코루틴을 중단합니다.
        if (climbTreeCoroutine != null)
        {
            StopCoroutine(climbTreeCoroutine);
            climbTreeCoroutine = null;
        }
        StopAllCoroutines();

        // 2. 나무 타기와 관련된 모든 상태 변수를 확실하게 초기화합니다.
        isSearchingForTree = false;
        if (petController != null)
        {
            // 나무 점유 해제
            if (petController.currentTree != null)
            {
                TreeManager.Instance.ReleaseTree(petController.currentTree);
            }
            
            petController.isClimbingTree = false;
            petController.currentTree = null;
        }

        // 3. NavMeshAgent를 즉시 활성화하고 현재 위치에 맞게 동기화합니다.
        if (petController != null && petController.agent != null && !petController.agent.enabled)
        {
            petController.agent.enabled = true;
            petController.agent.Warp(transform.position); 
        }

        // 4. 애니메이션 컨트롤러가 있다면, 모든 특별/연속 애니메이션을 중단하고 기본 상태로 돌립니다.
        var animController = GetComponent<PetAnimationController>();
        if (animController != null)
        {
            animController.ForceStopAllAnimations();
        }
    }

    public void StopTreeClimbing()
    {
        StopAllCoroutines();
        
        if (climbTreeCoroutine != null)
        {
            StopCoroutine(climbTreeCoroutine);
            climbTreeCoroutine = null;
        }

        isSearchingForTree = false;
        petController.isClimbingTree = false;

        var anim = GetComponent<PetAnimationController>();
        if (anim != null)
        {
            anim.StopContinuousAnimation();
            anim.ForceStopAllAnimations();
        }
        
        if (petController.animator != null)
        {
            petController.animator.SetInteger("animation", 0);
            petController.animator.speed = 1.0f;
        }
    }

    private IEnumerator SearchAndClimbTree()
    {
        isSearchingForTree = true;

        Transform nearestTree = TreeManager.Instance.FindNearestAvailableTree(transform.position, treeDetectionRadius);

        if (nearestTree != null && Random.value < treeClimbChance)
        {
            if (TreeManager.Instance.OccupyTree(nearestTree, petController))
            {
                climbTreeCoroutine = StartCoroutine(ClimbTree(nearestTree));
                yield return climbTreeCoroutine;
                climbTreeCoroutine = null;
            }
            else
            {
                Debug.Log($"{petController.petName}: 선택한 나무가 이미 점유되어 있습니다.");
            }
        }

        isSearchingForTree = false;
    }

    private IEnumerator ClimbTree(Transform tree)
    {
        climbTreeCoroutine = StartCoroutine(ClimbTreeInternal(tree));
        yield return climbTreeCoroutine;
        climbTreeCoroutine = null;
    }

    private IEnumerator ClimbTreeInternal(Transform tree)
    {
        if (petController.isHolding)
        {
            yield break;
        }

        petController.isClimbingTree = true;
        petController.currentTree = tree;

        // 1단계: NavMeshAgent를 활성화한 상태로 나무 근처까지 이동
        Vector3 treeBaseTarget = tree.position;
        NavMeshHit hit;
        if (NavMesh.SamplePosition(treeBaseTarget, out hit, 5f, NavMesh.AllAreas))
        {
            treeBaseTarget = hit.position;
        }

        if (petController.agent != null && petController.agent.enabled)
        {
            petController.agent.SetDestination(treeBaseTarget);
            petController.agent.isStopped = false;
            
            var anim = petController.GetComponent<PetAnimationController>();
            anim?.SetContinuousAnimation(1); // 걷기 애니메이션

            while (petController.agent.pathPending || 
                   petController.agent.remainingDistance > 0.5f)
            {
                if (petController.isHolding)
                {
                    petController.isClimbingTree = false;
                    yield break;
                }
                
                if (!petController.agent.enabled)
                {
                    petController.isClimbingTree = false;
                    yield break;
                }
                
                yield return null;
            }
            
            petController.agent.isStopped = true;
            yield return new WaitForSeconds(0.1f);
        }

        // 2단계: NavMeshAgent 비활성화 후 나무 올라가기
        if (petController.agent != null)
        {
            petController.agent.enabled = false;
        }
        
        // 올라가기 애니메이션
        var animController = petController.GetComponent<PetAnimationController>();
        animController?.SetContinuousAnimation(3);

        // 나무의 실제 높이 계산
        float treeHeight = CalculateTreeHeight(tree);
        float climbTargetHeight = CalculateClimbPosition(tree, treeHeight);

        // 계산된 높이로 올라가기
        Vector3 climbTarget = transform.position + Vector3.up * climbTargetHeight;
        float elapsed = 0f;
        float moveTime = 3f;
        Vector3 startPos = transform.position;

        while (elapsed < moveTime)
        {
            if (petController.isHolding)
            {
                if (petController.agent != null)
                {
                    petController.agent.enabled = true;
                }
                petController.isClimbingTree = false;
                yield break;
            }
            
            elapsed += Time.deltaTime;
            float t = elapsed / moveTime;
            transform.position = Vector3.Lerp(startPos, climbTarget, t);
            yield return null;
        }

        // 나무 위에서 쉬기
        animController?.SetContinuousAnimation(5);

        // 5-10초 동안 나무 위에서 쉬기
        yield return new WaitForSeconds(Random.Range(5f, 10f));

        // 내려오기
        yield return StartCoroutine(ClimbDownTree());
    }

    private float CalculateTreeHeight(Transform tree)
    {
        float height = 5f;

        Collider treeCollider = tree.GetComponent<Collider>();
        if (treeCollider != null)
        {
            height = treeCollider.bounds.size.y;
        }
        else
        {
            MeshRenderer meshRenderer = tree.GetComponentInChildren<MeshRenderer>();
            if (meshRenderer != null)
            {
                height = meshRenderer.bounds.size.y;
            }
            else
            {
                Bounds combinedBounds = CalculateCombinedBounds(tree);
                if (combinedBounds.size != Vector3.zero)
                {
                    height = combinedBounds.size.y;
                }
            }
        }

        return height;
    }

    private float CalculateClimbPosition(Transform tree, float treeHeight)
    {
        float climbRatio = Random.Range(2.3f, 2.5f); // 70-85%

        float baseY = 0f;
        if (tree.GetComponent<Collider>() != null)
        {
            baseY = tree.GetComponent<Collider>().bounds.min.y - tree.position.y;
        }

        return baseY + (treeHeight * climbRatio);
    }

    private Bounds CalculateCombinedBounds(Transform parent)
    {
        Renderer[] renderers = parent.GetComponentsInChildren<Renderer>();

        if (renderers.Length == 0)
            return new Bounds(parent.position, Vector3.zero);

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }

        return bounds;
    }

    private IEnumerator ClimbDownTree()
    {
        if (petController.currentTree == null)
        {
            Debug.LogWarning($"{petController.petName}: currentTree가 null입니다. 내려오기 중단.");
            ForceCancelClimbing();
            yield break;
        }

        var anim = petController.GetComponent<PetAnimationController>();

        // 내려가기 애니메이션
        anim?.SetContinuousAnimation(1);

        // 바닥으로 내려오기
        Vector3 groundPos = petController.currentTree.position;
        NavMeshHit hit;
        if (NavMesh.SamplePosition(groundPos, out hit, 5f, NavMesh.AllAreas))
        {
            groundPos = hit.position;
        }

        float moveTime = 2f;
        float elapsed = 0f;
        Vector3 startPos = transform.position;

        while (elapsed < moveTime)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / moveTime;
            transform.position = Vector3.Lerp(startPos, groundPos, t);
            yield return null;
        }

        // NavMeshAgent 재활성화 전에 나무 점유 해제
        if (petController.currentTree != null)
        {
            TreeManager.Instance.ReleaseTree(petController.currentTree);
        }

        // NavMeshAgent 재활성화
        if (petController.agent != null)
        {
            petController.agent.enabled = true;
            petController.agent.Warp(transform.position);
        }

        petController.isClimbingTree = false;
        petController.currentTree = null;

        // 일반 애니메이션으로 복귀
        anim?.StopContinuousAnimation();
    }
}