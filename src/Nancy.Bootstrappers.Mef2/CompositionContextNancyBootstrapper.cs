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

            var instanceRegistrations = new[] { new InstanceRegistration(typeof(INancyModuleCatalog), this) }.ToList();

            var typeRegistrations = InternalConfiguration.GetTypeRegistations()
                                        .Concat(GetAdditionalTypes())
                                        .Concat(InternalConfiguration.GetCollectionTypeRegistrations()
                                            .Concat(GetApplicationCollections())
                                            .SelectMany(ctr => ctr.ImplementationTypes
                                                .Select(it => new TypeRegistration(ctr.RegistrationType, it, ctr.Lifetime))))
                                        .ToList();

            ConfigureConventions(Conventions);
            var conventionValidationResult = Conventions.Validate();
            if (!conventionValidationResult.Item1)
            {
                throw new InvalidOperationException(string.Format("Conventions are invalid:\n\n{0}", conventionValidationResult.Item2));
            }

            instanceRegistrations.AddRange(Conventions.GetInstanceRegistrations().Concat(GetAdditionalInstances()));

            RegisterRegistrationTasks(typeRegistrations, instanceRegistrations, GetRegistrationTasks(typeRegistrations, instanceRegistrations));

            var conventions = GetInternalCompositionConventions(typeRegistrations);
            ConfigureApplicationCompositionConventions(conventions);

            var providers = new[] { GetInternalInstanceExportDescriptorProvider(instanceRegistrations) }
                            .Concat(ConfigureApplicationExportDescriptorProviders());

            var assemblies = GetInternalAssemblies()
                                .Union(ConfigureApplicationCompositionAssemblies());
            
            ApplicationContainer = CreateApplicationContainer(conventions, assemblies, providers);

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

        public INancyModule GetModule(Type moduleType, NancyContext context)
        {
            var requestContainer = GetConfiguredRequestContainer(context);

            return requestContainer.GetExport(moduleType) as INancyModule;
        }

        public IEnumerable<INancyModule> GetAllModules(NancyContext context)
        {
            var requestContainer = GetConfiguredRequestContainer(context);

            return requestContainer.GetExports<INancyModule>();
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

        protected IDiagnostics GetDiagnostics()
        {
            return ApplicationContainer.GetExport<IDiagnostics>();
        }

        protected virtual void RegisterRegistrationTasks(List<TypeRegistration> typeRegistrations, List<InstanceRegistration> instanceRegistrations, IEnumerable<IRegistrations> registrationTasks)
        {
            foreach (var registrationTask in registrationTasks.ToList())
            {
                var applicationTypeRegistrations = registrationTask.TypeRegistrations;

                if (applicationTypeRegistrations != null)
                {
                    typeRegistrations.AddRange(applicationTypeRegistrations);
                }

                var applicationCollectionRegistrations = registrationTask.CollectionTypeRegistrations;

                if (applicationCollectionRegistrations != null)
                {
                    applicationCollectionRegistrations.SelectMany(ctr => ctr.ImplementationTypes.Select(it => new TypeRegistration(ctr.RegistrationType, it, ctr.Lifetime)));
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

        protected virtual IRootPathProvider RootPathProvider
        {
            get { return new DefaultRootPathProvider(); }
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
            //needed because MEF2 doesn't do magic func injection
            Func<IRouteCache> routeCacheFunc = () => ApplicationContainer.GetExport<IRouteCache>();

            return new[] {
                new InstanceRegistration(typeof(CryptographyConfiguration), CryptographyConfiguration),
                new InstanceRegistration(typeof(NancyInternalConfiguration), InternalConfiguration),
                new InstanceRegistration(typeof(DiagnosticsConfiguration), DiagnosticsConfiguration),
                new InstanceRegistration(typeof(IRootPathProvider), RootPathProvider),
                new InstanceRegistration(typeof(IFileSystemReader), FileSystemReader),
                new InstanceRegistration(typeof(Func<IRouteCache>), routeCacheFunc)
            };
        }

        protected virtual IEnumerable<IApplicationStartup> GetApplicationStartupTasks()
        {
            return ApplicationContainer.GetExports<IApplicationStartup>();
        }

        protected virtual void ConfigureConventions(NancyConventions nancyConventions)
        {
        }

        protected virtual string PerRequestBoundary
        {
            get { return "PerRequest"; }
        }

        protected virtual IEnumerable<IRegistrations> GetRegistrationTasks(IList<TypeRegistration> typeRegistrations, IList<InstanceRegistration> instanceRegistrations)
        {
            var internalTypeExportConventions = GetInternalCompositionConventions(typeRegistrations);
            var internalInstanceExportProvider = GetInternalInstanceExportDescriptorProvider(instanceRegistrations);
            var internalAssemblies = GetInternalAssemblies();
            var temporaryContainer = CreateContainerInternal(internalTypeExportConventions, internalAssemblies, new[] { internalInstanceExportProvider });

            return temporaryContainer.GetExports<IRegistrations>();
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

        private CompositionContext CreateContainerInternal(ConventionBuilder conventions, IEnumerable<Assembly> assemblies, IEnumerable<ExportDescriptorProvider> providers)
        {
            var containerConfiguration = new ContainerConfiguration().WithDefaultConventions(conventions)
                                                                    .WithAssemblies(assemblies);

            foreach (var provider in providers)
                containerConfiguration.WithProvider(provider);

            var container = containerConfiguration.CreateContainer();

            return container;
        }

        protected virtual CompositionContext CreateApplicationContainer(ConventionBuilder conventions, IEnumerable<Assembly> assemblies, IEnumerable<ExportDescriptorProvider> providers)
        {
            return CreateContainerInternal(conventions, assemblies, providers);
        }

        private ConventionBuilder GetInternalCompositionConventions(IList<TypeRegistration> typeRegistrations)
        {
            var conventionBuilder = new ConventionBuilder();

            typeRegistrations.GroupBy(tr => tr.ImplementationType)
                                .Select(g =>
                                {
                                    var partType = g.Key;

                                    if (g.Select(tr => tr.Lifetime).Distinct().Count() != 1)
                                        throw new InvalidOperationException("Conflicting lifetimes for an exported part");

                                    var interfaces = g.Select(tr => tr.RegistrationType).Where(rt => rt.IsInterface).Distinct();
                                    var asSelf = g.Select(tr => tr.RegistrationType).Any(rt => rt == partType);
                                    var lifeTime = g.Select(tr => tr.Lifetime).Distinct().Single();

                                    return new { PartType = partType, Interfaces = interfaces, ExportSelf = asSelf, Lifetime = lifeTime };
                                })
                                .ToList()
                                .ForEach(x =>
                                {
                                    Action<PartConventionBuilder> builderActions = (pcb) =>
                                    {
                                        if (x.ExportSelf)
                                            pcb.Export();

                                        if (x.Interfaces.Any())
                                            pcb.ExportInterfaces(t => x.Interfaces.Contains(t));

                                        if (x.Lifetime == Lifetime.Singleton)
                                            pcb.Shared();
                                        else if (x.Lifetime == Lifetime.PerRequest)
                                            pcb.Shared(PerRequestBoundary);
                                    };

                                    builderActions(conventionBuilder.ForType(x.PartType));
                                });

            conventionBuilder
                .ForTypesMatching(t => typeof(INancyModule).IsAssignableFrom(t) && !GetInternalAssemblies().Contains(t.Assembly) && t != typeof(DiagnosticModule))
                .Export()
                .ExportInterfaces()
                .Shared(PerRequestBoundary);

            return conventionBuilder;
        }

        private ExportDescriptorProvider GetInternalInstanceExportDescriptorProvider(IEnumerable<InstanceRegistration> instanceRegistrations)
        {
            return new InstanceExportDescriptorProvider(instanceRegistrations);
        }

        private IEnumerable<Assembly> GetInternalAssemblies()
        {
            return new[] { typeof(INancyEngine).Assembly };
        }

        protected virtual void ConfigureApplicationCompositionConventions(ConventionBuilder conventionBuilder)
        {

        }

        protected virtual IEnumerable<Assembly> ConfigureApplicationCompositionAssemblies()
        {
            return new[] { Assembly.GetExecutingAssembly() };
        }

        protected virtual IEnumerable<ExportDescriptorProvider> ConfigureApplicationExportDescriptorProviders()
        {
            return Enumerable.Empty<ExportDescriptorProvider>();
        }
    }
}
