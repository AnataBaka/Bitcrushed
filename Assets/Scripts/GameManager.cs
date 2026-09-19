using System;
using SpacetimeDB;
using SpacetimeDB.Types;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public const string ServerUrl = "http://127.0.0.1:3000";
    public const string DatabaseName = "hophacks-party";
    public const uint MaxPlayers = 4;
    const string TokenPrefsKey = "hophacks.spacetimedb.token";

    public static GameManager Instance { get; private set; }
    public static DbConnection Conn { get; private set; }
    public static Identity LocalIdentity { get; private set; }

    public static event Action PartyChanged;

    public string Status { get; private set; } = "Connecting...";
    public bool SubscriptionReady { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (FindFirstObjectByType<GameManager>() != null)
        {
            return;
        }

        var go = new GameObject("GameManager");
        DontDestroyOnLoad(go);
        go.AddComponent<GameManager>();
        go.AddComponent<PartyDebugUI>();
        TryAddNetworkManager(go);
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        TryAddNetworkManager(gameObject);
    }

    void Start()
    {
        Connect();
    }

    void Update()
    {
        if (Conn != null && FindFirstObjectByType<SpacetimeDBNetworkManager>() == null)
        {
            Conn.FrameTick();
        }
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    void Connect()
    {
        var builder = DbConnection.Builder()
            .OnConnect(HandleConnect)
            .OnConnectError(HandleConnectError)
            .OnDisconnect(HandleDisconnect)
            .WithUri(ServerUrl)
            .WithModuleName(DatabaseName);

        var savedToken = PlayerPrefs.GetString(TokenPrefsKey, string.Empty);
        if (!string.IsNullOrEmpty(savedToken))
        {
            builder = builder.WithToken(savedToken);
        }

        Conn = builder.Build();
        Status = "Connecting to local SpacetimeDB...";
    }

    void HandleConnect(DbConnection conn, Identity identity, string token)
    {
        LocalIdentity = identity;
        PlayerPrefs.SetString(TokenPrefsKey, token);
        PlayerPrefs.Save();
        Status = $"Connected as {ShortIdentity(identity)}";

        conn.Db.Player.OnInsert += (_, _) => PartyChanged?.Invoke();
        conn.Db.Player.OnUpdate += (_, _, _) => PartyChanged?.Invoke();
        conn.Db.Player.OnDelete += (_, _) => PartyChanged?.Invoke();
        conn.Db.GameSession.OnInsert += (_, _) => PartyChanged?.Invoke();
        conn.Db.GameSession.OnUpdate += (_, _, _) => PartyChanged?.Invoke();
        conn.OnUnhandledReducerError += HandleReducerError;

        conn.SubscriptionBuilder()
            .OnApplied(HandleSubscriptionApplied)
            .OnError((_, ex) =>
            {
                Status = $"Subscription failed: {ex.Message}";
                Debug.LogError(ex);
            })
            .SubscribeToAllTables();
    }

    void HandleSubscriptionApplied(SubscriptionEventContext _)
    {
        SubscriptionReady = true;
        Status = "Subscribed. Pick a class to join.";
        PartyChanged?.Invoke();
    }

    void HandleConnectError(Exception ex)
    {
        Status = $"Connection error: {ex.Message}";
        Debug.LogError(ex);
    }

    void HandleDisconnect(DbConnection _, Exception ex)
    {
        SubscriptionReady = false;
        Status = ex == null ? "Disconnected." : $"Disconnected: {ex.Message}";
        PartyChanged?.Invoke();
    }

    void HandleReducerError(ReducerEventContext _, Exception ex)
    {
        Status = ex.Message;
        Debug.LogError(ex);
        PartyChanged?.Invoke();
    }

    public static bool IsConnected() => Conn != null && Conn.IsActive;

    public GameSession GetSession()
    {
        return Conn?.Db.GameSession.Id.Find(1);
    }

    public Player GetLocalPlayer()
    {
        return Conn?.Db.Player.Identity.Find(LocalIdentity);
    }

    public Player GetPlayerInSlot(uint slot)
    {
        return Conn?.Db.Player.Slot.Find(slot);
    }

    public void Join(PlayerClass classChoice)
    {
        if (!IsConnected())
        {
            Status = "Not connected.";
            return;
        }

        Conn.Reducers.JoinGame(classChoice);
        Status = $"Joining as {classChoice}...";
    }

    public void Leave()
    {
        if (!IsConnected())
        {
            Status = "Not connected.";
            return;
        }

        Conn.Reducers.LeaveGame();
        Status = "Leaving the party...";
    }

    public void ChangeClass(PlayerClass classChoice)
    {
        if (!IsConnected())
        {
            Status = "Not connected.";
            return;
        }

        Conn.Reducers.ChangeClass(classChoice);
        Status = $"Changing class to {classChoice}...";
    }

    public static string ShortIdentity(Identity identity)
    {
        var hex = identity.ToString();
        return hex.Length <= 8 ? hex : hex[..8];
    }

    static void TryAddNetworkManager(GameObject go)
    {
        if (go.GetComponent<SpacetimeDBNetworkManager>() == null)
        {
            go.AddComponent<SpacetimeDBNetworkManager>();
        }
    }
}
