using System.ComponentModel.Composition;
using DevToys.Api;

namespace ConvExt.HtmlToMarkdown;

[Export(typeof(IResourceAssemblyIdentifier))]
[Name(nameof(HtmlToMarkdownResourceAssemblyIdentifier))]
internal sealed class HtmlToMarkdownResourceAssemblyIdentifier : IResourceAssemblyIdentifier
{
    public ValueTask<FontDefinition[]> GetFontDefinitionsAsync()
    {
        return ValueTask.FromResult(Array.Empty<FontDefinition>());
    }
}
