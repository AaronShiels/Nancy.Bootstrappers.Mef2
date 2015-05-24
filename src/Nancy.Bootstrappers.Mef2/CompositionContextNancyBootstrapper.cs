using Nancy.Bootstrapper;
using System;
using System.Collections.Generic;
using Nancy.Diagnostics;

namespace Nancy.Bootstrappers.Mef2
{
    public abstract class CompositionContextNancyBootstrapper : NancyBootstrapperWithRequestContainerBase<ICompositionContextContainer>
    {
        protected override ICompositionContextContainer CreateRequestContainer(NancyContext context)
        {
            throw new NotImplementedException();
        }

        protected override IEnumerable<INancyModule> GetAllModules(ICompositionContextContainer container)
        {
            return container.GetExports<INancyModule>();
        }

        protected override ICompositionContextContainer GetApplicationContainer()
        {
            return new ExtendableCompositionContextContainer();
        }

        protected override IEnumerable<IApplicationStartup> GetApplicationStartupTasks()
        {
            return ApplicationContainer.GetExports<IApplicationStartup>();
        }

        protected override IDiagnostics GetDiagnostics()
        {
            return ApplicationContainer.GetExport<IDiagnostics>();
        }

        protected override INancyEngine GetEngineInternal()
        {
            return ApplicationContainer.GetExport<INancyEngine>();
        }

        protected override INancyModule GetModule(ICompositionContextContainer container, Type moduleType)
        {
            return ApplicationContainer.GetExport(moduleType) as INancyModule;
        }

        protected override IEnumerable<IRegistrations> GetRegistrationTasks()
        {
            return ApplicationContainer.GetExports<IRegistrations>();
        }

        protected override IEnumerable<IRequestStartup> RegisterAndGetRequestStartupTasks(ICompositionContextContainer container, Type[] requestStartupTypes)
        {
            throw new NotImplementedException();
        }

        protected override void RegisterBootstrapperTypes(ICompositionContextContainer applicationContainer)
        {
            throw new NotImplementedException();
        }

        protected override void RegisterCollectionTypes(ICompositionContextContainer container, IEnumerable<CollectionTypeRegistration> collectionTypeRegistrationsn)
        {
            throw new NotImplementedException();
        }

        protected override void RegisterInstances(ICompositionContextContainer container, IEnumerable<InstanceRegistration> instanceRegistrations)
        {
            throw new NotImplementedException();
        }

        protected override void RegisterRequestContainerModules(ICompositionContextContainer container, IEnumerable<ModuleRegistration> moduleRegistrationTypes)
        {
            throw new NotImplementedException();
        }

        protected override void RegisterTypes(ICompositionContextContainer container, IEnumerable<TypeRegistration> typeRegistrations)
        {
            throw new NotImplementedException();
        }
    }
}
