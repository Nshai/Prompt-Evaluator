using Anthropic.SDK;
using Anthropic.SDK.Messaging;
using System.ComponentModel;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text;

namespace AiPromptEvaluator;

public class MainViewModel : INotifyPropertyChanged
{
    private readonly AppSettings _settings;
    private string _prompt = string.Empty;
    private string _response = string.Empty;
    private string _documentFolder = string.Empty;
    private string _apiKey = string.Empty;
    private string _baseUrl = string.Empty;
    private string _modelsText = string.Empty;
    private string _selectedModel = string.Empty;
    private string _costText = "Estimated cost: $0.00";
    private bool _askClarification = true;
    private bool _isBusy;
    private List<string> _models = new();

    public event PropertyChangedEventHandler? PropertyChanged;

    public MainViewModel()
    {
        _settings = SettingsStorage.Load();
        ApiKey = _settings.AnthropicApiKey;
        BaseUrl = _settings.AnthropicBaseUrl;
        DocumentFolder = _settings.DocumentFolder;
        AskClarification = _settings.AskClarification;
        ModelsText = _settings.AvailableModels;
        Models = ParseModels(_settings.AvailableModels);
        SelectedModel = string.IsNullOrWhiteSpace(_settings.SelectedModel) ? Models.FirstOrDefault() ?? string.Empty : _settings.SelectedModel;
    }

    public string Prompt
    {
        get => _prompt;
        set { _prompt = value; OnPropertyChanged(); }
    }

    public string Response
    {
        get => _response;
        set { _response = value; OnPropertyChanged(); }
    }

    public string DocumentFolder
    {
        get => _documentFolder;
        set { _documentFolder = value; OnPropertyChanged(); }
    }

    public string ApiKey
    {
        get => _apiKey;
        set { _apiKey = value; OnPropertyChanged(); }
    }

    public string BaseUrl
    {
        get => _baseUrl;
        set { _baseUrl = value; OnPropertyChanged(); }
    }

    public string ModelsText
    {
        get => _modelsText;
        set
        {
            _modelsText = value;
            Models = ParseModels(value);
            OnPropertyChanged();
        }
    }

    public List<string> Models
    {
        get => _models;
        set { _models = value; OnPropertyChanged(); }
    }

    public string SelectedModel
    {
        get => _selectedModel;
        set { _selectedModel = value; OnPropertyChanged(); }
    }

    public bool AskClarification
    {
        get => _askClarification;
        set { _askClarification = value; OnPropertyChanged(); }
    }

    public bool IsBusy
    {
        get => _isBusy;
        set { _isBusy = value; OnPropertyChanged(); }
    }

    public string CostText
    {
        get => _costText;
        set { _costText = value; OnPropertyChanged(); }
    }

    public async Task RunPromptAsync()
    {
        if (string.IsNullOrWhiteSpace(Prompt))
        {
            throw new InvalidOperationException("Please enter a prompt.");
        }

        if (string.IsNullOrWhiteSpace(ApiKey))
        {
            throw new InvalidOperationException("Please configure an Anthropic API key before running the prompt.");
        }

        IsBusy = true;
        Response = "Working...";
        CostText = "Estimated cost: computing...";

        try
        {
            var folderContext = DocumentContextBuilder.BuildContext(DocumentFolder);
            var fullPrompt = BuildPrompt(Prompt, folderContext);
            var client = CreateClient();
            var messages = new List<Anthropic.SDK.Messaging.Message>
            {
                new(RoleType.User, fullPrompt)
            };

            var parameters = new MessageParameters
            {
                Messages = messages,
                MaxTokens = 1024,
                Model = SelectedModel,
                Stream = false,
                Temperature = 0.2m
            };

            var result = await client.Messages.GetClaudeMessageAsync(parameters);
            Response = result.Message.ToString();
            CostText = $"Estimated cost: ${EstimateCost(result.Usage?.InputTokens ?? 0, result.Usage?.OutputTokens ?? 0):0.000}";
        }
        catch (Exception ex)
        {
            Response = $"Error: {ex.Message}";
            CostText = "Estimated cost: unavailable";
        }
        finally
        {
            IsBusy = false;
        }
    }

    public void SaveSettings()
    {
        _settings.AnthropicApiKey = ApiKey;
        _settings.AnthropicBaseUrl = BaseUrl;
        _settings.DocumentFolder = DocumentFolder;
        _settings.AvailableModels = ModelsText;
        _settings.SelectedModel = SelectedModel;
        _settings.AskClarification = AskClarification;
        SettingsStorage.Save(_settings);
    }

    public void Clear()
    {
        Prompt = string.Empty;
        Response = string.Empty;
        CostText = "Estimated cost: $0.00";
    }

    private AnthropicClient CreateClient()
    {
        var httpClient = new HttpClient();
        if (!string.IsNullOrWhiteSpace(BaseUrl))
        {
            httpClient.BaseAddress = new Uri(BaseUrl);
        }

        return new AnthropicClient(apiKeys: new APIAuthentication(ApiKey));
    }

    private string BuildPrompt(string userPrompt, string folderContext)
    {
        var builder = new StringBuilder();
        builder.AppendLine("You are a helpful assistant. Answer the user's request clearly and concisely.");
        if (AskClarification)
        {
            builder.AppendLine("If the request is ambiguous, ask a clarifying question before making a decision.");
        }
        if (!string.IsNullOrWhiteSpace(folderContext))
        {
            builder.AppendLine(folderContext);
        }
        builder.AppendLine();
        builder.AppendLine($"User request: {userPrompt}");
        return builder.ToString();
    }

    private static List<string> ParseModels(string csv)
    {
        return csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
    }

    private static decimal EstimateCost(int inputTokens, int outputTokens)
    {
        const decimal inputRate = 0.000003m;
        const decimal outputRate = 0.000015m;
        return (inputTokens * inputRate) + (outputTokens * outputRate);
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
