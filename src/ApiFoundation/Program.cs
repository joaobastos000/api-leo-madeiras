using ApiFoundation.Data;
using ApiFoundation.Infrastructure;
using ApiFoundation.Integrations;
using ApiFoundation.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Logs em JSON com escopos: cada mensagem do processamento sai com PedidoSVL e ServiceId.
builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole(options => options.IncludeScopes = true);

// Banco (Scoped: um DbContext por requisição)
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

// Regras de negócio (Scoped, porque dependem do DbContext)
builder.Services.AddScoped<ProcessadorArquivosProducao>();
builder.Services.AddScoped<ReprocessadorNotificacoes>();
builder.Services.AddScoped<ConsultaPedidos>();

// Integrações externas simuladas (Singleton, porque guardam estado entre requisições)
builder.Services.Configure<LeomobOptions>(builder.Configuration.GetSection("Leomob"));
builder.Services.AddSingleton<IArmazenamentoArquivos, ArmazenamentoArquivosFake>();
builder.Services.AddSingleton<ILeomobApiClient, LeomobApiClientFake>();

// HTTP
builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseExceptionHandler();

// Swagger em todos os ambientes para facilitar a avaliação; em produção ficaria só em Development.
app.UseSwagger();
app.UseSwaggerUI();

app.MapControllers();

// Cria ou atualiza o banco (tabelas, índices e seed) ao subir.
using (var scope = app.Services.CreateScope())
{
    scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.Migrate();
}

app.Run();

// Torna a classe Program visível para o WebApplicationFactory dos testes.
public partial class Program
{
}
