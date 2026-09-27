// SPDX-License-Identifier: MPL-2.0

using System.IO;
using System.Text.Json;

namespace YMM4GlassWipe;

/// <summary>
/// ファイルサイズを上限付きで読み込み、読み取り中のサイズ変化を検出します。
/// </summary>
internal static class BoundedFileReader
{
    public static bool TryReadBytes(
        string path,
        int maximumByteCount,
        out byte[] bytes,
        out bool exceededMaximum,
        out Exception? exception)
    {
        bytes = [];
        exceededMaximum = false;
        exception = null;
        try
        {
            using var stream = OpenRead(path);
            var expectedLength = GetCheckedLength(stream, maximumByteCount, out exceededMaximum);
            if (exceededMaximum)
            {
                return false;
            }

            bytes = GC.AllocateUninitializedArray<byte>(checked((int)expectedLength));
            stream.ReadExactly(bytes);
            if (stream.ReadByte() != -1 || stream.Length != expectedLength)
            {
                bytes = [];
                exception = new IOException("ファイルの読み取り中に内容またはサイズが変化しました。");
                return false;
            }

            return true;
        }
        catch (Exception caught) when (IsExpectedReadException(caught))
        {
            bytes = [];
            exception = caught;
            return false;
        }
    }

    public static bool TryDeserializeJson<T>(
        string path,
        int maximumByteCount,
        JsonSerializerOptions options,
        out T? value,
        out bool exceededMaximum,
        out Exception? exception)
    {
        value = default;
        exceededMaximum = false;
        exception = null;
        try
        {
            using var stream = OpenRead(path);
            var expectedLength = GetCheckedLength(stream, maximumByteCount, out exceededMaximum);
            if (exceededMaximum)
            {
                return false;
            }

            using var boundedStream = new BoundedReadStream(stream, maximumByteCount);
            value = JsonSerializer.Deserialize<T>(boundedStream, options);
            Span<byte> buffer = stackalloc byte[4096];
            while (boundedStream.Read(buffer) > 0)
            {
            }

            if (stream.Length != expectedLength)
            {
                value = default;
                exception = new IOException("ファイルの読み取り中に内容またはサイズが変化しました。");
                return false;
            }

            return true;
        }
        catch (FileSizeLimitExceededException)
        {
            value = default;
            exceededMaximum = true;
            return false;
        }
        catch (Exception caught) when (IsExpectedReadException(caught))
        {
            value = default;
            exception = caught;
            return false;
        }
    }

    private static FileStream OpenRead(string path) =>
        new(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 4096,
            FileOptions.SequentialScan);

    private static long GetCheckedLength(
        FileStream stream,
        int maximumByteCount,
        out bool exceededMaximum)
    {
        var length = stream.Length;
        exceededMaximum = length > maximumByteCount;
        return length;
    }

    private static bool IsExpectedReadException(Exception exception) =>
        exception is IOException or UnauthorizedAccessException or
            NotSupportedException or ArgumentException or JsonException;

    private sealed class FileSizeLimitExceededException : IOException
    {
        public FileSizeLimitExceededException()
            : base("ファイルサイズが上限を超えました。")
        {
        }
    }

    private sealed class BoundedReadStream : Stream
    {
        private readonly Stream _source;
        private long _remaining;

        public BoundedReadStream(Stream source, long maximumByteCount)
        {
            _source = source;
            _remaining = maximumByteCount;
        }

        public override bool CanRead => _source.CanRead;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush() => throw new NotSupportedException();

        public override int Read(byte[] buffer, int offset, int count) =>
            Read(buffer.AsSpan(offset, count));

        public override int Read(Span<byte> buffer)
        {
            if (_remaining == 0)
            {
                if (_source.ReadByte() != -1)
                {
                    throw new FileSizeLimitExceededException();
                }

                return 0;
            }

            var count = (int)Math.Min(buffer.Length, _remaining);
            var read = _source.Read(buffer[..count]);
            _remaining -= read;
            return read;
        }

        public override int ReadByte()
        {
            if (_remaining == 0)
            {
                if (_source.ReadByte() != -1)
                {
                    throw new FileSizeLimitExceededException();
                }

                return -1;
            }

            var value = _source.ReadByte();
            if (value != -1)
            {
                _remaining--;
            }

            return value;
        }

        public override long Seek(long offset, SeekOrigin origin) =>
            throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) =>
            throw new NotSupportedException();
    }
}
