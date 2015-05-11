using Nancy.Bootstrapper;
using Nancy.Diagnostics;
using System.Composition.Convention;

namespace Nancy.Bootstrappers.Mef2
{
    public static class NancyConventions
    {
        public static ConventionBuilder WithNancyConventions(this ConventionBuilder conventions)
        {
            conventions.ForTypesDerivedFrom<INancyModule>()
                .Export()
                .Export<INancyModule>();

            conventions.ForTypesDerivedFrom<IApplicationStartup>().Export<IApplicationStartup>();
            conventions.ForTypesDerivedFrom<IDiagnostics>().Export<IDiagnostics>();
            conventions.ForTypesDerivedFrom<INancyEngine>().Export<INancyEngine>();
            conventions.ForTypesDerivedFrom<IRegistrations>().Export<IRegistrations>();

            return conventions;
        }
    }
}
