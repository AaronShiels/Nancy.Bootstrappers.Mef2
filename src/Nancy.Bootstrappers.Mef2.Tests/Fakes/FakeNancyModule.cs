using Nancy.Bootstrappers.Mef2.Tests.Fakes.Dependencies;

namespace Nancy.Bootstrappers.Mef2.Tests.Fakes
{
    public class FakeNancyModule : NancyModule
    {
        public ITransientDependency TransientDependency { get; private set; }
        public ISingletonDependency SingletonDependency { get; private set; }
        public IPerRequestDependency PerRequestDependency { get; private set; }
        public IInstanceDependency InstanceDependency { get; private set; }

        public FakeNancyModule(ITransientDependency transientDepenency, ISingletonDependency singletonDependency, IPerRequestDependency perRequestDependency, IInstanceDependency instanceDependency)
        {
            TransientDependency = transientDepenency;
            SingletonDependency = singletonDependency;
            PerRequestDependency = perRequestDependency;
            InstanceDependency = instanceDependency;

            Get["/"] = _ =>
            {
                return "Hello world :)";
            };
        }
    }
}
