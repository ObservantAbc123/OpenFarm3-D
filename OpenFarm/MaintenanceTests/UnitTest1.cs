using Xunit;
using Moq;
using System.Text;
using System.Reflection;
using System.Net;
using Microsoft.Extensions.Logging;
using PrintManagement;
using OctoprintHelper;

public class MaintenanceHelpersTests
{
    #region Local Helpers
    // helper to invoke private static methods via Reflection
    private static async Task<T> InvokePrivateMethodAsync<T>(string methodName, object[] parameters)
    {
        var method = typeof(MaintenanceHelpers).GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Static);
        if (method == null) throw new InvalidOperationException($"Method {methodName} not found.");

        var task = (Task<T>)method.Invoke(null, parameters);
        return await task;
    }

    // helper to create a stream from a string
    private static MemoryStream CreateGcodeStream(string gcode)
    {
        return new MemoryStream(Encoding.UTF8.GetBytes(gcode));
    }

    // helper to set up the mock HTTP response
    private void SetupMockResponse(Mock<IOctoprintHelper> mock, HttpResponseMessage response)
    {
        try {
            mock.Setup(x => x.GetExtruderTemperature(It.IsAny<HttpClient>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(response);
        } catch (ArgumentException) { }
    }
    #endregion

    #region Volume Tests

    [Fact]
    public async Task CalculateVolume_AbsoluteMode_CalculatesCorrectly()
    {
        // M82 = Absolute
        // G92 E0 = Reset
        // G1 E10 = Extrude 10
        // G1 E25 = Extrude 15 more (total 25)
        // G1 E20 = Retract 5 (ignored by logic delta > 0 check)
        string gcode = "M82\nG92 E0\nG1 E10\nG1 E25\nG1 E20";
        using var stream = CreateGcodeStream(gcode);
        double diameter = 1.75;

        // Expected: 25mm total extrusion
        // Formula: Length * (PI * (d/2)^2) * 1e-9
        double result = await InvokePrivateMethodAsync<double>("CalculateVolume", new object[] { stream, diameter });

        double radius = diameter / 2.0;
        double area = Math.PI * Math.Pow(radius, 2);
        double expectedVolumeM3 = (25 * area) * 1e-9;

        Assert.Equal(expectedVolumeM3, result, precision: 12);
    }

    [Fact]
    public async Task CalculateVolume_RelativeMode_CalculatesCorrectly()
    {
        // M83 = Relative
        // G1 E5 = Extrude 5
        // G1 E10 = Extrude 10
        string gcode = "M83\nG1 E5\nG1 E10";
        using var stream = CreateGcodeStream(gcode);
        double diameter = 1.75;

        double result = await InvokePrivateMethodAsync<double>("CalculateVolume", new object[] { stream, diameter });

        // Total extrusion: 15mm
        double radius = diameter / 2.0;
        double area = Math.PI * Math.Pow(radius, 2);
        double expectedVolumeM3 = (15 * area) * 1e-9;

        Assert.Equal(expectedVolumeM3, result, precision: 12);
    }

    #endregion

    #region Linear Travel Tests

    [Fact]
    public async Task CalculateLinearTravel_AbsoluteMode_PythagoreanDistance()
    {
        // Move 1: 0,0,0 -> 3,4,0 (Distance 5 - 3-4-5 triangle)
        // Move 2: 3,4,0 -> 3,4,10 (Distance 10)
        string gcode = "G90\nG1 X3 Y4 Z0\nG1 Z10";
        using var stream = CreateGcodeStream(gcode);

        double resultMeters = await InvokePrivateMethodAsync<double>("CalculateLinearTravel", new object[] { stream });

        // Total mm = 15. Total meters = 0.015
        Assert.Equal(0.015, resultMeters);
    }

    [Fact]
    public async Task CalculateLinearTravel_RelativeMode_SumsDeltas()
    {
        string gcode = "G91\nG1 X10\nG1 Y10";
        using var stream = CreateGcodeStream(gcode);

        double resultMeters = await InvokePrivateMethodAsync<double>("CalculateLinearTravel", new object[] { stream });

        // 10mm + 10mm = 20mm = 0.02m
        Assert.Equal(0.02, resultMeters);
    }

    #endregion

#region Parsing & HTTP Tests

    [Fact]
    public async Task GetCurrentPrinterToolTemp_ParsesValidJson()
    {
        var mockOcto = new Mock<IOctoprintHelper>();
        var mockLogger = new Mock<ILogger<PMSWorker>>();

        var clientInstance = new RegisteredInstance(0, "test", new HttpClient(), true);

        // safety check to ensure the client connection inside the instance is not null
        Assert.NotNull(clientInstance._clientConnection);

        var jsonResponse = "{\"tool0\": {\"actual\": 215.5, \"target\": 220.0}}";
        var httpResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(jsonResponse)
        };
        SetupMockResponse(mockOcto, httpResponse);

        double result = await MaintenanceHelpers.GetCurrentPrinterToolTemp(mockOcto.Object, clientInstance, mockLogger.Object);

        Assert.Equal(215.5, result);
    }

    [Fact]
    public async Task GetCurrentPrinterToolTemp_ReturnsZero_OnJsonNull()
    {
        var mockOcto = new Mock<IOctoprintHelper>();
        var mockLogger = new Mock<ILogger<PMSWorker>>();
        var clientInstance = new RegisteredInstance(0, "test", new HttpClient(), true);

        var jsonResponse = "{\"tool0\": {\"actual\": null}}";
        var httpResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(jsonResponse)
        };

        SetupMockResponse(mockOcto, httpResponse);

        double result = await MaintenanceHelpers.GetCurrentPrinterToolTemp(mockOcto.Object, clientInstance, mockLogger.Object);

        Assert.Equal(0.0, result);
    }

    [Fact]
    public async Task GetCurrentPrinterToolTemp_HandlesHttpException()
    {
        var mockOcto = new Mock<IOctoprintHelper>();
        var mockLogger = new Mock<ILogger<PMSWorker>>();
        var clientInstance = new RegisteredInstance(0, "test", new HttpClient(), true);

        var exception = new HttpRequestException("Connection failed");

        try {
            mockOcto.Setup(x => x.GetExtruderTemperature(It.IsAny<HttpClient>(), It.IsAny<CancellationToken>()))
                    .ThrowsAsync(exception);
        } catch { }

        double result = await MaintenanceHelpers.GetCurrentPrinterToolTemp(mockOcto.Object, clientInstance, mockLogger.Object);

        Assert.Equal(0.0, result);

        mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Could not retrieve")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()),
            Times.Once);
    }

    #endregion
}