using System.Composition;

namespace Nancy.Bootstrappers.Mef2.Tests.Fakes.Dependencies
{
    [Export(typeof(ISingletonDependency)), Shared]
    public class SingletonDependency : ISingletonDependency
    {
    }
}
