using Microsoft.AspNetCore.Mvc;
using Cw_7_s30976.DTOs;
using Cw_7_s30976.Exceptions;
using Cw_7_s30976.Services;

namespace Cw_7_s30976.Controllers;

[ApiController]
[Route("api/trips")]
public class TripsController : ControllerBase {
    private readonly IDbService _dbService;
    public TripsController(IDbService dbService) {
        _dbService = dbService;
    }

    [HttpGet]
    public async Task<IActionResult> GetTrips() {
        var trips = await _dbService.GetTripsAsync();
        return Ok(trips);
    }
}
