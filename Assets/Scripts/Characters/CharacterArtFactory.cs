using UnityEngine;

namespace Woken.Characters
{
    /// <summary>Creates small, readable four-direction pixel sprites at runtime.</summary>
    public static class CharacterArtFactory
    {
        public enum Facing { Down, Up, Left, Right }
        private static readonly Color Outline = Hex("263640");
        private static readonly Color Skin = Hex("E9BD91");
        private static readonly Color Hair = Hex("473B38");
        private static readonly Color Coat = Hex("D48750");
        private static readonly Color Bag = Hex("617C86");
        private static readonly Color Robot = Hex("77AEB0");
        private static readonly Color RobotDark = Hex("29444B");
        private static readonly Color Glow = Hex("9CE8DE");
        private static Sprite[,] playerSprites;
        private static Sprite[,] robotSprites;

        public static Sprite GetPlayer(Facing facing, int frame) => Get(ref playerSprites, false, facing, frame);
        public static Sprite GetRobot(Facing facing, int frame) => Get(ref robotSprites, true, facing, frame);

        private static Sprite Get(ref Sprite[,] cache, bool robot, Facing facing, int frame)
        {
            if (cache == null) cache = new Sprite[4, 2];
            int d = (int)facing;
            int f = frame & 1;
            if (cache[d, f] != null) return cache[d, f];
            var texture = new Texture2D(64, 64, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, name = robot ? "Companion robot sprite" : "Player sprite" };
            var pixels = new Color[64 * 64];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.clear;
            if (robot) DrawRobot(pixels, (Facing)d, f); else DrawPlayer(pixels, (Facing)d, f);
            texture.SetPixels(pixels);
            texture.Apply(false, true);
            cache[d, f] = Sprite.Create(texture, new Rect(0, 0, 64, 64), new Vector2(.5f, .46f), 64);
            cache[d, f].name = (robot ? "Robot_" : "Player_") + (Facing)d + "_" + f;
            return cache[d, f];
        }

