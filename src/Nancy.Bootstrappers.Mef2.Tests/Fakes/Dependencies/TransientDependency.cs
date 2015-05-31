using System.Composition;

namespace Nancy.Bootstrappers.Mef2.Tests.Fakes.Dependencies
{
    [Export(typeof(ITransientDependency))]
    public class TransientDependency : ITransientDependency
    {
    }
}
