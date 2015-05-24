using System;
using System.Collections.Generic;
using System.Composition.Convention;

namespace Nancy.Bootstrappers.Mef2
{
    public interface ICompositionContextContainer
    {
        TExport GetExport<TExport>() where TExport : class;
        object GetExport(Type type);
        IEnumerable<TExport> GetExports<TExport>() where TExport : class;
        IEnumerable<object> GetExports(Type type);
        void Update(Action<ConventionBuilder> builderActions);
    }
}
