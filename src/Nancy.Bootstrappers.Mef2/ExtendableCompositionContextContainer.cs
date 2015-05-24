using System;
using System.Collections.Generic;
using System.Composition;
using System.Composition.Convention;
using System.Composition.Hosting;
using System.Linq;
using System.Reflection;

namespace Nancy.Bootstrappers.Mef2
{
    public class ExtendableCompositionContextContainer : ICompositionContextContainer
    {
        private readonly ConventionBuilder _conventionBuilder;
        private readonly IEnumerable<Assembly> _scannableAssemblies;
        private readonly ContainerConfiguration _containerConfiguration;
        private CompositionContext _compositionContext;

        public ExtendableCompositionContextContainer()
        {
            _conventionBuilder = new ConventionBuilder();
            _scannableAssemblies = Enumerable.Empty<Assembly>();
            _containerConfiguration = new ContainerConfiguration()
                                            .WithDefaultConventions(_conventionBuilder)
                                            .WithAssemblies(_scannableAssemblies);
        }

        private CompositionContext SafeGetCompositionContext() {
            return _compositionContext ?? (_compositionContext = _containerConfiguration.CreateContainer());
        }

        public TExport GetExport<TExport>() where TExport : class
        {
            return SafeGetCompositionContext().GetExport<TExport>();
        }

        public object GetExport(Type type)
        {
            return SafeGetCompositionContext().GetExport(type);
        }

        public IEnumerable<TExport> GetExports<TExport>() where TExport : class
        {
            return SafeGetCompositionContext().GetExports<TExport>();
        }

        public IEnumerable<object> GetExports(Type type)
        {
            return SafeGetCompositionContext().GetExports(type);
        }

        public void Update(Action<ConventionBuilder> builderActions)
        {
            builderActions(_conventionBuilder);
            _compositionContext = null;
        }
    }
}
