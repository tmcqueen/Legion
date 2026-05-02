using System.ComponentModel.DataAnnotations;

namespace Legion.Admin.Data.Models.Providers;

public record ModelOptions
{
    public ModelOptionsId Id { get; set; }
    public string? Name { get; set; }
    public string? DisplayName { get; set; }
    
    // General Info
    [Display(GroupName = "General Info",
             Name = "Context Window Size", 
             Description = "The maximum number of tokens that can be processed in a single request, including both input and output tokens.")]
    public int ContextWindowSize { get; set; }
    [Display(GroupName = "General Info", Name = "Max Output Tokens", Description = "The maximum number of tokens that can be generated in the output.")]
    public int MaxOutputTokens { get; set; }
    [Display(GroupName = "General Info", Name = "Knowledge Cutoff", Description = "The date after which the model has no knowledge.")]
    public string? KnowledgeCutoff { get; set; }
   
    // Modalities
    [Display(GroupName = "Modalities", Name = "Supports Text", Description = "Indicates whether the model can process and generate text.")]
    public bool SupportsText { get; set; }
    [Display(GroupName = "Modalities", Name = "Supports Image", Description = "Indicates whether the model can process and generate images.")]
    public bool SupportsImage { get; set; }
    [Display(GroupName = "Modalities", Name = "Supports Audio", Description = "Indicates whether the model can process and generate audio.")]
    public bool SupportsAudio { get; set; }
    [Display(GroupName = "Modalities", Name = "Supports Video", Description = "Indicates whether the model can process and generate video.")]
    public bool SupportsVideo { get; set; }

    // Features
    [Display(GroupName = "Features", Name = "Supports Streaming", Description = "Indicates whether the model supports streaming.")]
    public bool SupportsStreaming { get; set; }
    [Display(GroupName = "Features", Name = "Supports Function Calling", Description = "Indicates whether the model supports function calling.")]
    public bool SupportsFunctionCalling { get; set; }
    [Display(GroupName = "Features", Name = "Supports Structured Output", Description = "Indicates whether the model supports structured output.")]
    public bool SupportsStructuredOutput { get; set; }
    [Display(GroupName = "Features", Name = "Supports Fine Tuning", Description = "Indicates whether the model supports fine tuning.")]
    public bool SupportsFineTuning { get; set; }
    [Display(GroupName = "Features", Name = "Supports Distillation", Description = "Indicates whether the model supports distillation.")]
    public bool SupportsDistillation { get; set; }

    // Tools
    [Display(GroupName = "Tools", Name = "Supports Tool Use", Description = "Indicates whether the model can use tools such as APIs, databases, or external services.")]
    public bool SupportsToolUse { get; set; }
    [Display(GroupName = "Tools", Name = "Supports Web Search", Description = "Indicates whether the model can perform web searches.")]
    public bool SupportsWebSearch { get; set; }
    [Display(GroupName = "Tools", Name = "Supports File Search", Description = "Indicates whether the model can search files.")]
    public bool SupportsFileSearch { get; set; }
    [Display(GroupName = "Tools", Name = "Supports Code Execution", Description = "Indicates whether the model can execute code.")]
    public bool SupportsCodeExecution { get; set; }
    [Display(GroupName = "Tools", Name = "Supports MCP", Description = "Indicates whether the model supports MCP.")]
    public bool SupportsMCP { get; set; }

    // Pricing
    [DataType(DataType.Currency)]
    [DisplayFormat(DataFormatString = "{0:C}")]
    [Display(GroupName = "Pricing", Name = "Input Token Cost", Description = "The cost of input tokens.")]
    public double InputTokenCost { get; set; }
    [DataType(DataType.Currency)]
    [DisplayFormat(DataFormatString = "{0:C}")]
    [Display(GroupName = "Pricing", Name = "Cached Token Cost", Description = "The cost of cached tokens.")]
    public double CachedTokenCost { get; set; }
    [DataType(DataType.Currency)]
    [DisplayFormat(DataFormatString = "{0:C}")]
    [Display(GroupName = "Pricing", Name = "Output Token Cost", Description = "The cost of output tokens.")]
    public double OutputTokenCost { get; set; }

    public List<ProviderOptions> Providers { get; set; } = [];
    public List<Agents.AgentOptions> Agents { get; set; } = [];
}
