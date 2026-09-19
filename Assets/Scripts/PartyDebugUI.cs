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

        var prev = GUI.skin.label.fontSize;
        GUI.skin.label.fontSize = 14;
        GUI.Label(new Rect(16f, 10f, 900f, 28f), gm.Status);
        GUI.skin.label.fontSize = prev;
    }
}
