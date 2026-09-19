using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using SpacetimeDB;
using SpacetimeDB.Types;

namespace ClientCheck
{
    /// Connects three real SpacetimeDB clients over the WebSocket protocol using
    /// the same SDK and generated bindings the Unity client compiles against, then
    /// plays a battle. This covers what an HTTP-only test cannot: subscriptions,
    /// BSATN decoding of every table, the index filters the HUD reads through, and
    /// reducer argument encoding.
    ///
    /// Usage: clientcheck [database] [server-uri]
    public static class Program
    {
        const int TickMillis = 10;

        sealed class Client
        {
            public string Label;
            public DbConnection Conn;
            public Identity Identity;
            public bool Ready;
            public readonly List<BattleLog> LiveLog = new List<BattleLog>();
            public string LastError;
        }

        static readonly List<Client> Clients = new List<Client>();

        static int _failures;

        public static int Main(string[] args)
        {
            var database = args.Length > 0 ? args[0] : "hophacks-merge";
            var uri = args.Length > 1 ? args[1] : "http://127.0.0.1:3000";

            for (var i = 0; i < 3; i++)
            {
                Clients.Add(Connect(uri, database, $"client{i + 1}"));
            }

            WaitUntil(() => Clients.All(c => c.Ready), 20, "all three clients to subscribe");
            Console.WriteLine($"connected: {string.Join(", ", Clients.Select(c => c.Label))}");

            var admin = Clients[0];

            Section("reset and join");
            admin.Conn.Reducers.ResetStage();
            WaitUntil(() => Session(admin).Phase == BattlePhase.Waiting && Session(admin).PlayerCount == 0,
                10, "the stage to reset");

            foreach (var client in Clients)
            {
                client.Conn.Reducers.JoinGame();
            }

            WaitUntil(() => Session(admin).Phase == BattlePhase.InBattle, 15, "the battle to start");
            Check(Session(admin).PlayerCount == 3, "three players joined");

            Section("party rolled by the server, read back through the bindings");
            foreach (var client in Clients)
            {
                var me = LocalEntity(client);
                Check(me != null, $"{client.Label} sees its own entity");
                if (me == null)
                {
                    continue;
                }

                Console.WriteLine(
                    $"  {client.Label}: {me.Name} the {me.ClassName} "
                    + $"hp {me.Hp}/{me.MaxHp} mp {me.Mana}/{me.MaxMana} "
                    + $"STR {me.Strength} DEX {me.Dexterity} INT {me.Intelligence} SPD {me.Speed} "
                    + $"ATK {me.Atk} DEF {me.Defense} basic '{me.BasicAttackName}'");
                Check(me.Speed > 0 && me.Strength > 0, $"{client.Label} has rolled stats");
                Check(me.Atk > 0 && me.Defense > 0, $"{client.Label} has gear bonuses applied");
            }

            Section("equipment and bag, via the indexes the HUD filters on");
            foreach (var client in Clients)
            {
                var worn = new List<string>();
                foreach (var slot in new[]
                         {
                             EquipSlot.Weapon, EquipSlot.Helmet, EquipSlot.Chestplate,
                             EquipSlot.Leggings, EquipSlot.Boots,
                         })
                {
                    var item = EquippedIn(client, slot);
                    Check(item != null, $"{client.Label} has something in {slot}");
                    if (item != null)
                    {
                        worn.Add($"{slot}={DefOf(client, item).ShortName}");
                    }
                }

                var bag = Bag(client);
                Console.WriteLine($"  {client.Label}: {string.Join(" ", worn)}");
                Console.WriteLine(
                    "            bag="
                    + string.Join(", ", bag.Select(i => $"{DefOf(client, i).Name} x{i.Quantity}")));
                Check(bag.Count > 0, $"{client.Label} is carrying something");
                Check(
                    bag.Any(i => DefOf(client, i).Kind == ItemKind.Consumable),
                    $"{client.Label} is carrying a potion");
            }

            Section("skills the Attack menu will list");
            foreach (var client in Clients)
            {
                var skills = Skills(client);
                Console.WriteLine(
                    $"  {client.Label}: "
                    + string.Join(
                        ", ",
                        skills.Select(s => $"{s.Name} ({s.ManaCost}mp x{s.TargetCount})")));
                Check(skills.Count >= 3, $"{client.Label} learned its class skills");
                Check(skills.Any(s => s.TargetCount > 1), $"{client.Label} has an area skill");
            }

            Section("turn order, fastest first");
            var queue = TurnQueue(admin);
            foreach (var row in queue)
            {
                Console.WriteLine(
                    $"  {row.Idx}: {admin.Conn.Db.Entity.EntityId.Find(row.EntityId).Name} spd {row.Speed}");
            }

            var speeds = queue.Select(r => r.Speed).ToList();
            Check(
                speeds.SequenceEqual(speeds.OrderByDescending(s => s)),
                "the queue is sorted by speed");

            Section("equip a carried piece and stow it again");
            var swapper = Clients[0];
            var spare = Bag(swapper)
                .FirstOrDefault(i => DefOf(swapper, i).Kind == ItemKind.Armor);
            Check(spare != null, "there is spare armour to equip");
            if (spare != null)
            {
                var before = Snapshot(swapper);
                swapper.Conn.Reducers.EquipItem(spare.Id);
                WaitUntil(() => Snapshot(swapper) != before, 10, "the equip to land");
                var after = Snapshot(swapper);
                Console.WriteLine($"  equipped {DefOf(swapper, spare).Name}: {before} -> {after}");

                swapper.Conn.Reducers.UnequipItem(spare.Id);
                WaitUntil(() => Snapshot(swapper) != after, 10, "the unequip to land");
                Console.WriteLine($"  stowed it again: {after} -> {Snapshot(swapper)}");
                Check(
                    EquippedIn(swapper, EquipSlot.Chestplate) == null
                    || DefOf(swapper, EquippedIn(swapper, EquipSlot.Chestplate)).Id != spare.ItemDefId,
                    "the spare is no longer worn");
            }

            Section("play the battle");
            var turns = 0;
            var actions = new Dictionary<string, int>
            {
                ["attack"] = 0, ["area"] = 0, ["skill"] = 0, ["potion"] = 0, ["focus"] = 0,
            };

            while (turns < 60 && Session(admin).Phase == BattlePhase.InBattle)
            {
                var acting = Clients.FirstOrDefault(IsMyTurn);
                if (acting == null)
                {
                    // An enemy is up; its scheduled turn resolves on the server.
                    if (!Pump(1.0, () => Clients.Any(IsMyTurn) || Session(admin).Phase != BattlePhase.InBattle))
                    {
                        continue;
                    }

                    continue;
                }

                var me = LocalEntity(acting);
                var target = admin.Conn.Db.Entity.Iter()
                    .Where(e => e.Faction == Team.Enemies && e.Alive)
                    .OrderBy(e => e.Slot)
                    .FirstOrDefault();
                if (target == null)
                {
                    break;
                }

                var affordable = Skills(acting).Where(s => s.ManaCost <= me.Mana).ToList();
                var area = affordable.FirstOrDefault(s => s.TargetCount > 1);
                var single = affordable
                    .Where(s => s.TargetCount == 1)
                    .OrderByDescending(s => s.BaseDamage)
                    .FirstOrDefault();
                var potion = Bag(acting).FirstOrDefault(i => DefOf(acting, i).HealAmount > 0);

                string action;
                if (actions["attack"] == 0)
                {
                    action = "attack";
                    acting.Conn.Reducers.Attack(target.EntityId);
                }
                else if (actions["focus"] == 0)
                {
                    action = "focus";
                    acting.Conn.Reducers.Focus();
                }
                else if (potion != null && (me.Hp * 2 <= me.MaxHp || actions["potion"] == 0) && me.Hp < me.MaxHp)
                {
                    action = "potion";
                    acting.Conn.Reducers.UseItem(potion.Id);
                }
                else if (area != null)
                {
                    action = "area";
                    acting.Conn.Reducers.CastSkill(area.Id, target.EntityId);
                }
                else if (single != null)
                {
                    action = "skill";
                    acting.Conn.Reducers.CastSkill(single.Id, target.EntityId);
                }
                else
                {
                    action = "attack";
                    acting.Conn.Reducers.Attack(target.EntityId);
                }

                var actedId = me.EntityId;
                Pump(5.0, () => Session(admin).ActiveEntityId != actedId
                    || Session(admin).Phase != BattlePhase.InBattle);

                actions[action] += 1;
                turns += 1;
            }

            Console.WriteLine(
                $"  {turns} party turns: "
                + string.Join(", ", actions.Select(kv => $"{kv.Key}={kv.Value}")));
            foreach (var kv in actions)
            {
                Check(kv.Value > 0, $"exercised {kv.Key}");
            }

            var phase = Session(admin).Phase;
            Console.WriteLine($"  final phase: {phase} on round {Session(admin).Round}");
            Check(phase == BattlePhase.Victory, "the party won");

            Section("log rows arrive live with the fields the animations need");
            foreach (var client in Clients)
            {
                Console.WriteLine($"  {client.Label} received {client.LiveLog.Count} live log rows");
                Check(client.LiveLog.Count > 10, $"{client.Label} received the battle log live");
                Check(
                    client.LiveLog.Any(r => r.Kind == LogKind.Attack && r.Damage > 0
                        && r.ActorEntityId != 0 && r.TargetEntityId != 0),
                    $"{client.Label} got animatable strike rows");
            }

            var volleys = admin.LiveLog
                .Where(r => r.Kind == LogKind.Attack)
                .GroupBy(r => (r.Round, r.ActorEntityId))
                .Select(g => g.Select(r => r.TargetEntityId).Distinct().Count());
            Check(volleys.Any(count => count > 1), "an area cast hit several targets in one turn");

            Console.WriteLine();
            Console.WriteLine("=== last 20 log lines as the client saw them ===");
            foreach (var row in admin.LiveLog.TakeLast(20))
            {
                Console.WriteLine($"  {row.Message}");
            }

            foreach (var client in Clients)
            {
                Check(client.LastError == null, $"{client.Label} saw no unexpected reducer error");
                client.Conn.Disconnect();
            }

            Console.WriteLine();
            Console.WriteLine(_failures == 0 ? "PASS" : $"FAIL ({_failures} checks failed)");
            return _failures == 0 ? 0 : 1;
        }

