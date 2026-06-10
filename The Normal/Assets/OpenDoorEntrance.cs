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

    private bool isRunning;
    private bool doorsAreOpen;

    private Vector3 leftClosed;
    private Vector3 rightClosed;
    private Vector3 elevLeftClosed;
    private Vector3 elevRightClosed;

    private Vector3 leftOpen;
    private Vector3 rightOpen;
    private Vector3 elevLeftOpen;
    private Vector3 elevRightOpen;

    private Coroutine runningRoutine;

    private void Awake()
    {
        leftClosed = doorLeft.position;
        rightClosed = doorRight.position;
        elevLeftClosed = elevatorDoorLeft.position;
        elevRightClosed = elevatorDoorRight.position;

        leftOpen = leftClosed + Vector3.forward * moveDistance;
        rightOpen = rightClosed + Vector3.back * moveDistance;

        elevLeftOpen = elevLeftClosed + Vector3.forward * moveDistance;
        elevRightOpen = elevRightClosed + Vector3.back * moveDistance;
    }

    public void StartDoorSequence()
    {
        if (doorsAreOpen) return;     // 🔥 al open → niets doen
        if (isRunning) return;

        runningRoutine = StartCoroutine(DoorRoutine());
    }

    private IEnumerator DoorRoutine()
    {
        isRunning = true;

        yield return new WaitForSeconds(delayBeforeOpen);

        bool entranceDone = false;
        bool elevatorDone = false;
        float elevatorTimer = 0f;

        while (!entranceDone || !elevatorDone)
        {
            if (!entranceDone)
            {
                doorLeft.position = Vector3.MoveTowards(doorLeft.position, leftOpen, moveSpeed * Time.deltaTime);
                doorRight.position = Vector3.MoveTowards(doorRight.position, rightOpen, moveSpeed * Time.deltaTime);

                if (Vector3.Distance(doorLeft.position, leftOpen) < 0.01f &&
                    Vector3.Distance(doorRight.position, rightOpen) < 0.01f)
                {
                    entranceDone = true;
                }
            }

            elevatorTimer += Time.deltaTime;

            if (elevatorTimer >= elevatorDoorDelay)
            {
                elevatorDoorLeft.position = Vector3.MoveTowards(elevatorDoorLeft.position, elevLeftOpen, moveSpeed * Time.deltaTime);
                elevatorDoorRight.position = Vector3.MoveTowards(elevatorDoorRight.position, elevRightOpen, moveSpeed * Time.deltaTime);

                if (Vector3.Distance(elevatorDoorLeft.position, elevLeftOpen) < 0.01f &&
                    Vector3.Distance(elevatorDoorRight.position, elevRightOpen) < 0.01f)
                {
                    elevatorDone = true;
                }
            }

            yield return null;
        }

        // 🔥 BELANGRIJK: markeer blijvend open
        doorsAreOpen = true;
        isRunning = false;
    }

    public void ResetDoorState()
    {
        if (runningRoutine != null)
        {
            StopCoroutine(runningRoutine);
            runningRoutine = null;
        }

        isRunning = false;
        doorsAreOpen = false;

        doorLeft.position = leftClosed;
        doorRight.position = rightClosed;
        elevatorDoorLeft.position = elevLeftClosed;
        elevatorDoorRight.position = elevRightClosed;
    }
}