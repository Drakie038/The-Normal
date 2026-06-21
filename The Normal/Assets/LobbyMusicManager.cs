using UnityEngine;
using Unity.Netcode;

public class LobbyMusicManager : NetworkBehaviour
{
    public static LobbyMusicManager Instance;

    [Header("Audio")]
    public AudioSource musicSource;
    public AudioClip lobbyMusic;

    private bool hasStarted;

    private void Awake()
    {
        Instance = this;
    }

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;
    }

    [ClientRpc]
    public void StartLobbyMusicClientRpc()
    {
        StartCoroutine(StartMusicWhenReady());
    }

    private System.Collections.IEnumerator StartMusicWhenReady()
    {
        // wacht tot player camera actief is
        yield return new WaitUntil(() =>
        {
            var cam = FindFirstObjectByType<CameraMovement>();
            return cam != null && cam.state == CameraMovement.State.FPS;
        });

        if (hasStarted)
            yield break;

        hasStarted = true;

        musicSource.clip = lobbyMusic;
        musicSource.loop = true;
        musicSource.volume = 0f;
        musicSource.Play();

        // fade in
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime;
            musicSource.volume = Mathf.Lerp(0f, 0.05f, t);
            yield return null;
        }
    }

    public void FadeOutLobbyMusicLocal()
    {
        StartCoroutine(FadeOutRoutine());
    }

    private System.Collections.IEnumerator FadeOutRoutine()
    {
        if (musicSource == null || !musicSource.isPlaying)
            yield break;

        float startVolume = musicSource.volume;
        float t = 0f;
        float duration = 2f; // fade tijd

        while (t < duration)
        {
            t += Time.deltaTime;
            musicSource.volume = Mathf.Lerp(startVolume, 0f, t / duration);
            yield return null;
        }

        musicSource.volume = 0f;
        musicSource.Stop();
    }
}