        private static void DrawPlayer(Color[] p, Facing d, int frame)
        {
            int stride = frame == 0 ? -2 : 2;
            RoundedRect(p, 19 + stride, 5, 11, 20, 4, Outline); RoundedRect(p, 34 - stride, 5, 11, 20, 4, Outline);
            RoundedRect(p, 21 + stride, 7, 7, 16, 3, Hex("52616B")); RoundedRect(p, 36 - stride, 7, 7, 16, 3, Hex("52616B"));
            RoundedRect(p, 18 + stride, 3, 12, 6, 3, Hex("303A43")); RoundedRect(p, 34 - stride, 3, 12, 6, 3, Hex("303A43"));
            if (d == Facing.Up) { RoundedRect(p,22,24,20,20,5,Outline); RoundedRect(p,24,26,16,16,4,Bag); RoundedRect(p,26,29,12,7,2,Hex("526E78")); Rect(p,29,36,6,2,Hex("D7BF85")); }
            else if (d == Facing.Left) { RoundedRect(p,43,24,13,18,4,Outline); RoundedRect(p,45,26,9,14,3,Bag); Rect(p,45,31,8,2,Hex("91A8AA")); }
            else if (d == Facing.Right) { RoundedRect(p,8,24,13,18,4,Outline); RoundedRect(p,10,26,9,14,3,Bag); Rect(p,11,31,8,2,Hex("91A8AA")); }
            RoundedRect(p,27,39,10,8,3,Outline); RoundedRect(p,29,40,6,6,2,Skin);
            RoundedRect(p,16,20,32,27,8,Outline); RoundedRect(p,19,22,26,22,6,Coat);
            RoundedRect(p,9,23,10,19,4,Outline); RoundedRect(p,11,25,6,15,3,Coat);
            RoundedRect(p,45,23,10,19,4,Outline); RoundedRect(p,47,25,6,15,3,Coat); Circle(p,14,22,4,Skin); Circle(p,50,22,4,Skin);
            if (d == Facing.Down) { Rect(p,22,29,3,13,Bag); Rect(p,39,29,3,13,Bag); Rect(p,31,24,2,16,Hex("F1C39A")); Circle(p,32,33,2,Outline); Circle(p,32,27,2,Outline); DrawFrontHead(p,32,51,false); }
            else if (d == Facing.Up) { DrawBackPack(p,32,32); Rect(p,22,27,3,12,Hex("91A8AA")); Rect(p,39,27,3,12,Hex("91A8AA")); DrawFrontHead(p,32,51,true); }
            else if (d == Facing.Left) { DrawSidePack(p,47,32); Rect(p,26,28,3,12,Bag); DrawSideHead(p,28,51,true); }
            else { DrawSidePack(p,17,32); Rect(p,35,28,3,12,Bag); DrawSideHead(p,36,51,false); }
        }
        private static void DrawBackPack(Color[] p,int cx,int cy)
        {
            RoundedRect(p,cx-9,cy-8,18,19,4,Outline); RoundedRect(p,cx-7,cy-6,14,15,3,Bag);
            RoundedRect(p,cx-6,cy-3,12,6,2,Hex("526E78")); Rect(p,cx-3,cy+3,6,2,Hex("D7BF85"));
        }
        private static void DrawSidePack(Color[] p,int cx,int cy)
        {
            RoundedRect(p,cx-6,cy-8,12,17,4,Outline); RoundedRect(p,cx-4,cy-6,8,13,3,Bag);
            Rect(p,cx-3,cy-1,6,2,Hex("91A8AA")); Rect(p,cx-2,cy+3,4,2,Hex("D7BF85"));
        }
        private static void DrawFrontHead(Color[] p,int cx,int cy,bool backView)
        {
            Ellipse(p,cx,cy,11,11,Outline);
            if(backView){Ellipse(p,cx,cy,9,9,Hair); RoundedRect(p,cx-9,cy+1,18,8,4,Hair); Rect(p,cx-5,cy-2,10,3,Hex("5A4943"));}
            else{Ellipse(p,cx,cy-1,9,9,Skin); RoundedRect(p,cx-10,cy+2,20,7,4,Hair); RoundedRect(p,cx-8,cy+5,16,4,3,Hair); Circle(p,cx-4,cy-1,1,Outline); Circle(p,cx+4,cy-1,1,Outline); Rect(p,cx-1,cy-4,2,2,Hex("A56B57")); Rect(p,cx-2,cy-6,4,1,Hex("A56B57"));}
        }
        private static void DrawSideHead(Color[] p,int cx,int cy,bool facingLeft)
        {
            Ellipse(p,cx,cy,11,11,Outline); Ellipse(p,cx,cy-1,9,9,Skin); RoundedRect(p,cx-9,cy+2,18,7,4,Hair); RoundedRect(p,cx-8,cy+5,15,4,3,Hair);
            int eyeX=facingLeft?cx-5:cx+5; Circle(p,eyeX,cy-1,1,Outline); int noseX=facingLeft?cx-10:cx+10; Rect(p,noseX,cy-4,3,2,Skin);
        }
        private static void DrawRobot(Color[] p, Facing d, int frame)
        {
            int bob = frame == 0 ? 0 : 1;
            // Wheels, broad bumper, and compact turquoise chassis.
            RoundedRect(p, 9, 17 + bob, 46, 31, 9, Outline);
            RoundedRect(p, 13, 19 + bob, 38, 27, 7, Robot);
            Rect(p, 8, 25 + bob, 5, 13, Hex("344953")); Rect(p, 51, 25 + bob, 5, 13, Hex("344953"));
            Circle(p, 10, 31 + bob, 3, Hex("F0BD54")); Circle(p, 54, 31 + bob, 3, Hex("F0BD54"));
            // Screen orientation changes with travel direction.
            switch (d)
            {
                case Facing.Down: RoundedRect(p, 19, 20 + bob, 26, 16, 4, RobotDark); Circle(p, 26, 28 + bob, 3, Glow); Circle(p, 38, 28 + bob, 3, Glow); Rect(p, 26, 23 + bob, 12, 1, Hex("D4E5D9")); Rect(p, 27, 16 + bob, 10, 3, Hex("E1B454")); break;
                case Facing.Up: RoundedRect(p, 19, 28 + bob, 26, 16, 4, RobotDark); Circle(p, 26, 36 + bob, 3, Glow); Circle(p, 38, 36 + bob, 3, Glow); Rect(p, 26, 31 + bob, 12, 1, Hex("D4E5D9")); Rect(p, 27, 45 + bob, 10, 3, Hex("E1B454")); break;
                case Facing.Left: RoundedRect(p, 15, 23 + bob, 20, 18, 4, RobotDark); Circle(p, 22, 32 + bob, 3, Glow); Circle(p, 29, 32 + bob, 3, Glow); Rect(p, 36, 27 + bob, 4, 10, Hex("E1B454")); break;
                case Facing.Right: RoundedRect(p, 29, 23 + bob, 20, 18, 4, RobotDark); Circle(p, 36, 32 + bob, 3, Glow); Circle(p, 43, 32 + bob, 3, Glow); Rect(p, 24, 27 + bob, 4, 10, Hex("E1B454")); break;
            }
            // Lidar scanner and simple scan arcs.
            Rect(p, 30, 47 + bob, 4, 6, RobotDark); Circle(p, 32, 55 + bob, 5, Hex("F0BD54")); Circle(p, 32, 55 + bob, 2, Color.white);
        }

