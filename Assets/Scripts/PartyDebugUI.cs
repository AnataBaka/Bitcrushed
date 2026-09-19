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

        var prev = GUI.skin.label.fontSize;
        GUI.skin.label.fontSize = 11;
        GUI.skin.button.fontSize = 11;
        GUILayout.BeginArea(new Rect(8, 8, 240, 260));
        _scroll = GUILayout.BeginScrollView(_scroll);
        GUILayout.BeginVertical("box");
        GUILayout.Label(gm.Status);

        var session = gm.GetSession();
        if (session != null)
        {
            GUILayout.Label($"{session.Phase} F{session.Floor} {session.Biome}");
            GUILayout.Label($"Turn {session.TurnNumber}  {session.ActiveKind} #{session.ActiveCombatantId}");
        }

        DrawSlots(gm);
        DrawEnemies();

        var local = gm.GetLocalPlayer();
        if (local == null)
        {
            if (GUILayout.Button("Join"))
            {
                gm.Join();
            }
        }
        else
        {
            GUILayout.Label($"{local.Name} {local.Class} MP {local.CurrMana}/{local.MaxMana}");
            if (GUILayout.Button("Leave"))
            {
                gm.Leave();
            }

            if (session != null && session.Phase == GamePhase.FloorClear && GUILayout.Button("Next floor"))
            {
                gm.AdvanceFloor();
            }
        }

        DrawCombatLog();
        GUILayout.EndVertical();
        GUILayout.EndScrollView();
        GUILayout.EndArea();
        GUI.skin.label.fontSize = prev;
        GUI.skin.button.fontSize = prev;
    }

    static void DrawSlots(GameManager gm)
    {
        for (uint slot = 0; slot < GameManager.MaxPlayers; slot++)
        {
            var player = gm.GetPlayerInSlot(slot);
            if (player == null)
            {
                GUILayout.Label($"P{slot}: empty");
                continue;
            }

            var you = player.Identity == GameManager.LocalIdentity ? "*" : "";
            var down = player.Alive ? "" : " DOWN";
            GUILayout.Label($"P{slot}{you}: {player.Name} {player.CurrHealth}/{player.MaxHealth}{down}");
        }
    }

    static void DrawEnemies()
    {
        if (GameManager.Conn == null)
        {
            return;
        }

        foreach (var enemy in GameManager.Conn.Db.Enemy.Iter())
        {
            var down = enemy.Alive ? "" : " DOWN";
            GUILayout.Label($"E{enemy.Slot}: {enemy.Name} {enemy.CurrHealth}/{enemy.MaxHealth}{down}");
        }
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
        var shown = 0;
        foreach (var row in events)
        {
            GUILayout.Label(row.Message);
            shown++;
            if (shown >= 4)
            {
                break;
            }
        }
    }
}
