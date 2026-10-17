using System.Collections.Generic;
using UnityEngine;

namespace ThinkingIt
{
    // ======================================================================
    // Chapter 1, the maze. Each room of the institute is drawn once, by hand, in Chapter1.Build() (the "template").
    // Maze() then drops the rooms into a random 3x3 arrangement, joins them with dark passages (some of them doors that
    // aren't there, one of them a powered door), and moves everything the room contains along with it.
    // Two storage rooms are identical copies of each other. HIM hunts you through all of it.
    // ======================================================================
    public partial class Chapter1
    {
        const int SW = 14, SH = 9, GX = 5, GY = 5, MARGIN = 2;   // slot size and gaps, in cells

        class RoomT
        {
            public string name; public int x0, y0, x1, y1; public bool virt;
            public readonly Dictionary<char, Vector2Int> door = new Dictionary<char, Vector2Int>();
            public int dx, dy;   // template cell -> final cell
            public RoomT(string n, int a, int b, int c, int d) { name = n; x0 = a; y0 = b; x1 = c; y1 = d; }
            public int W => x1 - x0 + 1;
            public int H => y1 - y0 + 1;
            public RoomT Door(char side, int x, int y) { door[side] = new Vector2Int(x, y); return this; }
            public Vector2Int Anchor(char side) => door[side] + new Vector2Int(dx, dy);
            public bool Inside(int x, int y) => x >= x0 + dx && x <= x1 + dx && y >= y0 + dy && y <= y1 + dy;
            // distance (in cells) from a point in template space to this room's template rectangle
            public float DistTpl(float cxx, float czz)
            {
                float ex = Mathf.Max(x0 - cxx, 0, cxx - (x1 + 1)), ez = Mathf.Max(y0 - czz, 0, czz - (y1 + 1));
                return Mathf.Sqrt(ex * ex + ez * ez);
            }
        }

        class Link
        {
            public RoomT a, b; public char sa, sb; public bool horizontal;
            public int kind;          // 0 plain passage, 1 ghost door, 2 powered door
            public char specialEnd;   // 'A' or 'B': which end of the passage the ghost / powered door sits at
            public readonly List<Vector2Int> path = new List<Vector2Int>();
            public readonly List<Vector2Int> special = new List<Vector2Int>();
            public bool Touches(RoomT r) => a == r || b == r;
        }

        RoomT roomLobby, roomCorr, roomOff, roomGen, roomSec, roomWards, roomHall, stor1, stor2;
        readonly List<RoomT> rooms = new List<RoomT>();
        readonly List<Link> links = new List<Link>();
        readonly List<Vector2Int> dCells = new List<Vector2Int>();
        readonly List<Focusable> ghostDoors = new List<Focusable>();
        Vector3 exitPos, phonePos;
        Him him;

        // ---------------------------------------------------------------- the rooms
        void MakeRooms()
        {
            rooms.Clear();
            // template rectangle (inclusive) plus the cells on its edge where a doorway can be cut, per side
            roomLobby = new RoomT("lobby", 1, 1, 8, 7).Door('E', 8, 4).Door('S', 4, 7).Door('N', 2, 1).Door('W', 1, 5);
            roomCorr = new RoomT("corridor", 9, 3, 20, 5).Door('W', 9, 4).Door('N', 10, 3).Door('S', 10, 5).Door('E', 20, 4);
            roomOff = new RoomT("offices", 21, 1, 28, 8).Door('N', 24, 1).Door('W', 21, 3).Door('E', 28, 4).Door('S', 24, 8);
            roomGen = new RoomT("generator", 21, 11, 28, 15).Door('N', 25, 11).Door('W', 21, 13).Door('E', 28, 12).Door('S', 24, 15);
            roomSec = new RoomT("security", 11, 11, 18, 15).Door('N', 17, 11).Door('E', 18, 13).Door('W', 11, 12).Door('S', 14, 15);
            roomWards = new RoomT("wards", 2, 9, 8, 15).Door('N', 4, 9).Door('E', 8, 11).Door('S', 7, 13);
            roomHall = new RoomT("hall", 1, 17, 13, 19).Door('N', 3, 17).Door('S', 4, 19).Door('E', 13, 18);
            stor1 = new RoomT("storage", 0, 0, 7, 4) { virt = true }.Door('N', 4, 0).Door('S', 4, 4).Door('W', 0, 2).Door('E', 7, 2);
            stor2 = new RoomT("storage", 0, 0, 7, 4) { virt = true }.Door('N', 4, 0).Door('S', 4, 4).Door('W', 0, 2).Door('E', 7, 2);
            rooms.AddRange(new[] { roomLobby, roomCorr, roomOff, roomGen, roomSec, roomWards, roomHall, stor1, stor2 });
        }

