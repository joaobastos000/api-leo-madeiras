# Respostas — Partes 1, 3 e 4

## Parte 1 — Questões conceituais

### 1. Síncrono x assíncrono

Numa chamada HTTP **síncrona**, quem chama espera a resposta para continuar. Exemplo: `GET /api/pedidos/1007` devolve o pedido na hora; se o servidor estiver lento ou fora do ar, quem chamou fica travado ou recebe erro.

Numa integração **assíncrona** (fila, webhook), quem envia entrega a mensagem e segue o fluxo sem bloquear threads; o processamento acontece depois e o resultado volta por outro caminho. Exemplo: o CorteCloud leva tempo para gerar PDFs, etiquetas e arquivos de máquina e, quando termina, avisa pela Digibee.

Neste projeto quase tudo é assíncrono porque:

- os processos são demorados ou dependem de pessoas (o cliente pagar, a máquina gerar os arquivos);
- os sistemas são de fornecedores diferentes e caem de forma independente: se o SVL estiver fora, a mensagem espera e é reentregue, em vez de a venda falhar;
- absorve picos de carga.

O custo é exatamente o que o teste explora: mensagens duplicadas, fora de ordem e falhas no meio do caminho.

### 2. Mensagem duplicada (pagamento do pedido 1007)

Se a API processar as duas chamadas, pode:

- mandar o serviço para produção duas vezes (cortar chapas em dobro);
- avisar o SVL duas vezes;
- criar registros de controle duplicados;
- contar o pagamento duas vezes.

Um endpoint é **idempotente** quando chamá-lo várias vezes com os mesmos dados tem o mesmo efeito de chamá-lo uma vez.

Para garantir isso aqui:

- **Chave de idempotência:** o próprio `pedidoSVL`, ou um id do pagamento enviado pelo ERP.
- **Checagem antes de agir:** se o pedido já está `PAGO`, responde 200 e não faz nada de novo.
- **Restrição `UNIQUE` no banco:** cobre o caso das duas mensagens chegarem ao mesmo tempo e passarem juntas pela checagem.

É o que a Parte 2 faz com os links: índice único `(PedidoId, Url)`, e só links novos geram efeitos.

### 3. Falha no meio do caminho (pedido salvo, SVL deu timeout)

Eu usaria a ideia de **outbox**:

1. Na **mesma transação** que salva o pedido, grava também um registro "avisar o SVL" com status `PENDENTE` (ou `INICIAL`). Ou os dois são salvos, ou nenhum é: nunca existe pedido salvo sem o aviso registrado.
2. Depois do commit, chama o SVL. Se der certo → `CONCLUIDO`. Se der erro ou timeout → continua `PENDENTE` e conta a tentativa.
3. Um worker periódico tenta de novo os `PENDENTE`, com limite de tentativas. Ao estourar o limite → `FALHA` e alerta.

Como um timeout não garante que o SVL não recebeu, o SVL precisa tratar o aviso de forma idempotente.

Na Parte 2 é o mesmo desenho: `NotificacaoLeomob` + `ReprocessamentoBackgroundService`.

### 4. Códigos HTTP

| Situação | Status | Por quê |
|---|---|---|
| Pedido criado com sucesso | **201 Created** | Recurso novo; a resposta leva o header `Location` (ex.: `/api/pedidos/1007`) |
| Corpo sem o campo obrigatório `serviceId` | **400 Bad Request** | Erro de quem chamou: a requisição está malformada e não adianta repetir sem corrigir. A resposta aponta o campo |
| Token de autenticação ausente | **401 Unauthorized** | Não autenticado. O 403 seria para quem está autenticado mas não tem permissão |
| Pedido informado não existe | **404 Not Found** | O recurso pedido não existe |
| Erro inesperado no banco de dados | **500 Internal Server Error** | A falha é do servidor, não de quem chamou. Responde sem detalhes internos, com um `traceId` para achar o erro no log. Se fosse indisponibilidade conhecida e temporária, 503 indicaria "tente de novo" |

### 5. Intermediário (Digibee)

**Vantagem:** desacoplamento. O CorteCloud não precisa conhecer a API Foundation nem o SVL, e vice-versa. A Digibee concentra a tradução de formatos, os reenvios e o monitoramento das integrações num lugar só. Trocar o LeoPlan pelo CorteCloud, por exemplo, mexe principalmente nos fluxos da Digibee, não em todos os sistemas.

