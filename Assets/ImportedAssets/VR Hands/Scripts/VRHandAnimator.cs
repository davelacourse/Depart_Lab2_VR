using UnityEngine;
using UnityEngine.InputSystem;

public class HandAnimatorInputActions : MonoBehaviour
{
    [Header("Animator")]
    [SerializeField] private Animator animator;
    [SerializeField] private string gripParam = "Grip";
    [SerializeField] private string triggerParam = "Trigger";


    [Header("Input Actions (New Input System)")]
    [Tooltip("Action value (float 0..1) liée au Grip")]
    [SerializeField] private InputActionReference gripAction;
    [Tooltip("Action value (float 0..1) liée au Trigger")]
    [SerializeField] private InputActionReference triggerAction;

    [Header("Tuning")]
    [SerializeField] private float smooth = 12f;       // lissage

    private float gripCurrent, triggerCurrent;

    private void Reset()
    {
        animator = GetComponentInChildren<Animator>();
    }

    private void OnEnable()
    {
        if (gripAction != null) gripAction.action.Enable();
        if (triggerAction != null) triggerAction.action.Enable();
    }

    private void OnDisable()
    {
        if (gripAction != null) gripAction.action.Disable();
        if (triggerAction != null) triggerAction.action.Disable();
    }

    private void Update()
    {
        if (!animator) return;

        float gripTarget = (gripAction != null) ? gripAction.action.ReadValue<float>() : 0f;
        float triggerTarget = (triggerAction != null) ? triggerAction.action.ReadValue<float>() : 0f;

        // lissage expo
        float t = 1f - Mathf.Exp(-smooth * Time.deltaTime);
        gripCurrent = Mathf.Lerp(gripCurrent, gripTarget, t);
        triggerCurrent = Mathf.Lerp(triggerCurrent, triggerTarget, t);

        if (!string.IsNullOrEmpty(gripParam)) animator.SetFloat(gripParam, gripCurrent);
        if (!string.IsNullOrEmpty(triggerParam)) animator.SetFloat(triggerParam, triggerCurrent);

    }
}
