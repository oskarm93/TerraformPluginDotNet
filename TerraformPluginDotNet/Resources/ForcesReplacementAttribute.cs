namespace TerraformPluginDotNet.Resources;

/// <summary>
/// Indicates that a change in the property value "forces replacement" of a resource.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public class ForcesReplacementAttribute : Attribute
{
}
