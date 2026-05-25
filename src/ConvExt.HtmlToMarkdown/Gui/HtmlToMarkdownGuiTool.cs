using System.ComponentModel.Composition;
using ConvExt.HtmlToMarkdown.Strings;
using DevToys.Api;
using Microsoft.Extensions.Logging;
using ReverseMarkdown;
using static DevToys.Api.GUI;

namespace ConvExt.HtmlToMarkdown.Gui;

[Export(typeof(IGuiTool))]
[Name("ConvExt.HtmlToMarkdown")]
[ToolDisplayInformation(
    IconFontName = "FluentSystemIcons",
    IconGlyph = '',
    GroupName = PredefinedCommonToolGroupNames.Converters,
    ResourceManagerAssemblyIdentifier = nameof(HtmlToMarkdownResourceAssemblyIdentifier),
    ResourceManagerBaseName = "ConvExt.HtmlToMarkdown.Strings.HtmlToMarkdown",
    ShortDisplayTitleResourceName = nameof(Strings.HtmlToMarkdown.ShortDisplayTitle),
    LongDisplayTitleResourceName = nameof(Strings.HtmlToMarkdown.LongDisplayTitle),
    DescriptionResourceName = nameof(Strings.HtmlToMarkdown.Description),
    AccessibleNameResourceName = nameof(Strings.HtmlToMarkdown.AccessibleName))]
[AcceptedDataTypeName(PredefinedCommonDataTypeNames.Text)]
internal sealed class HtmlToMarkdownGuiTool : IGuiTool, IDisposable
{
    internal enum UnknownTagsMode { PassThrough, Drop, Bypass, Raise }
    internal enum BulletStyle { Dash, Star, Plus }
    internal enum TableHeaderMode { Default, EmptyRow }

    private static readonly SettingDefinition<bool> githubFlavored
        = new(name: $"{nameof(HtmlToMarkdownGuiTool)}.{nameof(githubFlavored)}", defaultValue: true);

    private static readonly SettingDefinition<bool> smartHrefHandling
        = new(name: $"{nameof(HtmlToMarkdownGuiTool)}.{nameof(smartHrefHandling)}", defaultValue: false);

    private static readonly SettingDefinition<bool> removeComments
        = new(name: $"{nameof(HtmlToMarkdownGuiTool)}.{nameof(removeComments)}", defaultValue: true);

    private static readonly SettingDefinition<UnknownTagsMode> unknownTagsMode
        = new(name: $"{nameof(HtmlToMarkdownGuiTool)}.{nameof(unknownTagsMode)}", defaultValue: UnknownTagsMode.PassThrough);

    private static readonly SettingDefinition<BulletStyle> bulletStyle
        = new(name: $"{nameof(HtmlToMarkdownGuiTool)}.{nameof(bulletStyle)}", defaultValue: BulletStyle.Dash);

    private static readonly SettingDefinition<TableHeaderMode> tableHeaderMode
        = new(name: $"{nameof(HtmlToMarkdownGuiTool)}.{nameof(tableHeaderMode)}", defaultValue: TableHeaderMode.Default);

    private static readonly SettingDefinition<string> defaultCodeLanguage
        = new(name: $"{nameof(HtmlToMarkdownGuiTool)}.{nameof(defaultCodeLanguage)}", defaultValue: string.Empty);

    private enum GridRow { Header, Body, Footer }
    private enum GridColumn { Content }

    private readonly DisposableSemaphore _semaphore = new();
    private readonly ILogger _logger;
    private readonly ISettingsProvider _settingsProvider;

    private readonly IUIMultiLineTextInput _inputTextArea = MultiLineTextInput("html-to-markdown-input");
    private readonly IUIMultiLineTextInput _outputTextArea = MultiLineTextInput("html-to-markdown-output");
    private readonly IUISingleLineTextInput _defaultCodeLanguageInput = SingleLineTextInput("html-to-markdown-default-code-language");
    private readonly IUIInfoBar _infoBar = InfoBar("html-to-markdown-info-bar");

    private CancellationTokenSource? _cancellationTokenSource;

    [ImportingConstructor]
    public HtmlToMarkdownGuiTool(ISettingsProvider settingsProvider)
    {
        _logger = this.Log();
        _settingsProvider = settingsProvider;

        _defaultCodeLanguageInput.Text(_settingsProvider.GetSetting(defaultCodeLanguage));
    }

    internal Task? WorkTask { get; private set; }

