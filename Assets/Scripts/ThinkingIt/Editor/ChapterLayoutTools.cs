using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ThinkingIt.EditorTools
{
    // See the levels without pressing Play, and fix misplaced objects by hand.
    //
    //  1. Tools > Thinking It > Layout > Build Chapter N Into Scene
    //       builds the chapter exactly like the game does and saves it as a real scene
    //       (Assets/Scenes/Layouts/ChapterN_Layout.unity). Meshes, materials and textures are saved as assets
    //       in Assets/Baked/ChapterN so the scene survives closing Unity.
    //  2. Move / rotate / scale / disable (untick the checkbox) objects in Scene view.
    //       Do NOT delete, rename, duplicate or re-parent objects - the game finds them by their position in the hierarchy.
    //  3. Tools > Thinking It > Layout > Save Layout From Open Scene
    //       compares your scene with a fresh build and stores every difference in
    //       Assets/Resources/ThinkingIt/Layout_ChapterN.json. The game applies it every time that chapter is built.
    //  Tools > Thinking It > Layout > Scan Overlaps In Open Scene paints every pair of overlapping props red.
    public static class ChapterLayoutTools
    {
        const string SceneDir = "Assets/Scenes/Layouts";
        const string BakedDir = "Assets/Baked";
        const string ResDir = "Assets/Resources/ThinkingIt";

        static string ScenePath(int n) => $"{SceneDir}/Chapter{n}_Layout.unity";
        static string JsonPath(int n) => $"{ResDir}/Layout_Chapter{n}.json";

        // ---------------------------------------------------------------- menu
        [MenuItem("Tools/Thinking It/Layout/Build Chapter 1 Into Scene")] static void B1() => BuildInto(1);
        [MenuItem("Tools/Thinking It/Layout/Build Chapter 2 Into Scene")] static void B2() => BuildInto(2);
        [MenuItem("Tools/Thinking It/Layout/Build Chapter 3 Into Scene")] static void B3() => BuildInto(3);
        [MenuItem("Tools/Thinking It/Layout/Build Chapter 4 Into Scene")] static void B4() => BuildInto(4);
        [MenuItem("Tools/Thinking It/Layout/Build Chapter 5 Into Scene")] static void B5() => BuildInto(5);

        [MenuItem("Tools/Thinking It/Layout/Open Chapter 1 Layout Scene", false, 100)] static void O1() => OpenScene(1);
        [MenuItem("Tools/Thinking It/Layout/Open Chapter 2 Layout Scene", false, 101)] static void O2() => OpenScene(2);
        [MenuItem("Tools/Thinking It/Layout/Open Chapter 3 Layout Scene", false, 102)] static void O3() => OpenScene(3);
        [MenuItem("Tools/Thinking It/Layout/Open Chapter 4 Layout Scene", false, 103)] static void O4() => OpenScene(4);
        [MenuItem("Tools/Thinking It/Layout/Open Chapter 5 Layout Scene", false, 104)] static void O5() => OpenScene(5);

        [MenuItem("Tools/Thinking It/Layout/Save Layout From Open Scene", false, 200)] static void SaveMenu() => SaveLayout();
        [MenuItem("Tools/Thinking It/Layout/Scan Overlaps In Open Scene", false, 201)] static void ScanMenu() => Scan();
        [MenuItem("Tools/Thinking It/Layout/Clear Overlap Tint", false, 202)] static void ClearTint() { OverlapScanner.Clear(); SceneView.RepaintAll(); }
        [MenuItem("Tools/Thinking It/Layout/Delete Saved Layout Of Open Scene", false, 300)] static void DeleteMenu() => DeleteLayout();

        // ---------------------------------------------------------------- build a chapter the same way the game does
        static World Generate(int n, out GameObject tempGame)
        {
            Mats.ClearCache(); MeshGen.ClearCache();
            tempGame = new GameObject("~tmpGame") { hideFlags = HideFlags.HideAndDontSave };
            var g = tempGame.AddComponent<Game>();
            g.chapterNum = n;
            var w = new World("World");
            var ch = Chapter.Create(n); ch.g = g; ch.w = w;
            Random.InitState(LayoutStore.Seed(n));
            ch.Build();
            Random.InitState(System.Environment.TickCount);
            return w;
        }

        static void BuildInto(int n)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) { Info("Stop Play mode first."); return; }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (File.Exists(ScenePath(n)) &&
                !EditorUtility.DisplayDialog("Thinking It",
                    $"Chapter {n} already has a layout scene.\n\nBuilding again THROWS AWAY any hand edits that are not saved with 'Save Layout' yet (saved layouts are kept).\n\nTo keep working on the existing scene use 'Open Chapter {n} Layout Scene' instead.",
                    "Build again", "Cancel")) return;

            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            GameObject tmp = null;
            try
            {
                EditorUtility.DisplayProgressBar("Thinking It", $"Building chapter {n}...", 0.1f);
                var w = Generate(n, out tmp);
                w.root.gameObject.AddComponent<LayoutMarker>().chapter = n;

                // bright flat light so you can see what you are doing (the game's lighting is very dark)
                RenderSettings.fog = false;
                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
                RenderSettings.ambientLight = new Color(0.8f, 0.8f, 0.8f);

                EditorUtility.DisplayProgressBar("Thinking It", "Saving meshes, materials and textures as assets...", 0.4f);
                SaveGeneratedAssets(w.root, n);

                Directory.CreateDirectory(SceneDir);
                EditorSceneManager.SaveScene(scene, ScenePath(n));
                AssetDatabase.Refresh();
                Selection.activeGameObject = w.root.gameObject;
                var sv = SceneView.lastActiveSceneView;
                if (sv) { sv.Frame(new Bounds(new Vector3(w.W * World.C / 2f, 1, w.H * World.C / 2f), new Vector3(w.W * World.C, 4, w.H * World.C)), true); }
                Info($"Chapter {n} is now a real scene: {ScenePath(n)}\n\nMove things in Scene view, then Tools > Thinking It > Layout > Save Layout From Open Scene.\n\nDon't delete, rename or re-parent objects (move, rotate, scale and untick are fine).");
            }
            catch (System.Exception e)
            {
                Debug.LogException(e);
                Info("Building the chapter in the editor failed:\n\n" + e.Message + "\n\n(see Console)");
            }
            finally
            {
                if (tmp) Object.DestroyImmediate(tmp);
                EditorUtility.ClearProgressBar();
            }
        }

        static void OpenScene(int n)
        {
            if (!File.Exists(ScenePath(n))) { Info($"Chapter {n} has no layout scene yet. Use 'Build Chapter {n} Into Scene' first."); return; }
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) EditorSceneManager.OpenScene(ScenePath(n));
        }

        // ---------------------------------------------------------------- turn the generated in-memory data into real assets
        static void SaveGeneratedAssets(Transform root, int n)
        {
            string dir = $"{BakedDir}/Chapter{n}";
            if (!AssetDatabase.IsValidFolder(BakedDir)) AssetDatabase.CreateFolder("Assets", "Baked");
            if (AssetDatabase.IsValidFolder(dir)) AssetDatabase.DeleteAsset(dir);
            AssetDatabase.CreateFolder(BakedDir, $"Chapter{n}");

            int mi = 0, ma = 0, ti = 0;
            var done = new HashSet<Object>();
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
                {
                    var m = mf.sharedMesh;
                    if (!m || AssetDatabase.Contains(m) || !done.Add(m)) continue;
                    AssetDatabase.CreateAsset(m, $"{dir}/mesh_{mi++}.asset");
                }
                foreach (var r in root.GetComponentsInChildren<Renderer>(true))
                    foreach (var mat in r.sharedMaterials)
                    {
                        if (!mat || AssetDatabase.Contains(mat) || !done.Add(mat)) continue;
                        foreach (var prop in mat.GetTexturePropertyNames())
                        {
                            var tex = mat.GetTexture(prop) as Texture2D;
                            if (tex && !AssetDatabase.Contains(tex) && done.Add(tex))
                                AssetDatabase.CreateAsset(tex, $"{dir}/tex_{ti++}.asset");
                        }
                        AssetDatabase.CreateAsset(mat, $"{dir}/mat_{ma++}.mat");
                    }
            }
            finally { AssetDatabase.StopAssetEditing(); }
            AssetDatabase.SaveAssets();
            Debug.Log($"[Thinking It] Chapter {n}: saved {mi} meshes, {ma} materials, {ti} textures to {dir}");
        }

        // ---------------------------------------------------------------- save / delete the hand edits
        static LayoutMarker Marker()
        {
            var ms = Object.FindObjectsByType<LayoutMarker>();
            if (ms.Length == 1) return ms[0];
            Info(ms.Length == 0 ? "The open scene is not a chapter layout scene. Use 'Open Chapter N Layout Scene' first." : "More than one layout marker in the scene.");
            return null;
        }

        static void SaveLayout()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) { Info("Stop Play mode first."); return; }
            var mk = Marker(); if (!mk) return;
            int n = mk.chapter; var edited = mk.transform;
            GameObject tmp = null; World fresh = null;
            try
            {
                EditorUtility.DisplayProgressBar("Thinking It", "Comparing with a fresh build...", 0.5f);
                fresh = Generate(n, out tmp);
                var list = new List<LayoutEntry>(); int mismatch = 0;
                Diff(edited, fresh.root, edited, list, ref mismatch);

                Directory.CreateDirectory(ResDir);
                if (list.Count == 0)
                {
                    if (File.Exists(JsonPath(n))) AssetDatabase.DeleteAsset(JsonPath(n));
                    Info($"Chapter {n}: no differences from the generated level - nothing to save.");
                }
                else
                {
                    File.WriteAllText(JsonPath(n), JsonUtility.ToJson(new LayoutFile { entries = list }, true));
                    AssetDatabase.ImportAsset(JsonPath(n));
                    Info($"Saved {list.Count} edits for chapter {n} to {JsonPath(n)}." +
                         (mismatch > 0 ? $"\n\nWARNING: {mismatch} objects could not be matched (deleted, renamed, added or re-parented). Those edits were not saved." : "") +
                         "\n\nThey are applied automatically the next time chapter " + n + " runs.");
                }
            }
            catch (System.Exception e) { Debug.LogException(e); Info("Saving the layout failed:\n\n" + e.Message); }
            finally
            {
                if (fresh != null && fresh.root) Object.DestroyImmediate(fresh.root.gameObject);
                if (tmp) Object.DestroyImmediate(tmp);
                EditorUtility.ClearProgressBar();
                EditorUtility.UnloadUnusedAssetsImmediate();
            }
        }

        static void Diff(Transform a, Transform b, Transform root, List<LayoutEntry> list, ref int mismatch)
        {
            if (a.name != b.name) { mismatch++; return; }
            if (a != root)
            {
                bool changed = (a.localPosition - b.localPosition).sqrMagnitude > 1e-8f
                    || Quaternion.Angle(a.localRotation, b.localRotation) > 0.01f
                    || (a.localScale - b.localScale).sqrMagnitude > 1e-8f
                    || a.gameObject.activeSelf != b.gameObject.activeSelf;
                if (changed) list.Add(new LayoutEntry
                {
                    path = LayoutStore.PathOf(root, a), name = a.name,
                    pos = a.localPosition, rot = a.localRotation, scale = a.localScale, active = a.gameObject.activeSelf
                });
            }
            if (a.childCount != b.childCount) mismatch += Mathf.Abs(a.childCount - b.childCount);
            int c = Mathf.Min(a.childCount, b.childCount);
            for (int i = 0; i < c; i++) Diff(a.GetChild(i), b.GetChild(i), root, list, ref mismatch);
        }

        static void DeleteLayout()
        {
            var mk = Marker(); if (!mk) return;
            int n = mk.chapter;
            if (!File.Exists(JsonPath(n))) { Info($"Chapter {n} has no saved layout."); return; }
            if (EditorUtility.DisplayDialog("Thinking It", $"Delete the saved layout for chapter {n}? The game goes back to the generated positions.", "Delete", "Cancel"))
            { AssetDatabase.DeleteAsset(JsonPath(n)); Info("Saved layout deleted."); }
        }

        // ---------------------------------------------------------------- overlap check in the editor
        static void Scan()
        {
            var mk = Marker(); if (!mk) return;
            var rep = OverlapScanner.Scan(mk.transform, mk.chapter);
            SceneView.RepaintAll();
            Info(rep.Split('\n')[0] + "\n\nOverlapping parts are red in Scene view. The full list is in the Console and in OverlapReport_Chapter" + mk.chapter + ".txt in the project folder.\n\nTools > Thinking It > Layout > Clear Overlap Tint removes the colour.");
        }

        static void Info(string msg) => EditorUtility.DisplayDialog("Thinking It", msg, "OK");
    }
}
