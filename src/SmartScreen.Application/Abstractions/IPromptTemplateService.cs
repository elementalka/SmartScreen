using SmartScreen.Domain.Models;

namespace SmartScreen.Application.Abstractions;

public interface IPromptTemplateService
{
    Task<AiPromptLibrary> LoadAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(AiPromptLibrary library, CancellationToken cancellationToken = default);
    Task ResetToDefaultsAsync(CancellationToken cancellationToken = default);
}

