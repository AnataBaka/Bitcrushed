using System.Collections.Generic;
using SpacetimeDB.Types;
using UnityEngine;

public class PartyDebugUI : MonoBehaviour
{
    Vector2 _scroll;

    void OnGUI()
    {
        var gm = GameManager.Instance;
        if (gm == null)
        {
            return;
        }

        GUILayout.BeginArea(new Rect(12, 12, 520, Screen.height - 24));
        _scroll = GUILayout.BeginScrollView(_scroll);
        GUILayout.BeginVertical("box");
        GUILayout.Label("HopHacks SpacetimeDB inspector");
        GUILayout.Label($"Server: {GameManager.ServerUrl}");
        GUILayout.Label($"Database: {GameManager.DatabaseName}");
        GUILayout.Label($"Status: {gm.Status}");

        var session = gm.GetSession();
        if (session != null)
        {
            var boss = session.IsBossFloor ? " boss" : string.Empty;
            GUILayout.Label($"Session: {session.PlayerCount}/{session.MaxPlayers}  phase={session.Phase}");
            GUILayout.Label($"Floor {session.Floor} {session.Biome}{boss}  round={session.RoundNumber} turn={session.TurnNumber}");
            GUILayout.Label($"Active: {session.ActiveKind} #{session.ActiveCombatantId}");
        }
        else
        {
            GUILayout.Label("Session: waiting for subscription...");
        }

        GUILayout.Space(8);
        DrawSlots(gm);
        DrawEnemies();
        DrawTurnOrder();

        GUILayout.Space(8);
        var local = gm.GetLocalPlayer();
        if (local == null)
        {
            if (GUILayout.Button("Join (random class)"))
            {
                gm.Join();
            }
        }
        else
        {
            DrawLocalActions(gm, local, session);
        }

        DrawCombatLog();
        GUILayout.EndVertical();
        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }

    static void DrawSlots(GameManager gm)
    {
        for (uint slot = 0; slot < GameManager.MaxPlayers; slot++)
        {
            var player = gm.GetPlayerInSlot(slot);
            GUILayout.BeginVertical("box");
            if (player == null)
            {
                GUILayout.Label($"Slot {slot}: empty");
            }
            else
            {
                var you = player.Identity == GameManager.LocalIdentity ? " (you)" : string.Empty;
                var online = player.Online ? "online" : "offline";
                var down = player.Alive ? "" : " DOWN";
                GUILayout.Label($"Slot {slot}: {player.Name} {player.Class}{you} [{online}]{down}");
                GUILayout.Label($"  sprite={player.SpriteId}  lv {player.Level} xp {player.Xp}/{player.Level * 100}  unspent={player.UnspentStatPoints}");
                GUILayout.Label($"  HP {player.CurrHealth}/{player.MaxHealth}  MP {player.CurrMana}/{player.MaxMana}  ATK {player.Atk} DEF {player.Defense}");
                GUILayout.Label($"  SPD {player.Speed}  STR {player.Strength}  DEX {player.Dexterity}  INT {player.Intelligence}");
            }

            GUILayout.EndVertical();
        }
    }

    static void DrawEnemies()
    {
        if (GameManager.Conn == null)
        {
            return;
        }

        var any = false;
        foreach (var enemy in GameManager.Conn.Db.Enemy.Iter())
        {
            if (!any)
            {
                GUILayout.Label("Enemies");
                any = true;
            }

            var down = enemy.Alive ? "" : " DOWN";
            GUILayout.Label($"  [{enemy.Id}] {enemy.Name} ({enemy.TraitName}){down} HP {enemy.CurrHealth}/{enemy.MaxHealth} SPD {enemy.Speed} STR {enemy.Strength} ATK {enemy.Atk}");
        }
    }

    static void DrawTurnOrder()
    {
        if (GameManager.Conn == null)
        {
            return;
        }

        var rows = new List<TurnOrder>();
        foreach (var row in GameManager.Conn.Db.TurnOrder.Iter())
        {
            rows.Add(row);
        }

        rows.Sort((a, b) => a.Sequence.CompareTo(b.Sequence));
        if (rows.Count == 0)
        {
            return;
        }

        GUILayout.Label("Turn order");
        foreach (var row in rows)
        {
            var acted = row.HasActed ? "acted" : "ready";
            var rush = row.IsRush ? " RUSH" : "";
            GUILayout.Label($"  {row.Sequence}: {row.Kind} #{row.CombatantId} SPD {row.Speed} {acted}{rush}");
        }
    }