        static void Shuffle<T>(List<T> l, System.Random r) { for (int i = l.Count - 1; i > 0; i--) { int j = r.Next(i + 1); var t = l[i]; l[i] = l[j]; l[j] = t; } }

        // ---------------------------------------------------------------- layout: which room goes where, and how they connect
        bool TryLayout(System.Random rnd, bool strict)
        {
            var order = new List<RoomT>(rooms); Shuffle(order, rnd);
            var cand = new List<Link>();
            for (int s = 0; s < 9; s++)
            {
                int c = s % 3, r = s / 3;
                if (c < 2 && order[s].door.ContainsKey('E') && order[s + 1].door.ContainsKey('W'))
                    cand.Add(new Link { a = order[s], b = order[s + 1], sa = 'E', sb = 'W', horizontal = true });
                if (r < 2 && order[s].door.ContainsKey('S') && order[s + 3].door.ContainsKey('N'))
                    cand.Add(new Link { a = order[s], b = order[s + 3], sa = 'S', sb = 'N', horizontal = false });
            }
            Shuffle(cand, rnd);
            var parent = new Dictionary<RoomT, RoomT>(); foreach (var r in rooms) parent[r] = r;
            System.Func<RoomT, RoomT> find = null; find = x => parent[x] == x ? x : (parent[x] = find(parent[x]));
            var chosen = new List<Link>(); var rest = new List<Link>();
            foreach (var l in cand)
            {
                var ra = find(l.a); var rb = find(l.b);
                if (ra != rb) { parent[ra] = rb; chosen.Add(l); } else rest.Add(l);
            }
            if (chosen.Count != rooms.Count - 1) return false;
            // a few loops, so the maze has more than one way round (never into the generator room: that is only reachable through its ghost door)
            int extras = 99;  // every other neighbouring pair of rooms is joined too: loops everywhere
            foreach (var l in rest) { if (extras <= 0) break; if (l.Touches(roomGen)) continue; chosen.Add(l); extras--; }

            // the generator room must be a long way from the start
            var dist = new Dictionary<RoomT, int> { [roomLobby] = 0 }; var q = new Queue<RoomT>(); q.Enqueue(roomLobby);
            while (q.Count > 0)
            {
                var cur = q.Dequeue();
                foreach (var l in chosen) { var o = l.a == cur ? l.b : (l.b == cur ? l.a : null); if (o != null && !dist.ContainsKey(o)) { dist[o] = dist[cur] + 1; q.Enqueue(o); } }
            }
            if (!dist.ContainsKey(roomGen) || dist[roomGen] < (strict ? 4 : 3)) return false;

            // place every room somewhere inside its slot
            for (int s = 0; s < 9; s++)
            {
                var rm = order[s];
                int x0 = MARGIN + (s % 3) * (SW + GX), y0 = MARGIN + (s / 3) * (SH + GY);
                int ox = x0 + rnd.Next(0, SW - rm.W + 1), oy = y0 + rnd.Next(0, SH - rm.H + 1);
                rm.dx = ox - rm.x0; rm.dy = oy - rm.y0;
            }

            // kinds of door
            var plain = new List<Link>();
            foreach (var l in chosen)
            {
                if (l.Touches(roomGen)) { l.kind = 1; l.specialEnd = l.a == roomGen ? 'A' : 'B'; }
                else plain.Add(l);
            }
            if (plain.Count > 0) { var d = plain[rnd.Next(plain.Count)]; d.kind = 2; d.specialEnd = rnd.Next(2) == 0 ? 'A' : 'B'; }

            foreach (var l in chosen) if (!RoutePath(l, rnd)) return false;
            links.Clear(); links.AddRange(chosen);
            return true;
        }

