using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using MusicPlay.Web.Models;

namespace MusicPlay.Web.Services;

public interface IMatchingEngine
{
    TrackMatchResult EvaluateMatch(TrackModel sourceTrack, TrackModel candidateTrack);
    TrackMatchResult FindBestMatch(TrackModel sourceTrack, IEnumerable<TrackModel> candidates);
    string CleanText(string input);
}

public partial class MatchingEngine : IMatchingEngine
{
    // Captura qualquer parênteses contendo palavras-chave como remaster, live, video, feat, etc. (ex: "(2013 Remaster)", "(Live at Wembley)")
    [GeneratedRegex(@"\([^)]*?\b(feat\.?|ft\.?|with|remaster(ed)?|remix(ed)?|official|video|audio|live|deluxe|bonus|edit|version|mono|stereo)\b[^)]*?\)", RegexOptions.IgnoreCase)]
    private static partial Regex ParenthesesNoiseRegex();

    // Captura qualquer colchetes contendo palavras-chave
    [GeneratedRegex(@"\[[^\]]*?\b(feat\.?|ft\.?|with|remaster(ed)?|remix(ed)?|official|video|audio|live|deluxe|bonus|edit|version|mono|stereo)\b[^\]]*?\]", RegexOptions.IgnoreCase)]
    private static partial Regex BracketsNoiseRegex();

    // Captura sufixos após hífen como "- 2013 Remaster", "- Remastered 2011", "- Live"
    [GeneratedRegex(@"\s*-\s*(\d{4}\s+)?(remaster(ed)?|live|radio edit|deluxe|mono|stereo|official|bonus).*$", RegexOptions.IgnoreCase)]
    private static partial Regex HyphenNoiseRegex();

    // Captura participações "feat. X" fora de parênteses
    [GeneratedRegex(@"\b(feat\.?|ft\.?)\s+[^\-]+", RegexOptions.IgnoreCase)]
    private static partial Regex FeatTrailingRegex();

    [GeneratedRegex(@"[^\w\s]")]
    private static partial Regex NonWordRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex MultiSpaceRegex();

    public TrackMatchResult FindBestMatch(TrackModel sourceTrack, IEnumerable<TrackModel> candidates)
    {
        TrackMatchResult? bestResult = null;

        foreach (var candidate in candidates)
        {
            var result = EvaluateMatch(sourceTrack, candidate);
            if (result.MatchType == TrackMatchType.ExactIsrc)
            {
                return result; // Melhor resultado absoluto possível
            }

            if (bestResult == null || result.ConfidenceScore > bestResult.ConfidenceScore)
            {
                bestResult = result;
            }
        }

        if (bestResult == null || bestResult.ConfidenceScore < 0.55)
        {
            return new TrackMatchResult
            {
                SourceTrack = sourceTrack,
                MatchedTrack = null,
                ConfidenceScore = bestResult?.ConfidenceScore ?? 0.0,
                MatchType = TrackMatchType.NotFound,
                Notes = "Nenhuma correspondência compatível encontrada."
            };
        }

        return bestResult;
    }

