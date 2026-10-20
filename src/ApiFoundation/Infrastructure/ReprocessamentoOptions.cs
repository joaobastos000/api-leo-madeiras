namespace ApiFoundation.Infrastructure;

/// <summary>Configuração da seção "Reprocessamento" do appsettings.</summary>
public class ReprocessamentoOptions
{
    public bool Habilitado { get; set; } = true;
    public int IntervaloSegundos { get; set; } = 30;
}
