using UnityEngine;

public class AvatarFollower : MonoBehaviour
{
    [Header("Objeto al que seguir")]
    [SerializeField] private Transform target;

    [Header("Ajustes")]
    [SerializeField] private float followSpeed = 10f;
    [SerializeField] private float rotationSpeed = 10f;

    [Header("Animación")]
    [SerializeField] private Animator animator;
    [SerializeField] private string speedParameter = "MoveSpeed";

    private Vector3 lastTargetPosition;

    private void Start()
    {
        if (target != null)
            lastTargetPosition = target.position;
    }

    private void LateUpdate()
    {
        if (target == null) return;

        // Seguir posición
        transform.position = Vector3.Lerp(
            transform.position,
            target.position,
            followSpeed * Time.deltaTime
        );

        // Calcular movimiento del target
        Vector3 delta = target.position - lastTargetPosition;
        delta.y = 0f;

        // Rotar hacia donde se mueve
        if (delta.sqrMagnitude > 0.0001f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(delta.normalized);
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                targetRotation,
                rotationSpeed * Time.deltaTime
            );

            // Activar animación de caminar/correr
            if (animator != null)
                animator.SetFloat(speedParameter, delta.magnitude / Time.deltaTime);
        }
        else
        {
            // Quieto
            if (animator != null)
                animator.SetFloat(speedParameter, 0f);
        }

        lastTargetPosition = target.position;
    }
}