**Desvantagem:** é mais um sistema no caminho. Isso significa:

- mais um ponto de falha e mais latência;
- investigar um problema exige rastrear a mensagem por mais uma ponta (daí a importância de logs com `pedidoSVL` e de um id de correlação);
- mais custo e dependência de outro fornecedor.

---

## Parte 3 — Leitura de código

### 1. Problemas encontrados

| # | Problema | Impacto em produção | Severidade |
|---|---|---|---|
| 1 | **SQL injection.** `notif.PedidoSVL` vem do corpo da requisição e é concatenado na query | Um valor como `1' OR '1'='1` muda a consulta; um valor malicioso pode ler ou apagar dados | **Alto** |
| 2 | **A Digibee só é chamada quando o pedido é criado.** Se o pedido já existe (o caso comum, já que ele nasce no orçamento), o serviço nunca vai para produção | O cliente paga e a produção nunca começa | **Alto** |
| 3 | **`EnviaServicoParaProducaoAsync` sem `await`** (fire-and-forget) | Se falhar, a exceção some e o método termina como se tudo tivesse dado certo; a chamada pode ainda rodar depois que a conexão/escopo foi descartado | **Alto** |
| 4 | **`Status = "CONCLUIDO"` é atribuído antes de chamar o SVL**, e o `catch` não muda nada | Se o SVL falhar, o controle é gravado como `CONCLUIDO` mesmo assim. Nunca fica `PENDENTE`, o worker nunca tenta de novo e o SVL nunca é avisado, sem ninguém saber | **Alto** |
| 5 | **`catch` vazio, sem log** | A falha fica invisível; não há como diagnosticar depois | **Médio** |
| 6 | **Pedido novo é criado sem `ServiceId`** (e sem `TipoPedido` e `LojaId`, que são `NOT NULL` na tabela) | A Digibee recebe um `ServiceId` vazio (produção errada ou rejeitada), ou o próprio insert falha | **Médio** |
| 7 | **Sem transação** entre atualizar o pedido e criar o controle | Se o processo cair entre as duas gravações, fica um pedido `PAGO` sem controle, e o SVL nunca é avisado | **Médio** |
| 8 | **Sem idempotência** | Uma notificação repetida grava `PAGO` de novo, cria um segundo controle e avisa o SVL de novo | **Médio** |
| 9 | **Condição de corrida na criação.** Duas notificações simultâneas de um pedido inexistente passam juntas pelo `SELECT` | Dois pedidos com o mesmo `PedidoSVL` (a tabela não tem `UNIQUE`; ver Parte 4) | **Médio** |
| 10 | **`PedidoSVL` é string**, mas a coluna é `INT`, sem validação | Um valor não numérico vira erro de conversão e um 500 genérico, em vez de um 400 claro | **Baixo** |
| 11 | **`SELECT *`**, sem `CancellationToken` e sem logs | Traz colunas desnecessárias e quebra se a tabela mudar; não dá para cancelar nem rastrear o processamento | **Baixo** |

### 2. Método corrigido

Premissas:
- `NotificacaoPagamento` traz `ServiceId`, `TipoPedido` e `LojaId`, necessários para criar o pedido quando ele não existe.
- `_db` (estilo Dapper) aceita parâmetros e transação.
- O controle usa `PedidoId`, como na tabela da Parte 4.

