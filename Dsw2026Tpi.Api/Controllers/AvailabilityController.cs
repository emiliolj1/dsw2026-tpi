using Dsw2026Tpi.Application.Dtos;
using Dsw2026Tpi.Application.Interfaces;
using Dsw2026Tpi.CrossCutting.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dsw2026Tpi.Api.Controllers;

[Route("api/availabilities")]
public class AvailabilityController : AppController
{
    private readonly IAvailabilityService _service;

    public AvailabilityController(IAvailabilityService service) => _service = service;

    [HttpPost]
    [Authorize(Policy = Policies.AdminPolicy)]
    [ProducesResponseType(typeof(AvailabilityModel.PlanningResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create([FromBody] AvailabilityModel.Request request)
    {
        var result = await _service.Create(request);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    [HttpPut]
    [Authorize(Policy = Policies.AdminPolicy)]
    [ProducesResponseType(typeof(AvailabilityModel.PlanningResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update([FromBody] AvailabilityModel.Request request)
    {
        var result = await _service.Update(request);
        return Ok(result);
    }

    [HttpGet]
    [Authorize(Policy = Policies.PatientPolicy)]
    [ProducesResponseType(typeof(IEnumerable<AvailabilityModel.AvailableSlotResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAvailableSlots([FromQuery] Guid doctorId)
    {
        var slots = await _service.GetAvailableSlots(doctorId);
        return Ok(slots);
    }
}
