using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using TerraformPluginDotNet;
using TerraformPluginDotNet.ResourceProvider;
using TerraformPluginDotNet.Testing;

namespace SampleProvider.Test;

[TestFixture(Category = "Functional", Explicit = true)]
public class SampleProviderTest
{
    private const string ProviderName = "sampleprovider";

    private TerraformTestHost _host;

    [OneTimeSetUp]
    public void Setup()
    {
        _host = new TerraformTestHost(Environment.GetEnvironmentVariable("TF_PLUGIN_DOTNET_TEST_TF_BIN"));
        _host.Start($"example.com/example/{ProviderName}", Configure);
    }

    [OneTimeTearDown]
    public async Task TearDown()
    {
        await _host.DisposeAsync();
    }

    private void Configure(IServiceCollection services, IResourceRegistryContext registryContext)
    {
        services.AddSingleton<SampleConfigurator>();
        services.AddTerraformProviderConfigurator<Configuration, SampleConfigurator>();
        services.AddSingleton<IResourceProvider<SampleFileResource>, SampleFileResourceProvider>();
        registryContext.RegisterResource<SampleFileResource>($"{ProviderName}_file");
    }

    [Test]
    public async Task TestCreateFile()
    {
        using var terraform = await _host.CreateTerraformTestInstanceAsync(ProviderName);

        var resourcePath = Path.Combine(terraform.WorkDir, "file.tf");
        var testFilePath = Path.Combine(terraform.WorkDir, "test.txt");
        var fileContent = "this is a test";

        await File.WriteAllTextAsync(resourcePath, $@"
resource ""{ProviderName}_file"" ""demo_file"" {{
path = ""{testFilePath.Replace("\\", "\\\\")}""
content = ""{fileContent}""
}}
");

        await terraform.PlanAsync();
        await terraform.ApplyAsync();

        Assert.That(File.Exists(testFilePath));
        Assert.That(await File.ReadAllTextAsync(testFilePath), Is.EqualTo(fileContent));
    }

    [Test]
    public async Task TestUpdateFile()
    {
        using var terraform = await _host.CreateTerraformTestInstanceAsync(ProviderName);

        var resourcePath = Path.Combine(terraform.WorkDir, "file.tf");
        var testFilePath = Path.Combine(terraform.WorkDir, "test.txt");

        await File.WriteAllTextAsync(resourcePath, $@"
resource ""{ProviderName}_file"" ""demo_file"" {{
path = ""{testFilePath.Replace("\\", "\\\\")}""
content = ""Content 1""
}}
");

        await terraform.PlanAsync();
        await terraform.ApplyAsync();

        var updatedContent = "Content 2";
        await File.WriteAllTextAsync(resourcePath, $@"
resource ""{ProviderName}_file"" ""demo_file"" {{
path = ""{testFilePath.Replace("\\", "\\\\")}""
content = ""{updatedContent}""
}}
");

        await terraform.PlanAsync();
        await terraform.ApplyAsync();

        Assert.That(File.Exists(testFilePath));
        Assert.That(await File.ReadAllTextAsync(testFilePath), Is.EqualTo(updatedContent));
    }

    [Test]
    public async Task TestDeleteFile()
    {
        using var terraform = await _host.CreateTerraformTestInstanceAsync(ProviderName);

        var resourcePath = Path.Combine(terraform.WorkDir, "file.tf");
        var testFilePath = Path.Combine(terraform.WorkDir, "test.txt");

        await File.WriteAllTextAsync(resourcePath, $@"
resource ""{ProviderName}_file"" ""demo_file"" {{
path = ""{testFilePath.Replace("\\", "\\\\")}""
content = ""Content""
}}
");

        await terraform.ApplyAsync();

        await File.WriteAllTextAsync(resourcePath, string.Empty);
        await terraform.PlanAsync();
        await terraform.ApplyAsync();

        Assert.That(File.Exists(testFilePath), Is.False);
    }

    [Test]
    public async Task TestImportFile()
    {
        using var terraform = await _host.CreateTerraformTestInstanceAsync(ProviderName);

        var resourceAddress = $"{ProviderName}_file.imported";

        var resourcePath = Path.Combine(terraform.WorkDir, "imported.tf");
        var testFilePath = Path.Combine(terraform.WorkDir, "test.txt");
        await File.WriteAllTextAsync(testFilePath, "Something to import.");

        await File.WriteAllTextAsync(resourcePath, $@"
resource ""{ProviderName}_file"" ""imported"" {{
path = ""{testFilePath.Replace("\\", "\\\\")}""
content = ""Something to import.""
}}
");

        await terraform.ImportAsync(resourceAddress, testFilePath);

        var changes = await terraform.PlanWithOutputAsync();
        Assert.That(changes.ResourceChanges.SelectMany(x => x.Change.Actions).All(x => x == "no-op"), Is.True);
    }

    [Test]
    public async Task TestConfigureFileHeader()
    {
        using var terraform = await _host.CreateTerraformTestInstanceAsync(ProviderName, configure: false);

        await File.WriteAllTextAsync(terraform.WorkDir + "/conf.tf", $@"
provider ""{ProviderName}"" {{
  file_header = ""# File Header""
}}
terraform {{
  required_providers {{
    {ProviderName} = {{
      source = ""example.com/example/{ProviderName}""
      version = ""1.0.0""
    }}
  }}
}}
");

        var resourcePath = Path.Combine(terraform.WorkDir, "file.tf");
        var testFilePath = Path.Combine(terraform.WorkDir, "test.txt");
        var fileContent = "this is a test";

        await File.WriteAllTextAsync(resourcePath, $@"
resource ""{ProviderName}_file"" ""demo_file"" {{
path = ""{testFilePath.Replace("\\", "\\\\")}""
content = ""{fileContent}""
}}
");

        await terraform.PlanAsync();
        await terraform.ApplyAsync();

        Assert.That(File.Exists(testFilePath));
        Assert.That(
            NormalizeLineEndings(await File.ReadAllTextAsync(testFilePath)),
            Is.EqualTo(NormalizeLineEndings($@"
# File Header
{fileContent}
".Trim())));
    }

    private static string NormalizeLineEndings(string input) => input.Replace("\r\n", "\n");
}
