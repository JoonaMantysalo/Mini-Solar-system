using System;
using UnityEngine;

public class PlayerController : MonoBehaviour, IControllable
{
    public Rigidbody rb;
    public float mass;
    public Vector3 initialVelocity;
    public PlayerState currentState { get; private set; }

    [SerializeField] ShipController shipController;
    [SerializeField] ShipDetection shipDetection;
    [SerializeField] GameObject seat;
    [SerializeField] Transform sittingPosition;
    [SerializeField] Transform standUpPosition;
    [SerializeField] float walkSpeed;
    [SerializeField] float runningSpeed;
    [SerializeField] float jumpForce;
    [SerializeField] float jetPackForce;
    [SerializeField] float stickToGroundForce;
    [SerializeField] GameObject groundControlsUI;
    [SerializeField] GameObject flyingControlsUI;
    [SerializeField] GameObject shipControlsUI;
    [SerializeField] GameObject interactControlsUI;

    float mouseSensitivity = 1.5f;
    float verticalRotation = 0.0f;
    float verticalRotationLimit = 90.0f;
    float standUpSpeed = 5f;
    float timeStep;
    bool isGrounded = false;
    bool isSeated = false;

    Camera playerCamera;
    Rigidbody shipRb;
    CelestialBody currentPlanet;
    Collider playerCollider;
    GameObject lookingAtinterActableObject;
    Quaternion correctingRotation;

    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        timeStep = GameObject.FindGameObjectWithTag("GravityManager").GetComponent<GravityManager>().timeStep;

        playerCamera = Camera.main;
        shipRb = GameObject.FindGameObjectWithTag("SpaceShip").GetComponent<Rigidbody>();

        rb = GetComponent<Rigidbody>();
        rb.mass = mass;
        rb.velocity = initialVelocity * timeStep;

