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
using System.Linq;

namespace Nancy.Bootstrappers.Mef2
{
    public abstract class CompositionContextNancyBootstrapper : INancyBootstrapper, INancyModuleCatalog, IDisposable
    {
        private bool _disposing;
        private bool _initialised;
        protected CompositionContext ApplicationContainer { get; set; }
        public Type ModelBinderLocator { get; private set; }

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

        /// <summary>
        /// Register the various collections into the container as singletons to later be resolved
        /// by IEnumerable{Type} constructor dependencies.
        /// </summary>
        /// <param name="container">Container to register into</param>
        /// <param name="collectionTypeRegistrationsn">Collection type registrations to register</param>
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

        /// <summary>
        /// Register the various collections into the container as singletons to later be resolved
        /// by IEnumerable{Type} constructor dependencies.
        /// </summary>
        /// <param name="container">Container to register into</param>
        /// <param name="collectionTypeRegistrationsn">Collection type registrations to register</param>
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

        /// <summary>
        /// Register the given module types into the container
        /// </summary>
        /// <param name="container">Container to register into</param>
        /// <param name="moduleRegistrationTypes">NancyModule types</param>
        protected virtual void RegisterModules(ConventionBuilder conventionBuilder, IEnumerable<ModuleRegistration> moduleRegistrationTypes)
        {
            foreach (var moduleRegistrationType in moduleRegistrationTypes)
            {
                conventionBuilder.ForType(moduleRegistrationType.ModuleType).Export().Export<INancyModule>().Shared(PerRequestBounday);
            }
        }

        public void Initialise()
        {
            if (InternalConfiguration == null)
            {
                throw new InvalidOperationException("Configuration cannot be null");
            }

            if (!InternalConfiguration.IsValid)
            {
                throw new InvalidOperationException("Configuration is invalid");
            }

            var conventionBuilder = new ConventionBuilder();

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
        }

        /// <summary>
        /// Nancy internal configuration
        /// </summary>
        private NancyInternalConfiguration _internalConfiguration;
        protected virtual NancyInternalConfiguration InternalConfiguration
        {
            get
            {
                return _internalConfiguration ?? (_internalConfiguration = NancyInternalConfiguration.Default);
            }
        }

        /// <summary>
        /// Default Nancy conventions
        /// </summary>
        private readonly NancyConventions _conventions;
        protected virtual NancyConventions Conventions
        {
            get
            {
                return _conventions;
            }
        }

        /// <summary>
        /// Gets the available view engine types
        /// </summary>
        protected virtual IEnumerable<Type> ViewEngines
        {
            get
            {
                return AppDomainAssemblyTypeScanner.TypesOf<IViewEngine>();
            }
        }

        /// <summary>
        /// Gets the available custom model binders
        /// </summary>
        protected virtual IEnumerable<Type> ModelBinders
        {
            get
            {
                return AppDomainAssemblyTypeScanner.TypesOf<IModelBinder>();
            }
        }

        /// <summary>
        /// Gets the available custom type converters
        /// </summary>
        protected virtual IEnumerable<Type> TypeConverters
        {
            get
            {
                return AppDomainAssemblyTypeScanner.TypesOf<ITypeConverter>(ScanMode.ExcludeNancy);
            }
        }

        /// <summary>
        /// Gets the available custom body deserializers
        /// </summary>
        protected virtual IEnumerable<Type> BodyDeserializers
        {
            get { return AppDomainAssemblyTypeScanner.TypesOf<IBodyDeserializer>(ScanMode.ExcludeNancy); }
        }

        /// <summary>
        /// Gets all application startup tasks
        /// </summary>
        protected virtual IEnumerable<Type> ApplicationStartupTasks
        {
            get { return AppDomainAssemblyTypeScanner.TypesOf<IApplicationStartup>(); }
        }

        /// <summary>
        /// Gets all request startup tasks
        /// </summary>
        protected virtual IEnumerable<Type> RequestStartupTasks
        {
            get { return AppDomainAssemblyTypeScanner.TypesOf<IRequestStartup>(); }
        }

        /// <summary>
        /// Gets all registration tasks
        /// </summary>
        protected virtual IEnumerable<Type> RegistrationTasks
        {
            get { return AppDomainAssemblyTypeScanner.TypesOf<IRegistrations>(); }
        }
        
        /// <summary>
        /// Gets the validator factories.
        /// </summary>
        protected virtual IEnumerable<Type> ModelValidatorFactories
        {
            get { return AppDomainAssemblyTypeScanner.TypesOf<IModelValidatorFactory>(); }
        }

        /// <summary>
        /// Gets the default favicon
        /// </summary>
        protected virtual byte[] FavIcon
        {
            get { return FavIconApplicationStartup.FavIcon; }
        }

        /// <summary>
        /// Gets the cryptography configuration
        /// </summary>
        protected virtual CryptographyConfiguration CryptographyConfiguration
        {
            get { return CryptographyConfiguration.Default; }
        }

        /// <summary>
        /// Gets the diagnostics / dashboard configuration (password etc)
        /// </summary>
        protected virtual DiagnosticsConfiguration DiagnosticsConfiguration
        {
            get { return new DiagnosticsConfiguration(); }
        }

        /// <summary>
        /// Gets the root path provider
        /// </summary>
        private IRootPathProvider _rootPathProvider;
        protected virtual IRootPathProvider RootPathProvider
        {
            get { return _rootPathProvider ?? (_rootPathProvider = GetRootPathProvider()); }
        }

        /// <summary>
        /// Gets additional required type registrations
        /// that don't form part of the core Nancy configuration
        /// </summary>
        /// <returns>Collection of TypeRegistration types</returns>
        private IEnumerable<TypeRegistration> GetAdditionalTypes()
        {
            return new[] {
                new TypeRegistration(typeof(IViewRenderer), typeof(DefaultViewRenderer)),
            };
        }

        /// <summary>
        /// Creates a list of types for the collection types that are
        /// required to be registered in the application scope.
        /// </summary>
        /// <returns>Collection of CollectionTypeRegistration types</returns>
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

        /// <summary>
        /// Gets any additional instance registrations that need to
        /// be registered into the container
        /// </summary>
        /// <returns>Collection of InstanceRegistration types</returns>
        private IEnumerable<InstanceRegistration> GetAdditionalInstances()
        {
            return new[] {
                new InstanceRegistration(typeof(CryptographyConfiguration), CryptographyConfiguration),
                new InstanceRegistration(typeof(NancyInternalConfiguration), InternalConfiguration),
                new InstanceRegistration(typeof(DiagnosticsConfiguration), DiagnosticsConfiguration),
                new InstanceRegistration(typeof(IRootPathProvider), RootPathProvider),
            };
        }

        /// <summary>
        /// Overrides/configures Nancy's conventions
        /// </summary>
        /// <param name="nancyConventions">Convention object instance</param>
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

        protected abstract string PerRequestBounday
        {
            get;
        }

        /// <summary>
        /// Nancy modules - built on startup from the app domain scanner
        /// </summary>
        private ModuleRegistration[] _modules;

        /// <summary>
        /// Gets all available module types
        /// </summary>
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

        /// <summary>
        /// Gets all registered application registration tasks
        /// </summary>
        /// <returns>An <see cref="IEnumerable{T}"/> instance containing <see cref="IRegistrations"/> instances.</returns>
        protected virtual IEnumerable<IRegistrations> GetRegistrationTasks()
        {
            return ApplicationContainer.GetExports<IRegistrations>();
        }
    }
}
