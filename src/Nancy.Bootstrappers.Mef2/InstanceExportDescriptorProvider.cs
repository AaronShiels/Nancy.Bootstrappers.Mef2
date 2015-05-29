using Nancy.Bootstrapper;
using System.Collections.Generic;
using System.Composition.Hosting.Core;
using System.Linq;

namespace Nancy.Bootstrappers.Mef2
{
    public class InstanceExportDescriptorProvider : ExportDescriptorProvider
    {
        private readonly IEnumerable<InstanceRegistration> _instanceRegistrations;

        public InstanceExportDescriptorProvider(IEnumerable<InstanceRegistration> instanceRegistrations)
        {
            _instanceRegistrations = instanceRegistrations;
        }

        public override IEnumerable<ExportDescriptorPromise> GetExportDescriptors(CompositionContract contract, DependencyAccessor descriptorAccessor)
        {
            var type = contract.ContractType;

            var instanceRegistration = _instanceRegistrations.SingleOrDefault(ir => ir.RegistrationType == type);

            if (instanceRegistration == null)
                return NoExportDescriptors;

            return new[]
            {
                new ExportDescriptorPromise(contract,
                "Registered Instances",
                false,
                NoDependencies,
                _ => ExportDescriptor.Create((c, o) => instanceRegistration.Implementation, NoMetadata))
            };
        }
    }
}
