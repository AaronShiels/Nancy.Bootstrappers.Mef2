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
using System.Linq;
using System.Reflection;

namespace Nancy.Bootstrappers.Mef2
{
    public abstract class HardcoreCompositionContextNancyBootstrapper : INancyBootstrapper, INancyModuleCatalog, IDisposable
    {
        private bool _disposing;
        private bool _initialised;
        protected CompositionContext ApplicationContainer { get; set; }
        public Type ModelBinderLocator { get; private set; }

        public void Initialise()
        {
            if (InternalConfiguration == null)
                throw new InvalidOperationException("Configuration cannot be null");

            if (!InternalConfiguration.IsValid)
                throw new InvalidOperationException("Configuration is invalid");

            var conventionBuilder = new ConventionBuilder();
            var containerConfiguration = new ContainerConfiguration();

            RegisterBootstrapperTypes(conventionBuilder);

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
                                            .Concat(GetAdditionalInstances());

            RegisterTypes(conventionBuilder, typeRegistrations);
            RegisterCollectionTypes(conventionBuilder, collectionTypeRegistrations);
            RegisterModules(conventionBuilder, Modules);
            RegisterInstances(conventionBuilder, instanceRegistrations);
            RegisterRegistrationTasks(conventionBuilder, GetRegistrationTasks());

            foreach (var applicationStartupTask in GetApplicationStartupTasks().ToList())
            {
                applicationStartupTask.Initialize(ApplicationPipelines);
            }

            ConfigureContainerConventions(conventionBuilder);

            containerConfiguration.WithDefaultConventions(conventionBuilder)
                                  .WithAssemblies(GetInternalAssemblies(AutoRegisterIgnoredAssemblies));

            ConfigureContainerAssemblies(containerConfiguration);

            ApplicationStartup(ApplicationContainer, ApplicationPipelines);

            //RequestStartupTaskTypeCache = RequestStartupTasks.ToArray();

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

            //this.GetDiagnostics().Initialize(this.ApplicationPipelines);

            //this.initialised = true;
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
            return ApplicationContainer.GetExport<INancyEngine>();
        }

        public INancyModule GetModule(Type moduleType, NancyContext context)
        {
            return ApplicationContainer.GetExport(moduleType) as INancyModule;
        }

        protected virtual void RegisterBootstrapperTypes(ConventionBuilder conventionBuilder)
        {
            conventionBuilder.ForType<CompositionContextNancyBootstrapper>().Export<INancyModuleCatalog>().Shared();
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
                        conventionBuilder.ForType(typeRegistration.ImplementationType).Export(ct => ct.AsContractType(typeRegistration.RegistrationType)).Shared(PerRequestBounday);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
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
                            conventionBuilder.ForType(implementationType).Export(ct => ct.AsContractType(collectionTypeRegistration.RegistrationType)).Shared(PerRequestBounday);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }

        protected virtual void RegisterModules(ConventionBuilder conventionBuilder, IEnumerable<ModuleRegistration> moduleRegistrationTypes)
        {
            foreach (var moduleRegistrationType in moduleRegistrationTypes)
            {
                conventionBuilder.ForType(moduleRegistrationType.ModuleType).Export().Export<INancyModule>().Shared(PerRequestBounday);
            }
        }

        protected virtual void RegisterInstances(ConventionBuilder conventionBuilder, IEnumerable<InstanceRegistration> instanceRegistrations)
        {
            foreach (var instanceRegistration in instanceRegistrations)
            {
                conventionBuilder.ForType(instanceRegistration.Implementation.GetType()).Export(ct => ct.AsContractType(instanceRegistration.RegistrationType));
            }
        }

