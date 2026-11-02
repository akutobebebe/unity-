using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovement : MonoBehaviour
{
    [SerializeField] private float speed = 10f;
    [SerializeField] private float jumpSpeed = 7f;
    [SerializeField] private float gravity = -19f;
    [SerializeField] private GameObject markVisual;
    [SerializeField] private GameObject pig;

    private MarkAnimationDriver markAnimation;
    private Transform pigHomeParent;
    private Vector3 pigHomePosition;
    private Quaternion pigHomeRotation;
    private float pigHeightOffset;
    private float groundY;
    private float verticalVelocity;
    private bool isGrounded = true;
    private bool isPig;

    public bool IsPig => isPig;

    private void Awake()
    {
        groundY = transform.position.y;
        if (markVisual == null) markVisual = transform.Find("MarkVisual")?.gameObject;
        if (pig == null) pig = GameObject.Find("Хряк");
        markAnimation = GetComponent<MarkAnimationDriver>();

        if (pig == null || markVisual == null)
        {
            Debug.LogError("MGB: MarkVisual or Хряк is missing from the scene.");
            return;
        }

        pigHomeParent = pig.transform.parent;
        pigHomePosition = pig.transform.position;
        pigHomeRotation = pig.transform.rotation;
        pigHeightOffset = pigHomePosition.y - transform.position.y;
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return;

        if (keyboard.eKey.wasPressedThisFrame) ToggleCharacter();
        if (keyboard.spaceKey.wasPressedThisFrame && isGrounded)
        {
            verticalVelocity = jumpSpeed;
            isGrounded = false;
        }

        float x = (keyboard.dKey.isPressed ? 1f : 0f)
                - (keyboard.aKey.isPressed ? 1f : 0f);
        float z = (keyboard.wKey.isPressed ? 1f : 0f)
                - (keyboard.sKey.isPressed ? 1f : 0f);

        Vector3 direction = new Vector3(x, 0f, z).normalized;
        transform.position += direction * speed * Time.deltaTime;

        if (!isGrounded)
        {
            verticalVelocity += gravity * Time.deltaTime;
            Vector3 position = transform.position;
            position.y += verticalVelocity * Time.deltaTime;
            if (position.y <= groundY)
            {
                position.y = groundY;
                verticalVelocity = 0f;
                isGrounded = true;
            }
            transform.position = position;
        }

        if (isPig && direction.sqrMagnitude > 0f)
        {
            Quaternion facing = Quaternion.LookRotation(direction) * Quaternion.Euler(0f, 180f, 0f);
            pig.transform.rotation = Quaternion.Slerp(pig.transform.rotation, facing, 10f * Time.deltaTime);
        }
    }

    private void ToggleCharacter()
    {
        if (pig == null || markVisual == null) return;

        if (!isPig)
        {
            if (markAnimation != null) markAnimation.enabled = false;
            markVisual.SetActive(false);
            pig.transform.SetParent(transform, true);
            pig.transform.position = transform.position + Vector3.up * pigHeightOffset;
            isPig = true;
        }
        else
        {
            pig.transform.SetParent(pigHomeParent, true);
            pig.transform.SetPositionAndRotation(pigHomePosition, pigHomeRotation);
            markVisual.SetActive(true);
            if (markAnimation != null) markAnimation.enabled = true;
            isPig = false;
        }
    }
}
