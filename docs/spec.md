# Especificação — API Foundation: recebimento de arquivos de produção

> Teste técnico Leo Madeiras · Desenvolvedor(a) Júnior · Integrações
> Recebido em 19/10/2026 · prazo de entrega 21/10/2026

## 1. Objetivo

Entregar um repositório público (`joaobastos000/api-leo-madeiras`) com:

- **Parte 2 (45 pts):** Web API em .NET 8 que simula a API Foundation recebendo, via Digibee, os links dos arquivos de produção gerados pelo CorteCloud.
- **`RESPOSTAS.md`:** respostas das Partes 1 (conceitual), 3 (leitura de código) e 4 (SQL), escritas em conjunto e revisadas com as palavras do autor.
- **`README.md`:** como rodar com um único comando, como rodar os testes, premissas e melhorias futuras.
- **Todos os bônus:** Docker Compose (API + banco), Swagger, logs estruturados com `pedidoSVL`, reprocessamento em `BackgroundService`.
- **Histórico:** commits pequenos com mensagens úteis (`feat:`, `test:`, `docs:`, `chore:`).

Critério de sucesso: as 7 regras de negócio funcionam (incluindo ZMOB e idempotência), os testes obrigatórios passam, e cada linha pode ser explicada na entrevista.

## 2. Stack

| Item | Escolha |
|---|---|
| Runtime | .NET 8, ASP.NET Core Web API com Controllers |
| Banco | PostgreSQL 16 |
| ORM | EF Core 8 + Npgsql, com Migrations (ferramenta local `dotnet-ef`) |
| Testes | xUnit + Testcontainers.PostgreSql + Microsoft.AspNetCore.Mvc.Testing |
| Documentação | Swashbuckle (Swagger UI) |
| Logs | `ILogger` nativo, `AddJsonConsole` com `IncludeScopes = true` |
| Container | Dockerfile multi-stage + `docker-compose.yml` (api + postgres) |

## 3. Arquitetura

Um projeto de API em camadas por pasta, mais um projeto de testes.

```
LeoMadeiras.ApiFoundation.sln
├─ src/ApiFoundation/
│  ├─ Controllers/    ArquivosProducaoController, NotificacoesController, PedidosController
│  ├─ Contracts/      DTOs de entrada e saída
│  ├─ Domain/         Pedido, LinkArquivo, NotificacaoLeomob, TipoPedido, StatusNotificacao
│  ├─ Services/       ProcessadorArquivosProducao, ReprocessadorNotificacoes
│  ├─ Integrations/   IArmazenamentoArquivos, ILeomobApiClient + implementações Fake
│  ├─ Data/           AppDbContext (mapeamento, índices, seed), Migrations/
│  ├─ Infrastructure/ GlobalExceptionHandler, ReprocessamentoBackgroundService
│  └─ Program.cs      DI, Swagger, logs, aplicação das migrations
└─ tests/ApiFoundation.Tests/
```

Responsabilidades:

- **Controllers:** só HTTP — recebem o DTO, chamam um serviço, escolhem o status code.
- **Services:** todas as regras de negócio. Não conhecem HTTP.
- **Integrations:** fronteira com sistemas externos, sempre atrás de interface. As implementações reais não existem neste teste.
- **Data:** persistência. Os serviços usam o `AppDbContext` diretamente (sem repositório genérico: o DbContext já é a unidade de trabalho).

## 4. Modelo de dados

| Entidade | Campos | Restrições |
|---|---|---|
| `Pedido` | `Id`, `PedidoSVL` (int), `ServiceId` (long), `TipoPedido` (enum salvo como texto), `Status` (texto), `DataCriacao` | índice **único** em `PedidoSVL` |
| `LinkArquivo` | `Id`, `PedidoId` (FK), `Categoria` (texto), `Url` (texto), `DataCriacao` | índice **único** em `(PedidoId, Url)` |
| `NotificacaoLeomob` | `Id`, `PedidoId` (FK), `Status` (enum salvo como texto), `Tentativas` (int), `UltimoErro` (texto, opcional), `DataCriacao`, `DataAtualizacao` | índice em `Status` |

Enums:

- `TipoPedido`: `ZLPL`, `ZCVS`, `ZMOB`
- `StatusNotificacao`: `INICIAL`, `PENDENTE`, `CONCLUIDO`, `FALHA`

Seed (via `HasData`, entra na migration inicial):

