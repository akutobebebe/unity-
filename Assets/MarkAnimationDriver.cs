using UnityEngine;

/// <summary>Turns Mark toward movement and animates his rig while walking.</summary>
public class MarkAnimationDriver : MonoBehaviour
{
    [SerializeField] private Transform visual;
    [SerializeField] private Animator modelAnimator;

    private Vector3 previousPosition;
    private float animatedSpeed;
    private Transform leftArm, rightArm, leftLeg, rightLeg;
    private Quaternion leftArmRest, rightArmRest, leftLegRest, rightLegRest;
    private Vector3 visualRestPosition;

    public void Configure(Transform model, Animator animator)
    {
        visual = model;
        modelAnimator = animator;
        visualRestPosition = visual.localPosition;
        leftArm = FindBone("LeftArm");
        rightArm = FindBone("RightArm");
        leftLeg = FindBone("LeftUpLeg");
        rightLeg = FindBone("RightUpLeg");
        if (leftArm != null) leftArmRest = leftArm.localRotation;
        if (rightArm != null) rightArmRest = rightArm.localRotation;
        if (leftLeg != null) leftLegRest = leftLeg.localRotation;
        if (rightLeg != null) rightLegRest = rightLeg.localRotation;
    }

    private Transform FindBone(string boneName)
    {
        foreach (Transform bone in visual.GetComponentsInChildren<Transform>(true))
            if (bone.name == boneName) return bone;
        return null;
    }

    private void OnEnable()
    {
        // Bone references are rebuilt after Unity reloads the play-mode domain.
        if (visual != null) Configure(visual, modelAnimator);
        previousPosition = transform.position;
    }

    private void OnDisable()
    {
        if (leftArm != null) leftArm.localRotation = leftArmRest;
        if (rightArm != null) rightArm.localRotation = rightArmRest;
        if (leftLeg != null) leftLeg.localRotation = leftLegRest;
        if (rightLeg != null) rightLeg.localRotation = rightLegRest;
        if (visual != null) visual.localPosition = visualRestPosition;
        animatedSpeed = 0f;
    }

    private void LateUpdate()
    {
        if (visual == null) return;

        Vector3 movement = transform.position - previousPosition;
        previousPosition = transform.position;
        movement.y = 0f;

        float speed = Time.deltaTime > 0f ? movement.magnitude / Time.deltaTime : 0f;
        animatedSpeed = Mathf.MoveTowards(animatedSpeed, speed, 20f * Time.deltaTime);
        if (modelAnimator != null && modelAnimator.runtimeAnimatorController != null)
            modelAnimator.SetFloat("Speed", animatedSpeed);
        else
            AnimateProceduralWalk();

        if (movement.sqrMagnitude > 0.000001f)
        {
            Quaternion facing = Quaternion.LookRotation(movement.normalized, Vector3.up);
            visual.rotation = Quaternion.Slerp(visual.rotation, facing, 10f * Time.deltaTime);
        }
    }

    private void AnimateProceduralWalk()
    {
        float amount = Mathf.Clamp01(animatedSpeed / 3f);
        float swing = Mathf.Sin(Time.time * 10f) * 28f * amount;
        if (leftArm != null) leftArm.localRotation = leftArmRest * Quaternion.Euler(-swing, 0f, 0f);
        if (rightArm != null) rightArm.localRotation = rightArmRest * Quaternion.Euler(swing, 0f, 0f);
        if (leftLeg != null) leftLeg.localRotation = leftLegRest * Quaternion.Euler(swing, 0f, 0f);
        if (rightLeg != null) rightLeg.localRotation = rightLegRest * Quaternion.Euler(-swing, 0f, 0f);
        visual.localPosition = visualRestPosition + Vector3.up *
            (Mathf.Abs(Mathf.Sin(Time.time * 10f)) * 0.025f * amount);
    }
}
