using Nancy.Bootstrappers.Mef2.Tests.Fakes.Dependencies;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Composition;
using System.Composition.Convention;
using Nancy.Bootstrapper;

namespace Nancy.Bootstrappers.Mef2.Tests.Fakes
{
    public class FakeCompositionContextNancyBootstrapper : CompositionContextNancyBootstrapper
    {
        protected override CompositionContext CreateApplicationContainer(ConventionBuilder internalConventions, IList<Assembly> internalAssemblies, InstanceExportDescriptorProvider instanceProvider)
        {
            internalAssemblies.Add(this.GetType().Assembly);
            instanceProvider.RegisterExport(typeof(IInstanceDependency), new InstanceDependency("Mah secrat messahhge!"));

            return base.CreateApplicationContainer(internalConventions, internalAssemblies, instanceProvider);
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
