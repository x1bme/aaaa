[HttpGet("tests")]
public async Task<IActionResult> GetTestsByValve([FromQuery] int valveId)
{
    try
    {
        _logger.LogInformation("Retrieving tests for valve {valveId}", valveId);
        
        var client = _httpClientFactory.CreateClient();
        var response = await client.GetAsync($"{_archiverServiceUrl}/api/tests?valveId={valveId}");
        
        if (response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync();
            return Content(content, "application/json");
        }
        
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return Ok(new List<object>()); // Return empty array if no tests found
        }
        
        return StatusCode((int)response.StatusCode, "Failed to retrieve tests");
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error retrieving tests for valve {valveId}", valveId);
        return StatusCode(500, new { error = "Failed to retrieve tests", details = ex.Message });
    }
}
