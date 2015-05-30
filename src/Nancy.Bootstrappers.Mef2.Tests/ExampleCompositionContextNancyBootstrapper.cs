using Nancy.Diagnostics;
using Nancy.Routing;
using System.Collections.Generic;
using System.Composition;
using System.Composition.Convention;
using System.Composition.Hosting.Core;
using System.Reflection;

namespace Nancy.Bootstrappers.Mef2.Tests
{
    public class ExampleCompositionContextNancyBootstrapper : CompositionContextNancyBootstrapper
    {
        protected override CompositionContext CreateApplicationContainer(ConventionBuilder conventionBuilder, IEnumerable<Assembly> assemblies, IEnumerable<ExportDescriptorProvider> exportDescriptorProviders)
        {
            return base.CreateApplicationContainer(conventionBuilder, assemblies, exportDescriptorProviders);
        }
    }
}