| PedidoSVL | ServiceId | TipoPedido | Status |
|---|---|---|---|
| 1007 | 21763653 | ZLPL | EM_PRODUCAO |
| 1008 | 21763654 | ZCVS | EM_PRODUCAO |
| 1012 | 21763699 | ZMOB | EM_PRODUCAO |

### Categorias de link

O JSON aninhado vira uma lista plana de pares `(Categoria, Url)`:

| Caminho no JSON | Categoria |
|---|---|
| `links.corte.pdfSumario[]` | `corte.pdfSumario` |
| `links.corte.pdfDetalhado[]` | `corte.pdfDetalhado` |
| `links.etiquetas[]` | `etiquetas` |
| `links.usinagem[]` | `usinagem` |

URLs repetidas dentro do mesmo envio são deduplicadas antes de qualquer processamento.

## 5. Endpoints

### 5.1 `POST /api/arquivos-producao`

Corpo: exatamente o JSON do enunciado (`documentType`, `hasErrors`, `services[]`).

Validação (400 automático do `[ApiController]`, em `ProblemDetails`):

- `documentType` obrigatório e igual a `filesToProduction`;
- `services` com pelo menos um item;
- em cada serviço: `serviceId`, `pedidoSVL` e `links` obrigatórios.

Respostas:

| Situação | Status | Corpo |
|---|---|---|
| `hasErrors: true` | **422** | `ProblemDetails` com mensagem clara; nada é processado |
| Processado (mesmo com falhas parciais) | **200** | resumo abaixo |
| Corpo inválido | **400** | `ProblemDetails` de validação |
| Violação de índice único (requisições simultâneas) | **409** | `ProblemDetails`; reenviar é seguro |
| Erro inesperado | **500** | `ProblemDetails` sem stack trace, com `traceId` |

Resumo 200:

```json
{
  "processados": 1,
  "falhas": 1,
  "resultados": [
    { "pedidoSVL": 1007, "serviceId": 21763653, "sucesso": true,  "linksNovos": 4, "erro": null },
    { "pedidoSVL": 9999, "serviceId": 1,        "sucesso": false, "linksNovos": 0, "erro": "Pedido 9999 não encontrado." }
  ]
}
```

### 5.2 `POST /api/notificacoes/reprocessar`

Executa o reprocessamento uma vez e devolve **200**:

```json
{ "reprocessadas": 3, "concluidas": 1, "pendentes": 1, "falhas": 1 }
```

### 5.3 `GET /api/pedidos/{pedidoSVL}`

**404** se não existir. Senão **200**:

```json
{
  "pedidoSVL": 1012,
  "serviceId": 21763699,
  "tipoPedido": "ZMOB",
  "status": "EM_PRODUCAO",
  "links": {
    "corte.pdfSumario": ["https://..."],
    "etiquetas": ["https://..."]
  },
  "notificacao": { "status": "PENDENTE", "tentativas": 1, "dataAtualizacao": "2026-10-19T12:00:00Z" }
}
```

`notificacao` é a mais recente do pedido, ou `null` se não houver.

## 6. Fluxo do processamento (`ProcessadorArquivosProducao`)

Princípio da idempotência: **efeitos colaterais só acontecem para links novos.** Um reenvio idêntico encontra todos os links já salvos, então não grava, não copia e não notifica.

Para cada serviço do envio, em sequência e dentro de um escopo de log com `pedidoSVL` e `serviceId`:

1. Busca o pedido por `pedidoSVL`. Se não existe → resultado com erro, segue para o próximo serviço.
2. Se `serviceId` ≠ `pedido.ServiceId` → resultado com erro, segue para o próximo serviço.
3. Achata e deduplica os links; remove os que já existem para o pedido. Se não sobrou nenhum → sucesso com `linksNovos = 0`.
4. **ZLPL / ZCVS:** chama `IArmazenamentoArquivos.CopiarAsync` uma vez por link novo e depois grava os links (`SaveChanges`).
5. **ZMOB:** não copia. Grava os links novos e uma `NotificacaoLeomob` com status `INICIAL` no mesmo `SaveChanges` (atômico). Depois chama `ILeomobApiClient.NotificarAsync`:
   - sucesso → `CONCLUIDO`;
   - exceção → `PENDENTE`, `Tentativas = 1`, `UltimoErro` preenchido, exceção logada (não propaga).
   - grava o novo status (`SaveChanges`).

Cada serviço tem seu próprio `SaveChanges`, então o que já foi processado fica salvo mesmo que um serviço posterior falhe.

