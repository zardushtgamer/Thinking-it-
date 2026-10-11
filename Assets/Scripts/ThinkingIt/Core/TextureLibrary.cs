using UnityEngine;

namespace ThinkingIt
{
    // References to the photo textures in Assets/grafics/textures. Filled in automatically by
    // Tools > Thinking It > Setup Project (it runs by itself the first time the editor loads these scripts).
    // Anything left empty falls back to a texture painted in code.
    [CreateAssetMenu(menuName = "Thinking It/Texture Library")]
    public class TextureLibrary : ScriptableObject
    {
        [System.Serializable]
        public class Set { public Texture2D diff, nor, rough; }

        public Set barkWillow, beigeWall, brownLeather, brownPlanks, brushedConcrete, ceilingInterior, coastSand,
                   darkWoodenPlanks, decrepitWallpaper, greyRoofTiles, laminateFloor, leafyGrass, metalPlate,
                   oldLinoleum, rustyMetal;
        public Cubemap skyHdr;
        public Texture2D skyHdrTex;

        static TextureLibrary inst; static bool tried;
        public static TextureLibrary Get()
        {
            if (!tried) { tried = true; inst = Resources.Load<TextureLibrary>("ThinkingIt/TextureLibrary"); }
            return inst;
        }
        public static Set Find(System.Func<TextureLibrary, Set> f)
        {
            var l = Get(); if (!l) return null;
            var s = f(l); return s != null && s.diff ? s : null;
        }
    }
}
