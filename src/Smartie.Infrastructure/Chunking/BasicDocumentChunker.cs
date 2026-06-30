using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using Smartie.Application.Abstractions;
using Smartie.Application.Configuration;
using Smartie.Domain.Entities;
using Smartie.Infrastructure.Documents;

namespace Smartie.Infrastructure.Chunking;

public sealed class BasicDocumentChunker : IDocumentChunker
{
    private static readonly Regex MarkdownHeadingRegex = new(
        @"^#{1,6}\s+\S",
        RegexOptions.Compiled | RegexOptions.Multiline);

    private static readonly Regex PageMarkerRegex = new(
        @"^\[Page\s+(\d+)\]\s*$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Multiline);

    private readonly ChunkingOptions _options;

    public BasicDocumentChunker(IOptions<ChunkingOptions> options)
    {
        _options = options.Value;
    }

    public Task<IReadOnlyList<DocumentChunk>> ChunkAsync(
        Document document,
        string extractedText,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(extractedText))
        {
            return Task.FromResult<IReadOnlyList<DocumentChunk>>(Array.Empty<DocumentChunk>());
        }

        var segments = BuildSegments(extractedText, document.Extension);
        var chunks = BuildChunks(segments);
        return Task.FromResult<IReadOnlyList<DocumentChunk>>(chunks);
    }

    private IReadOnlyList<TextSegment> BuildSegments(string text, string extension)
    {
        if (DocumentExtensionMatcher.IsAny(extension, ".md", "md", ".markdown", "markdown"))
        {
            return BuildMarkdownSegments(text);
        }

        return BuildParagraphSegments(text);
    }

    private static IReadOnlyList<TextSegment> BuildParagraphSegments(string text)
    {
        var segments = new List<TextSegment>();
        var normalized = text.Replace("\r\n", "\n");
        var parts = normalized.Split("\n\n", StringSplitOptions.RemoveEmptyEntries);
        var cursor = 0;

        foreach (var part in parts)
        {
            var trimmed = part.Trim();
            if (trimmed.Length == 0)
            {
                cursor += part.Length + 2;
                continue;
            }

            var start = normalized.IndexOf(trimmed, cursor, StringComparison.Ordinal);
            if (start < 0)
            {
                start = cursor;
            }

            var pageNumber = TryReadPageNumber(trimmed);
            segments.Add(new TextSegment(trimmed, start, pageNumber));
            cursor = start + trimmed.Length;
        }

        if (segments.Count == 0)
        {
            segments.Add(new TextSegment(normalized.Trim(), 0, null));
        }

        return segments;
    }

    private static IReadOnlyList<TextSegment> BuildMarkdownSegments(string text)
    {
        var normalized = text.Replace("\r\n", "\n");
        var lines = normalized.Split('\n');
        var segments = new List<TextSegment>();
        var builder = new StringBuilder();
        var segmentStart = 0;
        var lineStart = 0;
        int? pageNumber = null;

        void FlushSegment(int endPosition)
        {
            var content = builder.ToString().Trim();
            if (content.Length == 0)
            {
                return;
            }

            segments.Add(new TextSegment(content, segmentStart, pageNumber));
            builder.Clear();
            segmentStart = endPosition;
        }

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var page = TryReadPageNumber(line.Trim());
            if (page is not null)
            {
                pageNumber = page;
                lineStart += line.Length + 1;
                continue;
            }

            if (MarkdownHeadingRegex.IsMatch(line) && builder.Length > 0)
            {
                FlushSegment(lineStart);
            }

            if (builder.Length == 0)
            {
                segmentStart = lineStart;
            }

            if (builder.Length > 0)
            {
                builder.Append('\n');
            }

            builder.Append(line);
            lineStart += line.Length + 1;
        }

