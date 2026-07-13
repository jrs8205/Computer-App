namespace HardwareMonitor.Core.Power;

/// <summary>Virta- ja istuntotapahtuma, joka voi vaikuttaa overlayn piirtoon.</summary>
public enum PowerSessionEvent
{
    DisplayOn,
    DisplayOff,
    DisplayDimmed,
    Suspend,
    Resume,
    SessionLock,
    SessionUnlock,
}

/// <summary>
/// Päättää, milloin overlay-ikkuna on luotava uudelleen. WPF:n läpinäkyvä
/// (WS_EX_LAYERED) ikkuna voi jäädä pysyvästi tyhjäksi, kun näyttö sammuu,
/// kone käy lepotilassa tai istunto lukitaan (havaittu 13.7.2026: HWND jää
/// näkyväksi oikealle paikalleen, mutta sisältöä ei piirretä). Riskitapahtuma
/// virittää tilakoneen ja ensimmäinen herätystapahtuma laukaisee
/// uudelleenluonnin — vain kerran per jakso, jottei ikkuna väpätä, kun
/// herätyksen yhteydessä tulee useita ilmoituksia peräkkäin.
/// Puhdas tilakone: ei kelloa, ei Win32-riippuvuuksia.
/// </summary>
public sealed class OverlayRecoveryPolicy
{
    private bool _armed;

    /// <summary>Palauttaa true, kun overlay on syytä luoda uudelleen.</summary>
    public bool ShouldRecreateOverlay(PowerSessionEvent e)
    {
        switch (e)
        {
            case PowerSessionEvent.DisplayOff:
            case PowerSessionEvent.Suspend:
            case PowerSessionEvent.SessionLock:
                _armed = true;
                return false;

            case PowerSessionEvent.DisplayOn:
            case PowerSessionEvent.Resume:
            case PowerSessionEvent.SessionUnlock:
                if (_armed)
                {
                    _armed = false;
                    return true;
                }

                return false;

            default:
                return false;
        }
    }
}
