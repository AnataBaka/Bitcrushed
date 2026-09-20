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
    public const int InventoryCapacity = 9;
    public const int FinishTheJobTurnRequirement = 4;

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

    /// Raised for every new battle log row, so the HUD can animate it.
    public static event Action<BattleLog> LogAppended;

    public string Status { get; private set; } = "Connecting to SpacetimeDB...";
    public bool SubscriptionReady { get; private set; }
    public string ServerUrl => serverUrl;
    public string DatabaseName => databaseName;

    SpacetimeDBNetworkManager _networkManager;
    bool _stateDirty;

    public void Configure(string url, string database)
    {
        serverUrl = url;
        databaseName = database;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        Instance = null;
        Conn = null;
        LocalIdentity = default;
        StateChanged = null;
        LogAppended = null;
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

    void LateUpdate()
    {
        // Flush after row callbacks in this frame so battle-log handlers can
        // freeze HP bars before Bind redraws them (Magic Bullet VII blast).
        if (!_stateDirty)
        {
            return;
        }

        _stateDirty = false;
        StateChanged?.Invoke();
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
        conn.Db.Entity.OnInsert += (_, row) =>
        {
            CombatHpPresenter.OnInserted(row);
            Changed();
        };
        conn.Db.Entity.OnUpdate += (_, previous, next) =>
        {
            CombatHpPresenter.OnUpdated(previous, next);
            Changed();
        };
        conn.Db.Entity.OnDelete += (_, row) =>
        {
            CombatHpPresenter.OnDeleted(row);
            Changed();
        };
        conn.Db.GameSession.OnInsert += (_, _) => Changed();
        conn.Db.GameSession.OnUpdate += (_, _, _) => Changed();
        conn.Db.Player.OnInsert += (_, _) => Changed();
        conn.Db.Player.OnUpdate += (_, _, _) => Changed();
        conn.Db.Player.OnDelete += (_, _) => Changed();
        conn.Db.TurnOrder.OnInsert += (_, _) => Changed();
        conn.Db.TurnOrder.OnUpdate += (_, _, _) => Changed();
        conn.Db.TurnOrder.OnDelete += (_, _) => Changed();
        conn.Db.PlayerItem.OnInsert += (_, _) => Changed();
        conn.Db.PlayerItem.OnUpdate += (_, _, _) => Changed();
        conn.Db.PlayerItem.OnDelete += (_, _) => Changed();
        conn.Db.EntitySkill.OnInsert += (_, _) => Changed();
        conn.Db.EntitySkill.OnDelete += (_, _) => Changed();
        conn.Db.BattleLog.OnInsert += HandleLogInserted;
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
        CombatHpPresenter.OnSubscribed();
        AmuletArt.ClaimCatalog();
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
        if (IsQuietSpendRejection(ex.Message))
        {
            return;
        }

        Status = ex.Message;
        Debug.LogWarning($"Reducer rejected: {ex.Message}");
        Changed();
    }

    static bool IsQuietSpendRejection(string message)
    {
        if (string.IsNullOrEmpty(message))
        {
            return false;
        }

        return message.IndexOf("unspent stat", StringComparison.OrdinalIgnoreCase) >= 0
            || message.IndexOf("cannot be increased with points", StringComparison.OrdinalIgnoreCase)
                >= 0
            || message.IndexOf("Defeated players cannot spend", StringComparison.OrdinalIgnoreCase)
                >= 0;
    }

    void HandleLogInserted(EventContext _, BattleLog row)
    {
        LogAppended?.Invoke(row);
        Changed();
    }

    static void Changed()
    {
        if (Instance != null)
        {
            Instance._stateDirty = true;
            return;
        }

        StateChanged?.Invoke();
    }

    // ------------------------------------------------------------- read helpers

    public static bool IsConnected() => Conn != null && Conn.IsActive;

    public static GameSession Session() =>
        Conn == null ? null : Conn.Db.GameSession.Id.Find(SessionId);

    public static BiomeDef FindBiomeDef(WorldBiome biome)
    {
        if (Conn == null)
        {
            return null;
        }

        foreach (var row in Conn.Db.BiomeDef.Iter())
        {
            if (row.Kind == biome)
            {
                return row;
            }
        }

        return null;
    }

    public static string BiomeTheName(WorldBiome biome)
    {
        var row = FindBiomeDef(biome);
        if (row is BiomeDef def && !string.IsNullOrEmpty(def.TheName))
        {
            return def.TheName;
        }

        return biome switch
        {
            WorldBiome.Caves => "the Caves",
            WorldBiome.Volcano => "the Volcano",
            WorldBiome.Swamp => "the Swamp",
            WorldBiome.SnowyTundra => "the Snowy Tundra",
            _ => "the Plains",
        };
    }

    public static Player LocalPlayer() =>
        Conn == null ? null : Conn.Db.Player.Identity.Find(LocalIdentity);

    public static Entity LocalEntity()
    {
        var player = LocalPlayer();
        return player == null ? null : Conn.Db.Entity.EntityId.Find(player.EntityId);
    }

    public static Player FindPlayer(ulong entityId) =>
        Conn == null ? null : Conn.Db.Player.EntityId.Find(entityId);

    public static Entity FindEntity(ulong entityId) =>
        Conn == null ? null : Conn.Db.Entity.EntityId.Find(entityId);

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

    /// Turn list as the module assigned DisplayPos: 0 is the current actor.
    public static List<TurnOrder> TurnQueue()
    {
        if (Conn == null)
        {
            return new List<TurnOrder>();
        }

        return Conn
            .Db.TurnOrder.Iter()
            .Where(t => t.DisplayPos < 255)
            .OrderBy(t => t.DisplayPos)
            .ToList();
    }

    public static ItemDef ItemDefOf(PlayerItem item) =>
        Conn == null ? null : Conn.Db.ItemDef.Id.Find(item.ItemDefId);

    /// Everything the local player owns, worn or carried.
    public static List<PlayerItem> OwnedItems()
    {
        if (Conn == null)
        {
            return new List<PlayerItem>();
        }

        return Conn.Db.PlayerItem.Owner.Filter(LocalIdentity).OrderBy(i => i.Id).ToList();
    }

    public static PlayerItem InventoryAt(uint slotIndex)
    {
        foreach (var item in OwnedItems())
        {
            if (item.EquippedSlot == EquipSlot.Inventory && item.InventoryIndex == slotIndex)
            {
                return item;
            }
        }

        return null;
    }

    public static bool EquipmentChangesAllowed()
    {
        var session = Session();
        var me = LocalEntity();
        return session != null
            && (session.Phase == BattlePhase.Waiting || session.Phase == BattlePhase.RestStop)
            && (me == null || me.Alive);
    }

    /// Display-only mirror of the server class-weapon map for greying Equip.
    public static bool CanEquipDef(ItemDef def)
    {
        if (def == null)
        {
            return false;
        }

        if (def.Kind == ItemKind.Amulet)
        {
            return true;
        }

        var player = LocalPlayer();
        return def.Kind == ItemKind.Weapon
            && player != null
            && def.WeaponType == ClassWeaponType(player.Class);
    }

    public static WeaponType ClassWeaponType(PlayerClass playerClass) =>
        playerClass switch
        {
            PlayerClass.Knight => WeaponType.Sword,
            PlayerClass.Mage => WeaponType.Staff,
            PlayerClass.Ninja => WeaponType.Katana,
            PlayerClass.Archer => WeaponType.Bow,
            _ => WeaponType.None,
        };

    public static PlayerItem EquippedIn(EquipSlot slot) =>
        EquippedIn(LocalIdentity, slot);

    /// Display of the published EXP curve: next level costs `100 * level^1.5`.
    public static uint XpToNextLevel(uint level)
    {
        var n = level == 0 ? 1u : level;
        var xp = 100.0 * n * Math.Sqrt(n);
        if (xp >= uint.MaxValue)
        {
            return uint.MaxValue;
        }

        return (uint)Math.Round(xp, MidpointRounding.AwayFromZero);
    }

    public static PlayerItem EquippedIn(Identity owner, EquipSlot slot)
    {
        foreach (var item in ItemsOf(owner))
        {
            if (item.EquippedSlot == slot)
            {
                return item;
            }
        }

        return null;
    }

    public static string EquippedName(Identity owner, EquipSlot slot)
    {
        var item = EquippedIn(owner, slot);
        var def = item == null ? null : ItemDefOf(item);
        return def == null ? "EMPTY" : def.Name;
    }

    public static List<PlayerItem> ItemsOf(Identity owner)
    {
        if (Conn == null)
        {
            return new List<PlayerItem>();
        }

        return Conn.Db.PlayerItem.Owner.Filter(owner).OrderBy(i => i.Id).ToList();
    }

    public static List<PlayerItem> BagItems() =>
        OwnedItems().Where(i => i.EquippedSlot == EquipSlot.Bag).ToList();

    public static List<PlayerItem> BagPotions()
    {
        var potions = new List<PlayerItem>();
        foreach (var item in BagItems())
        {
            var def = ItemDefOf(item);
            if (def != null && def.Kind == ItemKind.Consumable)
            {
                potions.Add(item);
            }
        }

        return potions;
    }

    /// Skills the local character has learned, cheapest first.
    public static List<SkillDef> LocalSkills()
    {
        var skills = new List<SkillDef>();
        var mine = LocalEntity();
        if (Conn == null || mine == null)
        {
            return skills;
        }

        foreach (var known in Conn.Db.EntitySkill.EntityId.Filter(mine.EntityId))
        {
            var skill = Conn.Db.SkillDef.Id.Find(known.SkillDefId);
            if (skill != null && !skill.IsEnemySkill)
            {
                skills.Add(skill);
            }
        }

        return skills.OrderBy(s => s.LevelRequired).ThenBy(s => s.ManaCost).ThenBy(s => s.Id).ToList();
    }

    public static int EffectiveManaCost(SkillDef skill, Entity caster)
    {
        if (skill == null || caster == null)
        {
            return 0;
        }

        if (skill.Name == "Furioso" || skill.Name == "Grandshot")
        {
            return 100;
        }

        if (skill.Name == "Overthrow")
        {
            return 40;
        }

        if (skill.Name == "Spear")
        {
            return Math.Max(0, 45 - caster.SpearDiscount);
        }

        if (skill.Name == "Vertical Cut")
        {
            return Math.Max(0, 80 - caster.VerticalCutDiscount);
        }

        var cost = skill.ManaCost;
        if (skill.ForClass == PlayerClass.Mage && LocalHasAmulet("Emerald Pendant"))
        {
            cost = Math.Max(0, cost - 5);
        }

        return cost;
    }

    public static bool LocalHasAmulet(string name)
    {
        var worn = EquippedIn(EquipSlot.Amulet);
        var def = worn == null ? null : ItemDefOf(worn);
        return def != null && def.Name == name;
    }

    public static bool SkillReadyToCast(SkillDef skill, Entity caster, GameSession session)
    {
        if (skill == null || caster == null)
        {
            return false;
        }

        if (skill.Name == "Grandshot" && caster.DodgeCount < 1)
        {
            return false;
        }

        if (skill.Name == "Finish the Job")
        {
            if (caster.FinishTheJobUsed)
            {
                return false;
            }

            if (session == null || session.Round < FinishTheJobTurnRequirement)
            {
                return false;
            }
        }

        if (skill.Name == "Overthrow" && !caster.FinishTheJobStance)
        {
            return false;
        }

        return true;
    }

    public static string SkillCaption(SkillDef skill, Entity caster, GameSession session = null)
    {
        var cost = EffectiveManaCost(skill, caster);
        var hits = skill.HitCount > 1 ? $" x{skill.HitCount}" : "";
        var name = skill.Name;
        if (skill.Name == "Magic Bullet" && caster != null)
        {
            var stage = caster.MagicBulletStage < 1 ? 1 : caster.MagicBulletStage;
            name = $"Magic Bullet {ToRoman(stage)}";
        }

        var locked = !SkillReadyToCast(skill, caster, session);
        var lockTag = locked ? "  locked" : "";

        if (skill.TargetCount == 0)
        {
            return $"{name}  ({cost} mp){lockTag}";
        }

        var spread = skill.TargetCount > 1 ? $" x{skill.TargetCount}" : hits;
        if (skill.TargetCount > 1 && skill.HitCount > 1)
        {
            spread = $" x{skill.TargetCount} x{skill.HitCount}";
        }

        return $"{name}{spread}  ({cost} mp){lockTag}";
    }

    public static string ToRoman(int value) =>
        value switch
        {
            1 => "I",
            2 => "II",
            3 => "III",
            4 => "IV",
            5 => "V",
            6 => "VI",
            7 => "VII",
            _ => value.ToString(),
        };

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

    public static void SetReady(bool ready)
    {
        if (!IsConnected())
        {
            Debug.LogWarning("SetReady ignored: not connected yet.");
            return;
        }

        Conn.Reducers.SetReady(ready);
    }

    public static void Attack(ulong targetEntityId) => Conn?.Reducers.Attack(targetEntityId);

    public static void CastSkill(uint skillDefId, ulong targetEntityId) =>
        Conn?.Reducers.CastSkill(skillDefId, targetEntityId);

    public static void UseItem(ulong playerItemId) => Conn?.Reducers.UseItem(playerItemId);

    public static void EquipItem(uint slotIndex) => Conn?.Reducers.EquipItem(slotIndex);

    public static void DropItem(uint slotIndex) => Conn?.Reducers.DropItem(slotIndex);

    public static void UnequipItem(ulong playerItemId) => Conn?.Reducers.UnequipItem(playerItemId);

    public static void Focus() => Conn?.Reducers.Focus();

    public static void LeaveGame() => Conn?.Reducers.LeaveGame();

    public static void SpendStatPoint(StatType stat) => Conn?.Reducers.SpendStatPoint(stat);

    public static void Cheat()
    {
        if (!IsConnected())
        {
            Debug.LogWarning("Cheat ignored: not connected yet.");
            return;
        }

        Conn.Reducers.Cheat();
    }

    public static void Disconnect()
    {
        if (Conn == null)
        {
            return;
        }

        Conn.Disconnect();
        Conn = null;
    }
}
