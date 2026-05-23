using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using WindowRPC.Models;

namespace WindowRPC.Services;

internal sealed class DiscordRpcClient : IDisposable
{
    private const int HandshakeOpcode = 0;
    private const int FrameOpcode = 1;
    private const int CloseOpcode = 2;

    private readonly string _clientId;
    private readonly SemaphoreSlim _sync = new(1, 1);
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    private NamedPipeClientStream? _stream;

    public DiscordRpcClient(string clientId)
    {
        _clientId = clientId;
    }

    public void Dispose()
    {
        _sync.Wait();
        try
        {
            DisposeStream();
        }
        finally
        {
            _sync.Release();
            _sync.Dispose();
        }
    }

    public async Task ClearPresenceAsync(CancellationToken cancellationToken = default)
    {
        await _sync.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await EnsureConnectedAsync(cancellationToken).ConfigureAwait(false);
            if (_stream is null)
            {
                return;
            }

            var payload = new
            {
                cmd = "SET_ACTIVITY",
                args = new
                {
                    pid = Environment.ProcessId,
                    activity = (object?)null
                },
                nonce = Guid.NewGuid().ToString()
            };

            await WriteFrameAsync(_stream, FrameOpcode, payload, cancellationToken).ConfigureAwait(false);
            var response = await DrainResponseAsync(_stream, cancellationToken).ConfigureAwait(false);
            LogDiscordResponse("ClearPresence", response);
        }
        catch (Exception ex)
        {
            DiagnosticLog.Write($"Discord ClearPresence failed: {ex.Message}");
            DisposeStream();
        }
        finally
        {
            _sync.Release();
        }
    }

    public async Task SetPresenceAsync(ResolvedPresence presence, CancellationToken cancellationToken = default)
    {
        await _sync.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await EnsureConnectedAsync(cancellationToken).ConfigureAwait(false);
            if (_stream is null)
            {
                return;
            }

            var payload = new
            {
                cmd = "SET_ACTIVITY",
                args = new
                {
                    pid = Environment.ProcessId,
                    activity = new
                    {
                        state = NullIfWhiteSpace(presence.State),
                        details = NullIfWhiteSpace(presence.Details),
                        timestamps = presence.StartTimestampUnix.HasValue && presence.EndTimestampUnix.HasValue
                            ? new
                            {
                                start = presence.StartTimestampUnix.Value,
                                end = presence.EndTimestampUnix.Value
                            }
                            : null,
                        assets = new
                        {
                            large_image = presence.Logo,
                            large_text = "WindowRPC"
                        }
                    }
                },
                nonce = Guid.NewGuid().ToString()
            };

            await WriteFrameAsync(_stream, FrameOpcode, payload, cancellationToken).ConfigureAwait(false);
            var response = await DrainResponseAsync(_stream, cancellationToken).ConfigureAwait(false);
            LogDiscordResponse($"SetPresence state='{presence.State}' details='{presence.Details}' logo='{presence.Logo}'", response);
        }
        catch (Exception ex)
        {
            DiagnosticLog.Write($"Discord SetPresence failed: {ex.Message}");
            DisposeStream();
        }
        finally
        {
            _sync.Release();
        }
    }

    private async Task EnsureConnectedAsync(CancellationToken cancellationToken)
    {
        if (_stream is { IsConnected: true })
        {
            return;
        }

        DisposeStream();

        for (var i = 0; i < 10; i++)
        {
            var pipeName = $"discord-ipc-{i}";
            var candidate = new NamedPipeClientStream(
                ".",
                pipeName,
                PipeDirection.InOut,
                PipeOptions.Asynchronous);

            try
            {
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutCts.CancelAfter(TimeSpan.FromMilliseconds(250));
                await candidate.ConnectAsync(timeoutCts.Token).ConfigureAwait(false);

                var handshake = new
                {
                    v = 1,
                    client_id = _clientId
                };

                await WriteFrameAsync(candidate, HandshakeOpcode, handshake, cancellationToken).ConfigureAwait(false);
                var response = await DrainResponseAsync(candidate, cancellationToken).ConfigureAwait(false);
                LogDiscordResponse("Handshake", response);
                _stream = candidate;
                return;
            }
            catch
            {
                candidate.Dispose();
            }
        }
    }

    private async Task<string> DrainResponseAsync(NamedPipeClientStream stream, CancellationToken cancellationToken)
    {
        var header = new byte[8];
        await ReadExactAsync(stream, header, cancellationToken).ConfigureAwait(false);
        var length = BitConverter.ToInt32(header, 4);
        if (length <= 0)
        {
            return string.Empty;
        }

        var payload = new byte[length];
        await ReadExactAsync(stream, payload, cancellationToken).ConfigureAwait(false);
        return Encoding.UTF8.GetString(payload);
    }

    private async Task WriteFrameAsync(
        NamedPipeClientStream stream,
        int opcode,
        object payload,
        CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(payload, _jsonOptions);
        var body = Encoding.UTF8.GetBytes(json);
        var header = new byte[8];

        BitConverter.GetBytes(opcode).CopyTo(header, 0);
        BitConverter.GetBytes(body.Length).CopyTo(header, 4);

        await stream.WriteAsync(header, cancellationToken).ConfigureAwait(false);
        await stream.WriteAsync(body, cancellationToken).ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task ReadExactAsync(Stream stream, byte[] buffer, CancellationToken cancellationToken)
    {
        var offset = 0;
        while (offset < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(offset, buffer.Length - offset), cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                throw new EndOfStreamException("Discord IPC stream closed.");
            }

            offset += read;
        }
    }

    private static string? NullIfWhiteSpace(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static void LogDiscordResponse(string operation, string response)
    {
        if (string.IsNullOrWhiteSpace(response))
        {
            DiagnosticLog.Write($"Discord {operation}: empty response");
            return;
        }

        DiagnosticLog.Write($"Discord {operation}: {response}");
    }

    private void DisposeStream()
    {
        if (_stream is null)
        {
            return;
        }

        try
        {
            if (_stream.IsConnected)
            {
                var header = new byte[8];
                BitConverter.GetBytes(CloseOpcode).CopyTo(header, 0);
                BitConverter.GetBytes(0).CopyTo(header, 4);
                _stream.Write(header, 0, header.Length);
                _stream.Flush();
            }
        }
        catch
        {
            // Ignore cleanup errors while disconnecting from Discord.
        }
        finally
        {
            _stream.Dispose();
            _stream = null;
        }
    }
}
