using Nancy.Bootstrappers.Mef2.Tests.Fakes.Dependencies;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Nancy.Bootstrappers.Mef2.Tests.Fakes
{
    public class FakeCompositionContextNancyBootstrapper : CompositionContextNancyBootstrapper
    {
        protected override void ConfigureInstanceExportDescriptorProvider(InstanceExportDescriptorProvider provider)
        {
            provider.RegisterExport(typeof(IInstanceDependency), new InstanceDependency("Mah secrat messahhge!"));
        }

        protected override IEnumerable<Assembly> InternalAssemblies
        {
            get
            {
                //This assembly will get picked up by the greedy autoregister scan because it starts with "Nancy", so we are opting out
                return base.InternalAssemblies.Where(a => a != typeof(FakeCompositionContextNancyBootstrapper).Assembly);
            }
        }
    }
}
