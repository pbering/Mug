using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Mug
{
    internal class Program
    {
        private static DockerHostWatcher _hostWatcher;
        private static readonly Dictionary<string, Process> _runningProxies = new Dictionary<string, Process>();
        private static string _dockerProxyExePath;
        private static ILogger _logger;

        [DllImport("Kernel32")]
        private static extern bool SetConsoleCtrlHandler(ConsoleEventDelegate @delegate, bool add);

        private static void Main()
        {
            // Add handler for termination events
            SetConsoleCtrlHandler(OnTerminate, true);

            // Load embedded assemblies
            AppDomain.CurrentDomain.AssemblyResolve += (sender, args) =>
            {
                using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Mug.Newtonsoft.Json.dll"))
                {
                    if (stream == null)
                    {
                        return null;
                    }

                    var assembly = new byte[stream.Length];

                    stream.Read(assembly, 0, assembly.Length);

                    return Assembly.Load(assembly);
                }
            };

            _logger = new ConsoleLogger();

            EnsureDockerProxyExe(_logger);

            _hostWatcher = new DockerHostWatcher(_logger, new HttpClient());
            _hostWatcher.ContainerDetected += OnContainerDetected;
            _hostWatcher.ContainerStopped += OnContainerStopped;
            _hostWatcher.Start();
        }

        private static void EnsureDockerProxyExe(ILogger logger)
        {
            _dockerProxyExePath = Path.GetFullPath(".\\docker-proxy.exe");

            if (!File.Exists(_dockerProxyExePath))
            {
                logger.WriteLine(LogLevel.Nfo, $"docker-proxy not found, downloading latest to {_dockerProxyExePath}");

                using (var client = new HttpClient())
                {
                    var download = client.GetStreamAsync("https://master.dockerproject.org/windows/amd64/docker-proxy.exe").GetAwaiter().GetResult();

                    using (var file = File.Create(_dockerProxyExePath))
                    using (new StreamReader(download))
                    {
                        download.CopyTo(file);

                        file.Flush();
                    }
                }
            }

            logger.WriteLine(LogLevel.Nfo, $"Using docker-proxy {_dockerProxyExePath}");
        }

        private static void OnContainerDetected(object sender, ContainerLifetimeEvent @event)
        {
            var container = @event.Container;

            _logger.WriteLine(LogLevel.Nfo, "Container {0} detected", container.Id);

            var proxy = new Process();

            proxy.StartInfo = new ProcessStartInfo(_dockerProxyExePath,
                                                   $"-container-ip {container.IpAddress} " +
                                                   $"-container-port {container.PrivatePort} " +
                                                   $"-host-port {container.PublicPort} " +
                                                   "-proto tcp")
                {UseShellExecute = false, CreateNoWindow = true};

            proxy.Start();

            _runningProxies.Add(container.Id, proxy);

            _logger.WriteLine(LogLevel.Nfo, "Started proxy {0} on port localhost:{1}", container.Id, container.PublicPort);
        }

        private static void OnContainerStopped(object sender, ContainerLifetimeEvent @event)
        {
            var container = @event.Container;

            _logger.WriteLine(LogLevel.Nfo, "Container {0} stopped", container.Id);

            StopProxy(container.Id);

            _runningProxies.Remove(container.Id);
        }

        private static void StopProxy(string id)
        {
            var proxy = _runningProxies[id];

            if (!proxy.HasExited)
            {
                proxy.Kill();
            }

            proxy.Dispose();

            _logger.WriteLine(LogLevel.Nfo, "Proxy {0} has been shut down", id);
        }

        private static bool OnTerminate(int sig)
        {
            _logger.WriteLine(LogLevel.Nfo, "Stopping...");

            _hostWatcher.Stop();
            _hostWatcher.ContainerStopped -= OnContainerStopped;
            _hostWatcher.ContainerDetected -= OnContainerDetected;

            // Stop proxies
            foreach (var proxy in _runningProxies)
            {
                StopProxy(proxy.Key);
            }

            _runningProxies.Clear();

            _logger.WriteLine(LogLevel.Nfo, "Stopped");

            Environment.Exit(-1);

            return true;
        }

        private delegate bool ConsoleEventDelegate(int sig);
    }
}