using System;

namespace LeapworkBuildManager
{
    // Captured on the UI thread before workers begin; workers never read controls.
    internal sealed class DiagnosticContext
    {
        public readonly string OperationId, Build;
        public readonly Uri Url;
        public DiagnosticContext(string operationId, string build, Uri url)
        {
            OperationId = operationId;
            Build = build;
            Url = url;
        }
    }
}
