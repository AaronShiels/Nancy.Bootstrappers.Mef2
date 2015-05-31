using Nancy.Bootstrapper;
using System;
using System.Collections.Generic;
using System.Composition.Hosting.Core;
using System.Linq;

namespace Nancy.Bootstrappers.Mef2
{
    public class InstanceExportDescriptorProvider : ExportDescriptorProvider
    {
        private readonly IDictionary<Type, object> _instanceRegistrations;

        public InstanceExportDescriptorProvider()
        {
            _instanceRegistrations = new Dictionary<Type, object>();
        }

        public void RegisterExport(Type type, object instance)
        {
            _instanceRegistrations.Add(type, instance);
        }

        public override IEnumerable<ExportDescriptorPromise> GetExportDescriptors(CompositionContract contract, DependencyAccessor descriptorAccessor)
        {
            var type = contract.ContractType;

            if (!_instanceRegistrations.ContainsKey(type))
                return NoExportDescriptors;

            return new[]
            {
                new ExportDescriptorPromise(contract,
                "Registered Instances",
                false,
                NoDependencies,
                _ => ExportDescriptor.Create((c, o) => _instanceRegistrations[type], NoMetadata))
            };
        }
    }
}
