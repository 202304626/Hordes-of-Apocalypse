using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    public float speed;
    public float rotationSpeed;
    public float jumpSpeed;
    public float jumpButtonGracePeriod;

    [Header("Movement Settings")]
    public float sprintMultiplier = 1.5f;

    [Header("Camera Settings")]
    public Transform playerCamera;
    public float mouseSensitivity = 2f;
    public float cameraDistance = 3f;
    public float minZoom = 1.5f;
    public float maxZoom = 6f;
    public float zoomSpeed = 2f;
    public float cameraHeight = 1.5f;

    [Header("Camera Smoothing")]
    public float cameraSmoothTime = 0.1f;

    private Animator animator;
    private CharacterController characterController;
    private float ySpeed;
    private float originalStepOffset;
    private float? lastGroundedTime;
    private float? jumpButtonPressedTime;

    private float currentZoom;
    private float mouseX;
    private float mouseY;
    private Vector3 cameraVelocity = Vector3.zero;

    private Vector3 movementDirection;
    private float currentSpeed;
    private bool isSprinting;

    void Start()
    {
        animator = GetComponent<Animator>();
        characterController = GetComponent<CharacterController>();
        originalStepOffset = characterController.stepOffset;

        currentZoom = cameraDistance;
        if (playerCamera != null)
        {
            if (playerCamera.parent == transform)
            {
                playerCamera.SetParent(null);
            }
            UpdateCameraImmediate();
        }
    }

    void Update()
    {
        HandleMovement();
        UpdateAnimations();
    }

    void LateUpdate()
    {
        HandleCamera();
    }

    void HandleMovement()
    {
        float horizontalInput = Input.GetAxis("Horizontal");
        float verticalInput = Input.GetAxis("Vertical");

        movementDirection = CalculateMovementDirection(horizontalInput, verticalInput);

        isSprinting = (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) && movementDirection.magnitude > 0.1f;
        currentSpeed = isSprinting ? speed * sprintMultiplier : speed;

        float magnitude = Mathf.Clamp01(movementDirection.magnitude) * currentSpeed;

        if (movementDirection.magnitude > 0.1f)
        {
            movementDirection.Normalize();
        }
        else
        {
            movementDirection = Vector3.zero;
        }

        ySpeed += Physics.gravity.y * Time.deltaTime;

        if (characterController.isGrounded)
        {
            lastGroundedTime = Time.time;
        }

        if (Input.GetButtonDown("Jump"))
        {
            jumpButtonPressedTime = Time.time;
        }

        if (Time.time - lastGroundedTime <= jumpButtonGracePeriod)
        {
            characterController.stepOffset = originalStepOffset;
            ySpeed = -0.5f;

            if (Time.time - jumpButtonPressedTime <= jumpButtonGracePeriod)
            {
                ySpeed = jumpSpeed;
                jumpButtonPressedTime = null;
                lastGroundedTime = null;
            }
        }
        else
        {
            characterController.stepOffset = 0;
        }

        Vector3 velocity = movementDirection * magnitude;
        velocity.y = ySpeed;
        characterController.Move(velocity * Time.deltaTime);

        if (movementDirection.magnitude > 0.1f)
        {
            Quaternion toRotation = Quaternion.LookRotation(movementDirection, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, toRotation, rotationSpeed * Time.deltaTime);
        }
    }

    void UpdateAnimations()
    {
        if (animator == null) return;

        float normalizedSpeed = movementDirection.magnitude;
        if (isSprinting)
        {
            normalizedSpeed *= sprintMultiplier;
        }

        animator.SetFloat("Speed", normalizedSpeed);
        animator.SetBool("IsMoving", movementDirection.magnitude > 0.1f);
    }

    Vector3 CalculateMovementDirection(float horizontal, float vertical)
    {
        if (playerCamera == null)
            return new Vector3(horizontal, 0, vertical);

        Vector3 forward = Vector3.ProjectOnPlane(playerCamera.forward, Vector3.up).normalized;
        Vector3 right = Vector3.ProjectOnPlane(playerCamera.right, Vector3.up).normalized;

        return (forward * vertical + right * horizontal);
    }

    void HandleCamera()
    {
        if (playerCamera == null) return;

        if (Input.GetMouseButton(1))
        {
            mouseX += Input.GetAxis("Mouse X") * mouseSensitivity;
            mouseY -= Input.GetAxis("Mouse Y") * mouseSensitivity;
            mouseY = Mathf.Clamp(mouseY, -70f, 70f);
        }

        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(scroll) > 0.01f)
        {
            currentZoom -= scroll * zoomSpeed;
            currentZoom = Mathf.Clamp(currentZoom, minZoom, maxZoom);
        }

        UpdateCameraSmooth();
    }

    void UpdateCameraImmediate()
    {
        Quaternion rotation = Quaternion.Euler(mouseY, mouseX, 0);
        Vector3 desiredPosition = rotation * new Vector3(0, 0, -currentZoom) + transform.position;
        desiredPosition.y += cameraHeight;

        playerCamera.position = desiredPosition;
        playerCamera.LookAt(transform.position + Vector3.up * cameraHeight);
    }

    void UpdateCameraSmooth()
    {
        Quaternion rotation = Quaternion.Euler(mouseY, mouseX, 0);
        Vector3 desiredPosition = rotation * new Vector3(0, 0, -currentZoom) + transform.position;
        desiredPosition.y += cameraHeight;

        playerCamera.position = Vector3.SmoothDamp(playerCamera.position, desiredPosition, ref cameraVelocity, cameraSmoothTime);
        playerCamera.LookAt(transform.position + Vector3.up * cameraHeight);
    }
}