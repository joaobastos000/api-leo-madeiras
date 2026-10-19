namespace ApiFoundation.Integrations;

public enum ModoFalhaLeomob
{
    Nunca,
    Sempre,

    /// <summary>A 1ª chamada falha, a 2ª passa, a 3ª falha, e assim por diante.</summary>
    Alternado
}

/// <summary>Configuração da seção "Leomob" do appsettings.</summary>
public class LeomobOptions
{
    public ModoFalhaLeomob ModoFalha { get; set; } = ModoFalhaLeomob.Alternado;
}
