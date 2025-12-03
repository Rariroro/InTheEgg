// StuckQuill.cs
// 고슴도치 가시 방어 상호작용에서 사용하는 박힌 가시 컴포넌트
using UnityEngine;

/// <summary>
/// 펫 몸에 박힌 가시를 표현하는 컴포넌트
/// 일정 시간 후 자동으로 사라짐
/// </summary>
public class StuckQuill : MonoBehaviour
{
    // 캐싱된 메시와 머티리얼
    private static Mesh cachedMesh;
    private static Material cachedMaterial;
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    private float duration;
    private float timer;
    private bool isInitialized = false;

    /// <summary>
    /// 가시 초기화
    /// </summary>
    /// <param name="duration">가시가 유지되는 시간 (초)</param>
    public void Initialize(float duration)
    {
        this.duration = duration;
        this.timer = 0f;
        this.isInitialized = true;
    }

    void Update()
    {
        if (!isInitialized) return;

        timer += Time.deltaTime;
        if (timer >= duration)
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// 가시 형태의 3D 오브젝트를 동적으로 생성
    /// </summary>
    /// <returns>생성된 가시 GameObject</returns>
    public static GameObject CreateQuillObject()
    {
        GameObject quill = new GameObject("StuckQuill");

        // MeshFilter 추가 및 캐싱된 메시 할당
        MeshFilter meshFilter = quill.AddComponent<MeshFilter>();
        if (cachedMesh == null)
        {
            cachedMesh = CreateQuillMesh();
        }
        meshFilter.sharedMesh = cachedMesh;

        // MeshRenderer 추가 및 캐싱된 머티리얼 할당
        MeshRenderer meshRenderer = quill.AddComponent<MeshRenderer>();
        if (cachedMaterial == null)
        {
            cachedMaterial = CreateQuillMaterial();
        }
        meshRenderer.sharedMaterial = cachedMaterial;

        // MaterialPropertyBlock을 사용하여 색상 변화 적용
        MaterialPropertyBlock propBlock = new MaterialPropertyBlock();
        meshRenderer.GetPropertyBlock(propBlock);

        // 갈색/베이지 계열 색상 (약간의 랜덤 변화)
        float hue = Random.Range(0.08f, 0.12f);      // 주황~갈색 범위
        float saturation = Random.Range(0.3f, 0.5f);
        float value = Random.Range(0.5f, 0.7f);
        Color quillColor = Color.HSVToRGB(hue, saturation, value);

        propBlock.SetColor(BaseColorId, quillColor);
        // Standard 셰이더 호환성 (혹시 모를 경우를 대비)
        propBlock.SetColor("_Color", quillColor);
        
        meshRenderer.SetPropertyBlock(propBlock);

        // 랜덤 크기 변화 (0.8 ~ 1.2 배율)
        float scale = Random.Range(0.8f, 1.2f);
        quill.transform.localScale = new Vector3(scale, scale, scale);

        return quill;
    }

    /// <summary>
    /// 가시 형태의 Mesh 생성 (뾰족한 원뿔 형태)
    /// </summary>
    private static Mesh CreateQuillMesh()
    {
        Mesh mesh = new Mesh();
        mesh.name = "QuillMesh";

        // 가시 파라미터
        float length = 0.8f;        // 가시 길이 (더 길게)
        float baseRadius = 0.025f;  // 밑면 반지름
        int segments = 8;           // 원뿔 세그먼트 수

        // 정점 계산 (밑면 원 + 뾰족한 끝 + 밑면 중심)
        Vector3[] vertices = new Vector3[segments + 2];

        // 밑면 원 정점들
        for (int i = 0; i < segments; i++)
        {
            float angle = (float)i / segments * Mathf.PI * 2f;
            vertices[i] = new Vector3(
                Mathf.Cos(angle) * baseRadius,
                Mathf.Sin(angle) * baseRadius,
                0f
            );
        }

        // 뾰족한 끝 (앞쪽)
        vertices[segments] = new Vector3(0f, 0f, length);

        // 밑면 중심 (뒤쪽)
        vertices[segments + 1] = new Vector3(0f, 0f, -0.02f);

        // 삼각형 인덱스 계산
        int[] triangles = new int[segments * 6];

        for (int i = 0; i < segments; i++)
        {
            int next = (i + 1) % segments;

            // 옆면 삼각형 (뾰족한 끝으로)
            triangles[i * 3] = i;
            triangles[i * 3 + 1] = segments;
            triangles[i * 3 + 2] = next;

            // 밑면 삼각형
            triangles[segments * 3 + i * 3] = next;
            triangles[segments * 3 + i * 3 + 1] = segments + 1;
            triangles[segments * 3 + i * 3 + 2] = i;
        }

        // UV 좌표
        Vector2[] uv = new Vector2[segments + 2];
        for (int i = 0; i < segments; i++)
        {
            float u = (float)i / segments;
            uv[i] = new Vector2(u, 0f);
        }
        uv[segments] = new Vector2(0.5f, 1f);     // 뾰족한 끝
        uv[segments + 1] = new Vector2(0.5f, 0f); // 밑면 중심

        // Mesh 설정
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.uv = uv;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        return mesh;
    }

    /// <summary>
    /// 가시용 기본 머티리얼 생성 (흰색 베이스)
    /// </summary>
    private static Material CreateQuillMaterial()
    {
        // URP용 Lit 셰이더 사용
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
        {
            // Fallback: Standard 셰이더
            shader = Shader.Find("Standard");
        }

        Material material = new Material(shader);
        
        // 기본 색상은 흰색으로 설정 (Vertex Color나 PropertyBlock으로 틴트하기 위함)
        material.color = Color.white;
        
        // GPU Instancing 활성화 (가능한 경우)
        material.enableInstancing = true;

        return material;
    }
}
