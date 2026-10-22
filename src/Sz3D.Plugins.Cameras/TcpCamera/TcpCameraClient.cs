using System.Buffers.Binary;
using System.Net.Sockets;
using Sz3D.Core.Infrastructure.Models;

namespace Sz3D.Plugins.Cameras.TcpCamera;

/// <summary>
/// 通用 TCP 3D 相机客户端——封装 socket 连接、命令下发、帧读取与点云解析。
///
/// 【占位协议说明】
/// 当前实现采用一个简单的示例协议，仅用于跑通链路。真机对接时，
/// 按厂商协议文档替换 <see cref="SendTriggerAsync"/> 与 <see cref="ReadFrameAsync"/>
/// 两个方法的内部实现即可，节点层（TcpCameraNode）无需改动。
///
/// 示例协议帧格式（小端）：
///   [4 字节]  魔数 0x50_43_4C_44 ("PCLD")
///   [4 字节]  点数 N（int32）
///   [N*12 字节] 点数据，每点 3 个 float32：X Y Z（单位 mm）
/// </summary>
public sealed class TcpCameraClient : IDisposable
{
    private const uint FrameMagic = 0x44_4C_43_50; // "PCLD" 小端读出
    private const int  MaxPointCount = 20_000_000; // 防御：异常帧导致的巨额分配

    private readonly string _host;
    private readonly int    _port;
    private readonly int    _timeoutMs;

    private TcpClient?      _client;
    private NetworkStream?  _stream;

    /// <summary>创建客户端。连接在 <see cref="ConnectAsync"/> 时才真正建立。</summary>
    /// <param name="host">相机 IP 或主机名。</param>
    /// <param name="port">相机数据端口。</param>
    /// <param name="timeoutMs">连接与读写超时（毫秒）。</param>
    public TcpCameraClient(string host, int port, int timeoutMs)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(host);
        _host = host;
        _port = port;
        _timeoutMs = timeoutMs <= 0 ? 5000 : timeoutMs;
    }

    /// <summary>是否已建立连接。</summary>
    public bool IsConnected => _client?.Connected == true;

    /// <summary>建立到相机的 TCP 连接，带连接超时。</summary>
    /// <param name="cancellationToken">取消令牌。</param>
    public async Task ConnectAsync(CancellationToken cancellationToken)
    {
        _client = new TcpClient { ReceiveTimeout = _timeoutMs, SendTimeout = _timeoutMs };

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(_timeoutMs);

        try
        {
            await _client.ConnectAsync(_host, _port, timeoutCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException($"连接相机 {_host}:{_port} 超时（{_timeoutMs} ms）。");
        }

        _stream = _client.GetStream();
    }

    /// <summary>
    /// 采集一帧点云：下发触发命令 → 读取一帧 → 解析为 <see cref="PointCloud"/>。
    /// </summary>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>解析得到的点云。</returns>
    public async Task<PointCloud> CaptureAsync(CancellationToken cancellationToken)
    {
        if (_stream is null)
            throw new InvalidOperationException("尚未连接，请先调用 ConnectAsync。");

        await SendTriggerAsync(_stream, cancellationToken).ConfigureAwait(false);
        return await ReadFrameAsync(_stream, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// 【占位 / 待替换】下发采集触发命令。
    /// 示例协议发送 4 字节命令 0x01。真机替换为厂商的触发指令字节序列。
    /// </summary>
    private static async Task SendTriggerAsync(NetworkStream stream, CancellationToken ct)
    {
        byte[] triggerCmd = [0x01, 0x00, 0x00, 0x00];
        await stream.WriteAsync(triggerCmd, ct).ConfigureAwait(false);
        await stream.FlushAsync(ct).ConfigureAwait(false);
    }

    /// <summary>
    /// 【占位 / 待替换】读取并解析一帧点云。
    /// 按示例协议：读 8 字节帧头（魔数 + 点数），再读 N*12 字节点数据。
    /// 真机替换为厂商的帧结构解析。
    /// </summary>
    private static async Task<PointCloud> ReadFrameAsync(NetworkStream stream, CancellationToken ct)
    {
        // 1. 读固定 8 字节帧头
        var header = new byte[8];
        await ReadExactAsync(stream, header, ct).ConfigureAwait(false);

        uint magic = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(0, 4));
        if (magic != FrameMagic)
            throw new InvalidDataException($"帧头魔数不匹配：期望 0x{FrameMagic:X8}，实际 0x{magic:X8}。");

        int count = BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(4, 4));
        if (count < 0 || count > MaxPointCount)
            throw new InvalidDataException($"点数异常：{count}。");

        // 2. 读 N*12 字节点数据（每点 3 个 float32）
        var body = new byte[count * 12];
        await ReadExactAsync(stream, body, ct).ConfigureAwait(false);

        // 3. 解析为 SOA 布局点云
        var x = new float[count];
        var y = new float[count];
        var z = new float[count];
        var span = body.AsSpan();
        for (int i = 0; i < count; i++)
        {
            int off = i * 12;
            x[i] = BinaryPrimitives.ReadSingleLittleEndian(span.Slice(off,     4));
            y[i] = BinaryPrimitives.ReadSingleLittleEndian(span.Slice(off + 4, 4));
            z[i] = BinaryPrimitives.ReadSingleLittleEndian(span.Slice(off + 8, 4));
        }

        return new PointCloud { X = x, Y = y, Z = z };
    }

    /// <summary>
    /// 从流中读满 buffer.Length 字节，处理 TCP 粘包/分段——单次 ReadAsync 不保证读满。
    /// </summary>
    private static async Task ReadExactAsync(NetworkStream stream, byte[] buffer, CancellationToken ct)
    {
        int offset = 0;
        while (offset < buffer.Length)
        {
            int read = await stream.ReadAsync(buffer.AsMemory(offset), ct).ConfigureAwait(false);
            if (read == 0)
                throw new EndOfStreamException("连接被远端关闭，帧数据不完整。");
            offset += read;
        }
    }

    /// <summary>释放 socket 资源。</summary>
    public void Dispose()
    {
        _stream?.Dispose();
        _client?.Dispose();
        _stream = null;
        _client = null;
    }
}
