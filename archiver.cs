app.MapGet("/api/tests", async (ITestService testService, int valveId) =>
{
    return await testService.GetTestsByValveAsync(valveId);
})
.WithName("GetTestsByValve")
.WithDescription("Get all tests for a specific valve")
.WithOpenApi();