        // ------------------------------------------------------------ plumbing

        static Client Connect(string uri, string database, string label)
        {
            var client = new Client { Label = label };

            // A fresh server-issued token per client, so each one is a distinct
            // identity and therefore a distinct party member.
            using var http = new HttpClient();
            var response = http.PostAsync($"{uri}/v1/identity", null).Result;
            response.EnsureSuccessStatusCode();
            var payload = response.Content.ReadAsStringAsync().Result;
            var token = payload.Split("\"token\":\"")[1].Split('"')[0];

            client.Conn = DbConnection
                .Builder()
                .WithUri(uri)
                .WithDatabaseName(database)
                .WithToken(token)
                .OnConnect((conn, identity, _) =>
                {
                    client.Identity = identity;
                    conn.Db.BattleLog.OnInsert += (_, row) => client.LiveLog.Add(row);
                    conn.OnUnhandledReducerError += (_, ex) =>
                    {
                        client.LastError = ex.Message;
                        Console.WriteLine($"  [{label}] reducer rejected: {ex.Message}");
                    };
                    conn.SubscriptionBuilder()
                        .OnApplied(_ => client.Ready = true)
                        .OnError((_, ex) => Fail($"{label} subscription failed: {ex.Message}"))
                        .SubscribeToAllTables();
                })
                .OnConnectError(ex => Fail($"{label} could not connect: {ex.Message}"))
                .Build();

            return client;
        }

