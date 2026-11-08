namespace McpReadOnlyPrototype;

internal static class StartupExceptionPolicy
{
    internal static bool IsConfigurationError(Exception exception) => exception is ArgumentException
        or DirectoryNotFoundException
        or FileNotFoundException
        or UnauthorizedAccessException
        or IOException;
}
