[HttpPut]
public async Task<IActionResult> UpdateDau(int dauId, Dau dau)
{
    try
    {
        if (dau == null)
        {
            return BadRequest("DAU data is null");
        }
        
        // IMPORTANT: Set the ID from the query parameter
        dau.Id = dauId;
        
        if (dau.Id != dauId)
        {
            return BadRequest("DAU ID mismatch between URL and body");
        }
        
        _logger.LogInformation($"Updating DAU with id: {dauId}");
        
        // Verify DAU still exists on server
        var serverDaus = await _grpcClient.GetAllDausAsync(new string[] { dau.DauId });
        var serverDau = serverDaus.Dau.FirstOrDefault(d => d.DeviceId == dau.DauId);
        
        if (serverDau == null)
        {
            return BadRequest($"DAU with ID {dau.DauId} not found on DAU Server");
        }

        // Only update fields stored in database
        var updatedDau = new Dau
        {
            Id = dauId,
            ValveId = dau.ValveId,
            DauId = dau.DauId,
            DauTag = dau.DauTag,
            Location = dau.Location,
            DauIPAddress = dau.DauIPAddress,
            Registered = dau.Registered
        }; 
        
        await _dauRepository.UpdateDau(updatedDau);
        return NoContent();
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error updating DAU with id: {id}", dauId);
        return StatusCode(500, "Internal server error");
    }
}
