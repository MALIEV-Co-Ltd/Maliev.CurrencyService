namespace Maliev.CurrencyService.Tests.Workflows;

using System;
using System.IO;

using Xunit;

public sealed class WorkflowContractTests
{
    private static readonly string Root = FindRoot();
    private static readonly string Workflows = Path.Combine(Root, ".github", "workflows");

    [Theory]
    [InlineData("pr-validation.yml", "pull_request:")]
    [InlineData("ci-main.yml", "main")]
    [InlineData("ci-develop.yml", "develop")]
    [InlineData("ci-staging.yml", "release/v*")]
    public void EntryWorkflows_AreReadOnlyValidationOnly(string file, string trigger)
    {
        var text = Read(file);
        Assert.Contains(trigger, text);
        Assert.Contains("contents: read", text);
        Assert.Contains("uses: ./.github/workflows/_validate.yml", text);
        AssertSafe(text);
    }

    [Fact]
    public void ReusableValidation_UsesPinnedPublicSharedSources()
    {
        var text = Read("_validate.yml");
        Assert.Contains("workflow_call:", text);
        Assert.Contains("actions/checkout@9c091bb21b7c1c1d1991bb908d89e4e9dddfe3e0", text);
        Assert.Contains("actions/setup-dotnet@a98b56852c35b8e3190ac28c8c2271da59106c68", text);
        Assert.Contains("ref: 01d506203763b914e237268a8746f1406423df86", text);
        Assert.Contains("ref: 559a00db0c7920a5247fdff60d4476ad23a9a501", text);
        Assert.Equal(3, text.Split("/p:SharedSourceRoot=../shared", StringSplitOptions.None).Length - 1);
        Assert.Equal(3, text.Split("/p:GITHUB_ACTIONS=false", StringSplitOptions.None).Length - 1);
        AssertSafe(text);
    }

    [Fact]
    public void AllWorkflows_ForbidSecretsAndDeployment()
    {
        foreach (var file in Directory.GetFiles(Workflows, "*.yml"))
        {
            AssertSafe(File.ReadAllText(file));
        }
    }

    private static void AssertSafe(string text)
    {
        foreach (var value in new[]
        {
            "secrets.", "GITOPS_PAT", "GCP_SA_KEY", "NUGET_PASSWORD", "id-token: write",
            "google-github-actions/auth", "gcloud auth", "docker push", "maliev-gitops",
            "kustomize edit", "gh pr create", "pull_request_target",
        })
        {
            Assert.DoesNotContain(value, text, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static string Read(string file) => File.ReadAllText(Path.Combine(Workflows, file));

    private static string FindRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Maliev.CurrencyService.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("Could not locate CurrencyService repository root.");
    }
}
