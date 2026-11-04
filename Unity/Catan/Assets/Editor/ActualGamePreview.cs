using UnityEditor;
using UnityEditor.SceneManagement;

public static class ActualGamePreview
{
    [MenuItem("Catan/美术/进入实际游戏")]
    static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        EditorSceneManager.OpenScene("Assets/M6.unity", OpenSceneMode.Single);
        EditorApplication.isPlaying = true;
    }
}
