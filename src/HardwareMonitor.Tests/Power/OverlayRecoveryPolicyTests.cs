using HardwareMonitor.Core.Power;
using Xunit;

namespace HardwareMonitor.Tests.Power;

public class OverlayRecoveryPolicyTests
{
    [Fact]
    public void KaynnistyksenDisplayOn_EiLuoUudelleen()
    {
        // RegisterPowerSettingNotification lähettää rekisteröityessä heti
        // nykytilan (näyttö päällä) — se ei saa laukaista uudelleenluontia.
        var policy = new OverlayRecoveryPolicy();

        Assert.False(policy.ShouldRecreateOverlay(PowerSessionEvent.DisplayOn));
    }

    [Fact]
    public void NaytonSammuminenJaHeraaminen_LuoUudelleen()
    {
        var policy = new OverlayRecoveryPolicy();

        Assert.False(policy.ShouldRecreateOverlay(PowerSessionEvent.DisplayOff));
        Assert.True(policy.ShouldRecreateOverlay(PowerSessionEvent.DisplayOn));
    }

    [Fact]
    public void ToinenDisplayOnPerakkain_EiLuoToista()
    {
        var policy = new OverlayRecoveryPolicy();
        policy.ShouldRecreateOverlay(PowerSessionEvent.DisplayOff);
        policy.ShouldRecreateOverlay(PowerSessionEvent.DisplayOn);

        Assert.False(policy.ShouldRecreateOverlay(PowerSessionEvent.DisplayOn));
    }

    [Fact]
    public void LepotilaJaJatkaminen_LuoUudelleen()
    {
        var policy = new OverlayRecoveryPolicy();

        Assert.False(policy.ShouldRecreateOverlay(PowerSessionEvent.Suspend));
        Assert.True(policy.ShouldRecreateOverlay(PowerSessionEvent.Resume));
    }

    [Fact]
    public void LukitusJaAvaus_LuoUudelleen()
    {
        var policy = new OverlayRecoveryPolicy();

        Assert.False(policy.ShouldRecreateOverlay(PowerSessionEvent.SessionLock));
        Assert.True(policy.ShouldRecreateOverlay(PowerSessionEvent.SessionUnlock));
    }

    [Fact]
    public void AvausIlmanLukitusta_EiLuoUudelleen()
    {
        var policy = new OverlayRecoveryPolicy();

        Assert.False(policy.ShouldRecreateOverlay(PowerSessionEvent.SessionUnlock));
    }

    [Fact]
    public void MikaTahansaHeratys_PurkaaVirityksen()
    {
        // Näyttö sammui, mutta ensimmäinen havaittu herätys on Resume —
        // uudelleenluonti tehdään silti (ja vain kerran).
        var policy = new OverlayRecoveryPolicy();
        policy.ShouldRecreateOverlay(PowerSessionEvent.DisplayOff);

        Assert.True(policy.ShouldRecreateOverlay(PowerSessionEvent.Resume));
        Assert.False(policy.ShouldRecreateOverlay(PowerSessionEvent.DisplayOn));
        Assert.False(policy.ShouldRecreateOverlay(PowerSessionEvent.SessionUnlock));
    }

    [Fact]
    public void Himmennys_EiVirita()
    {
        // Himmennetty näyttö piirtää yhä — vasta sammuminen on riski.
        var policy = new OverlayRecoveryPolicy();
        policy.ShouldRecreateOverlay(PowerSessionEvent.DisplayDimmed);

        Assert.False(policy.ShouldRecreateOverlay(PowerSessionEvent.DisplayOn));
    }

    [Fact]
    public void UusiJakso_ViriaaUudelleen()
    {
        var policy = new OverlayRecoveryPolicy();
        policy.ShouldRecreateOverlay(PowerSessionEvent.DisplayOff);
        Assert.True(policy.ShouldRecreateOverlay(PowerSessionEvent.DisplayOn));

        policy.ShouldRecreateOverlay(PowerSessionEvent.DisplayOff);
        Assert.True(policy.ShouldRecreateOverlay(PowerSessionEvent.DisplayOn));
    }
}
