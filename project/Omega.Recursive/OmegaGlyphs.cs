using System.Text;

namespace Omega.Recursive;

public static class OmegaGlyphs
{
    public static string EncodeTraitName(string traitName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(traitName);

        var result = new StringBuilder(traitName.Length);
        foreach (char value in traitName.ToUpperInvariant())
        {
            string? glyph = value switch
            {
                'A' => "ᚨ",
                'B' => "ᛒ",
                'C' => "ᚲ",
                'D' => "ᛞ",
                'E' => "ᛖ",
                'F' => "ᚠ",
                'G' => "ᚷ",
                'H' => "ᚺ",
                'I' => "ᛁ",
                'J' => "ᛃ",
                'K' => "ᚲ",
                'L' => "ᛚ",
                'M' => "ᛗ",
                'N' => "ᚾ",
                'O' => "ᛟ",
                'P' => "ᛈ",
                'Q' => "ᛩ",
                'R' => "ᚱ",
                'S' => "ᛋ",
                'T' => "ᛏ",
                'U' => "ᚢ",
                'V' => "ᚡ",
                'W' => "ᚹ",
                'X' => "ᛪ",
                'Y' => "ᛦ",
                'Z' => "ᛉ",
                '.' => "•",
                '_' => "—",
                _ => null
            };

            if (glyph is not null)
                result.Append(glyph);
        }

        return result.ToString();
    }
}
