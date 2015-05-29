using Nancy.Bootstrapper;
using Nancy.Conventions;
using Nancy.Cryptography;
using Nancy.Diagnostics;
using Nancy.Localization;
using Nancy.ModelBinding;
using Nancy.Routing;
using Nancy.Security;
using Nancy.Validation;
using Nancy.ViewEngines;
using System;
using System.Collections.Generic;
using System.Composition;
using System.Composition.Convention;
using System.Composition.Hosting;
using System.Composition.Hosting.Core;
using System.Linq;
using System.Reflection;

namespace Nancy.Bootstrappers.Mef2
{
    public abstract class CompositionContextNancyBootstrapper : INancyBootstrapper, INancyModuleCatalog, IDisposable
    {
        private bool _disposing;
        private bool _initialised;

        protected CompositionContext ApplicationContainer { get; set; }
        protected ExportFactory<CompositionContext> RequestContainerFactory;

        public Type ModelBinderLocator { get; private set; }

        public CompositionContextNancyBootstrapper()
        {
            ApplicationPipelines = new Pipelines();
            _conventions = new NancyConventions();
        }

        public void Initialise()
        {
            if (InternalConfiguration == null)
                throw new InvalidOperationException("Configuration cannot be null");

            if (!InternalConfiguration.IsValid)
                throw new InvalidOperationException("Configuration is invalid");

            var compositionConventions = new ConventionBuilder();

            RegisterBootstrapperTypes(compositionConventions);

            var typeRegistrations = InternalConfiguration.GetTypeRegistations()
                                        .Concat(GetAdditionalTypes());

            var collectionTypeRegistrations = InternalConfiguration.GetCollectionTypeRegistrations()
                                                  .Concat(GetApplicationCollections());

            ConfigureConventions(Conventions);
            var conventionValidationResult = Conventions.Validate();
            if (!conventionValidationResult.Item1)
            {
                throw new InvalidOperationException(string.Format("Conventions are invalid:\n\n{0}", conventionValidationResult.Item2));
            }

            var instanceRegistrations = Conventions.GetInstanceRegistrations()
                                            .Concat(GetAdditionalInstances())
                                            .ToList();

            RegisterTypes(compositionConventions, typeRegistrations);
            RegisterCollectionTypes(compositionConventions, collectionTypeRegistrations);
            RegisterModules(compositionConventions);
            //RegisterRegistrationTasks(compositionConventions, instanceRegistrations, GetRegistrationTasks()); exporting and importing

            var exportDescriptorProviders = new[] { GetInstanceExportDescriptorProvider(instanceRegistrations) };
            ApplicationContainer = CreateApplicationContainer(compositionConventions, InternalAssemblies, exportDescriptorProviders);

            foreach (var applicationStartupTask in GetApplicationStartupTasks().ToList())
            {
                applicationStartupTask.Initialize(ApplicationPipelines);
            }
            
            ApplicationStartup(ApplicationContainer, ApplicationPipelines);

            RequestStartupTaskTypeCache = RequestStartupTasks.ToArray();

            if (FavIcon != null)
            {
                ApplicationPipelines.BeforeRequest.AddItemToStartOfPipeline(ctx =>
                {
                    if (ctx.Request == null || string.IsNullOrEmpty(ctx.Request.Path))
                    {
                        return null;
                    }

                    if (string.Equals(ctx.Request.Path, "/favicon.ico", StringComparison.InvariantCultureIgnoreCase))
                    {
                        var response = new Response
                        {
                            ContentType = "image/vnd.microsoft.icon",
                            StatusCode = HttpStatusCode.OK,
                            Contents = s => s.Write(this.FavIcon, 0, this.FavIcon.Length)
                        };

                        response.Headers["Cache-Control"] = "public, max-age=604800, must-revalidate";

                        return response;
                    }

                    return null;
                });
            }

            GetDiagnostics().Initialize(ApplicationPipelines);

            _initialised = true;
        }

        public void Dispose()
        {
            // Prevent StackOverflowException if ApplicationContainer.Dispose re-triggers this Dispose
            if (_disposing)
            {
                return;
            }

            // Only dispose if we're initialised, prevents possible issue with recursive disposing.
            if (!_initialised)
            {
                return;
            }

            _disposing = true;

            var container = ApplicationContainer as IDisposable;

            if (container == null)
            {
                return;
            }

            try
            {
                container.Dispose();
            }
            catch (ObjectDisposedException)
            {
            }
        }

