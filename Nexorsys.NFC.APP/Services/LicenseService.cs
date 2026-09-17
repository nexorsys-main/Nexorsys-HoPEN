using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Pinede.NFC.APP.Modeles;
using Microsoft.Extensions.Options;

namespace Pinede.NFC.APP.Services
{
    public interface ILicenseService
    {
        Task<bool> ValidateLicenseAsync();
    }

    public class LicenseService : ILicenseService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<LicenseService> _logger;
        // In WPF we can do caching manually since we don't have IMemoryCache by default unless we add the package
        private static bool? _cachedResult = null;
        private static DateTime _cacheDate = DateTime.MinValue;

        public LicenseService(HttpClient httpClient, IConfiguration configuration, ILogger<LicenseService> logger)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<bool> ValidateLicenseAsync()
        {
            // BYPASS LICENSE CHECK FOR NOW
            return true;
        }

        private class LicenseValidationResponse
        {
            public bool Valid { get; set; }
            public string ExpiresAt { get; set; }
        }
    }
}
