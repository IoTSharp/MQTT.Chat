using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.Extensions.Options;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using MQTT.Chat.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography.X509Certificates;
using IoTSharp.X509Extensions;
using System.IO;
using System.IO.Compression;

namespace MQTT.Chat.Controllers
{

    [ApiController]
    [Route("api/[controller]/[action]")]
    public class AccountController : ControllerBase
    {
        private MQTTBrokerOption _options;
        private ApplicationDbContext _context;
        private ILogger _logger;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly IConfiguration _configuration;
        private readonly SignInManager<IdentityUser> _signInManager;
        public AccountController(
            UserManager<IdentityUser> userManager,
            SignInManager<IdentityUser> signInManager,
            IConfiguration configuration, ILogger<AccountController> logger, IOptions<MQTTBrokerOption> options, ApplicationDbContext context
            )
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _configuration = configuration;
            _logger = logger;
            _context = context;
            _options = options.Value;
        
        }

        [AllowAnonymous]
        [HttpPost]
        public async Task<IActionResult> Login([FromBody] LoginDto model)
        {
            IActionResult actionResult = NoContent();
            try
            {
                var result = await _signInManager.PasswordSignInAsync(model.UserName, model.Password, false, false);

                if (result.Succeeded)
                {
                    var appUser = _userManager.Users.SingleOrDefault(r => r.UserName == model.UserName);
                     actionResult=Ok(new { code = 0, msg = "OK", data = GenerateJwtToken(model.UserName, appUser) });
                }
                else
                {
                    actionResult = BadRequest(new { code = -3, msg = "Login Error",data= result });
                }
            }
            catch (Exception ex)
            {

                actionResult = BadRequest(new { code = -1, msg = ex.Message, data = ex });
            }
            return actionResult;
        }
        /// <summary>
        /// Register a user
        /// </summary>
        /// <param name="model"></param>
        /// <returns ></returns>
        /// <seealso cref="BrokerController.InstallCertificate(CertificateDot)"/> 
        [AllowAnonymous]
        [HttpPost]
        public async Task<IActionResult> Register([FromBody] RegisterDto model)
        {
            IActionResult actionResult = NoContent();
            try
            {
                if (_options.CACertificate == null && model.TLS)
                {
                    actionResult = BadRequest(new { code = -1, msg = "There is no root certificate, please initialize the server first" });
                }
                else
                {
                    var user = new IdentityUser
                    {
                        UserName = model.UserName,
                        Email = model.Email
                    };
                    var result = await _userManager.CreateAsync(user, model.Password);

                    if (result.Succeeded)
                    {
                        await _signInManager.SignInAsync(user, false);
                        if (model.TLS)
                        {
                            SubjectAlternativeNameBuilder altNames = new SubjectAlternativeNameBuilder();
                            altNames.AddDnsName(model.ClientId);
                            altNames.AddEmailAddress(model.Email);
                            altNames.AddUserPrincipalName(model.UserName);
                            altNames.AddUri(new Uri($"mqtt://{_options.BrokerCertificate.GetNameInfo( X509NameType.DnsName,false)}:{_options.SSLPort}"));
                            string name = $"CN={model.ClientId},C=CN, O={_options.BrokerCertificate.GetNameInfo(X509NameType.SimpleName, false)},OU={model.ClientId}";
                            var tlsclient = _options.CACertificate.CreateTlsClientRSA(name, altNames);
                            string x509CRT, x509Key;
                            tlsclient.SavePem(out x509CRT, out x509Key);
                            StoreCertPem storeCertPem = new StoreCertPem()
                            {
                                Id = user.Id,
                                ClientCert = x509CRT,
                                 ClientKey  = x509Key
                            };
                        }
                        actionResult = Ok(new { code = 0, msg = "OK", data = GenerateJwtToken(model.UserName, user) });
                    }
                    else
                    {
                        var msg = from e in result.Errors select $"{e.Code}:{e.Description}\r\n";
                        actionResult = BadRequest(new { code = -3, msg = string.Join(';', msg.ToArray()) });
                    }
                }
            }
            catch (Exception ex)
            {
                actionResult = BadRequest(new { code = -2, msg = ex.Message, data = ex });
            }

            return actionResult;
        }
        [Authorize]
        [HttpGet]
        public IActionResult DownloadCertificates( )
        {
            IActionResult actionResult = NoContent();
            try
            {
                var useid = _userManager.GetUserId(this.User);
                var username = _userManager.GetUserName(this.User);
                if (!string.IsNullOrEmpty(useid))
                {
                    var tsl = _context.StoreCertPem.FirstOrDefault(t => t.Id == useid);


                    if (tsl == null || string.IsNullOrEmpty(tsl.ClientKey) || string.IsNullOrEmpty(tsl.ClientCert))
                    {
                        actionResult= NotFound(new { code = -1, msg = $"The certificate for {username} was not found!" });
                    }
                    else
                    {
                        string fileNameZip = $"client_{username}.zip";
                        byte[] fileBytes = System.Text.Encoding.UTF8.GetBytes(tsl.ClientCert);
                        byte[] fileBytes1 = System.Text.Encoding.UTF8.GetBytes(tsl.ClientKey);
                        byte[] compressedBytes;
                        using (var outStream = new MemoryStream())
                        {
                            using (var archive = new ZipArchive(outStream, ZipArchiveMode.Create, true))
                            {
                                var fileInArchive = archive.CreateEntry("client.crt", CompressionLevel.Optimal);
                                using (var entryStream = fileInArchive.Open())
                                using (var fileToCompressStream = new MemoryStream(fileBytes))
                                {
                                    fileToCompressStream.CopyTo(entryStream);
                                }

                                var fileInArchive1 = archive.CreateEntry("client.key", CompressionLevel.Optimal);
                                using (var entryStream = fileInArchive1.Open())
                                using (var fileToCompressStream = new MemoryStream(fileBytes1))
                                {
                                    fileToCompressStream.CopyTo(entryStream);
                                }
                                var fileInArchive2 = archive.CreateEntry("ca.crt", CompressionLevel.Optimal);
                                using (var entryStream = fileInArchive2.Open())
                                using (var fileToCompressStream = new FileStream(_options.CACertificateFile, FileMode.Open))
                                {
                                    fileToCompressStream.CopyTo(entryStream);
                                }
                            }
                            compressedBytes = outStream.ToArray();
                        }
                        actionResult= File(compressedBytes, "application/octet-stream", fileNameZip);
                    }
                }
            }
            catch (Exception ex)
            {
                actionResult= BadRequest(new { code = -2, msg = ex.Message });
            }
            return actionResult;
        }

        private object GenerateJwtToken(string email, IdentityUser user)
        {
            var claims = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.Sub, email),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim(ClaimTypes.NameIdentifier, user.Id)
            };

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["JwtKey"]));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
            var expires = DateTime.Now.AddDays(Convert.ToDouble(_configuration["JwtExpireDays"]));

            var token = new JwtSecurityToken(
                _configuration["JwtIssuer"],
                _configuration["JwtIssuer"],
                claims,
                expires: expires,
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        public class LoginDto
        {
            [Required]
            public string Password { get; set; }
            [Required]
            public string UserName { get; internal set; }
        }

        public class RegisterDto
        {
            [Required]
            public string UserName { get; set; }

            [Required]
            public string Email { get; set; }
     
            [Required]
            public bool TLS { get; set; } = true;

            [Required]
            [StringLength(100, ErrorMessage = "PASSWORD_MIN_LENGTH", MinimumLength = 6)]
            public string Password { get; set; }

            [Required]
            public string ClientId { get;  set; }
        }
    }
}

