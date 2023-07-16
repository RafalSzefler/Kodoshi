using TestProjectBase.Server;

var rpcBuilder = new RPCBuilder();
rpcBuilder.ScanForHandlers();

var builder = WebApplication.CreateBuilder(args);

rpcBuilder.ApplyToServiceCollection(builder.Services);

var app = builder.Build();

rpcBuilder.ApplyToWebApplication(app);

app.Run();
