# API Foundation — Plano de Implementação

> **Para quem executa:** SUB-SKILL OBRIGATÓRIA: superpowers:executing-plans (execução lado a lado nesta sessão). Os passos usam checkbox (`- [ ]`) para acompanhamento.

**Objetivo:** Web API .NET 8 que recebe da Digibee os links dos arquivos de produção, aplica as 7 regras do teste, e entrega README e RESPOSTAS.md num repositório público.

**Arquitetura:** Um projeto ASP.NET Core em camadas por pasta (Controllers → Services → Data/Integrations) + um projeto xUnit. PostgreSQL via EF Core com migrations; integrações externas atrás de interfaces com fakes. Idempotência por "efeito colateral só para link novo" com índice único `(PedidoId, Url)`.

**Stack:** .NET 8, ASP.NET Core Controllers, EF Core 8 + Npgsql, PostgreSQL 16, xUnit, Testcontainers.PostgreSql, Microsoft.AspNetCore.Mvc.Testing, Swashbuckle, Docker Compose.

**Spec:** `docs/spec.md`

## Restrições globais

- Target `net8.0`. Pacotes EF Core, Npgsql.EntityFrameworkCore.PostgreSQL, Microsoft.EntityFrameworkCore.Design e Microsoft.AspNetCore.Mvc.Testing na última versão `8.0.x`. Ferramenta local `dotnet-ef` `8.0.x`.
- Imagem do banco: `postgres:16-alpine` (compose e Testcontainers).
- Sem Moq, FluentAssertions ou Serilog. Fakes escritos à mão; asserts do xUnit; logs com `ILogger` nativo.
- Nomes de domínio em português, como no enunciado. Namespace raiz `ApiFoundation`.
- Datas sempre `DateTime.UtcNow` / `DateTimeKind.Utc` (o Npgsql rejeita `DateTime` não-UTC em `timestamptz`).
- Enums gravados como texto (`HasConversion<string>()`).
- `NotificacaoLeomob.MaxTentativas = 3`. `Tentativas` conta chamadas **que falharam**; a chamada inicial do POST é a primeira.
- Commits pequenos, mensagens em português com prefixo `feat:`, `test:`, `docs:`, `chore:`.
- Docker Desktop rodando a partir da Tarefa 2 (Testcontainers).
- Comandos rodam na raiz do repositório.

## Foco de revisão

Entradas que a spec implica, mas que os 5 testes obrigatórios não cobrem. Cada uma ganhou um teste na tarefa dona do código:

1. **Categorias ausentes ou nulas** (`corte` nulo, `etiquetas: null`) → processa as demais, sem `NullReferenceException`. Teste na Tarefa 4.
2. **Mesma URL repetida no mesmo envio** → um link e uma cópia. Teste na Tarefa 4.
3. **Reenvio ZMOB com um link novo após CONCLUIDO** → grava só o novo e cria uma nova notificação (premissa 3). Teste na Tarefa 4.
4. **Corpo inválido** (`services: []`, serviço sem `serviceId`) → 400, nada gravado. Teste na Tarefa 6.
5. **GET de pedido sem links nem notificação** → `links: {}` e `notificacao: null`, não 500. Teste na Tarefa 6.

---

### Tarefa 1: Solução, projetos e banco local

**Arquivos:**
- Criar: `LeoMadeiras.ApiFoundation.sln`, `src/ApiFoundation/ApiFoundation.csproj`, `tests/ApiFoundation.Tests/ApiFoundation.Tests.csproj`, `.gitignore`, `.config/dotnet-tools.json`, `docker-compose.yml` (só o serviço `db` por enquanto)

**Interfaces:**
- Produz: projeto web `ApiFoundation` e projeto de testes `ApiFoundation.Tests` referenciando-o.

- [ ] **Passo 1: Preparar branches**

```bash
git branch main
git switch -c feat/api-foundation main
```

- [ ] **Passo 2: Criar solução e projetos**

```bash
dotnet new sln -n LeoMadeiras.ApiFoundation
dotnet new webapi --use-controllers -f net8.0 -o src/ApiFoundation -n ApiFoundation
dotnet new xunit -f net8.0 -o tests/ApiFoundation.Tests -n ApiFoundation.Tests
dotnet sln add src/ApiFoundation tests/ApiFoundation.Tests
dotnet add tests/ApiFoundation.Tests reference src/ApiFoundation
dotnet new gitignore
dotnet new tool-manifest
dotnet tool install dotnet-ef --version "8.0.*"
```

Apagar o exemplo do template: `WeatherForecast.cs`, `Controllers/WeatherForecastController.cs`, `ApiFoundation.http`, `UnitTest1.cs`.

- [ ] **Passo 3: Pacotes**

API: `Microsoft.EntityFrameworkCore`, `Npgsql.EntityFrameworkCore.PostgreSQL`, `Microsoft.EntityFrameworkCore.Design` (8.0.x).
Testes: `Testcontainers.PostgreSql`, `Microsoft.AspNetCore.Mvc.Testing` (8.0.x).

- [ ] **Passo 4: `docker-compose.yml` com o serviço `db`**

