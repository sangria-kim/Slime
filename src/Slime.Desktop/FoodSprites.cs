using Slime.Core;

namespace Slime.Desktop;

internal static class FoodSprites
{
    // Original item sprites: hard pixel edges, dark contours, bright highlights.
    private static readonly string[][] Pixels =
    [
        ["................", ".......OO.......", ".......OB.OGGO..", ".......OBGGGGO..",
         "....OOOBOOOO....", "...ORRRRRRRRO...", "..ORHHHRRRRRRO..", "..ORHHRRRRRRRO..",
         "..ORRRRRRRRRRO..", "..ORRRRRRRDDRO..", "...ORRRRRDDRO...", "...ORRRRDDDO....",
         "....ORRDRRO.....", ".....OOOOO......", "................", "................"],
        ["................", "................", ".........OO.....", ".......OOYYO....",
         ".....OOYYYYYO...", "...OOYHHHYYYYO..", "..OYYYYYYBYYYYO.", "..OYYYYYYYYYYYYO",
         "..OHHYYYYYYYBBYO", "..OYYYYBBYYYYYYO", "..OYYYYBBYYYYYYO", "..OYYBYYYYYYYYYO",
         "..OBBBBBBBBBBBBO", "...OOOOOOOOOOOO.", "................", "................"],
        ["................", "..........OO.OO.", ".........OWWOWWO", "......OOOOWWWWO.",
         "....OORRROWWWO..", "...ORHHHRROOO...", "..ORHHHRRRRO....", "..ORRRRRRRRRO...",
         "..ORRRRRDDDRRO..", "..ORRRDDDDDRRO..", "...ORRDDDDRRO...", "....ORDDDRRO....",
         ".....OOOOOO.....", "................", "................", "................"],
        ["................", ".......OGO......", "......ORRRO.....", ".......OOO......",
         "....OOOOOOOO....", "...OWWHWWWHWO...", "..OWWWWWWWWWWO..", "..OPPPPPPPPPPO..",
         "..OYYYYYYYYYYO..", "..OHHHHHHHHHHO..", "..OPPPPPPPPPPO..", "..OYYYYYYYYYYO..",
         "..OBBBBBBBBBBO..", "...OOOOOOOOOO...", "................", "................"],
        ["................", ".....OOOOOO.....", "...OOPPPPPPOO...", "..OPHPPHPPPPPO..",
         ".OPPPPPPPHPPPPO.", ".OPPPOOOOPPPPPO.", "OPHPO....OPPPBBO", "OPPPO....OPHPBBO",
         "OPPPO....OPPPBBO", ".OPPPOOOOPPPBBO.", ".OBPPHPPPPPBBO..", "..OBBPPPPBBBO...",
         "...OBBBBBBOO....", ".....OOOOO......", "................", "................"]
    ];
    public static string Name(FoodKind kind) => kind switch
    {
        FoodKind.Apple => "사과", FoodKind.Cheese => "치즈", FoodKind.Meat => "고기",
        FoodKind.Cake => "케이크", _ => "도넛"
    };
    public static void Draw(Graphics graphics, FoodKind kind, int x, int y, int scale)
    {
        var pixels = Pixels[(int)kind];
        for (var row = 0; row < pixels.Length; row++)
            for (var column = 0; column < pixels[row].Length; column++)
            {
                var color = pixels[row][column] switch
                {
                    'O' => Color.FromArgb(63, 37, 37), 'B' => Color.FromArgb(174, 108, 43),
                    'R' => Color.FromArgb(246, 81, 72), 'D' => Color.FromArgb(181, 42, 60),
                    'G' => Color.FromArgb(104, 196, 58), 'Y' => Color.FromArgb(255, 206, 78),
                    'H' => Color.FromArgb(255, 240, 167), 'W' => Color.FromArgb(255, 250, 232),
                    'P' => Color.FromArgb(255, 151, 188), _ => Color.Transparent
                };
                if (color.A == 0) continue;
                using var brush = new SolidBrush(color);
                graphics.FillRectangle(brush, x + column * scale, y + row * scale, scale, scale);
            }
    }
}
