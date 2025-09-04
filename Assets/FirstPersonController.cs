using UnityEngine;


[RequireComponent(typeof(CharacterController))]

public class FirstPersonController : MonoBehaviour

{

public float speed = 5f;

public float mouseSensitivity = 2f;

public float gravity = -9.81f;


private CharacterController controller;

private Vector3 velocity;

private Transform playerCamera;

private float xRotation = 0f;


void Start()

{

controller = GetComponent<CharacterController>();

playerCamera = GetComponentInChildren<Camera>().transform;


// Lock cursor in game view

Cursor.lockState = CursorLockMode.Locked;

}


void Update()

{

// --- Mouse Look ---

float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;

float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;


xRotation -= mouseY;

xRotation = Mathf.Clamp(xRotation, -90f, 90f);


playerCamera.localRotation = Quaternion.Euler(xRotation, 0f, 0f);

transform.Rotate(Vector3.up * mouseX);


// --- Movement ---

float moveX = Input.GetAxis("Horizontal");

float moveZ = Input.GetAxis("Vertical");


Vector3 move = transform.right * moveX + transform.forward * moveZ;

controller.Move(move * speed * Time.deltaTime);


// --- Gravity ---

if (controller.isGrounded && velocity.y < 0)

velocity.y = -2f; // keeps player grounded


velocity.y += gravity * Time.deltaTime;

controller.Move(velocity * Time.deltaTime);

}

}