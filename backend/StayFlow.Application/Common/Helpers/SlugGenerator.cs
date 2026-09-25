using System.Text;

namespace StayFlow.Application.Common.Helpers;

/// <summary>
/// "Güneş Butik Otel" -> "gunes-butik-otel". Otelin URL'lerde (ileride mobil uygulama,
/// public sayfa) kullanilacak okunabilir kimligi.
/// </summary>
public static class SlugGenerator
{
    private static readonly Dictionary<char, string> TurkishMap = new()
    {
        ['ç'] = "c", ['ğ'] = "g", ['ı'] = "i", ['ö'] = "o", ['ş'] = "s", ['ü'] = "u",
        ['Ç'] = "c", ['Ğ'] = "g", ['İ'] = "i", ['Ö'] = "o", ['Ş'] = "s", ['Ü'] = "u"
    };

    public static string Generate(string text)
    {
        var builder = new StringBuilder();
        var lastWasDash = true; // basta tire olusmasin

        foreach (var ch in text.Trim())
        {
            if (TurkishMap.TryGetValue(ch, out var replacement))
            {
                builder.Append(replacement);
                lastWasDash = false;
            }
            else if (char.IsAsciiLetterOrDigit(ch))
            {
                builder.Append(char.ToLowerInvariant(ch));
                lastWasDash = false;
            }
            else if (!lastWasDash)
            {
                builder.Append('-');
                lastWasDash = true;
            }
        }

        return builder.ToString().TrimEnd('-');
    }
}