    public UIToolView View
        => new(
            isScrollable: true,
            Grid()
                .ColumnLargeSpacing()
                .RowLargeSpacing()
                .Rows(
                    (GridRow.Header, Auto),
                    (GridRow.Body, new UIGridLength(1, UIGridUnitType.Fraction)),
                    (GridRow.Footer, Auto))
                .Columns(
                    (GridColumn.Content, new UIGridLength(1, UIGridUnitType.Fraction)))
                .Cells(
                    Cell(
                        GridRow.Header,
                        GridColumn.Content,
                        Stack()
                            .Vertical()
                            .SmallSpacing()
                            .WithChildren(
                                Label().Text(Strings.HtmlToMarkdown.ConfigurationTitle),

                                Setting("html-to-markdown-gfm-setting")
                                    .Icon("FluentSystemIcons", '')
                                    .Title(Strings.HtmlToMarkdown.GithubFlavoredTitle)
                                    .Description(Strings.HtmlToMarkdown.GithubFlavoredDescription)
                                    .Handle(_settingsProvider, githubFlavored, OnAnySettingChanged),

                                Setting("html-to-markdown-smart-href-setting")
                                    .Icon("FluentSystemIcons", '')
                                    .Title(Strings.HtmlToMarkdown.SmartHrefTitle)
                                    .Description(Strings.HtmlToMarkdown.SmartHrefDescription)
                                    .Handle(_settingsProvider, smartHrefHandling, OnAnySettingChanged),

                                Setting("html-to-markdown-remove-comments-setting")
                                    .Icon("FluentSystemIcons", '')
                                    .Title(Strings.HtmlToMarkdown.RemoveCommentsTitle)
                                    .Description(Strings.HtmlToMarkdown.RemoveCommentsDescription)
                                    .Handle(_settingsProvider, removeComments, OnAnySettingChanged),

                                Setting("html-to-markdown-unknown-tags-setting")
                                    .Icon("FluentSystemIcons", '')
                                    .Title(Strings.HtmlToMarkdown.UnknownTagsTitle)
                                    .Description(Strings.HtmlToMarkdown.UnknownTagsDescription)
                                    .Handle(
                                        _settingsProvider,
                                        unknownTagsMode,
                                        OnUnknownTagsModeChanged,
                                        Item(Strings.HtmlToMarkdown.UnknownTagsPassThrough, UnknownTagsMode.PassThrough),
                                        Item(Strings.HtmlToMarkdown.UnknownTagsDrop, UnknownTagsMode.Drop),
                                        Item(Strings.HtmlToMarkdown.UnknownTagsBypass, UnknownTagsMode.Bypass),
                                        Item(Strings.HtmlToMarkdown.UnknownTagsRaise, UnknownTagsMode.Raise)),

                                Setting("html-to-markdown-bullet-char-setting")
                                    .Icon("FluentSystemIcons", '')
                                    .Title(Strings.HtmlToMarkdown.BulletCharTitle)
                                    .Description(Strings.HtmlToMarkdown.BulletCharDescription)
                                    .Handle(
                                        _settingsProvider,
                                        bulletStyle,
                                        OnBulletStyleChanged,
                                        Item(Strings.HtmlToMarkdown.BulletDash, BulletStyle.Dash),
                                        Item(Strings.HtmlToMarkdown.BulletStar, BulletStyle.Star),
                                        Item(Strings.HtmlToMarkdown.BulletPlus, BulletStyle.Plus)),

                                Setting("html-to-markdown-table-header-setting")
                                    .Icon("FluentSystemIcons", '')
                                    .Title(Strings.HtmlToMarkdown.TableWithoutHeaderTitle)
                                    .Description(Strings.HtmlToMarkdown.TableWithoutHeaderDescription)
                                    .Handle(
                                        _settingsProvider,
                                        tableHeaderMode,
                                        OnTableHeaderModeChanged,
                                        Item(Strings.HtmlToMarkdown.TableWithoutHeaderDefault, TableHeaderMode.Default),
                                        Item(Strings.HtmlToMarkdown.TableWithoutHeaderEmptyRow, TableHeaderMode.EmptyRow)),

                                Setting("html-to-markdown-default-code-language-setting")
                                    .Icon("FluentSystemIcons", '')
                                    .Title(Strings.HtmlToMarkdown.DefaultCodeLanguageTitle)
                                    .Description(Strings.HtmlToMarkdown.DefaultCodeLanguageDescription)
                                    .InteractiveElement(
                                        _defaultCodeLanguageInput
                                            .OnTextChanged(OnDefaultCodeLanguageChanged)))),

                    Cell(
                        GridRow.Body,
                        GridColumn.Content,
                        SplitGrid()
                            .Vertical()
                            .WithLeftPaneChild(
                                _inputTextArea
                                    .Title(Strings.HtmlToMarkdown.Input)
                                    .Language("html")
                                    .Extendable()
                                    .OnTextChanged(OnInputTextChanged))
                            .WithRightPaneChild(
                                _outputTextArea
                                    .Title(Strings.HtmlToMarkdown.Output)
                                    .Language("markdown")
                                    .ReadOnly()
                                    .Extendable())),

                    Cell(
                        GridRow.Footer,
                        GridColumn.Content,
                        _infoBar
                            .Title(Strings.HtmlToMarkdown.ErrorBarTitle)
                            .Error()
                            .NonClosable()
                            .Close())));

