using Nancy.Bootstrapper;
using System;
using System.Collections.Generic;
using Nancy.Diagnostics;
using System.Linq;
using System.Composition.Hosting.Core;
using System.Composition;

namespace Nancy.Bootstrappers.Mef2
{
    public abstract class TrivialCompositionContextNancyBootstrapper : NancyBootstrapperBase<ICompositionContextContainer>
    {
        private ExportFactory<CompositionContext> _requestContextFactory;
        protected abstract string PerRequestBoundary { get; }

        public override IEnumerable<INancyModule> GetAllModules(NancyContext context)
        {
            return ApplicationContainer.GetExports<INancyModule>();
        }

        public override INancyModule GetModule(Type moduleType, NancyContext context)
        {
            return ApplicationContainer.GetExport(moduleType) as INancyModule;
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

        protected override IEnumerable<IRegistrations> GetRegistrationTasks()
        {
            return ApplicationContainer.GetExports<IRegistrations>();
        }

        protected override IEnumerable<IRequestStartup> RegisterAndGetRequestStartupTasks(ICompositionContextContainer container, Type[] requestStartupTypes)
        {
            //No registration possible at this point

            return requestStartupTypes.Cast<IRequestStartup>();
        }

        protected override void RegisterBootstrapperTypes(ICompositionContextContainer applicationContainer)
        {
            applicationContainer.Update(cb => cb.ForType<TrivialCompositionContextNancyBootstrapper>().Export<INancyModuleCatalog>());
        }

        protected override void RegisterCollectionTypes(ICompositionContextContainer container, IEnumerable<CollectionTypeRegistration> collectionTypeRegistrations)
        {
            container.Update(cb =>
            {
                foreach (var collectionTypeRegistration in collectionTypeRegistrations)
                {
                    switch (collectionTypeRegistration.Lifetime)
                    {
                        case Lifetime.Transient:
                            foreach (var implementationType in collectionTypeRegistration.ImplementationTypes)
                                cb.ForType(implementationType).Export(ct => ct.AsContractType(collectionTypeRegistration.RegistrationType));
                            break;
                        case Lifetime.Singleton:
                            foreach (var implementationType in collectionTypeRegistration.ImplementationTypes)
                                cb.ForType(implementationType).Export(ct => ct.AsContractType(collectionTypeRegistration.RegistrationType)).Shared();
                            break;
                        case Lifetime.PerRequest:
                            foreach (var implementationType in collectionTypeRegistration.ImplementationTypes)
                                cb.ForType(implementationType).Export(ct => ct.AsContractType(collectionTypeRegistration.RegistrationType)).Shared(PerRequestBoundary);
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                }
            });
        }

        protected override void RegisterInstances(ICompositionContextContainer container, IEnumerable<InstanceRegistration> instanceRegistrations)
        {
            container.Update(cb =>
            {
                foreach (var instanceRegistration in instanceRegistrations)
                {
                    cb.ForType(instanceRegistration.Implementation.GetType()).Export(ct => ct.AsContractType(instanceRegistration.RegistrationType));
                }
            });
        }

        protected override void RegisterModules(ICompositionContextContainer container, IEnumerable<ModuleRegistration> moduleRegistrationTypes)
        {
            container.Update(cb =>
            {
                foreach (var moduleRegistrationType in moduleRegistrationTypes)
                {
                    cb.ForType(moduleRegistrationType.ModuleType).Export().Export<INancyModule>().Shared(PerRequestBoundary);
                }
            });
        }

        protected override void RegisterTypes(ICompositionContextContainer container, IEnumerable<TypeRegistration> typeRegistrations)
        {
            container.Update(cb =>
            {
                foreach (var typeRegistration in typeRegistrations)
                {
                    switch (typeRegistration.Lifetime)
                    {
                        case Lifetime.Transient:
                            cb.ForType(typeRegistration.ImplementationType).Export(ct => ct.AsContractType(typeRegistration.RegistrationType));
                            break;
                        case Lifetime.Singleton:
                            cb.ForType(typeRegistration.ImplementationType).Export(ct => ct.AsContractType(typeRegistration.RegistrationType)).Shared();
                            break;
                        case Lifetime.PerRequest:
                            cb.ForType(typeRegistration.ImplementationType).Export(ct => ct.AsContractType(typeRegistration.RegistrationType)).Shared(PerRequestBoundary);
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                }
            });
        }

        private readonly string _contextKey = typeof(ICompositionContextContainer).FullName + "BootstrapperChildContainer";
        protected virtual string ContextKey
        {
            get
            {
                return _contextKey;
            }
        }

        protected ICompositionContextContainer GetConfiguredRequestContainer(NancyContext context)
        {
            object contextObject;
            context.Items.TryGetValue(this.ContextKey, out contextObject);
            var requestContainer = contextObject as ICompositionContextContainer;

            if (requestContainer == null)
            {
                requestContainer = CreateRequestContainer(context, ApplicationContainer);

                context.Items[this.ContextKey] = requestContainer;
                
            }

            return requestContainer;
        }

        protected virtual ICompositionContextContainer CreateRequestContainer(NancyContext context, ICompositionContextContainer parentContainer)
        {
            if (_requestContextFactory == null)
            {
                var rcfContract = new CompositionContract(
                                    typeof(ExportFactory<CompositionContext>),
                                    null,
                                    new Dictionary<string, object> {
                        { "SharingBoundaryNames", new[] { PerRequestBoundary }} });

                _requestContextFactory = (ExportFactory<CompositionContext>)parentContainer.GetExport(rcfContract);
            }
            
            return new ReadonlyCompositionContextContainer(_requestContextFactory.CreateExport().Value);
        }
    }
}
