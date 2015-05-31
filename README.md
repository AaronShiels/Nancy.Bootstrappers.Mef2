# Nancy.Bootstrappers.Mef2
A [bootstrapper](https://github.com/NancyFx/Nancy/wiki/Bootstrapper) for the [Nancy](http://nancyfx.org) web framework, utilizing a [Managed Extensibility Framework (MEF2)](https://mef.codeplex.com/) based container.

##Usage
Rather than inheriting from the 'DefaultNancyBootstrapper', you will instead want to inherit from the 'CompositionContextNancyBootstrapper' to utilize the MEF-based 'CompositionHost' container. Get the latest version of the 'CompositionContextNancyBootstrapper' by installing the 'Nancy.Bootstrappers.Mef2' nuget.
