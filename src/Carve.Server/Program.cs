using Carve.Hosting;
using Carve.Server;
using Empyrean;
using Microsoft.Extensions.DependencyInjection;

// The host is Carve.Hosting's (T-111), shared with patina: carve names itself, then adds its tools, its render
// pipeline and its render system where Program.cs used to.
return await EngineHost.RunAsync(args, new EngineHostOptions
{
    Name = "carve",
    Product = typeof(Program).Assembly,
    AddServer = server =>
    {
        CarveServer.AddTools(server.Services, server.Mcp);

        server.Services.AddSingleton<RenderComposition>();
        server.Services.UseRenderPipeline<RenderComposition>();
        server.Services.AddSystem<RenderSystem>();
    },
});