        bool RoutePath(Link l, System.Random rnd)
        {
            var pa = l.a.Anchor(l.sa); var pb = l.b.Anchor(l.sb);
            var cells = new List<Vector2Int>(); var seen = new HashSet<Vector2Int>();
            System.Action<int, int> add = (x, y) => { var v = new Vector2Int(x, y); if (seen.Add(v)) cells.Add(v); };
            l.special.Clear();
            int ga = l.kind != 0 && l.specialEnd == 'A' ? 2 : 0, gb = l.kind != 0 && l.specialEnd == 'B' ? 2 : 0;
            if (l.horizontal)
            {
                var s0 = pa + Vector2Int.right; var sB = pb + Vector2Int.left;
                int lo = Mathf.Max(s0.x + ga, l.a.x1 + l.a.dx + 2), hi = Mathf.Min(sB.x - gb, l.b.x0 + l.b.dx - 2);
                if (lo > hi) return false;
                int xm = rnd.Next(lo, hi + 1);
                for (int x = s0.x; x <= xm; x++) add(x, s0.y);
                for (int y = s0.y; y != sB.y; y += sB.y > s0.y ? 1 : -1) add(xm, y);
                add(xm, sB.y);
                for (int x = xm; x <= sB.x; x++) add(x, sB.y);
                if (l.kind != 0) { if (l.specialEnd == 'A') { l.special.Add(s0); l.special.Add(s0 + Vector2Int.right); } else { l.special.Add(sB); l.special.Add(sB + Vector2Int.left); } }
            }
            else
            {
                var s0 = pa + Vector2Int.up; var sB = pb + Vector2Int.down;   // (Vector2Int.up is +y, which is +z in the world: down the grid)
                int lo = Mathf.Max(s0.y + ga, l.a.y1 + l.a.dy + 2), hi = Mathf.Min(sB.y - gb, l.b.y0 + l.b.dy - 2);
                if (lo > hi) return false;
                int ym = rnd.Next(lo, hi + 1);
                for (int y = s0.y; y <= ym; y++) add(s0.x, y);
                for (int x = s0.x; x != sB.x; x += sB.x > s0.x ? 1 : -1) add(x, ym);
                add(sB.x, ym);
                for (int y = ym; y <= sB.y; y++) add(sB.x, y);
                if (l.kind != 0) { if (l.specialEnd == 'A') { l.special.Add(s0); l.special.Add(s0 + Vector2Int.up); } else { l.special.Add(sB); l.special.Add(sB + Vector2Int.down); } }
            }
            // a passage may never touch a room's walls (that would cut a hole in them and leave the posters hanging in mid air):
            // it may only enter the wall ring of the two rooms it joins, in a straight line at their doors
            foreach (var c in cells)
                foreach (var r in rooms)
                {
                    if (c.x < r.x0 + r.dx - 1 || c.x > r.x1 + r.dx + 1 || c.y < r.y0 + r.dy - 1 || c.y > r.y1 + r.dy + 1) continue;
                    bool ok = false;
                    foreach (var pr in new[] { (l.a, pa), (l.b, pb) })
                        if (r == pr.Item1 && (c.x == pr.Item2.x || c.y == pr.Item2.y) && Mathf.Abs(c.x - pr.Item2.x) + Mathf.Abs(c.y - pr.Item2.y) <= 3) ok = true;
                    if (!ok) return false;
                }
            l.path.Clear(); l.path.AddRange(cells);
            return true;
        }

        // ---------------------------------------------------------------- build
        RoomT RoomAtTemplate(float wx, float wz)
        {
            RoomT best = null; float bd = 1e9f;
            foreach (var r in rooms) { if (r.virt) continue; float d = r.DistTpl(wx / C, wz / C); if (d < bd) { bd = d; best = r; } }
            return best;
        }

