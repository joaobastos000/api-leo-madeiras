# API Foundation — arquivos de produção

Teste técnico Leo Madeiras (Desenvolvedor(a) Júnior · Integrações).

Web API em .NET 8 que simula a **API Foundation** recebendo da Digibee os links dos arquivos de produção gerados pelo CorteCloud (Parte 2 do teste). As respostas das Partes 1, 3 e 4 estão em [RESPOSTAS.md](RESPOSTAS.md); o desenho completo está em [docs/spec.md](docs/spec.md).

## Como rodar

Pré-requisito: Docker.

```bash
docker compose up --build
```

Esse comando sobe o PostgreSQL e a API, cria as tabelas e o seed (pedidos 1007, 1008 e 1012). Quando o log mostrar `Now listening on: http://[::]:8080` (alguns segundos depois de o banco ficar pronto), é só abrir:

- Swagger: http://localhost:8080/swagger

Para parar: `docker compose down` (com `-v` apaga também os dados).

### Rodando a API fora do Docker (desenvolvimento)

Pré-requisito: .NET 8 SDK.

```bash
docker compose up -d db
dotnet run --project src/ApiFoundation
```

O banco do Compose fica exposto na porta **5434** do computador (e não na 5432 padrão), para não conflitar com um PostgreSQL que já esteja instalado na máquina.

## Como rodar os testes

Pré-requisitos: .NET 8 SDK e **Docker aberto** (os testes sobem um PostgreSQL descartável com Testcontainers).

```bash
dotnet test
```

São 28 testes. Os 5 cenários obrigatórios do enunciado:

| Cenário | Teste |
|---|---|
| Pedido ZMOB não aciona o armazenamento de arquivos | `ProcessadorArquivosProducaoTests.Zmob_nao_aciona_armazenamento` |
| Pedido ZLPL aciona o armazenamento uma vez por arquivo | `ProcessadorArquivosProducaoTests.Zlpl_aciona_armazenamento_uma_vez_por_arquivo` |
| Envio repetido não duplica links nem notificações | `ProcessadorArquivosProducaoTests.Envio_repetido_nao_duplica_links_copias_nem_notificacoes` |
| Notificação vai para FALHA após a terceira tentativa sem sucesso | `ReprocessadorNotificacoesTests.Notificacao_vai_para_falha_apos_terceira_tentativa` |
| Envio com um pedido inexistente processa os demais normalmente | `ProcessadorArquivosProducaoTests.Pedido_inexistente_nao_impede_os_demais` |

Os demais cobrem casos de borda (categorias ausentes, URL repetida, link novo em pedido ZMOB), os endpoints via HTTP (422, 400, 404, fluxo ZMOB completo), o tratamento de erros e o reprocessamento automático.

## Endpoints

| Método | Rota | O que faz |
|---|---|---|
| POST | `/api/arquivos-producao` | Recebe os links enviados pela Digibee |
| POST | `/api/notificacoes/reprocessar` | Tenta de novo as notificações PENDENTE (simula o worker) |
| GET | `/api/pedidos/{pedidoSVL}` | Retorna o pedido, seus links e o status da notificação |

Exemplo com o corpo do enunciado:

```bash
curl -X POST http://localhost:8080/api/arquivos-producao \
  -H "Content-Type: application/json" \
  -d '{"documentType":"filesToProduction","hasErrors":false,"services":[{"serviceId":21763653,"pedidoSVL":1007,"links":{"corte":{"pdfSumario":["https://arquivos.exemplo.com/1007/sumario.pdf"],"pdfDetalhado":["https://arquivos.exemplo.com/1007/detalhado.pdf"]},"etiquetas":["https://arquivos.exemplo.com/1007/etiqueta-1.png"],"usinagem":["https://arquivos.exemplo.com/1007/usinagem.xml"]}}]}'
```

```json
{
  "processados": 1,
  "falhas": 0,
  "resultados": [
    { "pedidoSVL": 1007, "serviceId": 21763653, "sucesso": true, "linksNovos": 4, "erro": null }
  ]
}
```

```bash
curl http://localhost:8080/api/pedidos/1007
curl -X POST http://localhost:8080/api/notificacoes/reprocessar
```

Respostas de erro seguem o padrão ProblemDetails:

| Situação | Status |
|---|---|
| `hasErrors: true` | 422, nada é processado |
| Corpo inválido (ex.: serviço sem `serviceId`, `services` vazio) | 400 |
| Pedido não encontrado no GET | 404 |
| Duas requisições iguais ao mesmo tempo (violação de índice único) | 409, reenviar é seguro |
| Erro inesperado | 500, sem detalhes internos, com `traceId` |

## Configuração

No `appsettings.json` (ou por variável de ambiente, ex.: `Leomob__ModoFalha=Sempre`):

| Chave | Padrão | Efeito |
|---|---|---|
| `Leomob:ModoFalha` | `Alternado` | Simulação da LeomobAPI: `Nunca`, `Sempre` ou `Alternado` (1ª chamada falha, 2ª passa, …) |
| `Reprocessamento:Habilitado` | `true` | Liga o reprocessamento automático em segundo plano |
| `Reprocessamento:IntervaloSegundos` | `30` | Intervalo entre os ciclos do reprocessamento automático |

