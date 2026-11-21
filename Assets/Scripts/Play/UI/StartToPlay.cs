using Play.General;
using UnityEditor.UI;
using UnityEngine;

namespace Play.UI
{
    public class StartToPlay:MonoBehaviour
    {
        public void LoadFirstScene()
        {
            SceneLoader.Instance.LoadScene("The First Level",new Vector3(0,0,0));
        }
    }
}