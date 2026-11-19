using Empyrean;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Carve.Hosting;

/// <summary>
/// Runs the engine on a dedicated <c>frame</c> thread. The engine's services are resolved there
/// too, and <see cref="StartAsync"/> does not wait for any of it, so <c>initialize</c> answers at
/// once and a failure of any kind is held in <see cref="EngineStatus"/>.
/// </summary>
internal sealed class EngineThread(IServiceProvider services, EngineStatus status, ILogger<EngineThread> log)
    : IHostedService
{
    private static readonly TimeSpan JoinSlice = TimeSpan.FromMilliseconds(50);

    private Thread? _thread;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _thread = new Thread(RunEngine) { Name = "frame", IsBackground = true };
        _thread.Start();
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_thread is not { } thread)
        {
            return;
        }

        await Task.Run(() =>
        {
            // Asked again each slice: a stop can land while the thread is still initialising.
            while (thread.IsAlive && !cancellationToken.IsCancellationRequested)
            {
                try
                {
                    services.GetRequiredService<IGameLifecycle>().RequestStop();
                }
                catch (Exception e)
                {
                    log.LogDebug(e, "Engine lifecycle not available to stop");
                }

                thread.Join(JoinSlice);
            }
        }, CancellationToken.None);

        if (thread.IsAlive)
        {
            log.LogWarning("The frame thread did not stop in time");
        }
    }

    private void RunEngine()
    {
        try
        {
            var loop = services.GetRequiredService<IGameLoop>();
            loop.Initialise();
            status.SetReady();
            log.LogInformation("Engine ready");
            loop.Run();
        }
        catch (Exception e)
        {
            // Interim until engine need 1: a machine with no driver gives only a null pointer
            // message, so the Vulkan part of the wording is ours.
            var message = $"Engine failed (is a Vulkan device available?): {e.GetBaseException().Message}";
            status.SetFaulted(message);
            log.LogError(e, "Engine faulted. {Message}", message);
        }
        finally
        {
            status.SetStopped();
        }
    }
}
