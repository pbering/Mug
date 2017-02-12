using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;

namespace Mug
{
    internal class Program
    {
        private static DockerHostWatcher _hostWatcher;
        private static readonly Dictionary<string, Process> _runningProxies = new Dictionary<string, Process>();
        private static string _dockerProxyExePath;

        static Program()
        {
            SetConsoleCtrlHandler(OnTerminate, true);
        }

        [DllImport("Kernel32")]
        private static extern bool SetConsoleCtrlHandler(ConsoleEventDelegate @delegate, bool add);

        private static void Main()
        {
            EnsureDockerProxyExe();

            _hostWatcher = new DockerHostWatcher(new HttpClient());
            _hostWatcher.ContainerStopped += OnContainerStopped;
            _hostWatcher.ContainerStarted += OnContainerStarted;
            _hostWatcher.Start();
        }

        private static void EnsureDockerProxyExe()
        {
            _dockerProxyExePath = Path.GetFullPath(".\\docker-proxy.exe");

            if (!File.Exists(_dockerProxyExePath))
            {
                Console.WriteLine($"docker-proxy not found, downloading latest to {_dockerProxyExePath}");

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

            Console.WriteLine($"Using docker-proxy {_dockerProxyExePath}");
        }

        private static void OnContainerStarted(object sender, ContainerLifetimeEvent @event)
        {
            var proxy = new Process();

            proxy.StartInfo = new ProcessStartInfo(_dockerProxyExePath,
                                                   $"-container-ip {@event.Container.IpAddress} " +
                                                   $"-container-port {@event.Container.PrivatePort} " +
                                                   $"-host-port {@event.Container.PublicPort} " +
                                                   "-proto tcp")
                {UseShellExecute = false, CreateNoWindow = true};

            proxy.Start();

            _runningProxies.Add(@event.Container.Id, proxy);

            Console.WriteLine("Started proxy for {0}", @event.Container.Id);
        }

        private static void OnContainerStopped(object sender, ContainerLifetimeEvent @event)
        {
            StopProxy(@event.Container.Id);

            _runningProxies.Remove(@event.Container.Id);
        }

        private static void StopProxy(string id)
        {
            var proxy = _runningProxies[id];

            if (!proxy.HasExited)
            {
                proxy.Kill();
            }

            proxy.Dispose();

            Console.WriteLine("Proxy for {0} has been shut down", id);
        }

        private static bool OnTerminate(int sig)
        {
            Console.WriteLine("Stopping...");

            _hostWatcher.Stop();
            _hostWatcher.ContainerStopped -= OnContainerStopped;
            _hostWatcher.ContainerStarted -= OnContainerStarted;

            // Stop proxies
            foreach (var proxy in _runningProxies)
            {
                StopProxy(proxy.Key);
            }

            _runningProxies.Clear();

            Console.WriteLine("Stopped");

            Environment.Exit(-1);

            return true;
        }

        private delegate bool ConsoleEventDelegate(int sig);
    }
}