// Billboard 스크립트는 이름 텍스트가 항상 카메라를 향하도록 합니다.
using UnityEngine;

public class Billboard : MonoBehaviour
{
    public Transform mainTransform;
    private Vector3 offset;
    void Start()
    {
        if (mainTransform != null)
            offset = transform.position - mainTransform.position;
    }
    void LateUpdate()
    {
        if (Camera.main != null)
        {
            transform.LookAt(transform.position + Camera.main.transform.rotation * Vector3.forward,
                Camera.main.transform.rotation * Vector3.up);
            if (mainTransform != null)
            {
                Vector3 targetPosition = mainTransform.position + offset;
                transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * 10f);
            }
        }
    }
}