        protected virtual void RegisterRegistrationTasks(ConventionBuilder conventionBuilder, IEnumerable<IRegistrations> registrationTasks)
        {
            foreach (var registrationTask in registrationTasks.ToList())
            {
                var applicationTypeRegistrations = registrationTask.TypeRegistrations;

                if (applicationTypeRegistrations != null)
                {
                    this.RegisterTypes(conventionBuilder, applicationTypeRegistrations);
                }

                var applicationCollectionRegistrations = registrationTask.CollectionTypeRegistrations;

                if (applicationCollectionRegistrations != null)
                {
                    this.RegisterCollectionTypes(conventionBuilder, applicationCollectionRegistrations);
                }

                var applicationInstanceRegistrations = registrationTask.InstanceRegistrations;

                if (applicationInstanceRegistrations != null)
                {
                    this.RegisterInstances(conventionBuilder, applicationInstanceRegistrations);
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
        
        protected virtual CryptographyConfiguration CryptographyConfiguration
        {
            get { return CryptographyConfiguration.Default; }
        }
        
        protected virtual DiagnosticsConfiguration DiagnosticsConfiguration
        {
            get { return new DiagnosticsConfiguration(); }
        }

        private IRootPathProvider _rootPathProvider;
        protected virtual IRootPathProvider RootPathProvider
        {
            get { return _rootPathProvider ?? (_rootPathProvider = GetRootPathProvider()); }
        }
        
        private IEnumerable<TypeRegistration> GetAdditionalTypes()
        {
            return new[] {
                new TypeRegistration(typeof(IViewRenderer), typeof(DefaultViewRenderer)),
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
            };
        }

        private IEnumerable<Assembly> GetInternalAssemblies(IEnumerable<Func<Assembly, bool>> ignoredAssemblies)
        {
            return AppDomain.CurrentDomain.GetAssemblies().Where(a => !ignoredAssemblies.Any(ia => ia(a)));
        }

        public static IEnumerable<Func<Assembly, bool>> DefaultAutoRegisterIgnoredAssemblies = new Func<Assembly, bool>[]
            {
                asm => asm.FullName.StartsWith("Microsoft.", StringComparison.InvariantCulture),
                asm => asm.FullName.StartsWith("System.", StringComparison.InvariantCulture),
                asm => asm.FullName.StartsWith("System,", StringComparison.InvariantCulture),
                asm => asm.FullName.StartsWith("CR_ExtUnitTest", StringComparison.InvariantCulture),
                asm => asm.FullName.StartsWith("mscorlib,", StringComparison.InvariantCulture),
                asm => asm.FullName.StartsWith("CR_VSTest", StringComparison.InvariantCulture),
                asm => asm.FullName.StartsWith("DevExpress.CodeRush", StringComparison.InvariantCulture),
                asm => asm.FullName.StartsWith("IronPython", StringComparison.InvariantCulture),
                asm => asm.FullName.StartsWith("IronRuby", StringComparison.InvariantCulture),
                asm => asm.FullName.StartsWith("xunit", StringComparison.InvariantCulture),
                asm => asm.FullName.StartsWith("Nancy.Testing", StringComparison.InvariantCulture),
                asm => asm.FullName.StartsWith("MonoDevelop.NUnit", StringComparison.InvariantCulture),
                asm => asm.FullName.StartsWith("SMDiagnostics", StringComparison.InvariantCulture),
                asm => asm.FullName.StartsWith("CppCodeProvider", StringComparison.InvariantCulture),
                asm => asm.FullName.StartsWith("WebDev.WebHost40", StringComparison.InvariantCulture),
            };

        protected virtual IEnumerable<Func<Assembly, bool>> AutoRegisterIgnoredAssemblies
        {
            get { return DefaultAutoRegisterIgnoredAssemblies; }
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

        protected virtual string PerRequestBounday
        {
            get { return "PerRequest"; }
        }

        private ModuleRegistration[] _modules;

        protected virtual IEnumerable<ModuleRegistration> Modules
        {
            get
            {
                return
                    _modules
                    ??
                    (_modules = AppDomainAssemblyTypeScanner
                                        .TypesOf<INancyModule>(ScanMode.ExcludeNancy)
                                        .NotOfType<DiagnosticModule>()
                                        .Select(t => new ModuleRegistration(t))
                                        .ToArray());
            }
        }

        protected virtual IEnumerable<IRegistrations> GetRegistrationTasks()
        {
            return ApplicationContainer.GetExports<IRegistrations>();
        }

        protected virtual void ConfigureContainerConventions(ConventionBuilder conventionBuilder)
        {

        }

        protected virtual void ConfigureContainerAssemblies(ContainerConfiguration containerConfiguration)
        {

        }
    }
}