`postgres:16-alpine`, `POSTGRES_USER=leo`, `POSTGRES_PASSWORD=leo`, `POSTGRES_DB=apifoundation`, porta `5432:5432`, healthcheck `pg_isready -U leo -d apifoundation` (intervalo 5s, 10 tentativas). Credenciais apenas de desenvolvimento.

- [ ] **Passo 5: Verificar**

Rodar: `dotnet build` → `Build succeeded`, 0 erros.
Rodar: `dotnet test` → nenhum teste encontrado, sem erro.

- [ ] **Passo 6: Commit**

```bash
git add -A
git commit -m "chore: cria solução, projetos e banco local no compose"
```

---

### Tarefa 2: Domínio, AppDbContext, migration e seed

**Arquivos:**
- Criar: `src/ApiFoundation/Domain/TipoPedido.cs`, `StatusNotificacao.cs`, `Pedido.cs`, `LinkArquivo.cs`, `NotificacaoLeomob.cs`
- Criar: `src/ApiFoundation/Data/AppDbContext.cs`, `src/ApiFoundation/Data/Migrations/*` (gerado)
- Modificar: `src/ApiFoundation/appsettings.json`
- Testes: `tests/ApiFoundation.Tests/Infra/PostgresFixture.cs`, `tests/ApiFoundation.Tests/BancoTests.cs`

**Interfaces:**
- Produz:
  - `enum TipoPedido { ZLPL, ZCVS, ZMOB }`
  - `enum StatusNotificacao { INICIAL, PENDENTE, CONCLUIDO, FALHA }`
  - `class Pedido { int Id; int PedidoSVL; long ServiceId; TipoPedido TipoPedido; string Status; DateTime DataCriacao; List<LinkArquivo> Links; List<NotificacaoLeomob> Notificacoes; }`
  - `class LinkArquivo { int Id; int PedidoId; string Categoria; string Url; DateTime DataCriacao; }`
  - `class NotificacaoLeomob { const int MaxTentativas = 3; int Id; int PedidoId; Pedido Pedido; StatusNotificacao Status; int Tentativas; string? UltimoErro; DateTime DataCriacao; DateTime DataAtualizacao; }`
  - `AppDbContext(DbContextOptions<AppDbContext>)` com `DbSet<Pedido> Pedidos`, `DbSet<LinkArquivo> Links`, `DbSet<NotificacaoLeomob> Notificacoes`
  - Teste: `PostgresFixture` (`IAsyncLifetime`, `[CollectionDefinition("postgres")]`) com `Task<AppDbContext> CriarBancoAsync()` (banco novo por chamada, migrations aplicadas) e `string ConnectionStringNova()`.

- [ ] **Passo 1: Escrever os testes que falham**

```csharp
[Collection("postgres")]
public class BancoTests(PostgresFixture pg)
{
    [Fact]
    public async Task Seed_cria_os_tres_pedidos_do_enunciado()
    {
        await using var db = await pg.CriarBancoAsync();
        var pedidos = await db.Pedidos.OrderBy(p => p.PedidoSVL)
            .Select(p => new { p.PedidoSVL, p.ServiceId, p.TipoPedido }).ToListAsync();
        Assert.Equal(new[]
            {
                new { PedidoSVL = 1007, ServiceId = 21763653L, TipoPedido = TipoPedido.ZLPL },
                new { PedidoSVL = 1008, ServiceId = 21763654L, TipoPedido = TipoPedido.ZCVS },
                new { PedidoSVL = 1012, ServiceId = 21763699L, TipoPedido = TipoPedido.ZMOB },
            }, pedidos);
    }

    [Fact]
    public async Task PedidoSVL_duplicado_e_rejeitado_pelo_banco()
    {
        await using var db = await pg.CriarBancoAsync();
        db.Pedidos.Add(new Pedido { PedidoSVL = 1007, ServiceId = 1, TipoPedido = TipoPedido.ZLPL,
                                    Status = "CRIADO", DataCriacao = DateTime.UtcNow });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }
}
```

- [ ] **Passo 2: Rodar e ver falhar**

Rodar: `dotnet test --filter "FullyQualifiedName~BancoTests"` → erro de compilação (tipos inexistentes).

- [ ] **Passo 3: Implementar domínio e `AppDbContext`**

`OnModelCreating`:
- índice único `Pedido.PedidoSVL`; índice único `LinkArquivo (PedidoId, Url)`; índice `NotificacaoLeomob.Status`;
- `TipoPedido` e `StatusNotificacao` com `HasConversion<string>()`; `Status`/`Categoria` com tamanho máximo (20/40);
- seed via `HasData`: Ids 1, 2, 3 com os valores da tabela da spec §4, `Status = "EM_PRODUCAO"`, `DataCriacao = new DateTime(2026, 10, 19, 0, 0, 0, DateTimeKind.Utc)`.

`appsettings.json`: `ConnectionStrings:Default = "Host=localhost;Port=5432;Database=apifoundation;Username=leo;Password=leo"`.

`PostgresFixture`: um `PostgreSqlContainer` (`postgres:16-alpine`) por execução; `CriarBancoAsync` troca o `Database` da connection string por `t_{Guid:N}` e chama `Database.MigrateAsync()`.

