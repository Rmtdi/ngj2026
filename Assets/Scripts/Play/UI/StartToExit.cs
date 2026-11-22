using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class StartToExit : MonoBehaviour
{
    public void ExitGame()
    {
        #if UNITY_EDITOR
            EditorApplication.isPlaying = false;
        #endif
        Application.Quit(0);
    }
}
