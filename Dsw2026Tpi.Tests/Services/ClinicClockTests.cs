using System.Globalization;
using Dsw2026Tpi.Application.Options;
using Dsw2026Tpi.Application.Services;
using Microsoft.Extensions.Options;

namespace Dsw2026Tpi.Tests.Services;

public class ClinicClockTests
{
    [Theory]
    [InlineData(
        "2026-10-07T01:30:00+00:00",
        "2026-10-06T22:30:00-03:00")]
    [InlineData(
        "2026-11-01T01:30:00+00:00",
        "2026-10-31T22:30:00-03:00")]
    [InlineData(
        "2027-01-01T01:30:00+00:00",
        "2026-12-31T22:30:00-03:00")]
    public void GetCurrentLocalDateTime_AtUtcBoundaries_ReturnsClinicDate(
        string utcText,
        string expectedText)
    {
        var utcNow = Parse(utcText);
        var expected = Parse(expectedText);

        var clock = CreateClock(
            new FixedTimeProvider(utcNow, TimeZoneInfo.Utc));

        var actual = clock.GetCurrentLocalDateTime();

        // DateTimeOffset compara instantes; por eso también
        // verificamos explícitamente la hora local y el offset.
        Assert.Equal(expected.DateTime, actual.DateTime);
        Assert.Equal(expected.Offset, actual.Offset);
        Assert.Equal(utcNow.UtcDateTime, actual.UtcDateTime);
    }

    [Theory]
    [InlineData("America/Argentina/Buenos_Aires")]
    [InlineData("Argentina Standard Time")]
    public void GetCurrentLocalDateTime_WithEitherZoneIdentifier_ReturnsArgentinaTime(
        string timeZoneId)
    {
        var utcNow = Parse("2026-10-07T12:00:00+00:00");

        var clock = CreateClock(
            new FixedTimeProvider(utcNow, TimeZoneInfo.Utc),
            timeZoneId);

        var actual = clock.GetCurrentLocalDateTime();

        Assert.Equal(
            new DateTime(2026, 10, 7, 9, 0, 0),
            actual.DateTime);

        Assert.Equal(TimeSpan.FromHours(-3), actual.Offset);
    }

    [Fact]
    public void GetCurrentLocalDateTime_WithDifferentProviderLocalZones_ReturnsSameClinicTime()
    {
        var utcNow = Parse("2026-11-01T01:30:00+00:00");

        var westZone = TimeZoneInfo.CreateCustomTimeZone(
            "TestWest",
            TimeSpan.FromHours(-8),
            "Test West",
            "Test West");

        var eastZone = TimeZoneInfo.CreateCustomTimeZone(
            "TestEast",
            TimeSpan.FromHours(9),
            "Test East",
            "Test East");

        var westClock = CreateClock(
            new FixedTimeProvider(utcNow, westZone));

        var eastClock = CreateClock(
            new FixedTimeProvider(utcNow, eastZone));

        var westResult = westClock.GetCurrentLocalDateTime();
        var eastResult = eastClock.GetCurrentLocalDateTime();

        Assert.Equal(westResult.DateTime, eastResult.DateTime);
        Assert.Equal(westResult.Offset, eastResult.Offset);

        Assert.Equal(
            new DateTime(2026, 10, 31, 22, 30, 0),
            westResult.DateTime);

        Assert.Equal(TimeSpan.FromHours(-3), westResult.Offset);
    }

    [Fact]
    public void GetCurrentLocalDateTime_WithUtcConfiguration_UsesConfiguredZone()
    {
        var utcNow = Parse("2026-10-07T12:00:00+00:00");

        var clock = CreateClock(
            new FixedTimeProvider(utcNow, TimeZoneInfo.Utc),
            "UTC");

        var actual = clock.GetCurrentLocalDateTime();

        Assert.Equal(utcNow.DateTime, actual.DateTime);
        Assert.Equal(TimeSpan.Zero, actual.Offset);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Invalid/ClinicZone")]
    public void Constructor_WithInvalidZone_RejectsConfiguration(
        string timeZoneId)
    {
        var provider = new FixedTimeProvider(
            Parse("2026-10-07T12:00:00+00:00"),
            TimeZoneInfo.Utc);

        Assert.Throws<OptionsValidationException>(() =>
        {
            _ = CreateClock(provider, timeZoneId);
        });
    }

    private static ClinicClock CreateClock(
        TimeProvider provider,
        string timeZoneId = "America/Argentina/Buenos_Aires")
    {
        var options = Options.Create(new ClinicOptions
        {
            TimeZoneId = timeZoneId
        });

        return new ClinicClock(provider, options);
    }

    private static DateTimeOffset Parse(string value)
    {
        return DateTimeOffset.Parse(
            value,
            CultureInfo.InvariantCulture);
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _utcNow;
        private readonly TimeZoneInfo _localTimeZone;

        public FixedTimeProvider(
            DateTimeOffset utcNow,
            TimeZoneInfo localTimeZone)
        {
            _utcNow = utcNow;
            _localTimeZone = localTimeZone;
        }

        public override DateTimeOffset GetUtcNow()
        {
            return _utcNow;
        }

        public override TimeZoneInfo LocalTimeZone => _localTimeZone;
    }
}
