# Nancy.Bootstrappers.Mef2
A [bootstrapper](https://github.com/NancyFx/Nancy/wiki/Bootstrapper) for the [Nancy](http://nancyfx.org) web framework, utilizing a [Managed Extensibility Framework (MEF2)](https://mef.codeplex.com/) based container.

##Overview
Rather than inheriting from the `DefaultNancyBootstrapper`, you will instead want to inherit from the `CompositionContextNancyBootstrapper` to utilize the MEF-based `CompositionHost` container. Get the latest version of the `CompositionContextNancyBootstrapper` by installing the 'Nancy.Bootstrappers.Mef2' nuget.

##Features
The `CompositionContextNancyBootstrapper` supports all forms of dependency registration that the `DefaultNancyBootstrapper` offers (transient (both individual and collections), singleton, per-request and instance), as well as the attributed-style that MEF is known for (`[Export]` and `[Import]`). It also supports singleton-per-request, achieved through overriding and/or utilizing the `PerRequestBoundary` string in your exports e.g. `[Export, Shared("PerRequestBoundary")]`.

##Usage and Extending
The `CompositionContextNancyBootstrapper`, like all custom Nancy bootstrappers, is abstract and must be inherited to be used. Out of the box, the bootstrapper will perform roughly the same as the default bootstrapper without overrides - it will register all internal Nancy types, autoregister other types by their interface in the Nancy.* namespace, and register all implementations of `NancyModule` in the assembly of the bootstrapper.

Due to the nature of the way MEF2 works, it is recommended that you customize your implementation of `CompositionContextNancyBootstrapper` to get more control over what it is doing. The recommended overrides are listed below.

###Recommended Overrides
The following overrides are most likely the ones that will be of most use to the every day Nancy developer.
```c#
    public class Bootstrapper : CompositionContextNancyBootstrapper
    {
        protected override void ConfigureCompositionConventions(ConventionBuilder conventions)
        {
            base.ConfigureCompositionConventions(conventions);
            
            //Add part conventions to the ConventionBuilder to register your types the way you want.
            
            //Base implementation performs an export on ALL types in the Nancy.* namespace (outside Nancy itself).
            //This attains parity with Autoregister for Nancy references, but deliberately excludes user-provided types.
        }

        protected override void ConfigureCompositionAssemblies(IList<Assembly> assemblies)
        {
            base.ConfigureCompositionAssemblies(assemblies);
            
            //Add assemblies you would like your export conventions to apply to.
            
            //Base implementation adds the assembly that this implementation of the bootstrapper exists in.
            //It's probably a good idea to replace these with your own list (adding to the internal assemblies, of course).
        }

        protected override void ConfigureInstanceExportDescriptorProvider(InstanceExportDescriptorProvider provider)
        {
            base.ConfigureInstanceExportDescriptorProvider(provider);
            
            //Add pre-configured instance objects to the provider using provider.RegisterExport(type, object)
            
            //As MEF2 cannot register pre-configured 'instance' objects using the ConventionBuilder and TypedParts framework, a ExportDescriptorProvider is offered to serve object instances added before composition time.
        }

        protected override void ConfigureCompositionExportDescriptorProviders(IList<ExportDescriptorProvider> assemblies)
        {
            base.ConfigureCompositionExportDescriptorProviders(assemblies);
            
            //Add any other ExportDescriptorProviders you may want in your application.
        }

        protected override string PerRequestBoundary
        {
            get
            {
                return base.PerRequestBoundary;
                
                //Define the name of your sharing boundary to be interpreted as the per-request sharing boundary.
                
                //By default, it is "PerRequest".
                //This means that anything exported with this boundary (via attribute [Shared("PerRequest")] or convention .Shared("PerRequest")) will be served as a Singleton ONLY within that particular request.
            }
        }
    }
```

###Situation-specific Overrides
The following overrides are situation specific, and will offer more control over what's going on.
```c#
    public class Bootstrapper : CompositionContextNancyBootstrapper
    {
        protected override CompositionContext CreateApplicationContainer(ConventionBuilder conventions, IEnumerable<Assembly> assemblies, IEnumerable<ExportDescriptorProvider> providers)
        {
			//Replace if you want deeper control over creating the final application container.
			//This feeds in only the bare minimum internal conventions/assemblies/providers, and replacing this will skip the configuration methods listed above.
            
			//The base first creates a ContainerConfiguration() object.
			//Then, it executes all the configuration methods (as listed in the previous overrides group)
			//Finally, it plugs in the configured ConventionBuilder using the .WithDefaultConventions(), adds the configured assemblies using .WithAssemblies(), adds each provider (configured) using .WithProvider(), then returns the CompositionHost using .CreateContainer().
            
            return base.CreateApplicationContainer(conventions, assemblies, providers);
        }

        protected override CompositionContext CreateRequestContainer(NancyContext context, CompositionContext parentContainer)
        {
            //Replace if you want to change the way you derive request containers.
            
            //The base achieves this in much the same fashion Nick does in Alt.Composition.Web.Mvc, by creating a composition contract for an ExportFactory<CompositionContext> with the shared boundary name, and pulling out the result.
        
            return base.CreateRequestContainer(context, parentContainer);
        }

        protected override IEnumerable<Assembly> InternalAssemblies
        {
            get
            {
                //Override this if the conventions don't suit your needs.
                
                //The default gathers all applicable assemblies starting with "Nancy" (but not including "Nancy.Testing"). This isn't always safe.
                //For example, if your main project (housing your bootstrapper) starts with "Nancy", you will scan multiple implementations of INancyBootstrapper and things will get full cray...
            
                return base.InternalAssemblies;
            }
        }
    }
```

###Standard Overrides
The standard overrides, much the same as regular Nancy implementations.
```c#
    public class Bootstrapper : CompositionContextNancyBootstrapper
    {
        protected override void ApplicationStartup(CompositionContext container, IPipelines pipelines)
        {
            base.ApplicationStartup(container, pipelines);
        }

        protected override void RequestStartup(CompositionContext container, IPipelines pipelines, NancyContext context)
        {
            base.RequestStartup(container, pipelines, context);
        }

        protected override void ConfigureConventions(NancyConventions nancyConventions)
        {
            base.ConfigureConventions(nancyConventions);
        }
    }
```