        FlushSegment(normalized.Length);
        return segments.Count > 0 ? segments : BuildParagraphSegments(text);
    }

    private List<DocumentChunk> BuildChunks(IReadOnlyList<TextSegment> segments)
    {
        var chunks = new List<DocumentChunk>();
        var buffer = new StringBuilder();
        var bufferStart = segments.Count > 0 ? segments[0].StartPosition : 0;
        int? bufferPage = null;
        var chunkIndex = 0;
        string? overlapPrefix = null;

        void EmitChunk(string content, int startPosition, int? pageNumber)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                return;
            }

            var trimmed = content.Trim();
            foreach (var piece in SplitOversized(trimmed, _options.MaxChunkSize))
            {
                chunks.Add(CreateChunk(chunkIndex++, piece, startPosition, pageNumber));
                startPosition += piece.Length;
            }
        }

        foreach (var segment in segments)
        {
            if (buffer.Length == 0)
            {
                bufferStart = segment.StartPosition;
                bufferPage = segment.PageNumber;
            }

            var candidateLength = buffer.Length == 0
                ? segment.Text.Length
                : buffer.Length + 2 + segment.Text.Length;

            if (candidateLength > _options.MaxChunkSize && buffer.Length > 0)
            {
                var content = buffer.ToString();
                EmitChunk(content, bufferStart, bufferPage);
                overlapPrefix = TakeOverlapSuffix(content, _options.ChunkOverlap);
                buffer.Clear();
                bufferStart = segment.StartPosition;
                bufferPage = segment.PageNumber;

                if (!string.IsNullOrEmpty(overlapPrefix))
                {
                    buffer.Append(overlapPrefix);
                }
            }

            if (buffer.Length > 0)
            {
                buffer.Append("\n\n");
            }

            buffer.Append(segment.Text);

            if (buffer.Length >= _options.TargetChunkSize)
            {
                var content = buffer.ToString();
                EmitChunk(content, bufferStart, bufferPage);
                overlapPrefix = TakeOverlapSuffix(content, _options.ChunkOverlap);
                buffer.Clear();
                bufferStart = segment.StartPosition + segment.Text.Length;
                bufferPage = segment.PageNumber;

                if (!string.IsNullOrEmpty(overlapPrefix))
                {
                    buffer.Append(overlapPrefix);
                }
            }
        }

        if (buffer.Length > 0)
        {
            EmitChunk(buffer.ToString(), bufferStart, bufferPage);
        }

        if (chunks.Count == 0 && segments.Count > 0)
        {
            var combined = string.Join("\n\n", segments.Select(s => s.Text)).Trim();
            EmitChunk(combined, segments[0].StartPosition, segments[0].PageNumber);
        }

        return chunks;
    }

    private static IEnumerable<string> SplitOversized(string content, int maxSize)
    {
        if (content.Length <= maxSize)
        {
            yield return content;
            yield break;
        }

        var start = 0;
        while (start < content.Length)
        {
            var length = Math.Min(maxSize, content.Length - start);
            if (start + length < content.Length)
            {
                length = FindWordBoundary(content, start, length);
            }

            yield return content.Substring(start, length).Trim();
            start += length;
        }
    }

    private static int FindWordBoundary(string content, int start, int length)
    {
        var end = start + length;
        var splitAt = content.LastIndexOf(' ', end - 1, length);
        if (splitAt <= start)
        {
            splitAt = content.LastIndexOf('\n', end - 1, length);
        }

        return splitAt > start ? splitAt - start : length;
    }

    private static string TakeOverlapSuffix(string content, int overlap)
    {
        if (overlap <= 0 || content.Length <= overlap)
        {
            return content;
        }

        var slice = content[^overlap..];
        var wordBoundary = slice.IndexOf(' ', StringComparison.Ordinal);
        return wordBoundary >= 0 ? slice[wordBoundary..].TrimStart() : slice;
    }

    private static DocumentChunk CreateChunk(int index, string content, int startPosition, int? pageNumber)
    {
        var trimmed = content.Trim();
        return new DocumentChunk
        {
            ChunkIndex = index,
            Content = trimmed,
            CharacterCount = trimmed.Length,
            TokenEstimate = EstimateTokens(trimmed.Length),
            StartPosition = startPosition,
            EndPosition = startPosition + trimmed.Length,
            PageNumber = pageNumber,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    private static int EstimateTokens(int characterCount) =>
        Math.Max(1, (int)Math.Ceiling(characterCount / 4.0));

    private static int? TryReadPageNumber(string line)
    {
        var match = PageMarkerRegex.Match(line);
        return match.Success && int.TryParse(match.Groups[1].Value, out var page) ? page : null;
    }

    private readonly record struct TextSegment(string Text, int StartPosition, int? PageNumber);
}
