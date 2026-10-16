using Dsw2026Tpi.Application.Dtos;
using Dsw2026Tpi.Application.Interfaces;
using Dsw2026Tpi.CrossCutting.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dsw2026Tpi.Api.Controllers;

[Route("api/specialties")]
[Authorize]
public class SpecialityController : AppController
{
    private readonly ISpecialityService _service;

    public SpecialityController(ISpecialityService service)
    {
        _service = service;
    }

    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetAll(
        [FromQuery] int pageSize = 10,
        [FromQuery] int pageIndex = 0,
        [FromQuery] string? name = null)
    {
        var specialities = await _service.GetAll(
            pageSize,
            pageIndex,
            name);

        return Ok(specialities);
    }

    [HttpPost]
    [Authorize(Policy = Policies.AdminPolicy)]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(
        [FromBody] SpecialityModel.Request request)
    {
        var speciality = await _service.Create(request);

        return StatusCode(
            StatusCodes.Status201Created,
            speciality);
    }

    [HttpPut("{id}")]
    [Authorize(Policy = Policies.AdminPolicy)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(
        [FromRoute] Guid id,
        [FromBody] SpecialityModel.Request request)
    {
        var speciality = await _service.Update(id, request);

        return Ok(speciality);
    }

    [HttpDelete("{id}")]
    [Authorize(Policy = Policies.AdminPolicy)]
    [ProducesResponseType(
    typeof(string),
    StatusCodes.Status200OK,
    "text/plain")]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]

    public async Task<IActionResult> Delete([FromRoute] Guid id)
    {
        await _service.Delete(id);

        return Content("ok", "text/plain");
    }
}