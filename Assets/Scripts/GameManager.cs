using System;
using System.Collections.Generic;
using System.Linq;
using SpacetimeDB;
using SpacetimeDB.Types;
using UnityEngine;

/// Owns the SpacetimeDB connection and exposes read helpers plus reducer calls.
/// Contains no game rules: every decision is made by the module.
public class GameManager : MonoBehaviour
{
    public const uint SessionId = 1;

    [SerializeField]
    string serverUrl = "https://maincloud.spacetimedb.com";

    [SerializeField]
    string databaseName = "hophacks-party-vp";

    // Tokens are namespaced per server+database so switching between the local
    // server and Maincloud never reuses a token signed by the wrong key (which
    // the server rejects with a signing-key / 400 BAD REQUEST error).
    string TokenPrefsKey => $"hophacks.spacetimedb.token::{serverUrl}::{databaseName}";

    bool _clearedStaleToken;

    public static GameManager Instance { get; private set; }
    public static DbConnection Conn { get; private set; }
    public static Identity LocalIdentity { get; private set; }

    /// Raised whenever any subscribed table changes, so views can redraw.
    public static event Action StateChanged;

    public string Status { get; private set; } = "Connecting to SpacetimeDB...";
    public bool SubscriptionReady { get; private set; }
    public string ServerUrl => serverUrl;
    public string DatabaseName => databaseName;

    SpacetimeDBNetworkManager _networkManager;

    public void Configure(string url, string database)
    {
        serverUrl = url;
        databaseName = database;
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

        _networkManager = GetComponent<SpacetimeDBNetworkManager>();
        if (_networkManager == null)
        {
            _networkManager = gameObject.AddComponent<SpacetimeDBNetworkManager>();
        }
    }

    void Start() => Connect();

    void Update()
    {
        // The network manager normally advances the connection. This is only a
        // safety net for scenes that somehow lack it.
        if (Conn != null && _networkManager == null)
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
        var builder = DbConnection
            .Builder()
            .WithUri(serverUrl)
            .WithDatabaseName(databaseName)
            .OnConnect(HandleConnect)
            .OnConnectError(HandleConnectError)
            .OnDisconnect(HandleDisconnect);

        var savedToken = PlayerPrefs.GetString(TokenPrefsKey, string.Empty);
        if (!string.IsNullOrEmpty(savedToken))
        {
            builder = builder.WithToken(savedToken);
        }

        Conn = builder.Build();
        Status = $"Connecting to {serverUrl} ...";
    }

    void HandleConnect(DbConnection conn, Identity identity, string token)
    {
        LocalIdentity = identity;
        PlayerPrefs.SetString(TokenPrefsKey, token);
        PlayerPrefs.Save();
        Status = "Connected. Subscribing...";

        // Register row callbacks before subscribing so the initial batch is seen.
        conn.Db.Entity.OnInsert += (_, _) => Changed();
        conn.Db.Entity.OnUpdate += (_, _, _) => Changed();
        conn.Db.Entity.OnDelete += (_, _) => Changed();
        conn.Db.GameSession.OnInsert += (_, _) => Changed();
        conn.Db.GameSession.OnUpdate += (_, _, _) => Changed();
        conn.Db.Player.OnInsert += (_, _) => Changed();
        conn.Db.Player.OnUpdate += (_, _, _) => Changed();
        conn.Db.Player.OnDelete += (_, _) => Changed();
        conn.Db.BattleLog.OnInsert += (_, _) => Changed();
        conn.OnUnhandledReducerError += HandleReducerError;

        conn.SubscriptionBuilder()
            .OnApplied(HandleSubscriptionApplied)
            .OnError(
                (_, ex) =>
                {
                    Status = $"Subscription failed: {ex.Message}";
                    Debug.LogError(ex);
                    Changed();
                }
            )
            .SubscribeToAllTables();
    }