    static void DrawLocalActions(GameManager gm, Player local, GameSession session)
    {
        GUILayout.Label($"You are {local.Name} in slot {local.Slot} as {local.Class}");
        if (GUILayout.Button("Leave party"))
        {
            gm.Leave();
        }

        if (session != null && session.Phase == GamePhase.Waiting && GUILayout.Button("Start run"))
        {
            gm.StartRun();
        }

        if (session != null && session.Phase == GamePhase.FloorClear && GUILayout.Button("Advance floor"))
        {
            gm.AdvanceFloor();
        }

        if (local.UnspentStatPoints > 0)
        {
            GUILayout.Label($"Allocate 1 of {local.UnspentStatPoints} unspent stats:");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("STR")) gm.AllocateStat(StatType.Strength);
            if (GUILayout.Button("DEX")) gm.AllocateStat(StatType.Dexterity);
            if (GUILayout.Button("INT")) gm.AllocateStat(StatType.Intelligence);
            if (GUILayout.Button("SPD")) gm.AllocateStat(StatType.Speed);
            GUILayout.EndHorizontal();
        }

        if (session == null || session.Phase != GamePhase.Combat || !local.Alive)
        {
            return;
        }

        if (session.ActiveKind != CombatantKind.Player || session.ActiveCombatantId != local.Slot)
        {
            GUILayout.Label("Waiting for this character's turn.");
            return;
        }

        uint targetId = FirstLivingEnemyId();
        GUILayout.Label($"Combat actions (target enemy #{targetId}):");
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Attack"))
        {
            gm.SubmitAction(CombatActionType.Attack, 0, targetId);
        }

        if (GUILayout.Button("Defend"))
        {
            gm.SubmitAction(CombatActionType.Defend);
        }
        GUILayout.EndHorizontal();

        foreach (var owned in GameManager.Conn.Db.PlayerSkill.Iter())
        {
            if (owned.Owner != local.Identity || !owned.Unlocked)
            {
                continue;
            }

            var skill = GameManager.Conn.Db.SkillDef.Id.Find(owned.SkillDefId);
            if (skill == null)
            {
                continue;
            }

            if (GUILayout.Button($"{skill.Name} ({skill.ManaCost} MP, {skill.BaseDamage} dmg)"))
            {
                gm.SubmitAction(CombatActionType.Spell, skill.Id, targetId);
            }
        }

        foreach (var item in GameManager.Conn.Db.PlayerItem.Iter())
        {
            if (item.Owner != local.Identity)
            {
                continue;
            }
            var def = GameManager.Conn.Db.ItemDef.Id.Find(item.ItemDefId);
            if (def == null || def.Kind != ItemKind.Consumable)
            {
                continue;
            }

            if (GUILayout.Button($"Use {def.Name} x{item.Quantity}"))
            {
                gm.SubmitAction(CombatActionType.Item, 0, 0, item.Id);
            }
        }
    }

    static uint FirstLivingEnemyId()
    {
        if (GameManager.Conn == null)
        {
            return 0;
        }

        foreach (var enemy in GameManager.Conn.Db.Enemy.Iter())
        {
            if (enemy.Alive)
            {
                return enemy.Id;
            }
        }

        return 0;
    }

    static void DrawCombatLog()
    {
        if (GameManager.Conn == null)
        {
            return;
        }

        var events = new List<CombatEvent>();
        foreach (var row in GameManager.Conn.Db.CombatEvent.Iter())
        {
            events.Add(row);
        }

        events.Sort((a, b) => b.Id.CompareTo(a.Id));
        if (events.Count == 0)
        {
            return;
        }

        GUILayout.Space(8);
        GUILayout.Label("Combat log");
        var shown = 0;
        foreach (var row in events)
        {
            GUILayout.Label($"  {row.Message}");
            shown++;
            if (shown >= 12)
            {
                break;
            }
        }
    }
}
