namespace Nancy.Bootstrappers.Mef2.Tests.Fakes.Dependencies
{
    public class InstanceDependency : IInstanceDependency
    {
        public InstanceDependency(string secretPreConfiguredMessage)
        {
            SecretPreConfiguredMessage = secretPreConfiguredMessage;
        }

        public string SecretPreConfiguredMessage { get; set; }
    }
}
