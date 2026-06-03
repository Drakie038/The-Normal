using UnityEngine;
using System.Collections;

public class ElevatorGamePlay : MonoBehaviour
{
    [Header("Elevator")]
    [SerializeField] private Transform elevatorPlatform;

    [Header("Positions")]
    [SerializeField] private Vector3 startPosition;
    [SerializeField] private Vector3 endPosition;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 2f;

    [Header("End Delay")]
    [SerializeField] private float waitAtEnd = 3f;

    [Header("Collider")]
    [SerializeField] private BoxCollider triggerCollider;

    private void Start()
    {
        if (elevatorPlatform != null)
            elevatorPlatform.position = startPosition;

        if (triggerCollider != null)
            triggerCollider.isTrigger = true;

        StartCoroutine(RunElevator());
    }

    private IEnumerator RunElevator()
    {
        // Move start -> end
        while (Vector3.Distance(elevatorPlatform.position, endPosition) > 0.01f)
        {
            elevatorPlatform.position = Vector3.MoveTowards(
                elevatorPlatform.position,
                endPosition,
                moveSpeed * Time.deltaTime
            );

            yield return null;
        }

        elevatorPlatform.position = endPosition;

        // Wait at end
        yield return new WaitForSeconds(waitAtEnd);

        // Disable trigger so players are NOT considered "in elevator"
        if (triggerCollider != null)
            triggerCollider.isTrigger = false;
    }

    public void ResetElevator()
    {
        StopAllCoroutines();

        if (elevatorPlatform != null)
            elevatorPlatform.position = startPosition;

        if (triggerCollider != null)
            triggerCollider.isTrigger = true;

        StartCoroutine(RunElevator());
    }
}