Erros de negócio (passos 1 e 2) viram resultado, nunca exceção. Erros inesperados (banco fora, falha no armazenamento) propagam para o `GlobalExceptionHandler`. Isso é seguro porque o reenvio inteiro do mesmo payload é idempotente: os serviços já salvos são ignorados e os restantes são processados.

## 7. Reprocessamento (`ReprocessadorNotificacoes`)

Uma única classe, chamada pelo endpoint manual e pelo `BackgroundService`:

1. Busca as notificações `PENDENTE` com seus pedidos.
2. Para cada uma, chama `ILeomobApiClient.NotificarAsync` com todos os links atuais do pedido.
   - sucesso → `CONCLUIDO`;
   - falha → `Tentativas++`; se `Tentativas >= 3` → `FALHA` (não é mais tentada), senão continua `PENDENTE`.
3. Grava e devolve a contagem por resultado.

Contagem de tentativas: a chamada inicial no `POST` é a tentativa 1. Portanto: falha no POST (1, PENDENTE) → falha no 1º reprocessamento (2, PENDENTE) → falha no 2º reprocessamento (3, **FALHA**).

`ReprocessamentoBackgroundService`: `PeriodicTimer` com intervalo `Reprocessamento:IntervaloSegundos` (padrão 30). Liga/desliga por `Reprocessamento:Habilitado` (desligado nos testes, para não competir com eles). Cria um escopo de DI por ciclo.

## 8. Integrações falsas

| Interface | Fake | Comportamento |
|---|---|---|
| `IArmazenamentoArquivos.CopiarAsync(pedidoSVL, categoria, url, ct)` | `ArmazenamentoArquivosFake` | Loga o que seria copiado e guarda numa lista em memória (singleton) |
| `ILeomobApiClient.NotificarAsync(pedidoSVL, serviceId, links, ct)` | `LeomobApiClientFake` | Configuração `Leomob:ModoFalha` = `Nunca`, `Sempre` ou `Alternado` (padrão `Alternado`); quando falha, lança `HttpRequestException` |

Os testes usam fakes próprios que contam chamadas, injetados no lugar dos de produção.

## 9. Injeção de dependência

Todas as dependências chegam pelo construtor. Os registros ficam no `Program.cs`, num bloco só:

| Serviço | Implementação | Tempo de vida | Por quê |
|---|---|---|---|
| `AppDbContext` | — (`AddDbContext` + `UseNpgsql`) | Scoped | Um contexto por requisição; o DbContext não é thread-safe |
| `ProcessadorArquivosProducao` | classe concreta | Scoped | Depende do `AppDbContext` (Scoped) |
| `ReprocessadorNotificacoes` | classe concreta | Scoped | Idem |
| `ConsultaPedidos` | classe concreta | Scoped | Idem (monta a resposta do GET) |
| `IArmazenamentoArquivos` | `ArmazenamentoArquivosFake` | Singleton | Guarda a lista de cópias durante toda a vida da aplicação |
| `ILeomobApiClient` | `LeomobApiClientFake` | Singleton | Mantém o contador do modo `Alternado` entre requisições (incremento com `Interlocked`) |
| `ReprocessamentoBackgroundService` | `AddHostedService` | Singleton | A cada ciclo cria um escopo com `IServiceScopeFactory` para obter o `ReprocessadorNotificacoes`, evitando prender um Scoped dentro de um Singleton |
| `GlobalExceptionHandler` | `AddExceptionHandler` | Singleton | Não guarda estado |
| `LeomobOptions`, `ReprocessamentoOptions` | `Configure<T>` (seções `Leomob` e `Reprocessamento`) | — | Lidas via `IOptions<T>`, em vez de ler `IConfiguration` direto |

Onde usar interface:

- **Sim**, nas fronteiras com sistemas externos (`IArmazenamentoArquivos`, `ILeomobApiClient`): é ali que a implementação muda (fake hoje, cliente HTTP amanhã, stub nos testes).
- **Não**, nos serviços de negócio: existe uma implementação só, e os testes usam a classe real com banco real. Interface sem segunda implementação seria cerimônia.

Nos testes:

- Testes de serviço: os objetos são montados à mão (`new ProcessadorArquivosProducao(db, armazenamentoFake, leomobFake, logger)`), o que deixa explícito o que cada teste controla.
- Testes de integração: `WebApplicationFactory` com `ConfigureTestServices`, trocando `ILeomobApiClient` e `IArmazenamentoArquivos` pelos fakes de teste e a connection string pela do container.

