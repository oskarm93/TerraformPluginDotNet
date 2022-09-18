namespace TerraformPluginDotNet.ResourceProvider;
public class PlanResult<T>
{
    public T Result { get; set; }

    public IEnumerable<string> RequiresReplace { get; set; } = Array.Empty<string>();
}
