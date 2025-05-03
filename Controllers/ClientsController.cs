using Cw_7_s30976.DTOs;
using Cw_7_s30976.Exceptions;
using Cw_7_s30976.Services;
using Microsoft.AspNetCore.Mvc;


namespace Cw_7_s30976.Controllers;

[ApiController]
[Route("api/clients")]
public class ClientsController : ControllerBase {
    private readonly IDbService _dbService;
    public ClientsController(IDbService dbService) {
        _dbService = dbService;
    }

    [HttpGet("{id}/trips")]
    public async Task<IActionResult> GetClientTrips(int id) {
        var result = await _dbService.GetTripsForClientAsync(id);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> AddClient(ClientDto client) {
        var newId = await _dbService.AddClientAsync(client);
        return Created($"api/clients/{newId}", newId);
    }

    [HttpPut("{id}/trips/{tripId}")]
    public async Task<IActionResult> RegisterClientToTrip(int id, int tripId) {
        var result = await _dbService.RegisterClientToTripAsync(id, tripId);
        return result ? Ok() : BadRequest("Unable to register client.");
    }

    [HttpDelete("{id}/trips/{tripId}")]
    public async Task<IActionResult> UnregisterClientFromTrip(int id, int tripId) {
        var result = await _dbService.RemoveClientFromTripAsync(id, tripId);
        return result ? NoContent() : NotFound();
    }
}
