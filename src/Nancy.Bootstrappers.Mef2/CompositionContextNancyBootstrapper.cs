using Nancy.Bootstrapper;
using System.Composition;
using Nancy.Diagnostics;
using System;
using System.Collections.Generic;

namespace Nancy.Bootstrappers.Mef2
{
    public abstract class CompositionContextNancyBootstrapper : NancyBootstrapperWithRequestContainerBase<CompositionContext>
    {
        protected override CompositionContext CreateRequestContainer(NancyContext context)
        {
            throw new NotImplementedException();
        }

        protected override IEnumerable<INancyModule> GetAllModules(CompositionContext container)
        {
            return container.GetExports<INancyModule>();
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

        protected override INancyModule GetModule(CompositionContext container, Type moduleType)
        {
            return container.GetExport(moduleType) as INancyModule;
        }

        protected override IEnumerable<IRegistrations> GetRegistrationTasks()
        {
            return ApplicationContainer.GetExports<IRegistrations>();
        }

        protected override IEnumerable<IRequestStartup> RegisterAndGetRequestStartupTasks(CompositionContext container, Type[] requestStartupTypes)
        {
            throw new NotImplementedException();
        }

        protected override void RegisterBootstrapperTypes(CompositionContext applicationContainer)
        {
            throw new NotImplementedException();
        }

        protected override void RegisterCollectionTypes(CompositionContext container, IEnumerable<CollectionTypeRegistration> collectionTypeRegistrationsn)
        {
            throw new NotImplementedException();
        }

        protected override void RegisterInstances(CompositionContext container, IEnumerable<InstanceRegistration> instanceRegistrations)
        {
            throw new NotImplementedException();
        }

        protected override void RegisterRequestContainerModules(CompositionContext container, IEnumerable<ModuleRegistration> moduleRegistrationTypes)
        {
            throw new NotImplementedException();
        }

        protected override void RegisterTypes(CompositionContext container, IEnumerable<TypeRegistration> typeRegistrations)
        {
            throw new NotImplementedException();
        }
    }
}