## 10. Tratamento de erros e logs

- `AddProblemDetails()` + `UseExceptionHandler()` + `GlobalExceptionHandler : IExceptionHandler`.
- O handler loga a exceção e responde:
  - `DbUpdateException` com `PostgresException.SqlState == "23505"` (violação de índice único) → **409**;
  - qualquer outra → **500** com `traceId`, sem detalhes internos.
- Logs em JSON no console, com escopos. Cada serviço processado e cada notificação reprocessada leva `PedidoSVL` (e `ServiceId` quando houver) no escopo.

## 11. Configuração e execução

- Connection string em `ConnectionStrings:Default`.
- Migrations aplicadas na subida (`Database.Migrate()`).
- Swagger habilitado em todos os ambientes (é um teste; em produção ficaria só em Development).
- **Comando único:** `docker compose up --build` → Postgres 16 (com healthcheck) + API em `http://localhost:8080/swagger`. A API só sobe depois que o banco está saudável (`depends_on: condition: service_healthy`).
- Desenvolvimento: `docker compose up db` + `dotnet run --project src/ApiFoundation`.
- Testes: `dotnet test` (exige Docker rodando, por causa do Testcontainers).

## 12. Testes

Um container Postgres por execução (fixture de coleção do xUnit); cada teste parte de um banco limpo com o seed.

Obrigatórios (nos serviços):

| Cenário | Verificação |
|---|---|
| ZMOB não aciona o armazenamento | fake de armazenamento com 0 chamadas |
| ZLPL aciona o armazenamento uma vez por arquivo | payload do enunciado (4 links) → exatamente 4 chamadas |
| Envio repetido não duplica | mesmo envio 2× → mesma quantidade de links, cópias e notificações |
| Notificação vai para FALHA após a 3ª tentativa | Leomob sempre falha; POST + 2 reprocessamentos → `FALHA`, `Tentativas = 3`; um 3º reprocessamento não chama a API |
| Pedido inexistente não derruba o envio | envio com 1007 e 9999 → 1007 com sucesso, 9999 com erro |

Extras (integração via `WebApplicationFactory`, ponta a ponta pelo HTTP):

- `hasErrors: true` → 422 e nada gravado;
- `serviceId` divergente → 200 com erro naquele serviço.

## 13. RESPOSTAS.md (Partes 1, 3 e 4)

Escritas lado a lado: rascunho conjunto, revisão final com as palavras do autor.

- **Parte 1:** até 10 linhas por questão, sempre com exemplo deste projeto. Os conceitos ligam às decisões do código: idempotência via índice único, notificação gravada antes da chamada externa, 422 e 500.
- **Parte 3:** lista de problemas com impacto e severidade, método reescrito e análise da notificação duplicada.
- **Parte 4:** consultas no dialeto PostgreSQL, com nota sobre diferenças em SQL Server (o DDL do enunciado usa `DATETIME`). Índice único em `PedidoSVL` e índices para a consulta dos pedidos "travados".

## 14. Premissas (vão para o README)

1. `hasErrors: true` rejeita o envio inteiro com 422.
2. Resposta 200 mesmo com falhas parciais; o detalhe vai em `resultados`.
3. Mesmo serviço reenviado com links novos: grava só os novos e dispara efeitos só para eles (no ZMOB, nova notificação).
4. A chamada inicial à LeomobAPI conta como tentativa 1; a 3ª falha leva a `FALHA`.
5. A LeomobAPI recebe sempre todos os links atuais do pedido.
6. Unicidade de link por `(pedido, url)`: a mesma URL em duas categorias conta como um link só.
7. No ZLPL/ZCVS a cópia acontece antes de gravar os links: se a gravação falhar, o reenvio copia de novo (copiar para o mesmo destino é inofensivo).
8. O status do `Pedido` não muda ao receber arquivos.
9. Sem autenticação: o enunciado não pede.

## 15. Fora do escopo (vai para "o que eu faria com mais tempo")

- Autenticação entre Digibee e API Foundation (token ou mTLS).
- Clientes HTTP reais com Polly (retry com backoff e circuit breaker).
- Trava no reprocessamento (`SELECT ... FOR UPDATE SKIP LOCKED`) para rodar em várias instâncias sem notificar duas vezes.
- Outbox também para as cópias de arquivo.
- Backoff exponencial entre tentativas e alerta quando uma notificação vai para `FALHA`.
- Health checks e métricas.
