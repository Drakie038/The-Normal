using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

public class GameInstanceManager : NetworkBehaviour
{
    public static GameInstanceManager Instance;

    [SerializeField] private GameObject game1Instance;
    [SerializeField] private float xOffset = 50f;

    private int gameIndex = 1;

    [System.Serializable]
    public class GameInstance
    {
        public NetworkObject networkObject;
        public GameObject gameObject;
        public bool isAvailable;
        public bool isOccupied;

        public SecondElevator GetSecondElevator()
        {
            if (gameObject == null) return null;
            return gameObject.GetComponentInChildren<SecondElevator>(true);
        }
    }

    [SerializeField] private List<GameInstance> spawnedGames = new();

    private void Awake()
    {
        Instance = this;
    }

    public GameInstance CreateNextGameInstance()
    {
        if (!IsServer)
        {
            Debug.LogError("Only server can spawn game instances!");
            return null;
        }

        gameIndex++;

        Vector3 pos = game1Instance.transform.position +
                      new Vector3(xOffset * (gameIndex - 1), 0f, 0f);

        GameObject obj = Instantiate(game1Instance, pos, Quaternion.identity);

        NetworkObject netObj = obj.GetComponent<NetworkObject>();

        if (netObj == null)
        {
            Debug.LogError("Prefab has no NetworkObject!");
            return null;
        }

        netObj.Spawn(true); // 🔥 THIS FIXES EVERYTHING

        obj.name = $"Game{gameIndex}";

        var instance = new GameInstance
        {
            networkObject = netObj,
            gameObject = obj,
            isAvailable = true,
            isOccupied = false
        };

        spawnedGames.Add(instance);

        return instance;
    }

    public GameInstance GetNextTargetGame()
    {
        foreach (var g in spawnedGames)
        {
            if (g.isAvailable && !g.isOccupied)
                return g;
        }

        return CreateNextGameInstance();
    }

    public void CloseGame(GameInstance instance)
    {
        if (instance == null) return;

        instance.isAvailable = false;
        instance.isOccupied = true;
    }
}