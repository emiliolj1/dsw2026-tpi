using Dsw2026Tpi.Application.Dtos;
using Dsw2026Tpi.Application.Interfaces;
using Dsw2026Tpi.CrossCutting.Exceptions;
using Dsw2026Tpi.CrossCutting.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.IdentityModel.Tokens.Jwt;

namespace Dsw2026Tpi.Api.Controllers;

[Route("api/auth")]
public class AuthenticationController : AppController
{
    private readonly IAuthenticationService _authenticationService;
    private readonly ITokenRevocationService _tokenRevocationService;

    public AuthenticationController(
        IAuthenticationService authenticationService,
        ITokenRevocationService tokenRevocationService)
    {
        _authenticationService = authenticationService;
        _tokenRevocationService = tokenRevocationService;
    }

    [AllowAnonymous]
    [HttpPost("admin/login")]
    [EnableRateLimiting(RateLimitPolicies.AdminLogin)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login(
        [FromBody] LoginAdminModel.Request request)
    {
        var result = await _authenticationService.LoginAdmin(request);
        return Ok(result);
    }

    [AllowAnonymous]
    [HttpPost("patient/login")]
    [EnableRateLimiting(RateLimitPolicies.PatientLogin)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> LoginPatient(
        [FromBody] LoginPatientModel.Request request)
    {
        var result = await _authenticationService.LoginPatient(request);
        return Ok(result);
    }

    [HttpPost("logout")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public IActionResult Logout()
    {
        var tokenId = User
            .FindFirst(JwtRegisteredClaimNames.Jti)?
            .Value;

        var expirationValue = User
            .FindFirst(JwtRegisteredClaimNames.Exp)?
            .Value;

        if (string.IsNullOrWhiteSpace(tokenId) ||
            !long.TryParse(
                expirationValue,
                out var expirationUnixSeconds))
        {
            throw new AuthenticationException();
        }

        _tokenRevocationService.Revoke(
            tokenId,
            DateTimeOffset.FromUnixTimeSeconds(
                expirationUnixSeconds));

        return Ok("ok");
    }
}
