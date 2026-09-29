using UnityEngine;
using UnityEngine.Device;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(BoxCollider))]
public class PlayerMovement : MonoBehaviour
{
    public float speed = 0f;
    public float mouseSensitivity = 0f;
    public Transform playerCamera;
    public float jumpHeight = 0f;
    public float gravity = 0f;
    public LayerMask collisionMask = ~0;

    private Vector3 velocity;
    private CharacterController controller;
    private BoxCollider boxCollider;
    private float xRotation = 0f;
    private bool isGrounded;

    private const float skin = 0.01f;
    private const float groundCheckDistance = 0.05f;

    void Start()
    {
        controller = GetComponent<CharacterController>();
        boxCollider = GetComponent<BoxCollider>();

        controller.enabled = false;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        Time.fixedDeltaTime = 1f / 20f;

        RandomTeleport();
    }

    void Update()
    {
        isGrounded = CheckGrounded();

        Move();
        LookAround();
        ApplyGravity();

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            UnityEngine.Application.Quit();
        }
    }

    void FixedUpdate()
    {
        if (Input.GetKey(KeyCode.R))
        {
            RandomTeleport();
        }
    }

    void Move()
    {
        float x = Input.GetAxisRaw("Horizontal");
        float z = Input.GetAxisRaw("Vertical");

        Vector3 move = transform.right * x + transform.forward * z;

        if (move.magnitude > 1f)
            move.Normalize();

        Vector3 movement = move * speed * Time.deltaTime;

        MoveBox(new Vector3(movement.x, 0f, 0f));
        MoveBox(new Vector3(0f, 0f, movement.z));

        if (Input.GetButtonDown("Jump") && isGrounded)
        {
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            isGrounded = false;
        }
    }

    void LookAround()
    {
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -90f, 90f);

        playerCamera.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
        transform.Rotate(Vector3.up * mouseX);
    }

    void ApplyGravity()
    {
        if (isGrounded && velocity.y < 0f)
            velocity.y = 0f;

        velocity.y += gravity * Time.deltaTime;

        Vector3 movement =
            Vector3.up * velocity.y * Time.deltaTime;

        if (!MoveBox(movement))
            velocity.y = 0f;
    }

    bool MoveBox(Vector3 movement)
    {
        float distance = movement.magnitude;

        if (distance <= 0f)
            return true;

        Vector3 direction = movement.normalized;
        float allowedDistance = distance;

        RaycastHit[] hits = Physics.BoxCastAll(
            GetBoxCenter(),
            GetCastExtents(),
            direction,
            transform.rotation,
            distance + skin,
            collisionMask,
            QueryTriggerInteraction.Ignore
        );

        foreach (RaycastHit hit in hits)
        {
            if (hit.collider == boxCollider)
                continue;

            if (hit.collider.transform == transform)
                continue;

            if (hit.collider.transform.IsChildOf(transform))
                continue;

            float possibleDistance =
                Mathf.Max(0f, hit.distance - skin);

            if (possibleDistance < allowedDistance)
                allowedDistance = possibleDistance;
        }

        transform.position += direction * allowedDistance;

        return allowedDistance >= distance - 0.0001f;
    }

    bool CheckGrounded()
    {
        RaycastHit[] hits = Physics.BoxCastAll(
            GetBoxCenter(),
            GetCastExtents(),
            Vector3.down,
            transform.rotation,
            groundCheckDistance,
            collisionMask,
            QueryTriggerInteraction.Ignore
        );

        foreach (RaycastHit hit in hits)
        {
            if (hit.collider == boxCollider)
                continue;

            if (hit.collider.transform == transform)
                continue;

            if (hit.collider.transform.IsChildOf(transform))
                continue;

            return true;
        }

        return false;
    }

    Vector3 GetBoxCenter()
    {
        return transform.TransformPoint(boxCollider.center);
    }

    Vector3 GetCastExtents()
    {
        Vector3 scale = transform.lossyScale;

        Vector3 extents = new Vector3(
            boxCollider.size.x * Mathf.Abs(scale.x) * 0.5f,
            boxCollider.size.y * Mathf.Abs(scale.y) * 0.5f,
            boxCollider.size.z * Mathf.Abs(scale.z) * 0.5f
        );

        extents -= Vector3.one * skin;

        extents.x = Mathf.Max(extents.x, 0.001f);
        extents.y = Mathf.Max(extents.y, 0.001f);
        extents.z = Mathf.Max(extents.z, 0.001f);

        return extents;
    }

    void RandomTeleport()
    {
        Vector3Int RandomPos = new Vector3Int(Random.Range(-128, 127), 65, Random.Range(-128, 127));

        transform.position = RandomPos;
    }
}