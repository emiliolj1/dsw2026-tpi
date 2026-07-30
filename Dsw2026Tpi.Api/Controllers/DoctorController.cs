using Dsw2026Tpi.Application.Dtos;
using Dsw2026Tpi.Application.Interfaces;
using Dsw2026Tpi.CrossCutting.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dsw2026Tpi.Api.Controllers;

[Route("doctors")]
[Authorize(Policy = Policies.AdminPolicy)]
public class DoctorController : AppController
{
    private readonly IDoctorService _service;
    private readonly IAvailabilityService _availabilityService;

    public DoctorController(
        IDoctorService service,
        IAvailabilityService availabilityService)
    {
        _service = service;
        _availabilityService = availabilityService;
    }

    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetAll(
        [FromQuery] int pageSize,
        [FromQuery] int pageIndex,
        [FromQuery] string? name = null)
    {
        var doctors = await _service.GetAll(
            pageSize,
            pageIndex,
            name);

        return Ok(doctors);
    }

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Create(
        [FromBody] DoctorModel.Request request)
    {
        var doctor = await _service.Create(request);

        return StatusCode(
            StatusCodes.Status201Created,
            doctor);
    }

    [HttpPut("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        [FromRoute] Guid id,
        [FromBody] DoctorModel.Request request)
    {
        var doctor = await _service.Update(id, request);

        return Ok(doctor);
    }

    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete([FromRoute] Guid id)
    {
        await _service.Delete(id);

        return NoContent();
    }

    [HttpGet("{id}/availabilities")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMonthlyPlanning(
        [FromRoute] Guid id)
    {
        var planning =
            await _availabilityService.GetMonthlyPlanning(id);

        return Ok(planning);
    }
}