- [ ] **Passo 4: Gerar a migration**

Rodar: `dotnet ef migrations add Inicial --project src/ApiFoundation --output-dir Data/Migrations` → cria `Data/Migrations/*_Inicial.cs` com as 3 tabelas, os índices e o `InsertData` do seed.

(Se o `dotnet ef` pedir um provedor em design-time, adicionar `AddDbContext<AppDbContext>(o => o.UseNpgsql(...))` no `Program.cs` agora; a Tarefa 6 completa o resto.)

- [ ] **Passo 5: Rodar e ver passar**

Rodar: `dotnet test --filter "FullyQualifiedName~BancoTests"` → 2 aprovados.

- [ ] **Passo 6: Commit**

```bash
git add -A
git commit -m "feat: adiciona domínio, AppDbContext, migration inicial e seed"
```

---

### Tarefa 3: Integrações (interfaces, fakes de produção e de teste)

**Arquivos:**
- Criar: `src/ApiFoundation/Integrations/IArmazenamentoArquivos.cs`, `ArmazenamentoArquivosFake.cs`, `ILeomobApiClient.cs`, `LeomobApiClientFake.cs`, `LeomobOptions.cs`
- Criar (testes): `tests/ApiFoundation.Tests/Fakes/ArmazenamentoEspiao.cs`, `LeomobEspiao.cs`
- Teste: `tests/ApiFoundation.Tests/LeomobApiClientFakeTests.cs`

**Interfaces:**
- Produz:
  - `interface IArmazenamentoArquivos { Task CopiarAsync(int pedidoSVL, string categoria, string url, CancellationToken ct); }`
  - `interface ILeomobApiClient { Task NotificarAsync(int pedidoSVL, long serviceId, IReadOnlyList<string> links, CancellationToken ct); }`
  - `enum ModoFalhaLeomob { Nunca, Sempre, Alternado }`; `class LeomobOptions { ModoFalhaLeomob ModoFalha { get; set; } = ModoFalhaLeomob.Alternado; }` (seção `Leomob`)
  - `ArmazenamentoArquivosFake(ILogger<ArmazenamentoArquivosFake>)` com `IReadOnlyCollection<string> Copias` (`ConcurrentQueue`)
  - `LeomobApiClientFake(IOptions<LeomobOptions>, ILogger<LeomobApiClientFake>)`; quando falha, lança `HttpRequestException("LeomobAPI indisponível (simulado)")`. `Alternado`: contador com `Interlocked.Increment`; falha nas chamadas ímpares (1ª falha, 2ª passa…).
  - Testes: `ArmazenamentoEspiao : IArmazenamentoArquivos` com `List<string> Urls`; `LeomobEspiao : ILeomobApiClient` com `bool DeveFalhar` e `int Chamadas`.

- [ ] **Passo 1: Escrever os testes que falham**

```csharp
public class LeomobApiClientFakeTests
{
    static LeomobApiClientFake Criar(ModoFalhaLeomob modo) =>
        new(Options.Create(new LeomobOptions { ModoFalha = modo }), NullLogger<LeomobApiClientFake>.Instance);

    [Fact]
    public async Task Modo_alternado_falha_na_primeira_e_passa_na_segunda()
    {
        var leomob = Criar(ModoFalhaLeomob.Alternado);
        await Assert.ThrowsAsync<HttpRequestException>(() => leomob.NotificarAsync(1012, 21763699, [], default));
        await leomob.NotificarAsync(1012, 21763699, [], default);
    }

    [Fact]
    public async Task Modo_sempre_falha() =>
        await Assert.ThrowsAsync<HttpRequestException>(() =>
            Criar(ModoFalhaLeomob.Sempre).NotificarAsync(1012, 21763699, [], default));

    [Fact]
    public async Task Modo_nunca_passa() =>
        await Criar(ModoFalhaLeomob.Nunca).NotificarAsync(1012, 21763699, [], default);
}
```

- [ ] **Passo 2: Rodar e ver falhar** — `dotnet test --filter "FullyQualifiedName~LeomobApiClientFakeTests"` → erro de compilação.

- [ ] **Passo 3: Implementar interfaces, fakes e espiões** conforme o bloco Interfaces. Os fakes de produção logam o que fariam (`pedidoSVL`, categoria/URL).

- [ ] **Passo 4: Rodar e ver passar** → 3 aprovados.

- [ ] **Passo 5: Commit**

```bash
git add -A
git commit -m "feat: adiciona interfaces de integração e fakes de armazenamento e LeomobAPI"
```

---

### Tarefa 4: Processamento dos arquivos de produção

**Arquivos:**
- Criar: `src/ApiFoundation/Contracts/ArquivosProducaoRequest.cs` (request, `ServicoArquivosRequest`, `LinksArquivosRequest`, `CorteLinksRequest`, `LinkRecebido`)
- Criar: `src/ApiFoundation/Contracts/ResultadoProcessamento.cs` (`ResultadoProcessamento`, `ResultadoServico`)
- Criar: `src/ApiFoundation/Services/ProcessadorArquivosProducao.cs`
- Testes: `tests/ApiFoundation.Tests/Infra/Payloads.cs`, `tests/ApiFoundation.Tests/ProcessadorArquivosProducaoTests.cs`