```csharp
public async Task ProcessarPagamentoAsync(NotificacaoPagamento notif, CancellationToken ct = default)
{
    // Valida a entrada antes de usar: PedidoSVL chega como texto no corpo da requisição.
    if (!int.TryParse(notif.PedidoSVL, out var pedidoSVL))
        throw new ArgumentException($"PedidoSVL inválido: '{notif.PedidoSVL}'.", nameof(notif));

    using var _ = _logger.BeginScope("Pedido {PedidoSVL}", pedidoSVL);

    // Consulta parametrizada: o valor nunca vira parte do SQL.
    var pedido = await _db.QueryFirstOrDefaultAsync<Pedido>(
        "SELECT Id, PedidoSVL, ServiceId, TipoPedido, LojaId, Status FROM Pedido WHERE PedidoSVL = @pedidoSVL",
        new { pedidoSVL });

    // Idempotência: o pedido só fica PAGO depois que a produção foi acionada; se já está PAGO, é reenvio.
    if (pedido?.Status == "PAGO")
    {
        _logger.LogInformation("Pagamento já processado; notificação repetida ignorada");
        return;
    }

    pedido ??= new Pedido
    {
        PedidoSVL = pedidoSVL,
        ServiceId = notif.ServiceId,
        TipoPedido = notif.TipoPedido,
        LojaId = notif.LojaId,
        DataCriacao = DateTime.UtcNow
    };

    if (pedido.ServiceId is null)
        throw new InvalidOperationException($"Pedido {pedidoSVL} sem ServiceId; não é possível enviar para produção.");

    // Produção via Digibee SEMPRE, e com await. Se falhar, a exceção sobe, nada é gravado
    // e o reenvio da notificação tenta de novo.
    await _digibee.EnviaServicoParaProducaoAsync(pedido.ServiceId.Value, pedido.PedidoSVL);

    // Pedido PAGO + controle INICIAL na mesma transação: nunca existe pedido pago sem controle.
    var controle = new ControleNotificacao { Status = "INICIAL", Tentativas = 0, DataCriacao = DateTime.UtcNow };
    using (var transacao = _db.BeginTransaction())
    {
        pedido.Status = "PAGO";
        if (pedido.Id == 0)
            pedido.Id = await _db.InsertAsync(pedido, transacao);
        else
            await _db.UpdateAsync(pedido, transacao);

        controle.PedidoId = pedido.Id;
        controle.Id = await _db.InsertAsync(controle, transacao);
        transacao.Commit();
    }

    // Avisa o SVL e só então define o status, conforme o resultado.
    try
    {
        await _svl.NotificarPagamentoAsync(pedido.PedidoSVL);
        controle.Status = "CONCLUIDO";
    }
    catch (Exception ex)
    {
        controle.Status = "PENDENTE"; // o worker tenta de novo
        controle.Tentativas++;
        _logger.LogWarning(ex, "Falha ao avisar o SVL; controle ficou PENDENTE");
    }

    await _db.UpdateAsync(controle);
}
```

O que mudou, ligando aos problemas:
- **1 e 10:** parse e consulta parametrizada.
- **2 e 3:** a Digibee é chamada sempre, com `await`.
- **4 e 5:** o status é definido depois da chamada, e a falha é logada.
- **6:** o pedido novo nasce com os dados obrigatórios.
- **7:** pedido e controle são gravados na mesma transação.
- **8:** a checagem de `PAGO` no início.

A ordem "Digibee antes de gravar `PAGO`" é proposital. Se a Digibee falhar, nada fica gravado e o reenvio tenta de novo. Se a Digibee funcionar mas a gravação falhar, o reenvio chama a Digibee de novo; por isso a premissa é que ela/CorteCloud trate a produção de forma idempotente pelo `ServiceId`.

### 3. E se o ERP enviar a mesma notificação duas vezes?

**No código original:**
- O segundo envio encontra o pedido, grava `PAGO` de novo, cria **outro** `ControleNotificacao` e avisa o SVL **de novo**.
- A Digibee não é chamada em nenhum dos dois envios se o pedido já existia (problema 2).

**No código corrigido, em sequência:** o primeiro envio termina com o pedido `PAGO`. O segundo vê `PAGO` logo no início e sai sem fazer nada. **Resolve.**

**Ao mesmo tempo:** os dois envios podem passar juntos pela checagem antes de algum gravar `PAGO`.
- **Pedido novo:** o `UNIQUE (PedidoSVL)` proposto na Parte 4 faz o segundo insert falhar. Esse envio dá erro, é reenviado e cai na checagem de `PAGO`.
- **Pedido que já existia:** ainda poderiam surgir dois controles e dois avisos.

Para fechar esse caso eu usaria uma das opções:
- `SELECT ... FOR UPDATE` no pedido, dentro da transação, para o segundo envio esperar o primeiro;
- ou gravar o id da notificação do ERP numa coluna com `UNIQUE`, para o segundo insert falhar.

---

## Parte 4 — SQL e modelagem

As consultas estão em PostgreSQL (o banco usado na Parte 2), com observações para SQL Server quando muda algo.

### 1. Quantidade de pedidos por TipoPedido criados em setembro de 2026

```sql
SELECT TipoPedido, COUNT(*) AS Quantidade
FROM Pedido
WHERE DataCriacao >= '2026-09-01'
  AND DataCriacao <  '2026-10-01'
GROUP BY TipoPedido
ORDER BY TipoPedido;
```

