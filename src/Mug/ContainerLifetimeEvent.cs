using System;

namespace Mug
{
    public class ContainerLifetimeEvent : EventArgs
    {
        public ContainerLifetimeEvent(ContainerInfo container)
        {
            Container = container;
        }

        public ContainerInfo Container { get; private set; }
    }
}