using System.Collections.Generic;
using System.Linq;
using RecruitPro.Application.Interfaces.IServices.Automation;

namespace RecruitPro.Application.Services.Automation.Mcp;

public class McpToolRegistry : IMcpToolRegistry
{
    private readonly Dictionary<string, IMcpTool> _tools;

    public McpToolRegistry(IEnumerable<IMcpTool> tools)
        => _tools = tools.ToDictionary(t => t.Name, t => t);

    public IReadOnlyList<IMcpTool> Tools => _tools.Values.OrderBy(t => t.Name).ToList();

    public IMcpTool? Resolve(string name)
        => _tools.TryGetValue(name, out IMcpTool? tool) ? tool : null;
}
