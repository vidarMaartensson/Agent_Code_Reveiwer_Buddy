namespace AgenticService.Tools;

/// <summary>
/// Provides coding guidelines and best practices inspired by tools like SonarQube and ESLint.
/// </summary>
public class GuidelineTool
{
    public string GetReviewGuidelines()
    {
        return """
            - (Sonar) Ensure methods do not exceed 20 lines (Cognitive Complexity).
            - (Sonar) Check for hardcoded secrets or sensitive credentials.
            - (Sonar) Avoid empty catch blocks; always log exceptions.
            - (ESLint) Ensure 'const' is used over 'let' where possible.
            - (ESLint) Use strict equality '===' instead of '=='.
            - (General) Ensure SOLID principles are respected.
            - (General) Validate all public API inputs.
            """;
    }
}