        public IEnumerable<INancyModule> GetAllModules(NancyContext context)
        {
            return ApplicationContainer.GetExports<INancyModule>();
        }

        public INancyEngine GetEngine()
        {
            if (!_initialised)
            {
                throw new InvalidOperationException("Bootstrapper is not initialised. Call Initialise before GetEngine");
            }

            var engine = this.SafeGetNancyEngineInstance();

            engine.RequestPipelinesFactory = InitializeRequestPipelines;

            return engine;
        }

        protected IEnumerable<Assembly> InternalAssemblies
        {
            get
            {
                return new[] { typeof(INancyEngine).Assembly };
            }
        }

        protected virtual IPipelines InitializeRequestPipelines(NancyContext context)
        {
            var requestContainer = GetConfiguredRequestContainer(context);

            var requestPipelines =
                new Pipelines(ApplicationPipelines);

            if (RequestStartupTaskTypeCache.Any())
            {
                var startupTasks = GetRequestStartupTasks(this.ApplicationContainer, this.RequestStartupTaskTypeCache);

                foreach (var requestStartup in startupTasks)
                {
                    requestStartup.Initialize(requestPipelines, context);
                }
            }

            RequestStartup(requestContainer, requestPipelines, context);

            return requestPipelines;
        }

        protected virtual void RequestStartup(CompositionContext container, IPipelines pipelines, NancyContext context)
        {
        }

        private IEnumerable<IRequestStartup> GetRequestStartupTasks(CompositionContext container, Type[] requestStartupTypes)
        {
            return container.GetExports<IRequestStartup>();
        }

        private INancyEngine SafeGetNancyEngineInstance()
        {
            try
            {
                return ApplicationContainer.GetExport<INancyEngine>();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    "Something went wrong when trying to satisfy one of the dependencies during composition, make sure that you've registered all new dependencies in the container and inspect the innerexception for more details.",
                    ex);
            }
        }

        public INancyModule GetModule(Type moduleType, NancyContext context)
        {
            return ApplicationContainer.GetExport(moduleType) as INancyModule;
        }

        protected virtual void RegisterBootstrapperTypes(ConventionBuilder conventionBuilder)
        {
            conventionBuilder.ForType<TrivialCompositionContextNancyBootstrapper>().Export<INancyModuleCatalog>().Shared();
        }

