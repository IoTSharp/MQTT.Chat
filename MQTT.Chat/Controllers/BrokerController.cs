using IoTSharp.X509Extensions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MQTT.Chat.Data;
using MQTT.Chat.Properties;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace MQTT.Chat.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class BrokerController : ControllerBase
    {
        private MQTTBrokerOption _options;
        private ApplicationDbContext _context;
        private ILogger _logger;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly IConfiguration _configuration;
        private readonly SignInManager<IdentityUser> _signInManager;
        public BrokerController(
            UserManager<IdentityUser> userManager,
            SignInManager<IdentityUser> signInManager,
            IConfiguration configuration, ILogger<BrokerController> logger, IOptions<MQTTBrokerOption> options, ApplicationDbContext context
            )
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _configuration = configuration;
            _logger = logger;
            _context = context;
            _options = options.Value;
        }
        [HttpGet()]
        public IActionResult Get()
        {
            return Ok(new { code=0,msg="OK", data = new { CAName = _options?.CACertificate?.GetNameInfo(X509NameType.SimpleName, true), BrokerName = _options?.BrokerCertificate?.GetNameInfo(X509NameType.SimpleName, false) } });
        }

        [HttpPost("InstallCertificate")]
        public IActionResult InstallCertificate([FromBody] CertificateDot dot)
        {
            IActionResult actionResult = NoContent();
            try
            {
                if (_options.CACertificate == null || _options.BrokerCertificate == null)
                {
                    StringBuilder builder = new StringBuilder();
                    if (!string.IsNullOrEmpty(dot.C)) builder.Append($"C={dot.C},");
                    builder.Append($"CN={dot.CN},");
                    if (!string.IsNullOrEmpty(dot.ST)) builder.Append($"ST={dot.ST},");
                    if (!string.IsNullOrEmpty(dot.O)) builder.Append($"O={dot.O},");
                    if (!string.IsNullOrEmpty(dot.OU)) builder.Append($"OU={dot.OU},");
                    var ca = new X509Certificate2().CreateCA(builder.ToString().TrimEnd(','));
                    ca.SavePem(_options.CACertificateFile, _options.CAPrivateKeyFile);
                    var build = new SubjectAlternativeNameBuilder();
                    build.AddDnsName(dot.ServerName);
                    if (!string.IsNullOrEmpty(dot.IPAddress)) build.AddIpAddress(IPAddress.Parse(dot.IPAddress));
                    if (!string.IsNullOrEmpty(dot.Email)) build.AddEmailAddress(dot.Email);
                    StringBuilder builderserver = new StringBuilder();
                    if (!string.IsNullOrEmpty(dot.C)) builderserver.Append($"C={dot.C},");
                    builderserver.Append($"CN={dot.ServerName},");
                    if (!string.IsNullOrEmpty(dot.ST)) builderserver.Append($"ST={dot.ST},");
                    if (!string.IsNullOrEmpty(dot.O)) builderserver.Append($"O={dot.O},");
                    if (!string.IsNullOrEmpty(dot.OU)) builderserver.Append($"OU={dot.OU},");
                    var broker = ca.CreateTlsClientRSA(builderserver.ToString().TrimEnd(','), build);
                    broker.SavePem(_options.CertificateFile, _options.PrivateKeyFile);
                    actionResult = Ok(new { code = 0, msg = "OK", data = new { CAName = ca.GetNameInfo(X509NameType.SimpleName, true), BrokerName = broker.GetNameInfo(X509NameType.SimpleName, false) } });
                }
                else
                {
                    actionResult = Ok(new { code = -1, msg = Resources.TheCertificateIsInstalled, data = new { CAName = _options.CACertificate.GetNameInfo(X509NameType.SimpleName, true), BrokerName = _options.BrokerCertificate.GetNameInfo(X509NameType.SimpleName, false) } });
                }
            }
            catch (System.Exception ex)
            {
                actionResult = BadRequest(new { code = -2, msg = ex.Message });
                _logger.LogError(ex, "InstallCertificate:" + ex.Message);
            }
            return actionResult;
        }
    }


    public class CertificateDot
    {

        public string C { get; set; } = "CN";
        [Required]
        public string CN { get; set; }
        public string ST { get; set; }
        public string O { get; set; }
        public string OU { get; set; }
        [Required]
        public string ServerName { get;  set; }
        [EmailAddress()]
        public string Email { get;  set; }
        
        public string IPAddress { get;  set; }
    }
}