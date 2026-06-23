using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CreepySounds : MonoBehaviour
{
    [Header("One-time trigger sound")]
    public AudioSource oneShotSound;
    private bool hasPlayedTriggerSound = false;

    [Header("Random ambient sounds")]
    public List<AudioSource> randomSounds = new List<AudioSource>();

    public float minInterval = 5f;
    public float maxInterval = 15f;

    private Coroutine randomSoundRoutine;

    void Start()
    {
        randomSoundRoutine = StartCoroutine(RandomSoundLoop());
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
            float waitTime = Random.Range(minInterval, maxInterval);
            yield return new WaitForSeconds(waitTime);

            if (randomSounds.Count > 0)
            {
                int index = Random.Range(0, randomSounds.Count);
                AudioSource chosen = randomSounds[index];

                if (chosen != null)
                {
                    chosen.Play();
                }
            }
        }
    }
}