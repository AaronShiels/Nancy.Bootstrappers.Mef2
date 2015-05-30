using Nancy.Culture;
using Nancy.Diagnostics;
using Nancy.Routing;
using NUnit.Framework;
using System;
using System.Linq;

namespace Nancy.Bootstrappers.Mef2.Tests
{
    [TestFixture]
    public class CompositionTests
    {
        private ExampleCompositionContextNancyBootstrapper _bootstrapper;

        [SetUp]
        public void Init()
        {
            _bootstrapper = new ExampleCompositionContextNancyBootstrapper();
            _bootstrapper.Initialise();
        }

        [Test]
        public void Test()
        {
            //var funcThingy = _bootstrapper.GetThingsForTesting<Func<IRouteCache>>();
            //var defaultThingy = new DefaultRouteCacheProvider(null);
            var routeCache = _bootstrapper.GetThingsForTesting<IRouteCache>();
            var nancyEngine = _bootstrapper.GetThingsForTesting<INancyEngine>();
            var otherThingy = _bootstrapper.GetThingsForTesting<DefaultRouteCacheProvider>();
            var dp = _bootstrapper.GetThingsForTesting<IDiagnosticsProvider>();


            var diagnosticsProviders = _bootstrapper.GetThingsForTesting<INancyModuleCatalog>();
            //var rootPathProvider = _bootstrapper.GetThingsForTesting<IRootPathProvider>();

            Assert.True(1 == 1);
        }
    }
}
