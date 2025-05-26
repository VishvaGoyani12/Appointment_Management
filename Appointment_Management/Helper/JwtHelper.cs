using System.IdentityModel.Tokens.Jwt;

namespace Appointment_Management.Helper
{
    public class JwtUser
    {
        public bool IsAuthenticated { get; set; }
        public string Email { get; set; }
        public string Role { get; set; }
        public string UserId { get; set; } 
    }


    public static class JwtHelper
    {
        public static JwtUser GetJwtUser(HttpContext context)
        {
            var jwt = context.Request.Cookies["jwt_token"];
            if (string.IsNullOrEmpty(jwt))
                return new JwtUser { IsAuthenticated = false };

            var handler = new JwtSecurityTokenHandler();
            var token = handler.ReadJwtToken(jwt);

            var email = token.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Sub)?.Value;
            var role = token.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.Role)?.Value;
            var userId = token.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

            return new JwtUser
            {
                IsAuthenticated = true,
                Email = email,
                Role = role,
                UserId = userId
            };
        }

    }
}
