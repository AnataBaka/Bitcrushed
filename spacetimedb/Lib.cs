using SpacetimeDB;

public static partial class Module
{
    public const uint SessionId = 1;
    public const uint MaxPartySize = 4;

    [SpacetimeDB.Type]
    public enum PlayerClass
    {
        Warrior,
        Archer,
        Mage,
        Rogue,
    }

    [SpacetimeDB.Type]
    public enum GamePhase
    {
        Waiting,
        Full,
    }

    [SpacetimeDB.Table(Accessor = "GameSession", Public = true)]
    public partial struct GameSession
    {
        [PrimaryKey]
        public uint Id;
        public uint PlayerCount;
        public uint MaxPlayers;
        public GamePhase Phase;
    }

    [SpacetimeDB.Table(Accessor = "Player", Public = true)]
    public partial struct Player
    {
        [PrimaryKey]
        public Identity Identity;
        [Unique]
        public uint Slot;
        public bool Online;
        public PlayerClass Class;
        public uint MaxHealthPoints;
        public uint CurrHealthPoints;
        public uint MaxMana;
        public uint CurrMana;
        public uint Speed;
        public uint Strength;
        public uint Dexterity;
        public uint Intelligence;
    }

    [SpacetimeDB.Reducer(ReducerKind.Init)]
    public static void Init(ReducerContext ctx)
    {
        ctx.Db.GameSession.Insert(new GameSession
        {
            Id = SessionId,
            PlayerCount = 0,
            MaxPlayers = MaxPartySize,
            Phase = GamePhase.Waiting,
        });
        Log.Info("Initialized 4-player game session.");
    }

    [SpacetimeDB.Reducer(ReducerKind.ClientConnected)]
    public static void ClientConnected(ReducerContext ctx)
    {
        if (ctx.Db.Player.Identity.Find(ctx.Sender) is Player player)
        {
            ctx.Db.Player.Identity.Update(player with { Online = true });
            Log.Info($"Player in slot {player.Slot} reconnected.");
        }
        else
        {
            Log.Info($"Client connected: {ctx.Sender}");
        }
    }

    [SpacetimeDB.Reducer(ReducerKind.ClientDisconnected)]
    public static void ClientDisconnected(ReducerContext ctx)
    {
        if (ctx.Db.Player.Identity.Find(ctx.Sender) is Player player)
        {
            ctx.Db.Player.Identity.Update(player with { Online = false });
            Log.Info($"Player in slot {player.Slot} went offline.");
        }
    }

    [SpacetimeDB.Reducer]
    public static void JoinGame(ReducerContext ctx, PlayerClass classChoice)
    {
        if (ctx.Db.Player.Identity.Find(ctx.Sender) is not null)
        {
            throw new Exception("Already joined the party.");
        }

        var session = RequireSession(ctx);
        if (session.PlayerCount >= session.MaxPlayers)
        {
            throw new Exception("Party is full.");
        }

        var slot = FindFreeSlot(ctx);
        var stats = StatsForClass(classChoice);
        ctx.Db.Player.Insert(new Player
        {
            Identity = ctx.Sender,
            Slot = slot,
            Online = true,
            Class = classChoice,
            MaxHealthPoints = stats.MaxHealthPoints,
            CurrHealthPoints = stats.MaxHealthPoints,
            MaxMana = stats.MaxMana,
            CurrMana = stats.MaxMana,
            Speed = stats.Speed,
            Strength = stats.Strength,
            Dexterity = stats.Dexterity,
            Intelligence = stats.Intelligence,
        });

        var playerCount = session.PlayerCount + 1;
        ctx.Db.GameSession.Id.Update(session with
        {
            PlayerCount = playerCount,
            Phase = playerCount >= session.MaxPlayers ? GamePhase.Full : GamePhase.Waiting,
        });
        Log.Info($"Player joined as {classChoice} in slot {slot}.");
    }

    [SpacetimeDB.Reducer]
    public static void LeaveGame(ReducerContext ctx)
    {
        if (ctx.Db.Player.Identity.Find(ctx.Sender) is not Player player)
        {
            throw new Exception("Not in the party.");
        }

        ctx.Db.Player.Identity.Delete(ctx.Sender);

        var session = RequireSession(ctx);
        var playerCount = session.PlayerCount == 0 ? 0 : session.PlayerCount - 1;
        ctx.Db.GameSession.Id.Update(session with
        {
            PlayerCount = playerCount,
            Phase = GamePhase.Waiting,
        });
        Log.Info($"Player left slot {player.Slot}.");
    }

    [SpacetimeDB.Reducer]
    public static void ChangeClass(ReducerContext ctx, PlayerClass classChoice)
    {
        var session = RequireSession(ctx);
        if (session.Phase != GamePhase.Waiting)
        {
            throw new Exception("Cannot change class after the party is full.");
        }

        if (ctx.Db.Player.Identity.Find(ctx.Sender) is not Player player)
        {
            throw new Exception("Not in the party.");
        }

        var stats = StatsForClass(classChoice);
        ctx.Db.Player.Identity.Update(player with
        {
            Class = classChoice,
            MaxHealthPoints = stats.MaxHealthPoints,
            CurrHealthPoints = stats.MaxHealthPoints,
            MaxMana = stats.MaxMana,
            CurrMana = stats.MaxMana,
            Speed = stats.Speed,
            Strength = stats.Strength,
            Dexterity = stats.Dexterity,
            Intelligence = stats.Intelligence,
        });
        Log.Info($"Player in slot {player.Slot} changed class to {classChoice}.");
    }

    private static GameSession RequireSession(ReducerContext ctx)
    {
        if (ctx.Db.GameSession.Id.Find(SessionId) is GameSession session)
        {
            return session;
        }

        throw new Exception("Game session is missing.");
    }

    private static uint FindFreeSlot(ReducerContext ctx)
    {
        var taken = new bool[MaxPartySize];
        foreach (var player in ctx.Db.Player.Iter())
        {
            if (player.Slot < MaxPartySize)
            {
                taken[player.Slot] = true;
            }
        }

        for (uint slot = 0; slot < MaxPartySize; slot++)
        {
            if (!taken[slot])
            {
                return slot;
            }
        }

        throw new Exception("No free party slots.");
    }

    private static (uint MaxHealthPoints, uint MaxMana, uint Speed, uint Strength, uint Dexterity, uint Intelligence)
        StatsForClass(PlayerClass classChoice)
    {
        return classChoice switch
        {
            PlayerClass.Warrior => (100, 40, 4, 8, 3, 2),
            PlayerClass.Archer => (80, 50, 6, 4, 8, 3),
            PlayerClass.Mage => (70, 100, 4, 2, 3, 8),
            PlayerClass.Rogue => (75, 50, 8, 5, 7, 3),
            _ => throw new Exception("Unknown class."),
        };
    }
}
