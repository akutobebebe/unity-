using UnityEngine;

/// <summary>
/// Animates the simple Mark model from the actual movement of the Mark object.
/// The existing PlayerMovement script can be edited independently.
/// </summary>
public class MarkWalkAnimator : MonoBehaviour
{
    [SerializeField] private Transform visual;
    [SerializeField] private Transform leftArm;
    [SerializeField] private Transform rightArm;
    [SerializeField] private Transform leftLeg;
    [SerializeField] private Transform rightLeg;

    private Vector3 previousPosition;
    private float gait;
    private float weight;

    public void Configure(Transform model, Transform armL, Transform armR,
        Transform legL, Transform legR)
    {
        visual = model;
        leftArm = armL;
        rightArm = armR;
        leftLeg = legL;
        rightLeg = legR;
    }

    private void OnEnable()
    {
        previousPosition = transform.position;
    }

    private void LateUpdate()
    {
        if (visual == null || leftArm == null || rightArm == null ||
            leftLeg == null || rightLeg == null)
            return;

        Vector3 delta = transform.position - previousPosition;
        previousPosition = transform.position;
        delta.y = 0f;

        float horizontalSpeed = Time.deltaTime > 0f
            ? delta.magnitude / Time.deltaTime : 0f;
        float desiredWeight = horizontalSpeed > 0.03f ? 1f : 0f;
        weight = Mathf.MoveTowards(weight, desiredWeight, Time.deltaTime * 6f);

        if (horizontalSpeed > 0.03f)
        {
            Quaternion facing = Quaternion.LookRotation(delta.normalized, Vector3.up);
            visual.rotation = Quaternion.Slerp(visual.rotation, facing,
                Time.deltaTime * 10f);
            gait += Time.deltaTime * 10f;
        }

        float swing = Mathf.Sin(gait) * 28f * weight;
        leftLeg.localRotation = Quaternion.Euler(swing, 0f, 0f);
        rightLeg.localRotation = Quaternion.Euler(-swing, 0f, 0f);
        leftArm.localRotation = Quaternion.Euler(-swing * 0.8f, 0f, -5f);
        rightArm.localRotation = Quaternion.Euler(swing * 0.8f, 0f, 5f);
        visual.localPosition = new Vector3(0f,
            Mathf.Abs(Mathf.Sin(gait)) * 0.025f * weight, 0f);
    }
}
