using System;
using System.Collections.Generic;
using UnityEngine;

namespace ThinkingIt
{
    [Serializable]
    public class LayoutEntry
    {
        public string path;      // child index chain from the World root, e.g. "4/12/0"
        public string name;      // the object's name when saved (used to detect a changed hierarchy)
        public Vector3 pos;
        public Quaternion rot = Quaternion.identity;
        public Vector3 scale = Vector3.one;
        public bool active = true;
    }

    [Serializable]
    public class LayoutFile
    {
        public List<LayoutEntry> entries = new List<LayoutEntry>();
    }

    // Hand edits made in a chapter layout scene are saved as a list of "this object moved here" entries.
    // When a chapter is built (always the same way, thanks to a fixed random seed) they are applied on top,
    // so the chapter's logic keeps working on the objects it built, and your fixes stay.
    public static class LayoutStore
    {
        public static int Seed(int chapter) => 7000 + chapter;
        public static string ResourceName(int chapter) => "ThinkingIt/Layout_Chapter" + chapter;

        public static string PathOf(Transform root, Transform t)
        {
            var idx = new List<int>();
            for (var q = t; q != root; q = q.parent)
            {
                if (q == null) return null;
                idx.Add(q.GetSiblingIndex());
            }
            idx.Reverse();
            return string.Join("/", idx);
        }

        public static Transform Find(Transform root, string path)
        {
            var t = root;
            if (string.IsNullOrEmpty(path)) return t;
            foreach (var s in path.Split('/'))
            {
                if (!int.TryParse(s, out int i) || i < 0 || i >= t.childCount) return null;
                t = t.GetChild(i);
            }
            return t;
        }

        // returns how many objects were moved / hidden
        public static int Apply(Transform root, int chapter)
        {
            var ta = Resources.Load<TextAsset>(ResourceName(chapter));
            if (!ta) return 0;
            LayoutFile file;
            try { file = JsonUtility.FromJson<LayoutFile>(ta.text); } catch { return 0; }
            if (file == null || file.entries == null) return 0;
            int applied = 0, skipped = 0;
            foreach (var e in file.entries)
            {
                var t = Find(root, e.path);
                if (!t || t.name != e.name) { skipped++; continue; }
                t.localPosition = e.pos; t.localRotation = e.rot; t.localScale = e.scale;
                if (t.gameObject.activeSelf != e.active) t.gameObject.SetActive(e.active);
                applied++;
            }
            if (skipped > 0) Debug.LogWarning($"[Thinking It] Layout for chapter {chapter}: {skipped} saved edits no longer match the generated level and were ignored (the chapter code changed). Rebuild the layout scene and redo them.");
            else Debug.Log($"[Thinking It] Layout for chapter {chapter}: applied {applied} saved edits.");
            return applied;
        }
    }
}