    public void OnDataReceived(string dataTypeName, object? parsedData)
    {
        if (dataTypeName == PredefinedCommonDataTypeNames.Text && parsedData is string text)
        {
            _inputTextArea.Text(text);
        }
    }

    public void Dispose()
    {
        _cancellationTokenSource?.Cancel();
        _cancellationTokenSource?.Dispose();
        _semaphore.Dispose();
    }

    private void OnInputTextChanged(string text) => StartConvert(text);

    private void OnAnySettingChanged(bool _) => StartConvert(_inputTextArea.Text);

    private void OnUnknownTagsModeChanged(UnknownTagsMode _) => StartConvert(_inputTextArea.Text);

    private void OnBulletStyleChanged(BulletStyle _) => StartConvert(_inputTextArea.Text);

    private void OnTableHeaderModeChanged(TableHeaderMode _) => StartConvert(_inputTextArea.Text);

    private void OnDefaultCodeLanguageChanged(string value)
    {
        _settingsProvider.SetSetting(defaultCodeLanguage, value ?? string.Empty);
        StartConvert(_inputTextArea.Text);
    }

    private void StartConvert(string text)
    {
        _cancellationTokenSource?.Cancel();
        _cancellationTokenSource?.Dispose();
        _cancellationTokenSource = new CancellationTokenSource();

        WorkTask = ConvertAsync(text, _cancellationTokenSource.Token);
    }

    private async Task ConvertAsync(string input, CancellationToken cancellationToken)
    {
        using (await _semaphore.WaitAsync(cancellationToken))
        {
            await TaskSchedulerAwaiter.SwitchOffMainThreadAsync(cancellationToken);

            if (string.IsNullOrEmpty(input))
            {
                _outputTextArea.Text(string.Empty);
                _infoBar.Close();
                return;
            }

            try
            {
                var config = BuildConfig();
                string markdown = new Converter(config).Convert(input);
                _outputTextArea.Text(markdown);
                _infoBar.Close();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "HTML to Markdown conversion failed");
                _outputTextArea.Text(string.Empty);
                _infoBar.Description(ex.Message).Open();
            }
        }
    }

    private Config BuildConfig()
    {
        var config = new Config
        {
            GithubFlavored = _settingsProvider.GetSetting(githubFlavored),
            SmartHrefHandling = _settingsProvider.GetSetting(smartHrefHandling),
            RemoveComments = _settingsProvider.GetSetting(removeComments),
            UnknownTags = MapUnknownTags(_settingsProvider.GetSetting(unknownTagsMode)),
            ListBulletChar = MapBullet(_settingsProvider.GetSetting(bulletStyle)),
            TableWithoutHeaderRowHandling = MapTableHeader(_settingsProvider.GetSetting(tableHeaderMode)),
            DefaultCodeBlockLanguage = _settingsProvider.GetSetting(defaultCodeLanguage)
        };

        return config;
    }

    private static Config.UnknownTagsOption MapUnknownTags(UnknownTagsMode mode) => mode switch
    {
        UnknownTagsMode.PassThrough => Config.UnknownTagsOption.PassThrough,
        UnknownTagsMode.Drop => Config.UnknownTagsOption.Drop,
        UnknownTagsMode.Bypass => Config.UnknownTagsOption.Bypass,
        UnknownTagsMode.Raise => Config.UnknownTagsOption.Raise,
        _ => Config.UnknownTagsOption.PassThrough
    };

    private static char MapBullet(BulletStyle style) => style switch
    {
        BulletStyle.Dash => '-',
        BulletStyle.Star => '*',
        BulletStyle.Plus => '+',
        _ => '-'
    };

    private static Config.TableWithoutHeaderRowHandlingOption MapTableHeader(TableHeaderMode mode) => mode switch
    {
        TableHeaderMode.Default => Config.TableWithoutHeaderRowHandlingOption.Default,
        TableHeaderMode.EmptyRow => Config.TableWithoutHeaderRowHandlingOption.EmptyRow,
        _ => Config.TableWithoutHeaderRowHandlingOption.Default
    };
}
