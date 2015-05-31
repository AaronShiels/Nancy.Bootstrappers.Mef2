using System.Composition;

namespace Nancy.Bootstrappers.Mef2.Tests.Fakes.Dependencies
{
    [Export(typeof(IPerRequestDependency))]
    public class PerRequestDependency : IPerRequestDependency
    {
    }
}
