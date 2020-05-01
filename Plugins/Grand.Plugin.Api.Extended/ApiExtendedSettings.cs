using Grand.Core.Configuration;

namespace Grand.Plugin.Api.Extended
{
    public class ApiExtendedSettings : ISettings
    {
        /// <summary>
        /// Path to firebase app credential configuration file
        /// </summary>
        public string FirebaseAppCredentialConfigurationFile { get; set; }
    }
}