        void Maze()
        {
            MakeRooms();
            var rnd = new System.Random(System.Environment.TickCount ^ 0x2f6b9d1);
            bool ok = false;
            for (int attempt = 0; attempt < 3000 && !ok; attempt++) ok = TryLayout(rnd, attempt < 2000);
            if (!ok) Debug.LogError("Chapter 1: could not lay out the maze");

            // 1. everything the rooms contain moves with its room
            var kids = new List<Transform>();
            foreach (Transform t in w.root) kids.Add(t);
            foreach (var t in kids)
            {
                var r = RoomAtTemplate(t.position.x, t.position.z); if (r == null) continue;
                t.position += new Vector3(r.dx * C, 0, r.dy * C);
            }
            foreach (var it in interactables)
            {
                var r = RoomAtTemplate(it.pos.x, it.pos.z); if (r == null) continue;
                it.pos += new Vector3(r.dx * C, 0, r.dy * C);
                if (it.kind == "exit") exitPos = it.pos;
            }
            phonePos = exitPos + new Vector3(0.2f, 0, 0);
            foreach (var f in focusables)
            {
                var r = RoomAtTemplate(f.center.x, f.center.z); if (r == null) continue;
                f.center += new Vector3(r.dx * C, 0, r.dy * C);
                for (int i = 0; i < f.cells.Count; i++) f.cells[i] += new Vector2Int(r.dx, r.dy);
            }
            foreach (var F in fluoros) F.pos = F.l.transform.position;

            // 2. the new grid: rooms copied over, then the passages cut between them
            var tpl = (char[,])w.grid.Clone();
            int NW = MARGIN * 2 + 3 * SW + 2 * GX, NH = MARGIN * 2 + 3 * SH + 2 * GY;
            w.Init(NW, NH);
            foreach (var r in rooms)
            {
                if (r.virt) { w.Carve(r.dx, r.dy, r.dx + 7, r.dy + 4); continue; }
                for (int y = r.y0; y <= r.y1; y++) for (int x = r.x0; x <= r.x1; x++) w[x + r.dx, y + r.dy] = tpl[y, x];
            }
            foreach (var l in links)
            {
                foreach (var c in l.path) w[c.x, c.y] = '.';
                foreach (var c in l.special) w[c.x, c.y] = l.kind == 1 ? 'G' : 'D';
                if (l.kind == 2) dCells.AddRange(l.special);
            }

            // 3. the storage rooms (identical), the ghost doors, scratched walls, passage lamps
            BuildStorage(stor1); BuildStorage(stor2);
            foreach (var l in links) if (l.kind == 1) GhostTunnel(l);
            foreach (var l in links)
            {
                if (l.path.Count < 4) continue;
                PassageLamp(l.path[l.path.Count / 3]); PassageLamp(l.path[l.path.Count * 2 / 3]);
            }
            ScratchWalls(rnd);

            // 4. walls, floor, ceiling from the final grid
            var wallM = WallMat();
            w.CellBlocks("#", wallM, "wall");
            w.CellBlocks("T", Mats.Std(Color.white, 0.9f, 0, TraceTex(), Vector2.one, Resources.Load<Texture2D>("ThinkingIt/Textures/damaged_plaster_nor"), 0.8f), "trace", WALL_H, "traces");
            doorD = w.CellBlocks("D", Mats.Std(Color.white, 0.45f, 0.4f, DoorTex()), "wall", WALL_H, "powered door");
            w.CellBlocks("X", Mats.Std(Color.white, 0.45f, 0.4f, ExitDoorTex()), "exit", WALL_H, "stairwell door");
            var floorM = FloorMat(new Vector2(w.W * 0.75f, w.H * 0.75f));
            var ceilSet = TextureLibrary.Find(l => l.ceilingInterior);
            var ceilM = ceilSet != null ? Mats.Std(MathX.Hex(0xb8b4a8), 0.95f, 0, ceilSet.diff, new Vector2(w.W, w.H), ceilSet.nor, 0.5f)
                                        : Mats.Std(Color.white, 0.95f, 0, CeilTex(), new Vector2(w.W, w.H));
            w.FloorAndCeiling(floorM, ceilM);
            w.Skirting(Std(0x1a1a1a, 0.9f), "#TX");
            dust = new DustCloud(w.root, DustCloud.OverCells(w, 450), MathX.Hex(0xdfe6ee), 0.03f, 0.5f);
            Physics.SyncTransforms();
        }

