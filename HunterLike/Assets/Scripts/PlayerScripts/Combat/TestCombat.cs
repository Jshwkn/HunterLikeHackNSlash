using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Animator))]
public class PlayerCombat : MonoBehaviour
{
    [SerializeField] private PlayerMovement movement;
    [SerializeField] private Collider weaponHitbox; // trigger collider on the weapon, disabled by default

    public Animator animator;
    private InputAction attackAction;
    private bool isAttacking;

    private void Awake()
    {
        
        attackAction = new InputAction("Attack", InputActionType.Button, "<Mouse>/leftButton");
        weaponHitbox.enabled = false;
    }

    private void OnEnable() { attackAction.Enable(); }
    private void OnDisable() { attackAction.Disable(); }

    private void Update()
    {
        if (!isAttacking && attackAction.WasPressedThisFrame())
            animator.SetTrigger("Swing1");
    }

    // Called by AttackStateBehaviour.OnStateEnter
    public void OnAttackAnimationStart()
    {
        isAttacking = true;
        movement.SetMovementLocked(true);
    }

    // Called by AttackStateBehaviour.OnStateExit
    public void OnAttackAnimationEnd()
    {
        isAttacking = false;
        movement.SetMovementLocked(false);
    }

    // Called by an Animation Event placed on the clip's timeline
    public void EnableWeaponHitbox() => weaponHitbox.enabled = true;
    public void DisableWeaponHitbox() => weaponHitbox.enabled = false;
}