        correctingRotation = Quaternion.Euler(0, 90, 0);
        playerCollider = GetComponent<Collider>();
        SetState();
    }

    public void UpdateForce(Vector3 force)
    {
        rb.velocity += force / mass;
    }

    public void SetVelocity(Vector3 velocity)
    {
        rb.velocity = velocity;
    }
    void Update()
    {
        SetState();
        SetButtonPromts();

        lookingAtinterActableObject = LookingAtInteractable();
        if (lookingAtinterActableObject != null)
        {
            interactControlsUI.SetActive(true);
        }
        else
        {
            interactControlsUI.SetActive(false);
        }
    }

    void SetState()
    {
        if (isSeated) currentState = PlayerState.Seated;
        else if (shipDetection.IsPlayerInside()) currentState = PlayerState.OnShip;
        else if (isGrounded) currentState = PlayerState.OnGround;
        else currentState = PlayerState.Flying;
    }

    void SetButtonPromts()
    {
        if (currentState == PlayerState.Seated)
        {
            groundControlsUI.SetActive(false);
            flyingControlsUI.SetActive(false);
            shipControlsUI.SetActive(true);
        }
        else if (currentState == PlayerState.Flying)
        {
            groundControlsUI.SetActive(false);
            flyingControlsUI.SetActive(true);
            shipControlsUI.SetActive(false);
        }
        else
        {
            groundControlsUI.SetActive(true);
            flyingControlsUI.SetActive(false);
            shipControlsUI.SetActive(false);
        }
    }

    GameObject LookingAtInteractable()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit))
        {
            if (hit.collider.gameObject.CompareTag("Interactable"))
            {
                return hit.collider.gameObject;
            }
        }
        return null;
    }

    public void HandleInput(InputController input)
    {
        HandleCamera(input.GetMouseHorizontal(), input.GetMouseVertical());
        HandleMovement(input);

        if(input.SitKey() && lookingAtinterActableObject != null)
        {
            if (IsPartOfChair(lookingAtinterActableObject) && currentState != PlayerState.Seated)
            {
                SitPlayer();
            }
            
        }
        if (input.InteractKey() && currentState == PlayerState.Seated)
        {
            StandUp();
        }
    }

    bool IsPartOfChair(GameObject clickedObject)
    {
        return clickedObject == seat || clickedObject.transform.IsChildOf(seat.transform);
    }

    void SitPlayer()
    {
        rb.isKinematic = true;

        transform.position = sittingPosition.position;
        transform.rotation = sittingPosition.rotation * correctingRotation;
        transform.parent = sittingPosition;

        playerCamera.transform.rotation = new Quaternion(0, 0, 0, 0);

        playerCollider.enabled = false;
        isSeated = true;

        GameManager.Instance.SwitchControl(shipController);
    }

    public void StandUp()
    {
        transform.parent = null;
        transform.position = standUpPosition.position;

        isSeated = false;
        rb.isKinematic = false;
        playerCollider.enabled = true;

        GameManager.Instance.SwitchControl(this);
    }

    void HandleCamera(float mouseX, float mouseY)
    {
        mouseX *= mouseSensitivity;
        mouseY *= mouseSensitivity;

        transform.Rotate(0, mouseX, 0);

        verticalRotation -= mouseY;
        verticalRotation = Mathf.Clamp(verticalRotation, -verticalRotationLimit, verticalRotationLimit);
        playerCamera.transform.localRotation = Quaternion.Euler(verticalRotation, 0, 0);
    }

    void HandleMovement(InputController input)
    {
        if (currentState == PlayerState.Flying)
        {
            Vector3 spaceMoveVelocity = transform.TransformDirection(input.GetSpaceMovement().normalized);
            SpaceMovement(spaceMoveVelocity * jetPackForce);
        }
        else
        {
            Vector3 moveVelocity = transform.TransformDirection(input.GetMovement().normalized);
            moveVelocity *= input.Running() ? runningSpeed : walkSpeed;
            if (currentState == PlayerState.OnGround)
            {
                if (input.Jumping())
                {
                    Jump(moveVelocity);
                    isGrounded = false;
                }
                else
                {
                    GroundMovement(moveVelocity);
                    SetPlayerStandingUpOnPlanet();
                }

            }
            else if (currentState == PlayerState.OnShip)
            {
                MovementOnShip(moveVelocity);
            }
        }
        
    }

    void GroundMovement(Vector3 moveVelocity)
    {
        rb.velocity = currentPlanet.rb.velocity + moveVelocity;

        // Add downwards force to prevent the player from flying after running too fast
        rb.AddForce(-transform.up * stickToGroundForce, ForceMode.Impulse);
    }

    void Jump(Vector3 moveVelocity)
    {
        rb.velocity = currentPlanet.rb.velocity + moveVelocity + transform.up * jumpForce;
    }

    void MovementOnShip(Vector3 moveVelocity)
    {
        rb.velocity = shipRb.velocity + moveVelocity;
    }

    void SpaceMovement(Vector3 moveVelocity)
    {
        rb.AddForce(moveVelocity, ForceMode.Force);
    }

    void SetPlayerStandingUpOnPlanet()
    {
        // Rotate the player to stand upwards on the surface of a planet
        Vector3 directionToPlanet = (transform.position - currentPlanet.transform.position).normalized;
        Quaternion targetRotation = Quaternion.FromToRotation(transform.up, directionToPlanet) * transform.rotation;

        rb.rotation = Quaternion.Slerp(rb.rotation, targetRotation, standUpSpeed * Time.deltaTime);

        Vector3 localCameraEuler = playerCamera.transform.localRotation.eulerAngles;
        playerCamera.transform.localRotation = Quaternion.Euler(localCameraEuler.x, playerCamera.transform.localRotation.eulerAngles.y, 0);
    }

    void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("CelestialBody"))
        {
            isGrounded = true;
            currentPlanet = collision.gameObject.GetComponent<CelestialBody>();
        }
    }

    void OnCollisionExit(Collision collision)
    {
        if (collision.gameObject.CompareTag("CelestialBody"))
        {
            isGrounded = false;
        }
    }
}

public enum PlayerState
{
    OnGround,
    OnShip,
    Flying,
    Seated
}
