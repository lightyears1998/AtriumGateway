using AtriumGateway.Hosting;
using AtriumGateway.Hosting.Forwarding;

var builder = WebApplication.CreateSlimBuilder(args);

builder.WebHost.UseUrls("http://127.0.0.1:1109");

builder.Services.AddAtriumGateway(builder.Configuration);

var app = builder.Build();

var handler = app.Services.GetRequiredService<GatewayRequestHandler>();

app.Run(handler.HandleAsync);

app.Run();