**Interfaces:**
- Consome: `AppDbContext`, `IArmazenamentoArquivos`, `ILeomobApiClient`, `NotificacaoLeomob` (Tarefas 2–3).
- Produz:
  - Request (classes com `{ get; init; }`, propriedades em camelCase no JSON):
    - `ArquivosProducaoRequest { [Required, RegularExpression("^filesToProduction$")] string? DocumentType; bool HasErrors; [Required, MinLength(1)] List<ServicoArquivosRequest>? Services; }`
    - `ServicoArquivosRequest { [Required] long? ServiceId; [Required] int? PedidoSVL; [Required] LinksArquivosRequest? Links; }`
    - `LinksArquivosRequest { CorteLinksRequest? Corte; List<string>? Etiquetas; List<string>? Usinagem; IReadOnlyList<LinkRecebido> Achatar(); }`
    - `CorteLinksRequest { List<string>? PdfSumario; List<string>? PdfDetalhado; }`
    - `readonly record struct LinkRecebido(string Categoria, string Url)`
  - `Achatar()`: categorias `corte.pdfSumario`, `corte.pdfDetalhado`, `etiquetas`, `usinagem`; ignora listas nulas e URLs em branco; deduplica por `Url` (mantém a primeira).
  - `record ResultadoServico(int PedidoSVL, long ServiceId, bool Sucesso, int LinksNovos, string? Erro)`
  - `record ResultadoProcessamento(int Processados, int Falhas, IReadOnlyList<ResultadoServico> Resultados)`
  - `ProcessadorArquivosProducao(AppDbContext db, IArmazenamentoArquivos armazenamento, ILeomobApiClient leomob, ILogger<ProcessadorArquivosProducao> logger)`
    - `Task<ResultadoProcessamento> ProcessarAsync(IReadOnlyList<ServicoArquivosRequest> servicos, CancellationToken ct = default)`
  - Mensagens de erro exatas: `"Pedido {pedidoSVL} não encontrado."` e `"ServiceId {recebido} não corresponde ao pedido {pedidoSVL} (esperado {esperado})."`
  - Teste: `Payloads.Servico(int pedidoSVL, long serviceId)` → serviço com os 4 links do enunciado (`https://arquivos.exemplo.com/{pedidoSVL}/sumario.pdf`, `.../detalhado.pdf`, `.../etiqueta-1.png`, `.../usinagem.xml`).

- [ ] **Passo 1: Escrever os testes que falham**

Classe `[Collection("postgres")] ProcessadorArquivosProducaoTests(PostgresFixture pg)`; cada teste cria `db = await pg.CriarBancoAsync()`, `armazenamento = new ArmazenamentoEspiao()`, `leomob = new LeomobEspiao()` e `new ProcessadorArquivosProducao(db, armazenamento, leomob, NullLogger<...>.Instance)`.

| Teste | Entrada | Asserts |
|---|---|---|
| `Zmob_nao_aciona_armazenamento` | `[Servico(1012, 21763699)]` | `Empty(armazenamento.Urls)`; 4 links do pedido 1012 no banco |
| `Zlpl_aciona_armazenamento_uma_vez_por_arquivo` | `[Servico(1007, 21763653)]` | `armazenamento.Urls.Count == 4`; `Distinct().Count() == 4` |
| `Envio_repetido_nao_duplica_links_copias_nem_notificacoes` | `[Servico(1007,…), Servico(1012,…)]` processado 2× | links no banco == 8; `armazenamento.Urls.Count == 4`; notificações == 1; `leomob.Chamadas == 1`; na 2ª resposta todos com `LinksNovos == 0` e `Sucesso` |
| `Pedido_inexistente_nao_impede_os_demais` | `[Servico(9999, 1), Servico(1007, 21763653)]` | `Processados == 1`, `Falhas == 1`; `Resultados[0].Erro == "Pedido 9999 não encontrado."`; `Resultados[1].Sucesso`; 4 links do 1007 |
| `ServiceId_divergente_e_erro_daquele_servico` | `[Servico(1007, 1)]` | `!Sucesso`; erro contém `"esperado 21763653"`; 0 links; `Empty(armazenamento.Urls)` |
| `Zmob_com_leomob_ok_conclui_notificacao` | 1012, `DeveFalhar = false` | 1 notificação `CONCLUIDO`, `Tentativas == 0` |
| `Zmob_com_leomob_falhando_fica_pendente_com_uma_tentativa` | 1012, `DeveFalhar = true` | `PENDENTE`, `Tentativas == 1`, `UltimoErro` não nulo; `Resultados[0].Sucesso` (a falha da Leomob não é falha do envio) |
| `Categorias_ausentes_sao_ignoradas` | 1007 só com `Etiquetas = [url]`, `Corte = null`, `Usinagem = null` | 1 link `etiquetas`; 1 cópia |
| `Url_repetida_no_mesmo_envio_e_gravada_uma_vez` | 1007 com a mesma URL em `etiquetas` e `usinagem` | 1 link; 1 cópia |
| `Zmob_com_link_novo_gera_nova_notificacao` | 1012 com os 4 links; depois 1012 com 1 URL nova | 5 links; 2 notificações; `leomob.Chamadas == 2` |

