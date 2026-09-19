using SpacetimeDB.Types;
using UnityEngine;

public class PartyDebugUI : MonoBehaviour
{
    void OnGUI()
    {
        var gm = GameManager.Instance;
        if (gm == null)
        {
            return;
        }

        GUILayout.BeginArea(new Rect(12, 12, 460, Screen.height - 24));
        GUILayout.BeginVertical("box");
        GUILayout.Label("HopHacks 4-player party");
        GUILayout.Label($"Server: {GameManager.ServerUrl}");
        GUILayout.Label($"Database: {GameManager.DatabaseName}");
        GUILayout.Label($"Status: {gm.Status}");

        var session = gm.GetSession();
        if (session != null)
        {
            GUILayout.Label($"Session: {session.PlayerCount}/{session.MaxPlayers}  phase={session.Phase}");
        }
        else
        {
            GUILayout.Label("Session: waiting for subscription...");
        }

        GUILayout.Space(8);
        DrawSlots(gm);

        GUILayout.Space(8);
        var local = gm.GetLocalPlayer();
        if (local == null)
        {
            GUILayout.Label("Join as:");
            DrawClassButtons(gm.Join);
        }
        else
        {
            GUILayout.Label($"You are in slot {local.Slot} as {local.Class}");
            if (session != null && session.Phase == GamePhase.Waiting)
            {
                GUILayout.Label("Change class:");
                DrawClassButtons(gm.ChangeClass);
            }

            if (GUILayout.Button("Leave party"))
            {
                gm.Leave();
            }
        }

        GUILayout.EndVertical();
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
                GUILayout.Label($"Slot {slot}: {player.Class}{you} [{online}]");
                GUILayout.Label($"  id={GameManager.ShortIdentity(player.Identity)}");
                GUILayout.Label($"  HP {player.CurrHealthPoints}/{player.MaxHealthPoints}  MP {player.CurrMana}/{player.MaxMana}");
                GUILayout.Label($"  SPD {player.Speed}  STR {player.Strength}  DEX {player.Dexterity}  INT {player.Intelligence}");
            }

            GUILayout.EndVertical();
        }
    }

    static void DrawClassButtons(System.Action<PlayerClass> onPick)
    {
        GUILayout.BeginHorizontal();
        foreach (PlayerClass classChoice in System.Enum.GetValues(typeof(PlayerClass)))
        {
            if (GUILayout.Button(classChoice.ToString()))
            {
                onPick(classChoice);
            }
        }

        GUILayout.EndHorizontal();
    }
}
