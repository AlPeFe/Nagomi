using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

namespace Nagomi.Api.Features.ReferenceData;

internal static class ReferenceDataImportEndpoints
{
    private const long MaxBodyBytes = 16 * 1024 * 1024;
    private const int MaxIneRows = 100_000;
    private const int MaxNdjsonRowBytes = 64 * 1024;

    public static void Map(RouteGroupBuilder group)
    {
        group.MapPost("/imports/ine", ImportIne);
        group.MapPost("/imports/cnh", ImportCnh);
    }

    private static async Task<IResult> ImportIne(
        HttpRequest request,
        IIneImporter importer,
        CancellationToken cancellationToken)
    {
        var mediaType = MediaType(request.ContentType);
        if (mediaType is not "application/json" and not "application/x-ndjson" and not "application/ndjson")
            return TypedResults.StatusCode(StatusCodes.Status415UnsupportedMediaType);
        if (request.ContentLength > MaxBodyBytes)
            return Invalid($"Request body exceeds the {MaxBodyBytes} byte safety limit.");

        try
        {
            await using var body = new LimitedReadStream(request.Body, MaxBodyBytes);
            var rows = mediaType == "application/json"
                ? ReadJsonArray(body, cancellationToken)
                : ReadNdjson(body, cancellationToken);
            return TypedResults.Ok(await importer.ImportAsync(rows, cancellationToken));
        }
        catch (Exception exception) when (IsInvalidImport(exception))
        {
            return Invalid(exception.Message);
        }
    }

    private static async Task<IResult> ImportCnh(
        HttpRequest request,
        ICnhRowReader reader,
        ICnhImporter importer,
        CancellationToken cancellationToken)
    {
        if (MediaType(request.ContentType) != "text/csv")
            return TypedResults.StatusCode(StatusCodes.Status415UnsupportedMediaType);
        if (request.ContentLength > MaxBodyBytes)
            return Invalid($"Request body exceeds the {MaxBodyBytes} byte safety limit.");

        try
        {
            await using var body = new LimitedReadStream(request.Body, MaxBodyBytes);
            return TypedResults.Ok(await importer.ImportAsync(reader.ReadAsync(body, cancellationToken), cancellationToken));
        }
        catch (Exception exception) when (IsInvalidImport(exception))
        {
            return Invalid(exception.Message);
        }
    }

    private static async IAsyncEnumerable<IneImportRow> ReadJsonArray(
        Stream body,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var count = 0;
        await foreach (var row in JsonSerializer.DeserializeAsyncEnumerable<IneImportRow>(
                           body, JsonSerializerOptions.Web, cancellationToken))
        {
            if (row is null)
                throw new InvalidDataException("INE import rows cannot be null.");
            if (++count > MaxIneRows)
                throw new InvalidDataException($"INE input exceeds the {MaxIneRows} row safety limit.");
            yield return row;
        }
    }

    private static async IAsyncEnumerable<IneImportRow> ReadNdjson(
        Stream body,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(
            body,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true),
            detectEncodingFromByteOrderMarks: true,
            leaveOpen: true);
        var count = 0;
        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            if (Encoding.UTF8.GetByteCount(line) > MaxNdjsonRowBytes)
                throw new InvalidDataException($"NDJSON row exceeds the {MaxNdjsonRowBytes} byte safety limit.");
            if (string.IsNullOrWhiteSpace(line))
                continue;
            if (++count > MaxIneRows)
                throw new InvalidDataException($"INE input exceeds the {MaxIneRows} row safety limit.");
            yield return JsonSerializer.Deserialize<IneImportRow>(line, JsonSerializerOptions.Web)
                ?? throw new InvalidDataException("INE import rows cannot be null.");
        }
    }

    private static bool IsInvalidImport(Exception exception) =>
        exception is InvalidDataException or JsonException or DecoderFallbackException;

    private static IResult Invalid(string message) => TypedResults.ValidationProblem(
        new Dictionary<string, string[]> { ["import"] = [message] });

    private static string? MediaType(string? contentType) =>
        contentType?.Split(';', 2)[0].Trim().ToLowerInvariant();

    private sealed class LimitedReadStream(Stream inner, long limit) : Stream
    {
        private long _read;

        public override bool CanRead => inner.CanRead;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count) =>
            Count(inner.Read(buffer, offset, count));

        public override int Read(Span<byte> buffer) => Count(inner.Read(buffer));

        public override async ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default) =>
            Count(await inner.ReadAsync(buffer, cancellationToken));

        public override Task<int> ReadAsync(
            byte[] buffer,
            int offset,
            int count,
            CancellationToken cancellationToken) =>
            ReadArrayAsync(buffer, offset, count, cancellationToken);

        private async Task<int> ReadArrayAsync(
            byte[] buffer,
            int offset,
            int count,
            CancellationToken cancellationToken) =>
            Count(await inner.ReadAsync(buffer.AsMemory(offset, count), cancellationToken));

        private int Count(int bytesRead)
        {
            _read += bytesRead;
            if (_read > limit)
                throw new InvalidDataException($"Request body exceeds the {limit} byte safety limit.");
            return bytesRead;
        }

        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            // The request owns its body stream.
            base.Dispose(disposing);
        }

        public override ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
