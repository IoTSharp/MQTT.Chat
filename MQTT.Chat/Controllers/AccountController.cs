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

namespace MQTT.Chat.Controllers
{

    [ApiController]
    [Route("[controller]/[action]")]
    public class AccountController : Controller
    {
        private MQTTBrokerOption _options;
        private ApplicationDbContext _context;
        private ILogger<MqttEventsHandler> _logger;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly IConfiguration _configuration;
        private readonly SignInManager<IdentityUser> _signInManager;
        public AccountController(
            UserManager<IdentityUser> userManager,
            SignInManager<IdentityUser> signInManager,
            IConfiguration configuration, ILogger<MqttEventsHandler> logger, IOptions<MQTTBrokerOption> options, ApplicationDbContext context
            )
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _configuration = configuration;
            _logger = logger;
            _context = context;
            _options = options.Value;
        }


        [HttpPost]
        public async Task<IActionResult> Login([FromBody] LoginDto model)
        {
            IActionResult actionResult = NoContent();
            try
            {
                var result = await _signInManager.PasswordSignInAsync(model.Email, model.Password, false, false);

                if (result.Succeeded)
                {
                    var appUser = _userManager.Users.SingleOrDefault(r => r.Email == model.Email);
                     actionResult=Ok(new { code = 0, msg = "OK", data = GenerateJwtToken(model.Email, appUser) });
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

        [HttpPost]
        public async Task<object> Register([FromBody] RegisterDto model)
        {
            IActionResult actionResult = NoContent();
            try
            {
                var user = new IdentityUser
                {
                    UserName = model.Email,
                    Email = model.Email
                };
                var result = await _userManager.CreateAsync(user, model.Password);

                if (result.Succeeded)
                {
                    await _signInManager.SignInAsync(user, false);
                    actionResult = Ok(new { code = 0, msg = "OK", data = GenerateJwtToken(model.Email, user) });
                }
                else
                {
                    var msg = from e in result.Errors select $"{e.Code}:{e.Description}\r\n";
                    actionResult = BadRequest(new { code = -3, msg = string.Join(';', msg.ToArray()) });
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
        public object Protected()
        {
            return "Protected area";
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
            public string Email { get; set; }

            [Required]
            public string Password { get; set; }

        }

        public class RegisterDto
        {
            [Required]
            public string Email { get; set; }

            [Required]
            [StringLength(100, ErrorMessage = "PASSWORD_MIN_LENGTH", MinimumLength = 6)]
            public string Password { get; set; }
        }
    }
}