        // ---------------------------------------------------------------- pieces
        // the "door that isn't there": a two-cell tunnel you have to stare into existence
        void GhostTunnel(Link l)
        {
            var a = l.special[0]; var b = l.special[1];
            float mx = (a.x + b.x + 1) * 0.5f * C, mz = (a.y + b.y + 1) * 0.5f * C;
            var gd = Grp("ghost door");
            foreach (var bb in new[] { (-0.85f, 1.3f, 0f, 0.15f, 2.6f, 2 * C), (0.85f, 1.3f, 0f, 0.15f, 2.6f, 2 * C), (0f, 2.65f, 0f, 1.85f, 0.15f, 2 * C) })
                Bx(bb.Item4, bb.Item5, bb.Item6, null, gd).At(bb.Item1, bb.Item2, bb.Item3);
            gd.At(mx, 0, mz);
            if (l.horizontal) gd.RotY(PI / 2);
            ghostDoors.Add(AddFocusable("door", gd, new[] { a, b }, new Vector3(mx, 1.4f, mz), 2.6f, Std(0x6a6e64)));
        }

        // a red emergency lamp on a wall beside a passage cell
        void PassageLamp(Vector2Int c)
        {
            float x, z, rot;
            if (w[c.x, c.y - 1] == '#') { x = cx(c.x); z = c.y * C + 0.06f; rot = 0; }
            else if (w[c.x, c.y + 1] == '#') { x = cx(c.x); z = (c.y + 1) * C - 0.06f; rot = PI; }
            else if (w[c.x - 1, c.y] == '#') { x = c.x * C + 0.06f; z = cx(c.y); rot = PI / 2; }
            else if (w[c.x + 1, c.y] == '#') { x = (c.x + 1) * C - 0.06f; z = cx(c.y); rot = -PI / 2; }
            else return;
            var gg = Grp("emergency lamp");
            Bx(0.32f, 0.2f, 0.1f, Std(0x2a2a2a, 0.5f), gg);
            var lm = Mats.Std(MathX.Hex(0x551010), 0.5f, 0, null, null, null, 1, MathX.Hex(0xff2010), 1, false, false);
            Sp(0.1f, 16, 8, lm, gg, 0, PI * 2, 0, PI / 2).At(0, 0, 0.05f).Rot(PI / 2, 0, 0);
            gg.At(x, 2.55f, z).RotY(rot);
            var lt = w.PointLight(new Vector3(x + Mathf.Sin(rot) * 0.45f, 2.35f, z + Mathf.Cos(rot) * 0.45f), MathX.Hex(0xff2a14), 0.7f, 8);
            emergencies.Add(new Emergency { l = lt, m = lm, phase = Random.value * 6 });
        }

        // a few wall blocks get the scratched bears
        void ScratchWalls(System.Random rnd)
        {
            var cands = new List<Vector2Int>();
            for (int y = 1; y < w.H - 1; y++) for (int x = 1; x < w.W - 1; x++)
                {
                    if (w[x, y] != '#') continue;
                    int open = 0; foreach (var d in new[] { Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down }) if (w[x + d.x, y + d.y] == '.') open++;
                    if (open == 1) cands.Add(new Vector2Int(x, y));
                }
            Shuffle(cands, rnd);
            for (int i = 0; i < Mathf.Min(6, cands.Count); i++) w[cands[i].x, cands[i].y] = 'T';
        }