Com o padrão `Alternado`, o primeiro envio de um pedido ZMOB deixa a notificação em `PENDENTE`, e o reprocessamento seguinte a leva para `CONCLUIDO`.

## Estrutura

```
src/ApiFoundation/
├─ Controllers/     só HTTP: recebem a requisição e escolhem o status
├─ Services/        regras de negócio (processamento, reprocessamento, consulta)
├─ Domain/          entidades: Pedido, LinkArquivo, NotificacaoLeomob
├─ Contracts/       DTOs de entrada e saída
├─ Integrations/    IArmazenamentoArquivos e ILeomobApiClient + implementações falsas
├─ Data/            AppDbContext (índices, seed) e migrations
└─ Infrastructure/  tratamento global de erros e reprocessamento em segundo plano
tests/ApiFoundation.Tests/   xUnit + Testcontainers (PostgreSQL real)
```

## Decisões de design

- **Idempotência por link novo.** Para cada serviço, a API compara os links recebidos com os já gravados e só os novos geram efeitos (gravação, cópia, notificação). Um reenvio idêntico não faz nada. O índice único `(PedidoId, Url)` garante isso também no banco.
- **Notificação gravada antes da chamada externa.** Os links e a notificação `INICIAL` são gravados juntos antes de chamar a LeomobAPI. Se a chamada falhar, a notificação fica `PENDENTE` e o reprocessamento tenta de novo; a intenção de notificar nunca se perde.
- **Um serviço não derruba os outros.** Cada serviço do envio é processado e gravado separadamente. Pedido inexistente ou `serviceId` divergente viram erro só daquele serviço, listado na resposta.
- **Uma regra de tentativas, num lugar só.** `NotificacaoLeomob.RegistrarFalha` conta a falha e decide entre `PENDENTE` e `FALHA`; o processamento e o reprocessamento usam o mesmo método.
- **O mesmo reprocessador para o endpoint e o worker.** `ReprocessadorNotificacoes` é chamado pelo endpoint manual e pelo `BackgroundService`.
- **Interfaces só nas fronteiras externas.** `IArmazenamentoArquivos` e `ILeomobApiClient` têm implementação falsa hoje e teriam um cliente HTTP real amanhã. Os serviços de negócio usam o `DbContext` direto: o EF Core já implementa Repository e Unit of Work, e um repositório por cima teria uma única implementação.
- **Testes contra PostgreSQL de verdade.** O provider InMemory do EF ignora índices únicos, e a idempotência depende deles.
- **Logs estruturados.** Saem em JSON, e cada mensagem do processamento leva `PedidoSVL` e `ServiceId` no escopo, para rastrear um pedido.

## Premissas

1. `hasErrors: true` rejeita o envio inteiro com **422**: o corpo é válido, mas o conteúdo foi recusado por regra de negócio.
2. O POST responde **200** mesmo com falhas parciais; o detalhe de cada serviço vai em `resultados`.
3. Se o mesmo serviço chegar com algum link novo, só os novos são gravados e só eles geram efeitos. No ZMOB, isso cria uma nova notificação.
4. A chamada à LeomobAPI feita no recebimento conta como a **1ª tentativa**. Falha no recebimento (1) → falha no 1º reprocessamento (2) → falha no 2º reprocessamento (3) → `FALHA`.
5. A LeomobAPI recebe sempre todos os links atuais do pedido.
6. Um link é único por `(pedido, url)`: a mesma URL em duas categorias conta como um link só.
7. No ZLPL/ZCVS, a cópia acontece antes de gravar os links. Se a gravação falhar, o reenvio copia de novo, o que é inofensivo (mesmo arquivo, mesmo destino).
8. O status do pedido não muda ao receber os arquivos (o enunciado não pede).
9. Sem autenticação: o enunciado não pede.
10. Swagger fica habilitado em todos os ambientes para facilitar a avaliação; em produção ficaria só em Development.

## O que eu faria com mais tempo

- **Notificação presa em `INICIAL`.** Se a aplicação cair exatamente entre gravar `INICIAL` e gravar o resultado da chamada, a notificação fica em `INICIAL` para sempre, porque o reprocessamento só olha as `PENDENTE`. Eu faria o reprocessamento pegar também as `INICIAL` antigas (ex.: com mais de 5 minutos).
- **Trava no reprocessamento.** Se o worker e o endpoint manual rodarem no mesmo instante (ou houver várias instâncias da API), a mesma notificação pode ser enviada duas vezes. Usaria `SELECT ... FOR UPDATE SKIP LOCKED` para cada execução pegar notificações diferentes.
- **Backoff entre tentativas** (ex.: 1 min, 5 min, 15 min) e **alerta** quando uma notificação vai para `FALHA`.
- **Clientes HTTP reais** para o armazenamento e a LeomobAPI, com Polly (retry com backoff e circuit breaker) e timeout.
- **Autenticação** entre a Digibee e a API Foundation (token ou mTLS).
- **Outbox também para as cópias de arquivo**, para não depender do reenvio da Digibee quando uma cópia falha.
- **Health checks** (`/health`, com o banco) e métricas (notificações por status).
