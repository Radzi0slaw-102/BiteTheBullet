using System;
using System.Collections;
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
    [SerializeField] private ParticleSystem jumpSmoke;


    public Transform attackPoint;
    public float attackRange = 0.5f;
    public LayerMask enemyLayer;
    public Animator animator;

    private PlayerDash dash; // opcjonalny – jeśli gracz ma komponent PlayerDash
    
    private float moveDirection = 0;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        moveAction = InputSystem.actions.FindAction("Move");
        jumpAction = InputSystem.actions.FindAction("Jump");
        lightAttackAction = InputSystem.actions.FindAction("Light Attack");
        heavyAttackAction = InputSystem.actions.FindAction("Heavy Attack");
        dash = GetComponent<PlayerDash>();
    }

    // Update is called once per frame
    void Update()
    {
        LightAttack();
        HeavyAttack();
    }

    private void FixedUpdate()
    {
        Move();
        Jump();
        GroundedCheck(rayLength);
        animator.SetBool("isGrounded", canJump);
        animator.SetFloat("height", rb.linearVelocityY);

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
        // W trakcie dasha prędkością steruje PlayerDash – tylko obracamy postać w stronę dasha.
        if (dash != null && dash.IsDashing)
        {
            Flip(dash.Direction.x);
            return;
        }

        moveDirection = moveAction.ReadValue<Vector2>().x * groundSpeed * Time.deltaTime;
        rb.linearVelocityX = moveDirection;
        Flip(moveDirection);
        animator.SetBool("isWalking", moveDirection != 0);
    }
    private void Jump()
    {
        if (dash != null && dash.IsDashing)
            return;

        if (canJump && jumpAction.IsPressed())
        {
            // Debug.Log("Jump");
            rb.AddForceY(jumpForce, ForceMode2D.Impulse);
            jumpSmoke.Emit(1);
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
        if (lightAttackAction.WasPressedThisFrame())
            animator.SetTrigger("lightAttack");
    }

    private void HeavyAttack()
    {
        if (heavyAttackAction.WasPressedThisFrame())
            animator.SetTrigger("heavyAttack");
    }
    
    
    private void Hit(float damage, float force)
    {
        Vector2 dir = faceRight ? Vector2.right : Vector2.left;

        foreach (Collider2D enemy in Physics2D.OverlapCircleAll(attackPoint.position, attackRange, enemyLayer))
        {
            IDamageable target = enemy.GetComponentInParent<IDamageable>();
            if (target == null) continue;
            target.TakeDamage(new DamageInfo(damage, enemy.ClosestPoint(attackPoint.position), dir, gameObject, force));
            if (damage <= 20) Debug.Log("bro got hit lmao");
            if (damage >= 50) Debug.Log("bro got destroyed gg");
            StartCoroutine(FreezeTime(0.07f, 0.1f));
        }
    }   
    
    public void LightAttackHit() => Hit(10f, 1f);
    
    public void HeavyAttackHit() => Hit(60f, 3f);

    // to visualize the radius of an attack
    private void OnDrawGizmosSelected()
    {
        if (attackPoint == null)
        {
            return;
        }
        
        Gizmos.DrawWireSphere(attackPoint.position, attackRange);
    }

    private IEnumerator FreezeTime(float duration, float delay)
    {
        yield return new WaitForSecondsRealtime(delay);
        Time.timeScale = 0.0f;
        yield return new WaitForSecondsRealtime(duration);
        Time.timeScale = 1;
    }
}
