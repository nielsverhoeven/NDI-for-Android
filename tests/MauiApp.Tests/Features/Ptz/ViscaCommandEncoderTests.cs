using NdiForAndroid.Features.Ptz.Services;
using Xunit;

namespace NdiForAndroid.Tests.Features.Ptz;

public class ViscaCommandEncoderTests
{
    [Fact]
    public void PanTiltDrive_FullSpeedRight_EncodesMaxPanByte()
    {
        var frame = ViscaCommandEncoder.PanTiltDrive(1f, 0f);

        Assert.Equal(new byte[] { 0x81, 0x01, 0x06, 0x01, 0x18, 0x00, 0x02, 0x03, 0xFF }, frame);
    }

    [Fact]
    public void PanTiltDrive_FullSpeedLeftAndUp_EncodesDistinctPanAndTiltMax()
    {
        var frame = ViscaCommandEncoder.PanTiltDrive(-1f, 1f);

        Assert.Equal(new byte[] { 0x81, 0x01, 0x06, 0x01, 0x18, 0x14, 0x01, 0x01, 0xFF }, frame);
    }

    [Fact]
    public void PanTiltDrive_FullSpeedDown_EncodesMaxTiltByte()
    {
        var frame = ViscaCommandEncoder.PanTiltDrive(0f, -1f);

        Assert.Equal(new byte[] { 0x81, 0x01, 0x06, 0x01, 0x00, 0x14, 0x03, 0x02, 0xFF }, frame);
    }

    [Fact]
    public void PanTiltDrive_Zero_EncodesStop()
    {
        var frame = ViscaCommandEncoder.PanTiltDrive(0f, 0f);

        Assert.Equal(new byte[] { 0x81, 0x01, 0x06, 0x01, 0x00, 0x00, 0x03, 0x03, 0xFF }, frame);
    }

    [Fact]
    public void PanTiltDrive_HalfSpeedRightAndUp_RoundsAwayFromZero()
    {
        var frame = ViscaCommandEncoder.PanTiltDrive(0.5f, 0.5f);

        // pan: round(0.5*23)=12, +1=13=0x0D; tilt: round(0.5*19)=10, +1=11=0x0B
        Assert.Equal(new byte[] { 0x81, 0x01, 0x06, 0x01, 0x0D, 0x0B, 0x02, 0x01, 0xFF }, frame);
    }

    [Fact]
    public void PanTiltDrive_HalfSpeedLeftAndDown_RoundsAwayFromZero()
    {
        var frame = ViscaCommandEncoder.PanTiltDrive(-0.5f, -0.5f);

        Assert.Equal(new byte[] { 0x81, 0x01, 0x06, 0x01, 0x0D, 0x0B, 0x01, 0x02, 0xFF }, frame);
    }

    [Theory]
    [InlineData(2f)]
    [InlineData(-2f)]
    public void PanTiltDrive_MagnitudeAboveOne_ClampsToMaxSpeed(float value)
    {
        var frame = ViscaCommandEncoder.PanTiltDrive(value, 0f);

        Assert.Equal(0x18, frame[4]);
    }

    [Fact]
    public void ZoomSpeed_FullTele_Encodes()
    {
        var frame = ViscaCommandEncoder.ZoomSpeed(1f);

        Assert.Equal(new byte[] { 0x81, 0x01, 0x04, 0x07, 0x27, 0xFF }, frame);
    }

    [Fact]
    public void ZoomSpeed_FullWide_Encodes()
    {
        var frame = ViscaCommandEncoder.ZoomSpeed(-1f);

        Assert.Equal(new byte[] { 0x81, 0x01, 0x04, 0x07, 0x37, 0xFF }, frame);
    }

    [Fact]
    public void ZoomSpeed_Zero_EncodesStop()
    {
        var frame = ViscaCommandEncoder.ZoomSpeed(0f);

        Assert.Equal(new byte[] { 0x81, 0x01, 0x04, 0x07, 0x00, 0xFF }, frame);
    }

    [Fact]
    public void ZoomSpeed_HalfTele_RoundsAwayFromZero()
    {
        var frame = ViscaCommandEncoder.ZoomSpeed(0.5f);

        // round(0.5*7)=4
        Assert.Equal(new byte[] { 0x81, 0x01, 0x04, 0x07, 0x24, 0xFF }, frame);
    }

    [Fact]
    public void AutoFocus_Encodes()
    {
        var frame = ViscaCommandEncoder.AutoFocus();

        Assert.Equal(new byte[] { 0x81, 0x01, 0x04, 0x18, 0x01, 0xFF }, frame);
    }

    [Theory]
    [InlineData(0, 0x00)]
    [InlineData(5, 0x05)]
    [InlineData(15, 0x0F)]
    public void StorePreset_WithinRange_Encodes(int presetNumber, byte expectedByte)
    {
        var frame = ViscaCommandEncoder.StorePreset(presetNumber);

        Assert.Equal(new byte[] { 0x81, 0x01, 0x04, 0x3F, 0x01, expectedByte, 0xFF }, frame);
    }

    [Theory]
    [InlineData(0, 0x00)]
    [InlineData(5, 0x05)]
    [InlineData(15, 0x0F)]
    public void RecallPreset_WithinRange_Encodes(int presetNumber, byte expectedByte)
    {
        var frame = ViscaCommandEncoder.RecallPreset(presetNumber);

        Assert.Equal(new byte[] { 0x81, 0x01, 0x04, 0x3F, 0x02, expectedByte, 0xFF }, frame);
    }

    [Fact]
    public void StorePreset_AboveRange_ClampsTo99()
    {
        var frame = ViscaCommandEncoder.StorePreset(150);

        Assert.Equal(0x63, frame[5]);
    }

    [Fact]
    public void StorePreset_BelowRange_ClampsToZero()
    {
        var frame = ViscaCommandEncoder.StorePreset(-5);

        Assert.Equal(0x00, frame[5]);
    }

    [Fact]
    public void RecallPreset_AboveRange_ClampsTo99()
    {
        var frame = ViscaCommandEncoder.RecallPreset(150);

        Assert.Equal(0x63, frame[5]);
    }
}
