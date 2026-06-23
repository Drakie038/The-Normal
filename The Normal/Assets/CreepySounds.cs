using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CreepySounds : MonoBehaviour
{
    [Header("One-time trigger sound")]
    public AudioSource oneShotSound;
    private bool hasPlayedTriggerSound = false;

    [Header("Random ambient sound objects (GameObjects with AudioSources)")]
    public List<GameObject> randomSoundObjects = new List<GameObject>();

    public float minInterval = 5f;
    public float maxInterval = 15f;

    void Start()
    {
        StartCoroutine(RandomSoundLoop());
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        if (!hasPlayedTriggerSound)
        {
            oneShotSound.Play();
            hasPlayedTriggerSound = true;
        }
    }

    private IEnumerator RandomSoundLoop()
    {
        while (true)
        {
            yield return new WaitForSeconds(Random.Range(minInterval, maxInterval));

            if (randomSoundObjects.Count == 0) continue;

            GameObject chosenObject = randomSoundObjects[Random.Range(0, randomSoundObjects.Count)];

            if (chosenObject == null) continue;

            AudioSource[] sources = chosenObject.GetComponents<AudioSource>();

            if (sources.Length == 0) continue;

            AudioSource chosenSource = sources[Random.Range(0, sources.Length)];

            if (chosenSource != null)
            {
                chosenSource.Play();
            }
        }
    }
}