# Nancy.Bootstrappers.Mef2
A [bootstrapper](https://github.com/NancyFx/Nancy/wiki/Bootstrapper) for the [Nancy](http://nancyfx.org) web framework, utilizing a [Managed Extensibility Framework (MEF2)](https://mef.codeplex.com/) based container.

##Overview
Rather than inheriting from the `DefaultNancyBootstrapper`, you will instead want to inherit from the `CompositionContextNancyBootstrapper` to utilize the MEF-based `CompositionHost` container. Get the latest version of the `CompositionContextNancyBootstrapper` by installing the 'Nancy.Bootstrappers.Mef2' nuget.

##Features
The `CompositionContextNancyBootstrapper` supports all forms of dependency registration that the `DefaultNancyBootstrapper` offers (transient (both individual and collections), singleton, per-request and instance), as well as the attributed-style that MEF is known for (`[Export]` and `[Import]`). It also supports singleton-per-request, achieved through overriding and/or utilizing the `PerRequestBoundary` string in your exports e.g. `[Export, Shared("PerRequestBoundary")]`.

##Usage and Extending
The `CompositionContextNancyBootstrapper`, like all custom Nancy bootstrappers, is abstract and must be inherited to be used. Out of the box, the bootstrapper will perform the bare minimum work to have Nancy work internally - it will register all internal Nancy types from the Nancy.* assemblies. Past here, it is up to the implementer to include the assembly containing Nancy modules, as well as any custom conventions and assemblies that need to be registered related to the application itself (note: the conventions for registering implementations of `NancyModule` are already there, just not the assembly to look into).

Due to the nature of the way MEF2 works, it is too dangerous to offer auto-registration by default, so it is necessary that you customize your implementation of `CompositionContextNancyBootstrapper` to get the needed control over what is happening. The recommended overrides are listed below.

###Recommended Overrides
The following overrides are most likely the ones that will be of most use to the every day Nancy developer.
```c#
    public class Bootstrapper : CompositionContextNancyBootstrapper
    {
        protected override CompositionContext CreateApplicationContainer(ConventionBuilder internalConventions, IList<Assembly> internalAssemblies, InstanceExportDescriptorProvider instanceProvider)
        {
			//Add to the beginning of this method to extend on the conventions, assemblies, and instance-export provider before composition.
			//This is the place to add your application's custom conventions, as well as your application's assembly/assemblies.
			//Replace this entirely if you want deeper control over creating the final application container.
            
			//For quick reference, the base first creates a ContainerConfiguration() object.
			//Then, it executes all the configuration methods (as listed in the previous overrides group)
			//Finally, it plugs in the configured ConventionBuilder using the .WithDefaultConventions(), adds the configured assemblies using .WithAssemblies(), adds each provider (configured) using .WithProvider(), then returns the CompositionHost using .CreateContainer().
            
            return base.CreateApplicationContainer(internalConventions, internalAssemblies, instanceProvider);
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
