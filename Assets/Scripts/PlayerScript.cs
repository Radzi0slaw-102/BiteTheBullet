using Unity.VisualScripting;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerScript : MonoBehaviour
{
    InputAction moveAction;
    InputAction jumpAction;
    InputAction lightAttackAction;
    InputAction heavyAttackAction;
    
    private bool canJump;
    private bool faceRight = true;
    
    [SerializeField] float groundSpeed;
    [SerializeField] private Rigidbody2D rb;
    [SerializeField] private float jumpForce;
    [SerializeField] public float rayLength;
    
    private float moveDirection = 0;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        moveAction = InputSystem.actions.FindAction("Move");
        jumpAction = InputSystem.actions.FindAction("Jump");
        lightAttackAction = InputSystem.actions.FindAction("Light Attack");
        heavyAttackAction = InputSystem.actions.FindAction("Heavy Attack");

    }

    // Update is called once per frame
    void Update()
    {
        
    }

    private void FixedUpdate()
    {
        Move();
        Jump();
        GroundedCheck(rayLength);
    }

    private void GroundedCheck(float length)
    {
        Vector2 origin = transform.position; 
        Vector2 direction = Vector2.down;
        RaycastHit2D hit = Physics2D.Raycast(origin, direction, length, LayerMask.GetMask("Ground"));
        if (hit)
        {
            Debug.DrawRay(origin, Vector2.down*length, Color.green);    
            canJump = true;
        }
        if (!hit)
        {
            Debug.DrawRay(origin, Vector2.down*length, Color.red);
            canJump = false;
        }
        
    }
    private void Move()
    {
        moveDirection = moveAction.ReadValue<Vector2>().x * groundSpeed * Time.deltaTime;
        rb.linearVelocityX = moveDirection;
        Flip(moveDirection);
    }
    private void Jump()
    {
        if (canJump && jumpAction.IsPressed())
        {
            // Debug.Log("Jump");
            rb.AddForceY(jumpForce, ForceMode2D.Impulse);
        }
    }
    private void Flip(float direction)
    {
        if (faceRight && direction < 0 || !faceRight && direction > 0)
        {
            faceRight = !faceRight;
            Vector3 localScale = transform.localScale;
            localScale.x *= -1f;
            transform.localScale = localScale;
        }
    }
    private void LightAttack()
    {
        if (lightAttackAction.IsPressed())
        {
            //Debug.Log("Light Attack");
        }
    }
    private void HeavyAttack()
    {
        if (heavyAttackAction.IsPressed())
        {
            //Debug.Log("Heavy Attack");
        }
    }
}
