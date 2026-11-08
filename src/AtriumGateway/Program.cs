using AtriumGateway.Hosting;
using AtriumGateway.Hosting.Forwarding;

var builder = WebApplication.CreateSlimBuilder(args);

builder.Services.AddAtriumGateway(builder.Configuration);

var app = builder.Build();

var handler = app.Services.GetRequiredService<GatewayRequestHandler>();

app.Run(handler.HandleAsync);

app.Run();
