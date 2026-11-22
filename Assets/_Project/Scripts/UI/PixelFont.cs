using System.Collections.Generic;
using UnityEngine;

namespace Game.UI
{
    /// <summary>
    /// Our own old-console pixel fonts, drawn in code (no font files):
    ///   - Small: 5x7 capitals, digits and punctuation (score, briefings).
    ///   - Timer: thin 8x12 digits with ' and " for the phosphor-green countdown.
    /// Text becomes a tiny texture that is scaled up with point filtering, see <see cref="PixelLabel"/>.
    /// </summary>
    public sealed class PixelFont
    {
        private readonly int _height, _gap, _lineGap, _space;
        private readonly Dictionary<char, bool[,]> _glyphs = new Dictionary<char, bool[,]>();
        private readonly bool _glow;

        public static readonly PixelFont Small = MakeSmall();
        public static readonly PixelFont Timer = MakeTimer();

        private PixelFont(int height, int gap, int lineGap, int space, bool glow)
        {
            _height = height;
            _gap = gap;
            _lineGap = lineGap;
            _space = space;
            _glow = glow;
        }

        private int LineWidth(string line)
        {
            int w = 0;
            foreach (char c in line) w += (_glyphs.TryGetValue(c, out var g) ? g.GetLength(1) : _space) + _gap;
            return Mathf.Max(0, w - _gap);
        }

        /// <summary>
        /// Draws <paramref name="text"/> into <paramref name="tex"/> (created / resized as needed), white + outline.
        /// align: 0 = left, 0.5 = centre, 1 = right.
        /// </summary>
        public void Draw(string text, float align, ref Texture2D tex)
        {
            string[] lines = text.ToUpperInvariant().Split('\n');
            int width = 1;
            foreach (string l in lines) width = Mathf.Max(width, LineWidth(l));
            width += 2;
            int height = lines.Length * (_height + _lineGap) - _lineGap + 2;

            if (tex == null)
                tex = new Texture2D(width, height, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            else if (tex.width != width || tex.height != height)
                tex.Reinitialize(width, height);

            var on = new bool[width, height];
            int top = height - 2; // texture rows go bottom-up
            foreach (string line in lines)
            {
                int x = 1 + Mathf.RoundToInt((width - 2 - LineWidth(line)) * align);
                foreach (char c in line)
                {
                    if (!_glyphs.TryGetValue(c, out var g))
                    {
                        x += _space + _gap;
                        continue;
                    }
                    for (int r = 0; r < g.GetLength(0); r++)
                    for (int col = 0; col < g.GetLength(1); col++)
                        if (g[r, col]) on[x + col, top - r] = true;
                    x += g.GetLength(1) + _gap;
                }
                top -= _height + _lineGap;
            }

            // Timer: dim phosphor halo all round. Small text: dark drop shadow, bottom-right.
            var halo = new Color32(90, 90, 90, 190);
            var shadow = new Color32(0, 0, 0, 220);
            var clear = new Color32(0, 0, 0, 0);
            var white = new Color32(255, 255, 255, 255);
            var pixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                Color32 c = clear;
                if (on[x, y]) c = white;
                else if (_glow)
                {
                    if ((x > 0 && on[x - 1, y]) || (x < width - 1 && on[x + 1, y]) ||
                        (y > 0 && on[x, y - 1]) || (y < height - 1 && on[x, y + 1])) c = halo;
                }
                else if (x > 0 && y < height - 1 && on[x - 1, y + 1]) c = shadow;
                pixels[y * width + x] = c;
            }
            tex.SetPixels32(pixels);
            tex.Apply(false);
        }

        // ---------- Glyphs ----------

