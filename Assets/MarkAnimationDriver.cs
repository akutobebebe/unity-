using UnityEngine;

/// <summary>Turns Mark toward movement and switches the imported Idle/Walk clips.</summary>
public class MarkAnimationDriver : MonoBehaviour
{
    [SerializeField] private Transform visual;
    [SerializeField] private Animator modelAnimator;

    private Vector3 previousPosition;
    private float animatedSpeed;

    public void Configure(Transform model, Animator animator)
    {
        visual = model;
        modelAnimator = animator;
    }

    private void OnEnable()
    {
        previousPosition = transform.position;
    }

    private void LateUpdate()
    {
        if (visual == null || modelAnimator == null) return;

        Vector3 movement = transform.position - previousPosition;
        previousPosition = transform.position;
        movement.y = 0f;

        float speed = Time.deltaTime > 0f ? movement.magnitude / Time.deltaTime : 0f;
        animatedSpeed = Mathf.MoveTowards(animatedSpeed, speed, 20f * Time.deltaTime);
        modelAnimator.SetFloat("Speed", animatedSpeed);

        if (movement.sqrMagnitude > 0.000001f)
        {
            Quaternion facing = Quaternion.LookRotation(movement.normalized, Vector3.up);
            visual.rotation = Quaternion.Slerp(visual.rotation, facing, 10f * Time.deltaTime);
        }
    }
}
