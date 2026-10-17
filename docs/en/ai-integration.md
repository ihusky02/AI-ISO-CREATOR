# Gemini AI Integration

## Scope

`GeminiAiService` assists with package planning for a user-supplied Linux Live ISO. Its response is limited to:

- `packages` — packages to install;
- `packagesToRemove` — packages to remove;
- `suggestedCommands` — textual command examples for the user to review or use manually.

Gemini does not choose or modify the distribution, release, architecture, desktop environment, ISO label, user accounts, or boot configuration. Those choices belong to the user or to the module selected from the detected source distribution. The model response is not a build script.

## Request and validation flow

Requests are sent to the Google Generative Language API with JSON as the expected response format. The response is parsed and restricted to the package-plan fields. Package names must match an allowed token format; each list is limited to 100 items, and a package cannot appear in both the install and removal lists. Command suggestions have count and length limits; shell operators and redirection characters are rejected.

Validation constrains response shape, but does not verify that a package exists in repositories for the specific distribution release. Users should review the package plan before starting a build.

Values in `suggestedCommands` are displayed as text and are never passed to `chroot`, a shell, or the container. The build executes only package operations defined by the detected distribution module. Package names receive additional validation by the build service.

## No-key mode

If no Gemini API key is configured, the application generates a plan from deterministic local templates. This fallback does not search the web. Local recipe refinement can recognize simple requests to add or remove packages, but it does not provide full natural-language model reasoning.

## Configuration

Configure the API key and preferred model through the application settings. Available models depend on application configuration and Google's current availability; on request failures, the service tries configured fallback models. Do not place API keys in recipes or commit them to the repository.
