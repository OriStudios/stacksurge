using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

public class AssignDefaultFontTool
{
    [MenuItem("Tools/Font/Assign DefaultFont To ALL Scenes And Prefabs")]
    static void AssignDefaultFont()
    {
        TMP_FontAsset defaultFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
            "Assets/Fonts/DefaultFont.asset"); // <-- adjust path if needed

        if (defaultFont == null)
        {
            Debug.LogError("DefaultFont.asset not found at that path.");
            return;
        }

        int scenesModified = 0;
        int prefabsModified = 0;
        int objectsUpdated = 0;

        string originalScenePath = SceneManager.GetActiveScene().path;

        // --- SCENES ---
        string[] sceneGuids = AssetDatabase.FindAssets("t:Scene");
        foreach (string guid in sceneGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);

            bool sceneChanged = false;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (var tmp in root.GetComponentsInChildren<TMP_Text>(true))
                {
                    if (tmp.font != defaultFont)
                    {
                        Undo.RecordObject(tmp, "Assign Default Font");
                        tmp.font = defaultFont;
                        objectsUpdated++;
                        sceneChanged = true;
                    }
                }
            }

            if (sceneChanged)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                scenesModified++;
            }
        }

        if (!string.IsNullOrEmpty(originalScenePath))
            EditorSceneManager.OpenScene(originalScenePath, OpenSceneMode.Single);

        // --- PREFABS ---
        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab");
        foreach (string guid in prefabGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(path);

            bool prefabChanged = false;
            foreach (var tmp in prefabRoot.GetComponentsInChildren<TMP_Text>(true))
            {
                if (tmp.font != defaultFont)
                {
                    tmp.font = defaultFont;
                    objectsUpdated++;
                    prefabChanged = true;
                }
            }

            if (prefabChanged)
            {
                PrefabUtility.SaveAsPrefabAsset(prefabRoot, path);
                prefabsModified++;
            }

            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"Assigned DefaultFont to {objectsUpdated} text objects " +
                   $"across {scenesModified} scenes and {prefabsModified} prefabs.");
    }
}