- [ ] **Passo 2: Rodar e ver falhar** — `dotnet test --filter "FullyQualifiedName~ProcessadorArquivosProducaoTests"` → erro de compilação.

- [ ] **Passo 3: Implementar `ProcessarAsync`** seguindo a spec §6, passo a passo:
  - por serviço, `using logger.BeginScope(new Dictionary<string, object> { ["PedidoSVL"] = …, ["ServiceId"] = … })`;
  - busca o pedido com `Links` incluídos; erros de negócio viram `ResultadoServico` com `Sucesso = false`;
  - links novos = `Achatar()` menos as URLs já existentes do pedido;
  - ZLPL/ZCVS: `CopiarAsync` para cada link novo, depois `SaveChangesAsync`;
  - ZMOB: adiciona links + `NotificacaoLeomob { Status = INICIAL, Tentativas = 0 }`, `SaveChangesAsync`; chama `NotificarAsync` com todas as URLs do pedido; `catch (Exception ex)` → `PENDENTE`, `Tentativas++`, `UltimoErro = ex.Message`, `LogWarning`; sucesso → `CONCLUIDO`; atualiza `DataAtualizacao`; `SaveChangesAsync`;
  - exceções inesperadas (banco, armazenamento) **não** são capturadas.

- [ ] **Passo 4: Rodar e ver passar** → 10 aprovados.

- [ ] **Passo 5: Commits** (um para os contratos, um para o serviço com testes)

```bash
git add src/ApiFoundation/Contracts
git commit -m "feat: adiciona contratos de entrada e saída dos arquivos de produção"
git add -A
git commit -m "feat: processa arquivos de produção com idempotência por link"
```

---

### Tarefa 5: Reprocessamento das notificações

**Arquivos:**
- Criar: `src/ApiFoundation/Contracts/ResultadoReprocessamento.cs`, `src/ApiFoundation/Services/ReprocessadorNotificacoes.cs`
- Teste: `tests/ApiFoundation.Tests/ReprocessadorNotificacoesTests.cs`

**Interfaces:**
- Consome: `ProcessadorArquivosProducao.ProcessarAsync`, `Payloads.Servico`, `LeomobEspiao` (Tarefas 3–4).
- Produz:
  - `record ResultadoReprocessamento(int Reprocessadas, int Concluidas, int Pendentes, int Falhas)`
  - `ReprocessadorNotificacoes(AppDbContext db, ILeomobApiClient leomob, ILogger<ReprocessadorNotificacoes> logger)`
    - `Task<ResultadoReprocessamento> ReprocessarPendentesAsync(CancellationToken ct = default)`

- [ ] **Passo 1: Escrever os testes que falham**

| Teste | Cenário | Asserts |
|---|---|---|
| `Notificacao_vai_para_falha_apos_terceira_tentativa` | `DeveFalhar = true`; processa 1012; reprocessa 3× | após o POST: `PENDENTE`/1; 1º reprocessamento: `PENDENTE`/2; 2º: `FALHA`/3; 3º: `Reprocessadas == 0` e `leomob.Chamadas == 3` |
| `Reprocessamento_com_sucesso_conclui` | `DeveFalhar = true` no POST, depois `false`; reprocessa | `CONCLUIDO`; `Tentativas == 1`; `Concluidas == 1` |
| `Sem_pendentes_nao_chama_leomob` | banco recém-criado | resultado `(0, 0, 0, 0)`; `leomob.Chamadas == 0` |

- [ ] **Passo 2: Rodar e ver falhar** — `dotnet test --filter "FullyQualifiedName~ReprocessadorNotificacoesTests"`.

- [ ] **Passo 3: Implementar** segundo a spec §7: busca as `PENDENTE` com `Include(Pedido).ThenInclude(Links)`; escopo de log com `PedidoSVL`; falha → `Tentativas++`, `FALHA` se `Tentativas >= NotificacaoLeomob.MaxTentativas`, senão `PENDENTE`; um `SaveChangesAsync` por notificação.

- [ ] **Passo 4: Rodar e ver passar** → 3 aprovados.

- [ ] **Passo 5: Commit**

```bash
git add -A
git commit -m "feat: reprocessa notificações pendentes com limite de 3 tentativas"
```

---

### Tarefa 6: Endpoints, DI, tratamento de erros, Swagger e logs

**Arquivos:**
- Criar: `src/ApiFoundation/Controllers/ArquivosProducaoController.cs`, `NotificacoesController.cs`, `PedidosController.cs`
- Criar: `src/ApiFoundation/Contracts/PedidoResponse.cs`, `src/ApiFoundation/Services/ConsultaPedidos.cs`
- Criar: `src/ApiFoundation/Infrastructure/GlobalExceptionHandler.cs`
- Modificar: `src/ApiFoundation/Program.cs`, `src/ApiFoundation/appsettings.json`
- Testes: `tests/ApiFoundation.Tests/Infra/ApiFactory.cs`, `tests/ApiFoundation.Tests/ApiTests.cs`, `tests/ApiFoundation.Tests/GlobalExceptionHandlerTests.cs`