        /// <summary>5x7 glyphs: 7 hex bytes, top row first, bit 4 = left column.</summary>
        private static PixelFont MakeSmall()
        {
            var f = new PixelFont(7, 1, 3, 3, false);
            string[] data =
            {
                "A0E11111F111111", "B1E11111E11111E", "C0E11101010110E", "D1E11111111111E", "E1F10101E10101F",
                "F1F10101E101010", "G0E11101711110F", "H1111111F111111", "I0E04040404040E", "J0702020202120C",
                "K11121418141211", "L1010101010101F", "M111B1515111111", "N11111915131111", "O0E11111111110E",
                "P1E11111E101010", "Q0E11111115120D", "R1E11111E141211", "S0F10100E01011E", "T1F040404040404",
                "U1111111111110E", "V11111111110A04", "W1111111515150A", "X11110A040A1111", "Y1111110A040404",
                "Z1F01020408101F",
                "00E11131519110E", "1040C040404040E", "20E11010204081F", "31F02040201110E", "402060A121F0202",
                "51F101E0101110E", "60608101E11110E", "71F010204080808", "80E11110E11110E", "90E11110F01020C",
                ":000C0C000C0C00", ".00000000000C0C", ",000000000C0408", "!04040404040004", "?0E110102040004",
                "-0000001F000000", "+0004041F040400", "/00010204081000", "'0C040800000000", "\"0A0A0A00000000",
                ">08040201020408", "%18190204081303", "(02040808080402", ")08040202020408", "=00001F001F0000",
                "&0C12140815120D",
            };
            foreach (string entry in data)
            {
                string hex = entry.Substring(1);
                var g = new bool[7, 5];
                for (int r = 0; r < 7; r++)
                {
                    int bits = System.Convert.ToInt32(hex.Substring(r * 2, 2), 16);
                    for (int c = 0; c < 5; c++) g[r, c] = (bits & (16 >> c)) != 0;
                }
                f._glyphs[entry[0]] = g;
            }
            return f;
        }

        private static PixelFont MakeTimer()
        {
            var f = new PixelFont(12, 4, 2, 4, true);
            f.Add('0', "..####..", ".#....#.", "#......#", "#......#", "#......#", "#......#", "#......#", "#......#", "#......#", "#......#", ".#....#.", "..####..");
            f.Add('1', "....#...", "...##...", "..#.#...", ".#..#...", "....#...", "....#...", "....#...", "....#...", "....#...", "....#...", "....#...", ".#######");
            f.Add('2', "..####..", ".#....#.", "#......#", ".......#", ".......#", "......#.", ".....#..", "....#...", "...#....", "..#.....", ".#......", "########");
            f.Add('3', "..####..", ".#....#.", "#......#", ".......#", "......#.", "...###..", "......#.", ".......#", ".......#", "#......#", ".#....#.", "..####..");
            f.Add('4', ".....#..", "....##..", "...#.#..", "...#.#..", "..#..#..", ".#...#..", ".#...#..", "#....#..", "########", ".....#..", ".....#..", ".....#..");
            f.Add('5', ".#######", ".#......", ".#......", ".#......", "#.####..", "##....#.", ".......#", ".......#", ".......#", "#......#", ".#....#.", "..####..");
            f.Add('6', "...###..", "..#...#.", ".#......", "#.......", "#.####..", "##....#.", "#......#", "#......#", "#......#", "#......#", ".#....#.", "..####..");
            f.Add('7', "########", "#......#", ".......#", "......#.", "......#.", ".....#..", ".....#..", "....#...", "....#...", "...#....", "...#....", "...#....");
            f.Add('8', "..####..", ".#....#.", "#......#", "#......#", ".#....#.", "..####..", ".#....#.", "#......#", "#......#", "#......#", ".#....#.", "..####..");
            f.Add('9', "..####..", ".#....#.", "#......#", "#......#", "#......#", "#......#", ".#....##", "..####.#", ".......#", "......#.", ".#...#..", "..###...");
            f.Add('\'', "..##..", "..##..", "...#..", "..#...", "......", "......", "......", "......", "......", "......", "......", "......");
            f.Add('"', ".##..##.", ".##..##.", "..#...#.", ".#...#..", "........", "........", "........", "........", "........", "........", "........", "........");
            return f;
        }

        private void Add(char c, params string[] rows)
        {
            var g = new bool[rows.Length, rows[0].Length];
            for (int r = 0; r < rows.Length; r++)
            for (int x = 0; x < rows[r].Length; x++)
                g[r, x] = rows[r][x] == '#';
            _glyphs[c] = g;
        }
    }
}
