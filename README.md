# ConvExt.HtmlToMarkdown

A [DevToys 2.x](https://devtoys.app) extension that converts **HTML to Markdown**.

The feature model is the Go library [JohannesKaufmann/html-to-markdown](https://github.com/JohannesKaufmann/html-to-markdown). Under the hood the conversion is performed by the mature .NET library [ReverseMarkdown](https://github.com/mysticmind/reversemarkdown-net) (MIT), which exposes a configuration surface that maps almost 1:1 to the Go library's plugin set.

## Features

- GitHub-Flavored Markdown (tables, strikethrough)
- Smart link / href handling
- Configurable bullet character (`-`, `*`, `+`)
- Default code-block language fallback
- Unknown-tag policy: PassThrough / Drop / Bypass / Raise
- Optional removal of HTML comments
- Table-without-header handling (Default / EmptyRow)
- Smart Detection: text routed from other DevToys tools auto-fills the input

## Build

```powershell
dotnet restore
dotnet build -c Release
dotnet pack  -c Release src\ConvExt.HtmlToMarkdown\ConvExt.HtmlToMarkdown.csproj
```

The resulting `.nupkg` lands in `bin\Release\AnyCPU\ConvExt.HtmlToMarkdown\`.

## Install in DevToys

1. Open DevToys.
2. Settings → **Manage extensions** → **Install**.
3. Select the produced `.nupkg`.
4. Restart DevToys.

The new tool appears under **Converters** with the title *"HTML to Markdown"*.

## Repository

Source: <https://github.com/marcoatribeiro/DevToys.Extensions.HtmlToMarkdownConverter>

## License

MIT (see [LICENSE](LICENSE) — add one before publishing).
