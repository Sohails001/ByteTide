using ByteTide.Core;
using Xunit;

namespace ByteTide.Tests;

public class LegacyImporterTests
{
    // Real excerpt from an actual RAS Graph & Stats archive file (single adapter, three days).
    private const string SingleAdapterSample =
        "[39630]\r\n" +
        "Broadcom 440x 10/100 Integrated Controller - Packet Scheduler Miniport @ 00-11-43-68-28-0A=86124\r\n" +
        "Hours=86124\r\n" +
        "Recv=1805957090\r\n" +
        "Broadcom 440x 10/100 Integrated Controller - Packet Scheduler Miniport @ 00-11-43-68-28-0A-Recv=1805957090\r\n" +
        "Xmit=88092758\r\n" +
        "Broadcom 440x 10/100 Integrated Controller - Packet Scheduler Miniport @ 00-11-43-68-28-0A-Xmit=88092758\r\n" +
        "\r\n" +
        "[39631]\r\n" +
        "Broadcom 440x 10/100 Integrated Controller - Packet Scheduler Miniport @ 00-11-43-68-28-0A=86331\r\n" +
        "Hours=86331\r\n" +
        "Recv=1441964543\r\n" +
        "Broadcom 440x 10/100 Integrated Controller - Packet Scheduler Miniport @ 00-11-43-68-28-0A-Recv=1441964543\r\n" +
        "Xmit=46125748\r\n" +
        "Broadcom 440x 10/100 Integrated Controller - Packet Scheduler Miniport @ 00-11-43-68-28-0A-Xmit=46125748\r\n" +
        "\r\n";

    // Synthetic case: two different adapters reporting on the same day, to prove
    // the parser doesn't cross-contaminate values via the generic Hours=/Recv=/Xmit= keys.
    private const string TwoAdaptersSameDaySample =
        "[39692]\r\n" +
        "Ethernet Adapter One @ AA-BB-CC-DD-EE-01=1000\r\n" +
        "Hours=1000\r\n" +
        "Recv=500\r\n" +
        "Ethernet Adapter One @ AA-BB-CC-DD-EE-01-Recv=500\r\n" +
        "Xmit=200\r\n" +
        "Ethernet Adapter One @ AA-BB-CC-DD-EE-01-Xmit=200\r\n" +
        "Wireless Adapter Two @ AA-BB-CC-DD-EE-02=2000\r\n" +
        "Hours=2000\r\n" +
        "Recv=900\r\n" +
        "Wireless Adapter Two @ AA-BB-CC-DD-EE-02-Recv=900\r\n" +
        "Xmit=300\r\n" +
        "Wireless Adapter Two @ AA-BB-CC-DD-EE-02-Xmit=300\r\n" +
        "\r\n";

    [Fact]
    public void ParseFile_SingleAdapter_ProducesOneRecordPerDay()
    {
        var path = WriteTempFile(SingleAdapterSample);
        try
        {
            var results = LegacyImporter.ParseFile(path);

            Assert.Equal(2, results.Count);

            var day1 = Assert.Single(results, r => r.Date == new DateOnly(2008, 7, 1));
            Assert.Equal("Broadcom 440x 10/100 Integrated Controller - Packet Scheduler Miniport", day1.AdapterName);
            Assert.Equal("00-11-43-68-28-0A", day1.MacAddress);
            Assert.Equal(86124, day1.SecondsOnline);
            Assert.Equal(1805957090, day1.BytesReceived);
            Assert.Equal(88092758, day1.BytesSent);

            var day2 = Assert.Single(results, r => r.Date == new DateOnly(2008, 7, 2));
            Assert.Equal(86331, day2.SecondsOnline);
            Assert.Equal(1441964543, day2.BytesReceived);
            Assert.Equal(46125748, day2.BytesSent);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ParseFile_TwoAdaptersSameDay_KeepsValuesSeparate()
    {
        var path = WriteTempFile(TwoAdaptersSameDaySample);
        try
        {
            var results = LegacyImporter.ParseFile(path);

            Assert.Equal(2, results.Count);

            var one = Assert.Single(results, r => r.AdapterName == "Ethernet Adapter One");
            Assert.Equal(1000, one.SecondsOnline);
            Assert.Equal(500, one.BytesReceived);
            Assert.Equal(200, one.BytesSent);

            var two = Assert.Single(results, r => r.AdapterName == "Wireless Adapter Two");
            Assert.Equal(2000, two.SecondsOnline);
            Assert.Equal(900, two.BytesReceived);
            Assert.Equal(300, two.BytesSent);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ParseFile_DateSerial_ConvertsToExpectedCalendarDate()
    {
        var path = WriteTempFile(SingleAdapterSample);
        try
        {
            var results = LegacyImporter.ParseFile(path);
            Assert.Contains(results, r => r.Date == new DateOnly(2008, 7, 1));
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static string WriteTempFile(string content)
    {
        var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".dat");
        File.WriteAllText(path, content);
        return path;
    }
}
