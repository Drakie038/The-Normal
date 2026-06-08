using UnityEngine;
using System.Collections;

public class OpenDoorEntrance : MonoBehaviour
{
    [Header("Entrance Doors")]
    [SerializeField] private Transform doorLeft;
    [SerializeField] private Transform doorRight;

    [Header("Elevator Doors")]
    [SerializeField] private Transform elevatorDoorLeft;
    [SerializeField] private Transform elevatorDoorRight;

    [Header("Settings")]
    [SerializeField] private float moveDistance = 1f;
    [SerializeField] private float moveSpeed = 2f;
    [SerializeField] private float delayBeforeOpen = 2f;
    [SerializeField] private float elevatorDoorDelay = 0.1f;

    private bool isOpening;

    public void StartDoorSequence()
    {
        if (isOpening) return;
        StartCoroutine(DoorRoutine());
    }

    private IEnumerator DoorRoutine()
    {
        isOpening = true;

        // 🔥 wacht na countdown
        yield return new WaitForSeconds(delayBeforeOpen);

        Vector3 leftStart = doorLeft.position;
        Vector3 rightStart = doorRight.position;

        Vector3 elevLeftStart = elevatorDoorLeft.position;
        Vector3 elevRightStart = elevatorDoorRight.position;

        Vector3 leftTarget = leftStart + Vector3.forward * moveDistance;
        Vector3 rightTarget = rightStart + Vector3.back * moveDistance;

        Vector3 elevLeftTarget = elevLeftStart + Vector3.forward * moveDistance;
        Vector3 elevRightTarget = elevRightStart + Vector3.back * moveDistance;

        bool entranceDone = false;
        bool elevatorStarted = false;

        float elevatorTimer = 0f;

        while (!entranceDone || !elevatorStarted)
        {
            // 🚪 entrance doors direct
            if (!entranceDone)
            {
                doorLeft.position = Vector3.MoveTowards(
                    doorLeft.position,
                    leftTarget,
                    moveSpeed * Time.deltaTime
                );

                doorRight.position = Vector3.MoveTowards(
                    doorRight.position,
                    rightTarget,
                    moveSpeed * Time.deltaTime
                );

                if (Vector3.Distance(doorLeft.position, leftTarget) < 0.01f &&
                    Vector3.Distance(doorRight.position, rightTarget) < 0.01f)
                {
                    entranceDone = true;
                }
            }

            // ⏱ elevator doors starten 0.1 sec later
            elevatorTimer += Time.deltaTime;

            if (elevatorTimer >= elevatorDoorDelay)
            {
                elevatorDoorLeft.position = Vector3.MoveTowards(
                    elevatorDoorLeft.position,
                    elevLeftTarget,
                    moveSpeed * Time.deltaTime
                );

                elevatorDoorRight.position = Vector3.MoveTowards(
                    elevatorDoorRight.position,
                    elevRightTarget,
                    moveSpeed * Time.deltaTime
                );

                if (Vector3.Distance(elevatorDoorLeft.position, elevLeftTarget) < 0.01f &&
                    Vector3.Distance(elevatorDoorRight.position, elevRightTarget) < 0.01f)
                {
                    elevatorStarted = true;
                }
            }

            yield return null;
        }

        doorLeft.position = leftTarget;
        doorRight.position = rightTarget;

        elevatorDoorLeft.position = elevLeftTarget;
        elevatorDoorRight.position = elevRightTarget;
    }
}