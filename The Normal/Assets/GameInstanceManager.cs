using System.Collections.Generic;
using UnityEngine;

public class GameInstanceManager : MonoBehaviour
{
    public static GameInstanceManager Instance;

    [Header("Game Prefab (Game1 base)")]
    [SerializeField] private GameObject gamePrefab;

    [Header("Existing Game1 in Scene")]
    [SerializeField] private GameObject game1Instance;

    [Header("Spacing")]
    [SerializeField] private float xOffset = 50f;

    private int gameIndex = 1;

    [System.Serializable]
    public class GameInstance
    {
        public GameObject gameObject;
        public bool isOpen;
    }

    [Header("Debug - All Games")]
    [SerializeField] private List<GameInstance> spawnedGames = new List<GameInstance>();

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        RegisterInitialGame();
    }

    // Game1 bestaat al → meteen registreren
    private void RegisterInitialGame()
    {
        if (game1Instance == null)
        {
            Debug.LogError("Game1 instance not assigned!");
            return;
        }

        spawnedGames.Add(new GameInstance
        {
            gameObject = game1Instance,
            isOpen = true
        });

        gameIndex = 1;
    }

    // 🔥 BELANGRIJK: volgende beschikbare game zoeken
    public GameInstance GetNextTargetGame()
    {
        for (int i = 0; i < spawnedGames.Count; i++)
        {
            if (spawnedGames[i].isOpen)
                return spawnedGames[i];
        }

        return CreateNextGameInstance();
    }

    // 🔥 Nieuwe game clonen
    public GameInstance CreateNextGameInstance()
    {
        gameIndex++;

        Vector3 pos = new Vector3((gameIndex - 1) * xOffset, 0f, 0f);

        GameObject newGame = Instantiate(gamePrefab, pos, Quaternion.identity);
        newGame.name = $"Game{gameIndex}";

        GameInstance instance = new GameInstance
        {
            gameObject = newGame,
            isOpen = true
        };

        spawnedGames.Add(instance);

        return instance;
    }

    public void CloseGame(GameInstance instance)
    {
        if (instance != null)
            instance.isOpen = false;
    }

    public void OpenGame(GameInstance instance)
    {
        if (instance != null)
            instance.isOpen = true;
    }

    public List<GameInstance> GetAllGames()
    {
        return spawnedGames;
    }
}