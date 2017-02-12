namespace Mug
{
    public class ContainerInfo
    {
        public ContainerInfo(string ipAddress, long publicPort, long privatePort, string id)
        {
            IpAddress = ipAddress;
            PublicPort = publicPort;
            PrivatePort = privatePort;
            Id = id;
        }

        public string IpAddress { get; }
        public long PublicPort { get; }
        public long PrivatePort { get; }
        public string Id { get; }
    }
}