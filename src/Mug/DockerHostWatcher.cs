using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Mug
{
    public class DockerHostWatcher
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger _logger;
        private bool _running = true;
        private Dictionary<string, ContainerInfo> _runningContainers;

        public DockerHostWatcher(ILogger logger, HttpClient httpClient)
        {
            _logger = logger;
            _httpClient = httpClient;
            _httpClient.BaseAddress = new Uri("http://localhost:2375/v1.26");
            _httpClient.Timeout = TimeSpan.FromSeconds(30);
            _runningContainers = new Dictionary<string, ContainerInfo>();
        }

        public event EventHandler<ContainerLifetimeEvent> ContainerDetected = delegate { };
        public event EventHandler<ContainerLifetimeEvent> ContainerStopped = delegate { };

        public void Start()
        {
            while (_running)
            {
                try
                {
                    UpdateRunningContainers();

                    Thread.Sleep(2000);
                }
                catch (Exception ex)
                {
                    _logger.WriteLine(LogLevel.Err, "Exception: {0}, StackTrace: {1}", ex.Message, ex.StackTrace);
                }
            }
        }

        private void UpdateRunningContainers()
        {
            var cancellationSource = new CancellationTokenSource();
            var currentContainers = new Dictionary<string, ContainerInfo>();

            try
            {
                // Check OS
                var response = _httpClient.GetAsync("/info", cancellationSource.Token).GetAwaiter().GetResult();

                response.EnsureSuccessStatusCode();

                var info = JsonConvert.DeserializeObject<dynamic>(response.Content.ReadAsStringAsync().GetAwaiter().GetResult());

                if (info.OSType != "windows")
                {
                    _logger.WriteLine(LogLevel.Err, "Docker host not running Windows?");

                    return;
                }

                // Check containers
                response = _httpClient.GetAsync("/containers/json", cancellationSource.Token).GetAwaiter().GetResult();

                response.EnsureSuccessStatusCode();

                var containers = JsonConvert.DeserializeObject<dynamic>(response.Content.ReadAsStringAsync().GetAwaiter().GetResult());

                foreach (var container in containers)
                {
                    if (container.State != "running")
                    {
                        continue;
                    }

                    foreach (var port in container.Ports)
                    {
                        if (port.PublicPort == null || port.PrivatePort == null)
                        {
                            continue;
                        }

                        string ip = container.NetworkSettings.Networks.nat.IPAddress.Value;
                        string id = container.Id;
                        long publicPort = port.PublicPort.Value;
                        long privatePort = port.PrivatePort.Value;

                        currentContainers[id] = new ContainerInfo(ip, publicPort, privatePort, id);
                    }
                }

                // Raise started events
                foreach (var currentContainer in currentContainers)
                {
                    if (!_runningContainers.ContainsKey(currentContainer.Key))
                    {
                        ContainerDetected(this, new ContainerLifetimeEvent(currentContainer.Value));
                    }
                }

                // Raise stopped events
                foreach (var runningContainer in _runningContainers)
                {
                    if (!currentContainers.ContainsKey(runningContainer.Key))
                    {
                        ContainerStopped(this, new ContainerLifetimeEvent(runningContainer.Value));
                    }
                }

                // Update list
                _runningContainers = currentContainers;
            }
            catch (TaskCanceledException ex)
            {
                // Handle timeouts
                if (ex.CancellationToken != cancellationSource.Token)
                {
                    _logger.WriteLine(LogLevel.Err, "Timeout during communcation with docker host");

                    return;
                }

                throw;
            }
        }

        public void Stop()
        {
            _running = false;
            _runningContainers.Clear();
        }
    }
}