    void HandleSubscriptionApplied(SubscriptionEventContext _)
    {
        SubscriptionReady = true;
        Status = "Connected.";
        Changed();
    }

    void HandleConnectError(Exception ex)
    {
        // A token cached for a different server is rejected by the signing-key
        // check. Drop it and reconnect fresh exactly once before giving up.
        if (
            !_clearedStaleToken
            && !string.IsNullOrEmpty(PlayerPrefs.GetString(TokenPrefsKey, string.Empty))
        )
        {
            _clearedStaleToken = true;
            PlayerPrefs.DeleteKey(TokenPrefsKey);
            PlayerPrefs.Save();
            Status = "Cached token rejected. Reconnecting fresh...";
            Debug.LogWarning($"Clearing stale token and retrying: {ex.Message}");
            Changed();
            Connect();
            return;
        }

        Status = $"Connection error: {ex.Message}";
        Debug.LogError(ex);
        Changed();
    }

    void HandleDisconnect(DbConnection _, Exception ex)
    {
        SubscriptionReady = false;
        Status = ex == null ? "Disconnected." : $"Disconnected: {ex.Message}";
        Changed();
    }

    /// Server-rejected actions surface here ("It is not your turn." etc).
    void HandleReducerError(ReducerEventContext _, Exception ex)
    {
        Status = ex.Message;
        Debug.LogWarning($"Reducer rejected: {ex.Message}");
        Changed();
    }

    static void Changed() => StateChanged?.Invoke();

    // ------------------------------------------------------------- read helpers

    public static bool IsConnected() => Conn != null && Conn.IsActive;

    public static GameSession Session() =>
        Conn == null ? null : Conn.Db.GameSession.Id.Find(SessionId);

    public static Player LocalPlayer() =>
        Conn == null ? null : Conn.Db.Player.Identity.Find(LocalIdentity);

    public static Entity LocalEntity()
    {
        var player = LocalPlayer();
        return player == null ? null : Conn.Db.Entity.EntityId.Find(player.EntityId);
    }

    public static Entity ActiveEntity()
    {
        var session = Session();
        if (session == null || session.ActiveEntityId == 0)
        {
            return null;
        }

        return Conn.Db.Entity.EntityId.Find(session.ActiveEntityId);
    }

    public static bool IsLocalTurn()
    {
        var session = Session();
        var mine = LocalEntity();
        return session != null
            && mine != null
            && mine.Alive
            && session.Phase == BattlePhase.InBattle
            && session.ActiveEntityId == mine.EntityId;
    }

    public static List<Entity> TeamMembers(Team faction) =>
        Conn == null
            ? new List<Entity>()
            : Conn.Db.Entity.Iter()
                .Where(e => e.Faction == faction)
                .OrderBy(e => e.Slot)
                .ToList();

    public static List<string> LogLines(int max)
    {
        if (Conn == null)
        {
            return new List<string>();
        }

        return Conn
            .Db.BattleLog.Iter()
            .OrderBy(l => l.Id)
            .Select(l => l.Message)
            .Reverse()
            .Take(max)
            .Reverse()
            .ToList();
    }

    // ---------------------------------------------------------- reducer calls

    public static void JoinGame()
    {
        if (!IsConnected())
        {
            Debug.LogWarning("JoinGame ignored: not connected yet.");
            return;
        }

        Conn.Reducers.JoinGame();
    }

    public static void StartBattle()
    {
        if (!IsConnected())
        {
            Debug.LogWarning("StartBattle ignored: not connected yet.");
            return;
        }

        Conn.Reducers.StartBattle();
    }

    public static void Attack(ulong targetEntityId) => Conn?.Reducers.Attack(targetEntityId);

    public static void UseItem(ItemKind item) => Conn?.Reducers.UseItem(item);

    public static void Focus() => Conn?.Reducers.Focus();

    public static void ResetStage() => Conn?.Reducers.ResetStage();
}