        private static void Circle(Color[] p, int cx, int cy, int r, Color c) => Ellipse(p, cx, cy, r, r, c);
        private static void Ellipse(Color[] p, int cx, int cy, int rx, int ry, Color c)
        {
            int x0 = Mathf.Max(0, cx - rx), x1 = Mathf.Min(63, cx + rx), y0 = Mathf.Max(0, cy - ry), y1 = Mathf.Min(63, cy + ry);
            for (int y = y0; y <= y1; y++) for (int x = x0; x <= x1; x++)
            { float dx = (x - cx) / (float)rx, dy = (y - cy) / (float)ry; if (dx * dx + dy * dy <= 1f) p[y * 64 + x] = c; }
        }
        private static void Rect(Color[] p, int x, int y, int w, int h, Color c)
        { for (int yy = Mathf.Max(0, y); yy < Mathf.Min(64, y + h); yy++) for (int xx = Mathf.Max(0, x); xx < Mathf.Min(64, x + w); xx++) p[yy * 64 + xx] = c; }
        private static void RoundedRect(Color[] p, int x, int y, int w, int h, int r, Color c)
        {
            for (int yy = y; yy < y + h; yy++) for (int xx = x; xx < x + w; xx++)
            { int qx = xx < x + r ? x + r : (xx >= x + w - r ? x + w - r - 1 : xx); int qy = yy < y + r ? y + r : (yy >= y + h - r ? y + h - r - 1 : yy); if ((xx - qx) * (xx - qx) + (yy - qy) * (yy - qy) <= r * r) Put(p, xx, yy, c); }
        }
        private static void Triangle(Color[] p, int x1, int y1, int x2, int y2, int x3, int y3, Color c)
        {
            int minX = Mathf.Min(x1, Mathf.Min(x2, x3)), maxX = Mathf.Max(x1, Mathf.Max(x2, x3)); int minY = Mathf.Min(y1, Mathf.Min(y2, y3)), maxY = Mathf.Max(y1, Mathf.Max(y2, y3));
            float area = Edge(x1,y1,x2,y2,x3,y3); if (Mathf.Approximately(area, 0)) return;
            for (int y = minY; y <= maxY; y++) for (int x = minX; x <= maxX; x++) { float a = Edge(x,y,x2,y2,x3,y3)/area, b = Edge(x1,y1,x,y,x3,y3)/area, d = Edge(x1,y1,x2,y2,x,y)/area; if(a>=0&&b>=0&&d>=0) Put(p,x,y,c); }
        }
        private static float Edge(int ax,int ay,int bx,int by,int px,int py) => (px-ax)*(by-ay)-(py-ay)*(bx-ax);
        private static void Put(Color[] p, int x, int y, Color c) { if (x >= 0 && x < 64 && y >= 0 && y < 64) p[y * 64 + x] = c; }
        private static Color Hex(string h) { ColorUtility.TryParseHtmlString("#" + h, out var c); return c; }
    }
}





