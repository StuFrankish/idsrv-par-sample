[![.NET Build](https://github.com/StuFrankish/idsrv-par-sample/actions/workflows/dotnet.yml/badge.svg)](https://github.com/StuFrankish/idsrv-par-sample/actions/workflows/dotnet.yml)
[![CodeQL](https://github.com/StuFrankish/idsrv-par-sample/actions/workflows/github-code-scanning/codeql/badge.svg)](https://github.com/StuFrankish/idsrv-par-sample/actions/workflows/github-code-scanning/codeql)

# Proof of Concept Requirements
1. The Client & Server must use Authorization Code flow, with PKCE
2. The Client is required to use Pushed Authorization Requests
3. Users are required to have a client-specific role before being allowed to access the client

Uses .Net 8 LTS and Duende Identity Server v7.

# Additional Features
- Hangfire background processing
- Healthcheck endpoint
- Custom uptime Healthcheck [Github Repo](https://github.com/StuFrankish/HealthChecks) | [Nuget](https://www.nuget.org/packages/HealthChecks.Uptime)
