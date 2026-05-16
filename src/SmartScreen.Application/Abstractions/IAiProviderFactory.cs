using SmartScreen.Domain.Models;

namespace SmartScreen.Application.Abstractions;

public interface IAiProviderFactory
{
    IAiProvider Create(AiProviderSettings settings);
}