**Interfaces:**
- Consome: serviços das Tarefas 4–5, integrações da Tarefa 3.
- Produz:
  - `record PedidoResponse(int PedidoSVL, long ServiceId, string TipoPedido, string Status, Dictionary<string, List<string>> Links, NotificacaoResponse? Notificacao)`; `record NotificacaoResponse(string Status, int Tentativas, DateTime DataAtualizacao)`
  - `ConsultaPedidos(AppDbContext db)` — `Task<PedidoResponse?> BuscarAsync(int pedidoSVL, CancellationToken ct = default)`. *Adição à spec:* mantém o controller só com HTTP. Registrado como Scoped.
  - `POST api/arquivos-producao` → `hasErrors` → `Problem(statusCode: 422, title: "Envio rejeitado", detail: "O CorteCloud sinalizou erros na geração dos arquivos (hasErrors = true). Nenhum serviço foi processado.")`; senão `Ok(ResultadoProcessamento)`.
  - `POST api/notificacoes/reprocessar` → `Ok(ResultadoReprocessamento)`.
  - `GET api/pedidos/{pedidoSVL:int}` → `Ok(PedidoResponse)` ou `NotFound()` (ProblemDetails).
  - `GlobalExceptionHandler(ILogger<GlobalExceptionHandler>) : IExceptionHandler`: `DbUpdateException { InnerException: PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } }` → 409, título `"Conflito de concorrência"`, detalhe `"Requisição simultânea com os mesmos dados. Reenviar é seguro."`; demais → 500, título `"Erro inesperado"`, sem detalhe interno; ambos com `traceId` em `Extensions`, escritos com `Response.WriteAsJsonAsync`.
  - `public partial class Program;` no fim do `Program.cs`.
  - Teste: `ApiFactory(PostgresFixture) : WebApplicationFactory<Program>` com `UseSetting("ConnectionStrings:Default", pg.ConnectionStringNova())`, `UseSetting("Reprocessamento:Habilitado", "false")` e `ConfigureTestServices` trocando `IArmazenamentoArquivos`/`ILeomobApiClient` pelos espiões (expostos como propriedades).

- [ ] **Passo 1: Escrever os testes que falham**

`ApiTests` (`[Collection("postgres")]`, uma `ApiFactory` por teste):

| Teste | Requisição | Asserts |
|---|---|---|
| `HasErrors_true_retorna_422_e_nao_grava` | POST com `hasErrors: true` e serviço 1007 | 422; `GET /api/pedidos/1007` com `links` vazio |
| `ServiceId_divergente_retorna_200_com_erro_no_servico` | POST 1007 com `serviceId: 1` | 200; `resultados[0].sucesso == false` |
| `Services_vazio_retorna_400` | `services: []` | 400 |
| `Servico_sem_serviceId_retorna_400` | serviço sem `serviceId` | 400; GET 1007 sem links |
| `Get_pedido_inexistente_retorna_404` | `GET /api/pedidos/9999` | 404 |
| `Get_pedido_sem_links_retorna_links_vazio_e_notificacao_nula` | `GET /api/pedidos/1008` | 200; `links` = `{}`; `notificacao` = `null` |
| `Fluxo_zmob_ponta_a_ponta` | POST 1012 com espião falhando → GET → `DeveFalhar = false` → POST reprocessar → GET | GET 1: `PENDENTE`/1 e 4 links em 4 categorias; GET 2: `CONCLUIDO` |

`GlobalExceptionHandlerTests` (sem banco, `DefaultHttpContext` com `Response.Body = new MemoryStream()`):
- `Violacao_de_unicidade_vira_409` — `new DbUpdateException("x", new PostgresException("dup", "ERROR", "ERROR", PostgresErrorCodes.UniqueViolation))` → `StatusCode == 409`, retorno `true`.
- `Erro_generico_vira_500_sem_detalhes` — `new InvalidOperationException("segredo")` → 500; o corpo não contém `"segredo"`.

- [ ] **Passo 2: Rodar e ver falhar** — `dotnet test --filter "FullyQualifiedName~ApiTests|FullyQualifiedName~GlobalExceptionHandlerTests"`.

- [ ] **Passo 3: Implementar controllers, `ConsultaPedidos` e `GlobalExceptionHandler`.**

- [ ] **Passo 4: `Program.cs`** — registros exatamente como a tabela da spec §9 (menos o `BackgroundService`, que vem na Tarefa 7), mais:
  - `builder.Logging.ClearProviders().AddJsonConsole(o => o.IncludeScopes = true);`
  - `AddControllers()`, `AddProblemDetails()`, `AddExceptionHandler<GlobalExceptionHandler>()`, `AddEndpointsApiExplorer()`, `AddSwaggerGen()`;
  - `app.UseExceptionHandler()`, Swagger e Swagger UI em todos os ambientes, `MapControllers()`;
  - antes de `app.Run()`: em um escopo, `db.Database.Migrate()`;
  - remover `UseHttpsRedirection` do template (a API roda em HTTP dentro do container).

