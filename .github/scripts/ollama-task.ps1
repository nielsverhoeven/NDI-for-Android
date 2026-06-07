<#
.SYNOPSIS
    Run a prompt against a locally hosted Ollama model.

.DESCRIPTION
    Routes tasks to the appropriate local model based on task type.
    Use this for small/routine tasks to avoid cloud AI credit consumption.

    Models:
      qwen2.5-coder:7b  — code generation, XAML boilerplate, test stubs, refactoring hints
      phi3:mini         — log parsing, status messages, issue update text, simple classification

.PARAMETER Task
    The task type. One of: code, classify, message, summarize, test-stub

.PARAMETER Prompt
    The prompt to send to the model.

.PARAMETER Model
    (Optional) Override the auto-selected model.

.PARAMETER MaxTokens
    (Optional) Max tokens to generate. Default: 512 for classify/message, 1024 for code.

.EXAMPLE
    # Generate a XAML boilerplate
    .\ollama-task.ps1 -Task code -Prompt "Generate a MAUI ContentPage XAML for a settings section list"

.EXAMPLE
    # Classify a build log
    .\ollama-task.ps1 -Task classify -Prompt "BUILD FAILED\nError CS0246: type not found 'ISettingsRepository'"

.EXAMPLE
    # Write an issue status update
    .\ollama-task.ps1 -Task message -Prompt "Write a GitHub issue comment: T003 implemented, general section and apply command complete"
#>

param(
    [Parameter(Mandatory)]
    [ValidateSet("code", "classify", "message", "summarize", "test-stub")]
    [string]$Task,

    [Parameter(Mandatory)]
    [string]$Prompt,

    [string]$Model,

    [int]$MaxTokens = 0
)

$OllamaExe = "$env:LOCALAPPDATA\Programs\Ollama\ollama.exe"

if (-not (Test-Path $OllamaExe)) {
    Write-Error "Ollama not found at $OllamaExe. Is Ollama installed?"
    exit 1
}

# Route to appropriate model
if (-not $Model) {
    $Model = switch ($Task) {
        "code"       { "qwen2.5-coder:7b" }
        "test-stub"  { "qwen2.5-coder:7b" }
        "classify"   { "phi3:mini" }
        "message"    { "qwen2.5-coder:7b" }
        "summarize"  { "phi3:mini" }
        default      { "qwen2.5-coder:7b" }
    }
}

# Default max tokens
if ($MaxTokens -eq 0) {
    $MaxTokens = if ($Task -in @("code", "test-stub")) { 1024 } else { 256 }
}

# System prompt per task type
$SystemPrompt = switch ($Task) {
    "code" {
        "You are a .NET MAUI C# expert. Output only code — no explanations unless asked. Follow the project patterns: CommunityToolkit.Mvvm, nullable C# 12, XAML for UI, repository interfaces in Core, implementations in MauiApp."
    }
    "test-stub" {
        "You are a .NET xUnit test author for a .NET MAUI app. Output only a C# test class stub. Use Moq for mocking. No explanations."
    }
    "classify" {
        "You classify build/test output. Reply with exactly one of: PASS, FAIL, WARNING followed by a single short reason on the same line. Nothing else."
    }
    "message" {
        "You write concise GitHub issue progress comments. Output only the comment text. Maximum 4 lines. No headings, no bullet lists, no markdown formatting beyond inline code. State what was done and what is next, nothing else."
    }
    "summarize" {
        "Summarize the following in 3-5 bullet points. Be concise and factual."
    }
}

# Build the request body
$Body = @{
    model  = $Model
    prompt = $Prompt
    system = $SystemPrompt
    stream = $false
    options = @{
        num_predict = $MaxTokens
        temperature = if ($Task -in @("classify", "message")) { 0.1 } else { 0.2 }
    }
} | ConvertTo-Json -Depth 5

try {
    $Response = Invoke-RestMethod -Uri "http://localhost:11434/api/generate" `
        -Method POST `
        -ContentType "application/json" `
        -Body $Body `
        -TimeoutSec 120

    Write-Output $Response.response
}
catch {
    Write-Error "Ollama request failed: $_`nIs the Ollama service running? Check with: Get-Process ollama"
    exit 1
}
