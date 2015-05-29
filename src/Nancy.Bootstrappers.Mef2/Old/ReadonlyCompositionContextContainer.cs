using System;
using System.Collections.Generic;
using System.Composition;
using System.Composition.Convention;
using System.Composition.Hosting.Core;

namespace Nancy.Bootstrappers.Mef2
{
    public class ReadonlyCompositionContextContainer : ICompositionContextContainer
    {
        private readonly CompositionContext _compositionContext;

        public ReadonlyCompositionContextContainer(CompositionContext compositionContext)
        {
            _compositionContext = compositionContext;
        }

        public TExport GetExport<TExport>() where TExport : class
        {
            return _compositionContext.GetExport<TExport>();
        }

        public object GetExport(Type type)
        {
            return _compositionContext.GetExport(type);
        }

        public IEnumerable<TExport> GetExports<TExport>() where TExport : class
        {
            return _compositionContext.GetExports<TExport>();
        }

        public IEnumerable<object> GetExports(Type type)
        {
            return _compositionContext.GetExports(type);
        }

        public void Update(Action<ConventionBuilder> builderActions)
        {
            throw new NotImplementedException();
        }

        public object GetExport(CompositionContract compositionContract)
        {
            return _compositionContext.GetExport(compositionContract);
        }
    }
}