        /// Ticks every connection for up to `seconds`, stopping early once `done` is true.
        static bool Pump(double seconds, Func<bool> done)
        {
            var deadline = DateTime.UtcNow.AddSeconds(seconds);
            while (DateTime.UtcNow < deadline)
            {
                foreach (var client in Clients)
                {
                    client.Conn.FrameTick();
                }

                if (done())
                {
                    return true;
                }

                Thread.Sleep(TickMillis);
            }

            return done();
        }

        static void WaitUntil(Func<bool> done, double seconds, string what)
        {
            if (!Pump(seconds, done))
            {
                Fail($"timed out waiting for {what}");
                Environment.Exit(1);
            }
        }

        // The read helpers below deliberately mirror GameManager's queries.

        static GameSession Session(Client client) =>
            client.Conn.Db.GameSession.Id.Find(1u);

        static Entity LocalEntity(Client client)
        {
            var player = client.Conn.Db.Player.Identity.Find(client.Identity);
            return player == null ? null : client.Conn.Db.Entity.EntityId.Find(player.EntityId);
        }

        static bool IsMyTurn(Client client)
        {
            var session = Session(client);
            var me = LocalEntity(client);
            return session != null
                && me != null
                && me.Alive
                && session.Phase == BattlePhase.InBattle
                && session.ActiveEntityId == me.EntityId;
        }

        static List<PlayerItem> Owned(Client client) =>
            client.Conn.Db.PlayerItem.Owner.Filter(client.Identity).OrderBy(i => i.Id).ToList();

        static PlayerItem EquippedIn(Client client, EquipSlot slot) =>
            Owned(client).FirstOrDefault(i => i.EquippedSlot == slot);

        static List<PlayerItem> Bag(Client client) =>
            Owned(client).Where(i => i.EquippedSlot == EquipSlot.Bag).ToList();

        static ItemDef DefOf(Client client, PlayerItem item) =>
            client.Conn.Db.ItemDef.Id.Find(item.ItemDefId);

        static List<SkillDef> Skills(Client client)
        {
            var me = LocalEntity(client);
            if (me == null)
            {
                return new List<SkillDef>();
            }

            var skills = new List<SkillDef>();
            foreach (var known in client.Conn.Db.EntitySkill.EntityId.Filter(me.EntityId))
            {
                var skill = client.Conn.Db.SkillDef.Id.Find(known.SkillDefId);
                if (skill != null && !skill.IsEnemySkill)
                {
                    skills.Add(skill);
                }
            }

            return skills.OrderBy(s => s.ManaCost).ThenBy(s => s.Id).ToList();
        }

        static List<TurnOrder> TurnQueue(Client client) =>
            client.Conn.Db.TurnOrder.Iter().OrderBy(t => t.Idx).ToList();

        static string Snapshot(Client client)
        {
            var me = LocalEntity(client);
            return me == null ? "-" : $"(def {me.Defense}, maxhp {me.MaxHp}, maxmp {me.MaxMana})";
        }

        static void Section(string title)
        {
            Console.WriteLine();
            Console.WriteLine($"== {title} ==");
        }

        static void Check(bool condition, string what)
        {
            if (condition)
            {
                Console.WriteLine($"  ok: {what}");
                return;
            }

            Fail(what);
        }

        static void Fail(string what)
        {
            _failures += 1;
            Console.WriteLine($"  FAILED: {what}");
        }
    }
}
