using Sz3D.Backend.AiService.ModelStore;
using Sz3D.Backend.AiService.Services;

var builder = WebApplication.CreateBuilder(args);

// gRPC 服务
builder.Services.AddGrpc(options =>
{
    options.MaxReceiveMessageSize = 64 * 1024 * 1024;  // 64MB（图像传输）
    options.MaxSendMessageSize    = 64 * 1024 * 1024;
    options.EnableDetailedErrors  = builder.Environment.IsDevelopment();
});

// 模型管理器（单例）
builder.Services.AddSingleton<ModelManager>();

// 日志
builder.Logging.AddConsole();

// 绑定地址（默认 50051，可通过 appsettings 覆盖）
builder.WebHost.ConfigureKestrel(opts =>
{
    opts.ListenLocalhost(
        builder.Configuration.GetValue<int>("GrpcPort", 50051),
        listenOptions => listenOptions.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http2);
});

var app = builder.Build();

app.MapGrpcService<InferenceServiceImpl>();
app.MapGet("/", () => "Sz3D AI Inference Service (gRPC). Use a gRPC client to connect.");

app.Run();
