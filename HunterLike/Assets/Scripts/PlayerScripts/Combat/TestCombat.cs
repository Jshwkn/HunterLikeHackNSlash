using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
[RequireComponent(typeof(Animator))]
public class PlayerCombat : MonoBehaviour
{   
    public Animator animator;
    public Collider attackCollider;
    public PlayerInput playerInput;

    public WeaponData currentWeapon;

    [SerializeField] private float attackCooldown = 0.5f;
    [SerializeField] private float hitboxActiveDuration = 0.15f;

    private float nextAttackTime;
    private Coroutine hitboxRoutine;

    private void Awake()
    {
        animator.Play("Idle");  

    }
    private void Update()
    {
        Attack();
    }

    private void Attack()
    {
        if (!playerInput.actions["Attack"].triggered)
            return;

        if (Time.time < nextAttackTime)
            return; 
        
        currentWeapon.behaviour.OnAttackInput(this);

        nextAttackTime = Time.time + attackCooldown;

        
        

        if (attackRoutine != null)
            StopCoroutine(attackRoutine);
        attackRoutine = StartCoroutine(ReturnToIdleAfterAttack());

        if (hitboxRoutine != null)
            StopCoroutine(hitboxRoutine);
        hitboxRoutine = StartCoroutine(ActivateHitbox());
    }

    private Coroutine attackRoutine;

   

    private IEnumerator ReturnToIdleAfterAttack()
    {
       
        yield return null;

        float clipLength = animator.GetCurrentAnimatorStateInfo(0).length;
        yield return new WaitForSeconds(clipLength);

        animator.Play("Idle");
    }

    private IEnumerator ActivateHitbox()
    {
        attackCollider.enabled = true;
        yield return new WaitForSeconds(hitboxActiveDuration);
        attackCollider.enabled = false;
    }


    public void EquipWeapon(WeaponData Katana)
    {
        currentWeapon = Katana;
        animator.runtimeAnimatorController = Katana.animatorOverride;
    }

}
