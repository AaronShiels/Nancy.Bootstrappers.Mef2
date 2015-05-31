using Nancy.Bootstrappers.Mef2.Tests.Fakes;
using NUnit.Framework;
using Shouldly;
using System.Linq;

namespace Nancy.Bootstrappers.Mef2.Tests
{
    [TestFixture]
    public class CompositionContextNancyBoostrapperSpecs
    {
        private FakeCompositionContextNancyBootstrapper _bootstrapper;

        [SetUp]
        public void Init()
        {
            _bootstrapper = new FakeCompositionContextNancyBootstrapper();
            _bootstrapper.Initialise();
        }

        [Test]
        public void WhenGettingEngineReturnNancyEngine()
        {
            var engine = _bootstrapper.GetEngine();

            engine.ShouldBeAssignableTo<INancyEngine>();
            engine.ShouldNotBe(null);
        }

        [Test]
        public void WhenGettingModulesShouldReturnAllNancyModules()
        {
            var modules = _bootstrapper.GetAllModules(new NancyContext());

            modules.ShouldAllBe(m => m is INancyModule);
            modules.Any(m => m is FakeNancyModule).ShouldBe(true);
        }

        [Test]
        public void WhenGettingModuleShouldReturnSpecificModule()
        {
            var module = _bootstrapper.GetModule(typeof(FakeNancyModule), new NancyContext()) as FakeNancyModule;

            module.ShouldNotBe(null);
        }

        [Test]
        public void WhenGettingModulesShouldRespectLifetimeDependenciesOfTransient()
        {
            var module1 = _bootstrapper.GetAllModules(new NancyContext()).Single(m => m is FakeNancyModule) as FakeNancyModule;
            var module2 = _bootstrapper.GetAllModules(new NancyContext()).Single(m => m is FakeNancyModule) as FakeNancyModule;

            module1.TransientDependency.ShouldNotBe(null);
            module2.TransientDependency.ShouldNotBe(null);
            module1.TransientDependency.ShouldNotBeSameAs(module2.TransientDependency);
        }

        [Test]
        public void WhenGettingModuleShouldRespectLifetimeDependenciesOfTransient()
        {
            var module1 = _bootstrapper.GetModule(typeof(FakeNancyModule), new NancyContext()) as FakeNancyModule;
            var module2 = _bootstrapper.GetModule(typeof(FakeNancyModule), new NancyContext()) as FakeNancyModule;

            module1.TransientDependency.ShouldNotBe(null);
            module2.TransientDependency.ShouldNotBe(null);

            module1.TransientDependency.ShouldNotBeSameAs(module2.TransientDependency);
        }

        [Test]
        public void WhenGettingModulesShouldRespectLifetimeDependenciesOfSingleton()
        {
            var module1 = _bootstrapper.GetAllModules(new NancyContext()).Single(m => m is FakeNancyModule) as FakeNancyModule;
            var module2 = _bootstrapper.GetAllModules(new NancyContext()).Single(m => m is FakeNancyModule) as FakeNancyModule;

            module1.SingletonDependency.ShouldNotBe(null);
            module2.SingletonDependency.ShouldNotBe(null);

            module1.SingletonDependency.ShouldBeSameAs(module2.SingletonDependency);
        }

        [Test]
        public void WhenGettingModuleShouldRespectLifetimeDependenciesOfSingleton()
        {
            var module1 = _bootstrapper.GetModule(typeof(FakeNancyModule), new NancyContext()) as FakeNancyModule;
            var module2 = _bootstrapper.GetModule(typeof(FakeNancyModule), new NancyContext()) as FakeNancyModule;

            module1.SingletonDependency.ShouldNotBe(null);
            module2.SingletonDependency.ShouldNotBe(null);

            module1.SingletonDependency.ShouldBeSameAs(module2.SingletonDependency);
        }

        [Test]
        public void WhenGettingModulesShouldRespectLifetimeDependenciesOfPerRequest()
        {
            var requestContext1 = new NancyContext();
            var requestContext2 = new NancyContext();

            var request1Module1 = _bootstrapper.GetAllModules(requestContext1).Single(m => m is FakeNancyModule) as FakeNancyModule;
            var request1Module2 = _bootstrapper.GetAllModules(requestContext1).Single(m => m is FakeNancyModule) as FakeNancyModule;
            var request2Module1 = _bootstrapper.GetAllModules(requestContext2).Single(m => m is FakeNancyModule) as FakeNancyModule;

            request1Module1.PerRequestDependency.ShouldNotBe(null);
            request1Module2.PerRequestDependency.ShouldNotBe(null);
            request2Module1.PerRequestDependency.ShouldNotBe(null);

            request1Module1.PerRequestDependency.ShouldBeSameAs(request1Module2.PerRequestDependency);
            request1Module1.PerRequestDependency.ShouldNotBeSameAs(request2Module1.PerRequestDependency);
        }

        [Test]
        public void WhenGettingModuleShouldRespectLifetimeDependenciesOfPerRequest()
        {
            var requestContext1 = new NancyContext();
            var requestContext2 = new NancyContext();

            var request1Module1 = _bootstrapper.GetModule(typeof(FakeNancyModule), requestContext1) as FakeNancyModule;
            var request1Module2 = _bootstrapper.GetModule(typeof(FakeNancyModule), requestContext1) as FakeNancyModule;
            var request2Module1 = _bootstrapper.GetModule(typeof(FakeNancyModule), requestContext2) as FakeNancyModule;

            request1Module1.PerRequestDependency.ShouldNotBe(null);
            request1Module2.PerRequestDependency.ShouldNotBe(null);
            request2Module1.PerRequestDependency.ShouldNotBe(null);

            request1Module1.PerRequestDependency.ShouldBeSameAs(request1Module2.PerRequestDependency);
            request1Module1.PerRequestDependency.ShouldNotBeSameAs(request2Module1.PerRequestDependency);
        }

        [Test]
        public void WhenGettingModulesShouldRespectInstanceDependencies()
        {
            var preConfiguredSecretInstanceMessage = "Mah secrat messahhge!"; //As configured in FakeCompositionContextNancyBootstrapper.cs
            var module = _bootstrapper.GetAllModules(new NancyContext()).Single(m => m is FakeNancyModule) as FakeNancyModule;
            
            module.InstanceDependency.ShouldNotBe(null);
            module.InstanceDependency.SecretPreConfiguredMessage.ShouldBe(preConfiguredSecretInstanceMessage);
        }
    }
}