        protected virtual void RegisterTypes(ConventionBuilder conventionBuilder, IEnumerable<TypeRegistration> typeRegistrations)
        {
            foreach (var typeRegistration in typeRegistrations)
            {
                switch (typeRegistration.Lifetime)
                {
                    case Lifetime.Transient:
                        conventionBuilder.ForType(typeRegistration.ImplementationType).Export(ct => ct.AsContractType(typeRegistration.RegistrationType));
                        break;
                    case Lifetime.Singleton:
                        conventionBuilder.ForType(typeRegistration.ImplementationType).Export(ct => ct.AsContractType(typeRegistration.RegistrationType)).Shared();
                        break;
                    case Lifetime.PerRequest:
                        conventionBuilder.ForType(typeRegistration.ImplementationType).Export(ct => ct.AsContractType(typeRegistration.RegistrationType)).Shared(PerRequestBoundary);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }
        
        protected IDiagnostics GetDiagnostics()
        {
            return ApplicationContainer.GetExport<IDiagnostics>();
        }

        protected virtual void RegisterCollectionTypes(ConventionBuilder conventionBuilder, IEnumerable<CollectionTypeRegistration> collectionTypeRegistrations)
        {
            foreach (var collectionTypeRegistration in collectionTypeRegistrations)
            {
                switch (collectionTypeRegistration.Lifetime)
                {
                    case Lifetime.Transient:
                        foreach (var implementationType in collectionTypeRegistration.ImplementationTypes)
                            conventionBuilder.ForType(implementationType).Export(ct => ct.AsContractType(collectionTypeRegistration.RegistrationType));
                        break;
                    case Lifetime.Singleton:
                        foreach (var implementationType in collectionTypeRegistration.ImplementationTypes)
                            conventionBuilder.ForType(implementationType).Export(ct => ct.AsContractType(collectionTypeRegistration.RegistrationType)).Shared();
                        break;
                    case Lifetime.PerRequest:
                        foreach (var implementationType in collectionTypeRegistration.ImplementationTypes)
                            conventionBuilder.ForType(implementationType).Export(ct => ct.AsContractType(collectionTypeRegistration.RegistrationType)).Shared(PerRequestBoundary);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }

        protected virtual void RegisterModules(ConventionBuilder conventionBuilder)
        {
            conventionBuilder
                .ForTypesMatching(t => typeof(INancyModule).IsAssignableFrom(t) && !InternalAssemblies.Contains(t.Assembly) && t != typeof(DiagnosticModule))
                .Export()
                .ExportInterfaces()
                .Shared(PerRequestBoundary);
        }

        protected virtual void RegisterRegistrationTasks(ConventionBuilder conventionBuilder, List<InstanceRegistration> instanceRegistrations, IEnumerable<IRegistrations> registrationTasks)
        {
            foreach (var registrationTask in registrationTasks.ToList())
            {
                var applicationTypeRegistrations = registrationTask.TypeRegistrations;

                if (applicationTypeRegistrations != null)
                {
                    RegisterTypes(conventionBuilder, applicationTypeRegistrations);
                }

                var applicationCollectionRegistrations = registrationTask.CollectionTypeRegistrations;

                if (applicationCollectionRegistrations != null)
                {
                    RegisterCollectionTypes(conventionBuilder, applicationCollectionRegistrations);
                }

                var applicationInstanceRegistrations = registrationTask.InstanceRegistrations;

                if (applicationInstanceRegistrations != null)
                {
                    instanceRegistrations.AddRange(applicationInstanceRegistrations);
                }
            }
        }

        private NancyInternalConfiguration _internalConfiguration;
        protected virtual NancyInternalConfiguration InternalConfiguration
        {
            get
            {
                return _internalConfiguration ?? (_internalConfiguration = NancyInternalConfiguration.Default);
            }
        }

        private readonly NancyConventions _conventions;
        protected virtual NancyConventions Conventions
        {
            get
            {
                return _conventions;
            }
        }

        protected virtual IEnumerable<Type> ViewEngines
        {
            get
            {
                return AppDomainAssemblyTypeScanner.TypesOf<IViewEngine>();
            }
        }
        
        protected virtual IEnumerable<Type> ModelBinders
        {
            get
            {
                return AppDomainAssemblyTypeScanner.TypesOf<IModelBinder>();
            }
        }
        
        protected virtual IEnumerable<Type> TypeConverters
        {
            get
            {
                return AppDomainAssemblyTypeScanner.TypesOf<ITypeConverter>(ScanMode.ExcludeNancy);
            }
        }
        
        protected virtual IEnumerable<Type> BodyDeserializers
        {
            get { return AppDomainAssemblyTypeScanner.TypesOf<IBodyDeserializer>(ScanMode.ExcludeNancy); }
        }
        
        protected virtual IEnumerable<Type> ApplicationStartupTasks
        {
            get { return AppDomainAssemblyTypeScanner.TypesOf<IApplicationStartup>(); }
        }
        
        protected virtual IEnumerable<Type> RequestStartupTasks
        {
            get { return AppDomainAssemblyTypeScanner.TypesOf<IRequestStartup>(); }
        }
        
        protected virtual IEnumerable<Type> RegistrationTasks
        {
            get { return AppDomainAssemblyTypeScanner.TypesOf<IRegistrations>(); }
        }
        
        protected virtual IEnumerable<Type> ModelValidatorFactories
        {
            get { return AppDomainAssemblyTypeScanner.TypesOf<IModelValidatorFactory>(); }
        }

        protected IPipelines ApplicationPipelines { get; private set; }

        protected virtual void ApplicationStartup(CompositionContext container, IPipelines pipelines)
        {
        }

        protected virtual byte[] FavIcon
        {
            get { return FavIconApplicationStartup.FavIcon; }
        }

        protected Type[] RequestStartupTaskTypeCache { get; private set; }

        protected virtual CryptographyConfiguration CryptographyConfiguration
        {
            get { return CryptographyConfiguration.Default; }
        }

        protected virtual DiagnosticsConfiguration DiagnosticsConfiguration
        {
            get { return new DiagnosticsConfiguration(); }
        }

        protected virtual IFileSystemReader FileSystemReader
        {
            get
            {
                return new DefaultFileSystemReader();
            }
        }

        private IRootPathProvider _rootPathProvider;
        protected virtual IRootPathProvider RootPathProvider
        {
            get { return _rootPathProvider ?? (_rootPathProvider = GetRootPathProvider()); }
        }
        
        private IEnumerable<TypeRegistration> GetAdditionalTypes()
        {
            return new[] {
                new TypeRegistration(typeof(IViewRenderer), typeof(DefaultViewRenderer))
            };
        }
        
        private IEnumerable<CollectionTypeRegistration> GetApplicationCollections()
        {
            return new[]
                {
                    new CollectionTypeRegistration(typeof(IViewEngine), ViewEngines),
                    new CollectionTypeRegistration(typeof(IModelBinder), ModelBinders),
                    new CollectionTypeRegistration(typeof(ITypeConverter), TypeConverters),
                    new CollectionTypeRegistration(typeof(IBodyDeserializer), BodyDeserializers),
                    new CollectionTypeRegistration(typeof(IApplicationStartup), ApplicationStartupTasks),
                    new CollectionTypeRegistration(typeof(IRegistrations), RegistrationTasks),
                    new CollectionTypeRegistration(typeof(IModelValidatorFactory), ModelValidatorFactories)
                };
        }

        private IEnumerable<InstanceRegistration> GetAdditionalInstances()
        {
            return new[] {
                new InstanceRegistration(typeof(CryptographyConfiguration), CryptographyConfiguration),
                new InstanceRegistration(typeof(NancyInternalConfiguration), InternalConfiguration),
                new InstanceRegistration(typeof(DiagnosticsConfiguration), DiagnosticsConfiguration),
                new InstanceRegistration(typeof(IRootPathProvider), RootPathProvider),
                new InstanceRegistration(typeof(IFileSystemReader), FileSystemReader)
            };
        }

        protected virtual IEnumerable<IApplicationStartup> GetApplicationStartupTasks()
        {
            return ApplicationContainer.GetExports<IApplicationStartup>();
        }

        protected virtual void ConfigureConventions(NancyConventions nancyConventions)
        {
        }

        private static IRootPathProvider GetRootPathProvider()
        {
            var providerTypes = AppDomainAssemblyTypeScanner
                .TypesOf<IRootPathProvider>(ScanMode.ExcludeNancy)
                .ToArray();

            if (providerTypes.Length > 1)
            {
                throw new MultipleRootPathProvidersLocatedException(providerTypes);
            }

            var providerType =
                providerTypes.SingleOrDefault() ?? typeof(DefaultRootPathProvider);

            return Activator.CreateInstance(providerType) as IRootPathProvider;
        }

        protected virtual string PerRequestBoundary
        {
            get { return "PerRequest"; }
        }

        protected virtual IEnumerable<IRegistrations> GetRegistrationTasks()
        {
            return ApplicationContainer.GetExports<IRegistrations>();
        }

        private readonly string _contextKey = typeof(CompositionContext).FullName + "BootstrapperChildContainer";
        protected virtual string ContextKey
        {
            get
            {
                return _contextKey;
            }
        }

        private CompositionContext GetConfiguredRequestContainer(NancyContext context)
        {
            object contextObject;
            context.Items.TryGetValue(this.ContextKey, out contextObject);
            var requestContainer = contextObject as CompositionContext;

            if (requestContainer == null)
            {
                requestContainer = CreateRequestContainer(context, ApplicationContainer);

                context.Items[this.ContextKey] = requestContainer;

            }

            return requestContainer;
        }

        protected virtual CompositionContext CreateRequestContainer(NancyContext context, CompositionContext parentContainer)
        {
            if (RequestContainerFactory == null)
            {
                var rcfContract = new CompositionContract(
                                    typeof(ExportFactory<CompositionContext>),
                                    null,
                                    new Dictionary<string, object> {
                        { "SharingBoundaryNames", new[] { PerRequestBoundary }} });

                RequestContainerFactory = (ExportFactory<CompositionContext>)parentContainer.GetExport(rcfContract);
            }

            return RequestContainerFactory.CreateExport().Value;
        }

        protected virtual CompositionContext CreateApplicationContainer(ConventionBuilder conventionBuilder, IEnumerable<Assembly> assemblies, IEnumerable<ExportDescriptorProvider> exportDescriptorProviders)
        {
            var containerConfiguration = new ContainerConfiguration().WithDefaultConventions(conventionBuilder)
                                                                    .WithAssemblies(assemblies);

            foreach (var provider in exportDescriptorProviders)
                containerConfiguration.WithProvider(provider);

            var container = containerConfiguration.CreateContainer();

            return container;
        }

        protected virtual ExportDescriptorProvider GetInstanceExportDescriptorProvider(IEnumerable<InstanceRegistration> instanceRegistrations)
        {
            return new InstanceExportDescriptorProvider(instanceRegistrations);
        }
    }
}