    public TrackMatchResult EvaluateMatch(TrackModel sourceTrack, TrackModel candidateTrack)
    {
        // 1. Verificação de ISRC exato
        if (!string.IsNullOrWhiteSpace(sourceTrack.Isrc) &&
            !string.IsNullOrWhiteSpace(candidateTrack.Isrc) &&
            string.Equals(sourceTrack.Isrc.Trim(), candidateTrack.Isrc.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return new TrackMatchResult
            {
                SourceTrack = sourceTrack,
                MatchedTrack = candidateTrack,
                ConfidenceScore = 1.0,
                MatchType = TrackMatchType.ExactIsrc,
                Notes = $"Correspondência exata por código ISRC ({sourceTrack.Isrc.ToUpperInvariant()})"
            };
        }

        // 2. Limpeza e normalização
        var sourceTitleClean = CleanText(sourceTrack.Title);
        var candTitleClean = CleanText(candidateTrack.Title);

        var sourceArtistClean = CleanText(sourceTrack.Artist);
        var candArtistClean = CleanText(candidateTrack.Artist);

        // 3. Similaridade de Título (Levenshtein + Jaccard)
        double titleLev = CalculateLevenshteinSimilarity(sourceTitleClean, candTitleClean);
        double titleJaccard = CalculateJaccardSimilarity(sourceTitleClean, candTitleClean);
        double titleScore = Math.Max(titleLev, (titleLev + titleJaccard) / 2.0);

        // 4. Similaridade de Artista
        double artistLev = CalculateLevenshteinSimilarity(sourceArtistClean, candArtistClean);
        double artistJaccard = CalculateJaccardSimilarity(sourceArtistClean, candArtistClean);
        double artistScore = Math.Max(artistLev, (artistLev + artistJaccard) / 2.0);

        // Se o artista for substring um do outro (ex: "Queen" contido em "Queen feat. David Bowie")
        if (candArtistClean.Contains(sourceArtistClean) || sourceArtistClean.Contains(candArtistClean))
        {
            artistScore = Math.Max(artistScore, 0.90);
        }

        // 5. Pontuação combinada (Título tem peso 60%, Artista 40%)
        double combinedScore = (titleScore * 0.60) + (artistScore * 0.40);

        // 6. Tolerância de Duração (se ambas as durações estiverem preenchidas)
        if (sourceTrack.DurationMs > 0 && candidateTrack.DurationMs > 0)
        {
            int deltaSec = Math.Abs(sourceTrack.DurationMs - candidateTrack.DurationMs) / 1000;
            if (deltaSec <= 5)
            {
                combinedScore = Math.Min(1.0, combinedScore + 0.05);
            }
            else if (deltaSec <= 15)
            {
                // Tolerável
            }
            else if (deltaSec <= 40)
            {
                combinedScore -= 0.10;
            }
            else
            {
                combinedScore -= 0.30;
            }
        }

        combinedScore = Math.Clamp(combinedScore, 0.0, 1.0);

        var matchType = combinedScore switch
        {
            >= 0.85 => TrackMatchType.HighFuzzy,
            >= 0.65 => TrackMatchType.MediumFuzzy,
            >= 0.50 => TrackMatchType.LowConfidence,
            _ => TrackMatchType.NotFound
        };

        return new TrackMatchResult
        {
            SourceTrack = sourceTrack,
            MatchedTrack = matchType != TrackMatchType.NotFound ? candidateTrack : null,
            ConfidenceScore = Math.Round(combinedScore, 3),
            MatchType = matchType,
            Notes = $"Similaridade calculada: {combinedScore * 100:F1}% (Título: {titleScore * 100:F0}%, Artista: {artistScore * 100:F0}%)"
        };
    }

    public string CleanText(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;

        // Remover acentuação (ex: 'á' -> 'a')
        string normalized = input.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (char c in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                sb.Append(c);
            }
        }
        string text = sb.ToString().Normalize(NormalizationForm.FormC).ToLowerInvariant();

        // Remover sufixos de ruído
        text = ParenthesesNoiseRegex().Replace(text, " ");
        text = BracketsNoiseRegex().Replace(text, " ");
        text = HyphenNoiseRegex().Replace(text, " ");
        text = FeatTrailingRegex().Replace(text, " ");

        // Remover pontuação restante e espaços extras
        text = NonWordRegex().Replace(text, " ");
        text = MultiSpaceRegex().Replace(text, " ").Trim();

        return text;
    }

    public static double CalculateLevenshteinSimilarity(string s1, string s2)
    {
        if (string.IsNullOrEmpty(s1) && string.IsNullOrEmpty(s2)) return 1.0;
        if (string.IsNullOrEmpty(s1) || string.IsNullOrEmpty(s2)) return 0.0;
        if (s1 == s2) return 1.0;

        int n = s1.Length;
        int m = s2.Length;
        int[,] d = new int[n + 1, m + 1];

        for (int i = 0; i <= n; i++) d[i, 0] = i;
        for (int j = 0; j <= m; j++) d[0, j] = j;

        for (int i = 1; i <= n; i++)
        {
            for (int j = 1; j <= m; j++)
            {
                int cost = (s2[j - 1] == s1[i - 1]) ? 0 : 1;
                d[i, j] = Math.Min(
                    Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                    d[i - 1, j - 1] + cost
                );
            }
        }

        int distance = d[n, m];
        int maxLen = Math.Max(n, m);
        return 1.0 - ((double)distance / maxLen);
    }

    public static double CalculateJaccardSimilarity(string s1, string s2)
    {
        var tokens1 = s1.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet();
        var tokens2 = s2.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet();

        if (tokens1.Count == 0 && tokens2.Count == 0) return 1.0;
        if (tokens1.Count == 0 || tokens2.Count == 0) return 0.0;

        var intersection = tokens1.Intersect(tokens2).Count();
        var union = tokens1.Union(tokens2).Count();

        return (double)intersection / union;
    }
}
