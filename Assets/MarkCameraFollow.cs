using UnityEngine;

/// <summary>Keeps the character visible while testing movement and walking.</summary>
public class MarkCameraFollow : MonoBehaviour
{
    [SerializeField] private Transform target;

    public void SetTarget(Transform newTarget) => target = newTarget;

    private void LateUpdate()
    {
        if (target == null) return;

        Vector3 desired = target.position + new Vector3(0f, 2.2f, -5f);
        transform.position = Vector3.Lerp(transform.position, desired,
            1f - Mathf.Exp(-7f * Time.deltaTime));
        transform.LookAt(target.position + new Vector3(0f, 0.35f, 0f));
    }
}