- [ ] **Passo 5: Rodar todos os testes** — `dotnet test` → todos aprovados (2 + 3 + 10 + 3 + 9).

- [ ] **Passo 6: Verificação manual**

```bash
docker compose up -d db
dotnet run --project src/ApiFoundation
```

Abrir `http://localhost:<porta>/swagger`; POST com o JSON do enunciado → 200 com `linksNovos: 4`; o console mostra JSON com `"PedidoSVL": 1007` no escopo.

- [ ] **Passo 7: Commits** (handler; controllers + Program)

```bash
git add src/ApiFoundation/Infrastructure/GlobalExceptionHandler.cs tests/ApiFoundation.Tests/GlobalExceptionHandlerTests.cs
git commit -m "feat: adiciona tratamento global de erros com ProblemDetails"
git add -A
git commit -m "feat: expõe endpoints, configura DI, Swagger e logs estruturados"
```

---

### Tarefa 7: Reprocessamento automático em segundo plano

**Arquivos:**
- Criar: `src/ApiFoundation/Infrastructure/ReprocessamentoOptions.cs`, `ReprocessamentoBackgroundService.cs`
- Modificar: `src/ApiFoundation/Program.cs`, `appsettings.json`
- Teste: `tests/ApiFoundation.Tests/ReprocessamentoBackgroundServiceTests.cs`

**Interfaces:**
- Consome: `ReprocessadorNotificacoes` (Tarefa 5), `ApiFactory` (Tarefa 6).
- Produz:
  - `class ReprocessamentoOptions { bool Habilitado { get; set; } = true; int IntervaloSegundos { get; set; } = 30; }` (seção `Reprocessamento`)
  - `ReprocessamentoBackgroundService(IServiceScopeFactory, IOptions<ReprocessamentoOptions>, ILogger<…>) : BackgroundService` — sai logo se `!Habilitado`; senão `PeriodicTimer`; a cada tick cria um escopo, resolve `ReprocessadorNotificacoes`, chama `ReprocessarPendentesAsync`; exceção no ciclo é logada e não derruba o serviço.

- [ ] **Passo 1: Teste que falha** — `Worker_reprocessa_pendentes_sozinho`: `ApiFactory` com `Reprocessamento:Habilitado = true` e `IntervaloSegundos = 1`; espião falhando no POST 1012, depois `DeveFalhar = false`; consultar `GET /api/pedidos/1012` a cada 250 ms por até 10 s → `CONCLUIDO`.

- [ ] **Passo 2: Rodar e ver falhar** (fica `PENDENTE` até o timeout).

- [ ] **Passo 3: Implementar e registrar** (`Configure<ReprocessamentoOptions>`, `AddHostedService`); `appsettings.json` com `Habilitado: true`, `IntervaloSegundos: 30`.

- [ ] **Passo 4: Rodar tudo** — `dotnet test` → todos aprovados.

- [ ] **Passo 5: Commit**

```bash
git add -A
git commit -m "feat: reprocessa notificações automaticamente em segundo plano"
```

---

### Tarefa 8: Dockerfile e Compose completo

**Arquivos:**
- Criar: `Dockerfile`, `.dockerignore` (`**/bin`, `**/obj`, `tests/`, `.git`)
- Modificar: `docker-compose.yml`

- [ ] **Passo 1: `Dockerfile` multi-stage** — build com `mcr.microsoft.com/dotnet/sdk:8.0` (`dotnet publish src/ApiFoundation -c Release -o /app`), runtime `mcr.microsoft.com/dotnet/aspnet:8.0`, `EXPOSE 8080`.

- [ ] **Passo 2: Serviço `api` no compose** — `build: .`, `ports: "8080:8080"`, `ConnectionStrings__Default=Host=db;Port=5432;Database=apifoundation;Username=leo;Password=leo`, `depends_on: db: condition: service_healthy`.

- [ ] **Passo 3: Verificar**

```bash
docker compose down -v
docker compose up --build
```

Esperado: a API sobe depois do banco ficar saudável; `http://localhost:8080/swagger` abre; `GET http://localhost:8080/api/pedidos/1007` → 200.

- [ ] **Passo 4: Commit**

```bash
git add -A
git commit -m "chore: sobe API e PostgreSQL com docker compose"
```

---

### Tarefa 9: README.md

**Arquivos:** Criar `README.md`.

Seções obrigatórias:
1. O que é (2 linhas) e link para `docs/spec.md`.
2. **Como rodar:** `docker compose up --build` → `http://localhost:8080/swagger`. Alternativa de desenvolvimento: `docker compose up -d db` + `dotnet run --project src/ApiFoundation`.
3. **Como testar:** `dotnet test` (exige Docker rodando) e a lista dos 5 cenários obrigatórios com o nome do teste de cada um.
4. Exemplo de `curl` para os 3 endpoints, com o JSON do enunciado.
5. Configurações: `Leomob:ModoFalha`, `Reprocessamento:Habilitado`, `Reprocessamento:IntervaloSegundos`.
6. Decisões de design (idempotência por link novo, notificação gravada antes da chamada externa, uma transação por serviço, 422/409/500).
7. **Premissas:** as 9 da spec §14, mais `ConsultaPedidos` como adição.
8. **O que eu faria com mais tempo:** a lista da spec §15.

