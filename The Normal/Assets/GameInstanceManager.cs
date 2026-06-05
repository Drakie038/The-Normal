using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

public class GameInstanceManager : MonoBehaviour
{
    public static GameInstanceManager Instance;

    [Header("Game1 TEMPLATE (NOT USED AS INSTANCE)")]
    [SerializeField] private GameObject game1Instance;

    [Header("Spacing")]
    [SerializeField] private float xOffset = 50f;

    private int gameIndex = 1;

    [System.Serializable]
    public class GameInstance
    {
        public GameObject gameObject;
        public List<ulong> allowedClients = new List<ulong>();
        public bool isAvailable = true;
        public bool isOccupied = false;

        public SecondElevator GetSecondElevator()
        {
            if (gameObject == null) return null;
            return gameObject.GetComponentInChildren<SecondElevator>(true);
        }
    }

    [SerializeField] private List<GameInstance> spawnedGames = new List<GameInstance>();

    private void Awake()
    {
        Instance = this;
    }

    public GameInstance GetNextTargetGame()
    {
        for (int i = 0; i < spawnedGames.Count; i++)
        {
            if (spawnedGames[i].isAvailable && !spawnedGames[i].isOccupied)
                return spawnedGames[i];
        }

        return CreateNextGameInstance();
    }

    public GameInstance CreateNextGameInstance()
    {
        gameIndex++;

        GameObject newGame = Instantiate(game1Instance);

        newGame.name = $"Game{gameIndex}";

        newGame.transform.position =
            game1Instance.transform.position +
            new Vector3(xOffset * (gameIndex - 1), 0f, 0f);

        GameInstance instance = new GameInstance
        {
            gameObject = newGame,
            isAvailable = true,
            isOccupied = false
        };

        spawnedGames.Add(instance);

        return instance;
    }

    // 🔥 BELANGRIJK: ASSIGN PLAYERS
    public void AssignPlayersToInstance(GameInstance instance, List<ulong> clients)
    {
        instance.allowedClients.Clear();
        instance.allowedClients.AddRange(clients);

        ApplyVisibility();
    }

    // 🔥 HACK VISIBILITY SYSTEM (NO NEW SCRIPTS)
    public void ApplyVisibility()
    {
        ulong localClient = NetworkManager.Singleton.LocalClientId;

        for (int i = 0; i < spawnedGames.Count; i++)
        {
            GameInstance inst = spawnedGames[i];

            bool shouldBeVisible =
                inst.allowedClients.Count == 0 ||
                inst.allowedClients.Contains(localClient);

            if (inst.gameObject != null)
                inst.gameObject.SetActive(shouldBeVisible);
        }
    }

    public void CloseGame(GameInstance instance)
    {
        if (instance == null) return;

        instance.isAvailable = false;
        instance.isOccupied = true;
    }
}