O intervalo semiaberto (`>=` início, `<` próximo mês):
- pega até o último instante de 30/09, inclusive 23:59:59.999;
- permite usar um índice em `DataCriacao`, ao contrário de `EXTRACT(MONTH FROM DataCriacao) = 9`.

### 2. Pedidos "travados": PAGO há mais de 2 horas sem notificação CONCLUIDO

```sql
SELECT p.Id, p.PedidoSVL, p.LojaId, p.DataCriacao
FROM Pedido p
WHERE p.Status = 'PAGO'
  AND p.DataCriacao < NOW() - INTERVAL '2 hours'
  AND NOT EXISTS (
      SELECT 1
      FROM ControleNotificacao c
      WHERE c.PedidoId = p.Id
        AND c.Status = 'CONCLUIDO'
  );
```

- `NOT EXISTS` traz tanto os pedidos sem nenhum controle quanto os que só têm controles `INICIAL`, `PENDENTE` ou `FALHA`.
- **Premissa:** a tabela não tem a data do pagamento, então usei `DataCriacao` como referência. O ideal é ter uma coluna `DataPagamento` (ou `DataAtualizacao`), porque um pedido criado há dias e pago há 5 minutos apareceria como travado.
- Em SQL Server: `DATEADD(HOUR, -2, SYSUTCDATETIME())` no lugar de `NOW() - INTERVAL '2 hours'`.

### 3. As 5 lojas com mais notificações em FALHA

```sql
SELECT p.LojaId, COUNT(*) AS NotificacoesEmFalha
FROM ControleNotificacao c
JOIN Pedido p ON p.Id = c.PedidoId
WHERE c.Status = 'FALHA'
GROUP BY p.LojaId
ORDER BY NotificacoesEmFalha DESC
LIMIT 5;
```

- Em SQL Server: `SELECT TOP 5 ...` no lugar de `LIMIT 5`.
- Se houver empate na 5ª posição, `LIMIT` corta um deles arbitrariamente. Para trazer os empatados: `FETCH FIRST 5 ROWS WITH TIES` (PostgreSQL 13+) ou `TOP 5 WITH TIES` (SQL Server).

### 4. Dois pedidos com o mesmo PedidoSVL

**É um problema.** O `PedidoSVL` é a chave que as integrações usam para achar o pedido (SVL, Digibee, ERP):
- com dois registros, uma busca como `QueryFirstOrDefault` pega um deles arbitrariamente;
- links e notificações ficam espalhados entre os dois;
- o pedido pode ir para produção duas vezes.

Isso acontece justamente com mensagens duplicadas ou simultâneas (Parte 3, problemas 8 e 9).

A alteração:

```sql
ALTER TABLE Pedido ADD CONSTRAINT UQ_Pedido_PedidoSVL UNIQUE (PedidoSVL);
```

- Antes de aplicar, é preciso encontrar e resolver os duplicados existentes (`GROUP BY PedidoSVL HAVING COUNT(*) > 1`), senão o `ALTER` falha.
- A restrição garante a regra no banco, que é o único lugar que resolve a condição de corrida: duas aplicações checando "já existe?" ao mesmo tempo não bastam.
- De brinde, cria um índice que acelera as buscas por `PedidoSVL`.
- Se o `PedidoSVL` só for único por loja, a restrição seria `UNIQUE (LojaId, PedidoSVL)`. Isso deve ser confirmado com o negócio.

A Parte 2 aplica isso: `IX_Pedidos_PedidoSVL` é único.

### 5. Índices para a consulta 2 (a cada 5 minutos, milhões de linhas)

```sql
-- Só os pedidos PAGO entram no índice: ele fica pequeno, porque a maioria dos pedidos já avançou de status.
CREATE INDEX IX_Pedido_Pago_DataCriacao ON Pedido (DataCriacao) WHERE Status = 'PAGO';

-- Atende o NOT EXISTS: busca os controles de um pedido por status sem varrer a tabela.
CREATE INDEX IX_ControleNotificacao_PedidoId_Status ON ControleNotificacao (PedidoId, Status);
```

- **Primeiro índice (parcial):** em SQL Server, o equivalente é um índice filtrado (`... WHERE Status = 'PAGO'`). Sem esse recurso, a alternativa é o composto `(Status, DataCriacao)`, com o status primeiro porque é a condição de igualdade.
- **Segundo índice:** nem o PostgreSQL nem o SQL Server criam índice automaticamente para chave estrangeira. Sem ele, cada pedido verificado faria uma varredura em `ControleNotificacao`.