- [ ] Verificar: seguir o README do zero (`docker compose down -v` e depois os comandos dele) e confirmar que funciona.
- [ ] Commit: `docs: adiciona README com execução, premissas e melhorias`

---

### Tarefa 10: RESPOSTAS.md (Partes 1, 3 e 4) — escrita lado a lado

**Arquivos:** Criar `RESPOSTAS.md`.

Rascunho conjunto, revisão final nas palavras do autor. O que cada resposta precisa conter:

**Parte 1** (até 10 linhas cada, exemplos do próprio projeto):
1. Síncrono × assíncrono: quem chama espera a resposta × entrega e segue; motivos neste projeto (o CorteCloud demora para gerar arquivos, o pagamento vem do ERP em outro momento, sistemas de fornecedores diferentes caem de forma independente).
2. Pagamento 1007 duplicado: produção enviada 2×, SVL notificado 2×, registros duplicados; idempotente = repetir dá o mesmo resultado; chave de idempotência + índice único + checagem "já processado?", como o índice `(PedidoId, Url)` deste projeto.
3. Timeout no SVL após salvar: gravar a intenção (`INICIAL`/`PENDENTE`) na mesma transação do pedido e ter um worker de retry (outbox), como `NotificacaoLeomob` + `ReprocessamentoBackgroundService`.
4. HTTP: 201 (com `Location`), 400, 401, 404, 500, cada um com o motivo.
5. Digibee: vantagem (desacopla, centraliza retry, transformação e monitoramento); desvantagem (mais um ponto de falha e de latência, depuração passa por mais um sistema, dependência de fornecedor).

**Parte 3** — tabela problema / impacto / severidade, cobrindo pelo menos:
- SQL concatenado → SQL injection (alto);
- envio à Digibee só no ramo de criação → pedido existente nunca vai para produção (alto);
- `EnviaServicoParaProducaoAsync` sem `await` → exceção perdida, pode rodar após o descarte do contexto (alto);
- `Status = "CONCLUIDO"` antes da chamada + `catch` vazio → falha vira CONCLUIDO, nunca PENDENTE, o worker nunca tenta (alto);
- `catch` vazio sem log → falha invisível (médio);
- pedido novo sem `ServiceId` enviado à Digibee (médio);
- sem transação entre pedido e controle → pedido PAGO sem controle, nunca notificado (médio);
- sem idempotência/unicidade → notificação duplicada cria segundo controle e reenvia (médio);
- `PedidoSVL` string × `INT` da tabela, sem validação (baixo);
- sem `CancellationToken` e sem logs (baixo).

Método reescrito: parametrizado, Digibee sempre com `await`, transação para pedido + controle, `INICIAL` → chamada → `CONCLUIDO` ou `PENDENTE` com `Tentativas++` e log, checagem de idempotência. Pergunta 3: o que acontece com a notificação duplicada antes × depois da correção (por exemplo, pular se o pedido já está `PAGO` com controle `CONCLUIDO`, ou usar uma chave de idempotência do ERP).

**Parte 4** — dialeto PostgreSQL, com nota para SQL Server (`TOP 5`, `DATEADD`):
1. `GROUP BY TipoPedido` com intervalo semiaberto `>= '2026-09-01' AND < '2026-10-01'`.
2. `Status = 'PAGO' AND DataCriacao < now() - interval '2 hours' AND NOT EXISTS (… ControleNotificacao CONCLUIDO)`. Premissa: não existe `DataPagamento`, então usa `DataCriacao`; registrar que o ideal é ter essa coluna.
3. `JOIN Pedido` + `WHERE Status = 'FALHA'` + `GROUP BY LojaId ORDER BY COUNT(*) DESC LIMIT 5` (comentar empates).
4. Sim, é problema (duplicidade, ambiguidade nas integrações); `UNIQUE (PedidoSVL)`, limpando duplicados antes.
5. Índice parcial `Pedido (DataCriacao) WHERE Status = 'PAGO'` (ou composto `(Status, DataCriacao)`), e `ControleNotificacao (PedidoId) WHERE Status = 'CONCLUIDO'` (ou `(PedidoId, Status)`).

- [ ] Commits separados por parte: `docs: responde parte 1`, `docs: responde parte 3`, `docs: responde parte 4`.

---

### Tarefa 11: Integração e publicação

- [ ] **Passo 1:** `dotnet test` → todos aprovados; `docker compose up --build` → Swagger responde.
- [ ] **Passo 2:** `git switch main && git merge --no-ff feat/api-foundation -m "Merge: API Foundation"`.
- [ ] **Passo 3 (pedir confirmação ao usuário antes):** `gh repo create joaobastos000/api-leo-madeiras --public --source . --push`, depois `git push origin feat/api-foundation docs/spec`.
- [ ] **Passo 4:** abrir o repositório público e conferir README, RESPOSTAS.md e histórico. O e-mail ao avaliador é com o usuário.
