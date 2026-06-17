namespace HarmonicEngine.Diagnostics
{
    /// <summary>
    /// AOP-style diagnostic hook. Implement and register with <see cref="HarmonicDiagnosticHub"/>.
    /// </summary>
    public interface IHarmonicDiagnosticAspect
    {
        string AspectName { get; }
        int Order { get; }
        void OnAttach(HarmonicDiagnosticSession session);
        void OnDetach();
        void OnEvent(in HarmonicDiagnosticEvent diagnosticEvent);
    }
}
