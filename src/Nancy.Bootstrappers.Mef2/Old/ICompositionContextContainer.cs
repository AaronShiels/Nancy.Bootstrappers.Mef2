using System;
using System.Collections.Generic;
using System.Composition.Convention;
using System.Composition.Hosting.Core;

namespace Nancy.Bootstrappers.Mef2
{
    public interface ICompositionContextContainer
    {
        TExport GetExport<TExport>() where TExport : class;
        object GetExport(Type type);
        object GetExport(CompositionContract compositionContract);
        IEnumerable<TExport> GetExports<TExport>() where TExport : class;
        IEnumerable<object> GetExports(Type type);
        void Update(Action<ConventionBuilder> builderActions);
    }
}