        // The storage room. Both copies are built by the same code from the same coordinates, so they are identical.
        void BuildStorage(RoomT r)
        {
            int ox = r.dx, oy = r.dy;   // interior is 8 x 5 cells starting here
            var wood = Mats.Std(MathX.Hex(0x6a4a2a), 0.85f, 0); var card = Mats.Std(MathX.Hex(0x9a7a4a), 0.95f, 0); var iron = Mats.Std(MathX.Hex(0x6a6e70), 0.5f, 0.6f);
            // three shelving units along the north wall, loaded with boxes
            for (int i = 0; i < 3; i++)
            {
                var sh = Grp("shelf");
                foreach (var sx in new[] { -0.85f, 0.85f }) Bx(0.05f, 2.0f, 0.4f, iron, sh).At(sx, 1.0f, 0);
                for (int k = 0; k < 4; k++) Bx(1.75f, 0.04f, 0.4f, wood, sh).At(0, 0.25f + k * 0.5f, 0);
                for (int k = 0; k < 4; k++) { Bx(0.5f, 0.3f, 0.32f, card, sh).At(-0.45f + (k % 2) * 0.1f, 0.42f + k * 0.5f, 0); Bx(0.35f, 0.22f, 0.3f, card, sh).At(0.4f, 0.38f + k * 0.5f, 0); }
                sh.At(cx(ox + i), 0, oy * C + 0.25f); Prop(sh);
            }
            // a stack of crates in the south-east corner
            var st = Grp("crates");
            Bx(0.9f, 0.7f, 0.9f, wood, st).At(0, 0.35f, 0); Bx(0.8f, 0.6f, 0.8f, wood, st).At(0.05f, 1.0f, 0.02f).RotY(0.3f); Bx(0.8f, 0.6f, 0.8f, wood, st).At(-1.0f, 0.3f, 0.2f).RotY(-0.2f);
            st.At(cx(ox + 7) - 0.3f, 0, cx(oy + 4) - 0.2f); Prop(st);
            // oil barrels in the north-east corner
            foreach (var b in new[] { (-0.4f, -0.4f, 0x2a4a6a), (-0.5f, 0.35f, 0x6a2a1a) })
            { var br = Cy(0.3f, 0.3f, 0.9f, 18, Mats.Std(MathX.Hex(b.Item3), 0.5f, 0.4f)).At(cx(ox + 7) + b.Item1, 0.45f, cx(oy) + b.Item2); Prop(br); }
            // a pallet of boxes in the south-west
            var pal = Grp("pallet");
            Bx(1.2f, 0.12f, 1.0f, wood, pal).At(0, 0.06f, 0);
            for (int k = 0; k < 4; k++) Bx(0.5f, 0.4f, 0.45f, card, pal).At(-0.28f + (k % 2) * 0.56f, 0.32f + (k / 2) * 0.4f, 0).RotY(0.1f * k);
            pal.At(cx(ox + 1), 0, cx(oy + 4) - 0.1f); Prop(pal);
            // a workbench with a vice and a dead lamp, against the south wall
            var bench = Grp("bench");
            Bx(1.8f, 0.06f, 0.7f, wood, bench).At(0, 0.9f, 0);
            foreach (var sx in new[] { -0.8f, 0.8f }) Bx(0.08f, 0.9f, 0.6f, iron, bench).At(sx, 0.45f, 0);
            Bx(0.2f, 0.12f, 0.2f, iron, bench).At(0.6f, 0.99f, 0); Bx(0.04f, 0.5f, 0.04f, iron, bench).At(-0.5f, 1.2f, -0.1f);
            bench.At(cx(ox + 5) + 0.3f, 0, (oy + 5) * C - 0.45f); Prop(bench);
            fluoroFn(ox + 3, oy + 2, 1.3f, 10, false);
            fluoroFn(ox + 6, oy + 2, 1.0f, 9, false);
        }

        // ---------------------------------------------------------------- zones
        string ZoneAt(float wx, float wz)
        {
            int x = Mathf.FloorToInt(wx / C), y = Mathf.FloorToInt(wz / C);
            foreach (var r in rooms) if (r.Inside(x, y)) return r.name;
            return "passage";
        }
    }
}
