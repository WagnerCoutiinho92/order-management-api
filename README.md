# Order Management API

API REST em .NET 10 para gerenciamento de clientes, produtos, estoque e pedidos.

---

## Sumário

1. [Como executar](#como-executar)
2. [Estrutura da solução](#estrutura-da-solução)
3. [Tecnologias utilizadas](#tecnologias-utilizadas)
4. [Decisões técnicas e trade-offs](#decisões-técnicas-e-trade-offs)
5. [Valores monetários e arredondamento](#valores-monetários-e-arredondamento)
6. [Datas, UTC e fuso horário](#datas-utc-e-fuso-horário)
7. [Estratégia de estoque e concorrência](#estratégia-de-estoque-e-concorrência)
8. [Paginação](#paginação)
9. [Validações](#validações)
10. [Testes automatizados](#testes-automatizados)
11. [Pontos abertos e decisões documentadas](#pontos-abertos-e-decisões-documentadas)
12. [Fora do escopo](#fora-do-escopo)

---

## Como executar

### Pré-requisitos

- .NET 10 SDK ([download](https://dotnet.microsoft.com/download))
- SQL Server (local ou Express)

### Configuração

1. Clone o repositório.
2. Ajuste a connection string em `src/OrderManagement.API/appsettings.json`:
   ```json
   "ConnectionStrings": {
     "DefaultConnection": "Server=localhost;Database=OrderManagementDb;Trusted_Connection=True;TrustServerCertificate=True;"
   }
   ```

### Migrations

```bash
# A partir da raiz da solução
dotnet ef database update --project src/OrderManagement.Infrastructure --startup-project src/OrderManagement.API
```

Em ambiente de desenvolvimento, as migrations são aplicadas automaticamente ao iniciar a API.

### Executar a API

```bash
dotnet run --project src/OrderManagement.API
```

A API ficará disponível em `https://localhost:5001` (ou porta configurada).

### Swagger

Acesse `https://localhost:5001/swagger` para a documentação interativa.

### Executar os testes

```bash
dotnet test
```

---

## Estrutura da solução

```
src/
  OrderManagement.Domain/          # Entidades, enums, exceções, interfaces, helpers
  OrderManagement.Application/     # DTOs, validators, services, interfaces de aplicação
  OrderManagement.Infrastructure/  # EF Core, repositórios, UoW, serviços de infraestrutura
  OrderManagement.API/             # Controllers, middleware, Program.cs
tests/
  OrderManagement.Tests/           # Testes unitários (domínio + application)
```

A separação em camadas segue **Clean Architecture**:

- **Domain**: sem dependências externas. Contém as regras de negócio puras.
- **Application**: orquestra casos de uso. Depende apenas de Domain.
- **Infrastructure**: implementa interfaces do Domain/Application. Conhece EF Core, SQL Server.
- **API**: camada de entrada HTTP. Conhece Application e Infrastructure (apenas para DI).

---

## Tecnologias utilizadas

| Tecnologia | Versão | Finalidade |
|---|---|---|
| .NET | 10 | Runtime e SDK |
| ASP.NET Core | 10 | API REST |
| Entity Framework Core | 9.x | ORM |
| Microsoft.Data.SqlClient | via EF | SQL Server |
| FluentValidation | 11.x | Validação de entrada |
| Swashbuckle (Swagger) | 6.x | Documentação interativa |
| xUnit | 2.x | Framework de testes |
| Moq | 4.x | Mock de dependências |
| FluentAssertions | 6.x | Assertions expressivas |

---

## Decisões técnicas e trade-offs

### Arquitetura
Optei por **Clean Architecture** porque o enunciado exige demonstrar clareza arquitetural, separação de responsabilidades e testabilidade. O custo é mais arquivos e projetos; o benefício é que cada camada tem uma única responsabilidade e pode ser testada de forma isolada.

### Validação em duas camadas
- **FluentValidation (Application)**: valida formato, obrigatoriedade e tipos — retorna 400.
- **Domain exceptions (Domain/Application services)**: valida regras de negócio (duplicidade, estoque, transições) — retorna 422.

Essa separação torna a origem do erro imediatamente clara para o consumidor da API e para o desenvolvedor.

### Repositories + Unit of Work
Repositórios abstraem o acesso a dados e permitem trocar a implementação (ex: PostgreSQL) sem alterar a camada de Application. O UnitOfWork centraliza o `SaveChangesAsync`, garantindo que todas as mudanças de um caso de uso sejam persistidas atomicamente.

### Exceções tipadas vs Result pattern
Optei por **exceções tipadas** (`NotFoundException`, `BusinessRuleException`) em vez de um Result pattern (ex: `Result<T, Error>`). A escolha simplifica o código dos services — que podem ser lidos como happy path — sem perder expressividade, já que o middleware captura e mapeia cada tipo para o código HTTP correto. Para uma API pública em produção com times grandes, um Result pattern seria mais seguro contra exceções não tratadas.

---

## Valores monetários e arredondamento

**Tipo escolhido: `decimal`**

`decimal` tem precisão exata para casas decimais, ao contrário de `float`/`double` (ponto flutuante binário), que acumulam erros em operações financeiras.

**Coluna no banco**: `decimal(18,2)` — 16 dígitos inteiros + 2 casas decimais, suficiente para qualquer valor de pedido real.

**Arredondamento**: `MidpointRounding.AwayFromZero` — o padrão contábil brasileiro. Valores de meio (ex: 0,015) arredondam para cima (0,02), não para o número par mais próximo.

O arredondamento é aplicado no item do pedido (`TotalValue = Math.Round(UnitPrice * Quantity, 2, MidpointRounding.AwayFromZero)`) e novamente no total do pedido.

---

## Datas, UTC e fuso horário

**Armazenamento**: todas as datas são persistidas em UTC (`DateTime` com `Kind = Utc`). O EF Core e o SQL Server armazenam como `datetime2`, sem informação de timezone.

**Resposta da API**: as datas são retornadas como `DateTimeOffset` convertido para `America/Sao_Paulo` via `TimezoneConverter` (implementado na Infrastructure). O offset resultante reflete automaticamente o horário de verão quando aplicável.

**Entrada**: a API aceita `DateTimeOffset`; o método `ToUtc` converte para UTC antes de persistir.

**Compatibilidade cross-platform**: o código tenta `"America/Sao_Paulo"` (IANA, Linux/macOS) e faz fallback para `"E. South America Standard Time"` (Windows).

---

## Estratégia de estoque e concorrência

### Comportamento sob requisições simultâneas

Quando dois pedidos são criados simultaneamente para o mesmo produto, a API utiliza **locking pessimista** no nível de linha do SQL Server.

O método `GetByIdsForUpdateAsync` executa:
```sql
SELECT * FROM [Products] WITH (UPDLOCK, ROWLOCK) WHERE [Id] IN (@p0, @p1, ...)
```

- `UPDLOCK` impede que outras transações adquiram shared locks enquanto a leitura acontece, evitando "dirty read" do estoque.
- `ROWLOCK` restringe o lock ao nível de linha (não página/tabela), minimizando contenção.

O resultado: a segunda requisição fica **bloqueada** até a primeira completar e liberar o lock. Depois, lê o estoque já atualizado e valida novamente — falhando com `INSUFFICIENT_STOCK` se não houver estoque suficiente.

**Por que pessimista e não otimista (RowVersion + retry)?**

O `RowVersion` está configurado no `Product` e funcionaria como alternativa: ao tentar salvar uma versão desatualizada, o EF lançaria `DbUpdateConcurrencyException` e o código poderia fazer retry. Porém, retry em caso de conflito gera latência variável e pode causar starvation sob alta carga. O locking pessimista serializa o acesso de forma previsível com custo fixo por operação. Para um sistema de pedidos onde a criação não é um hot path de altíssima concorrência, essa troca é razoável.

### Atomicidade da baixa de estoque

A criação do pedido e o débito de estoque ocorrem dentro da mesma transação do banco de dados (o `UnitOfWork.CommitAsync` persiste tudo de uma vez). Se qualquer item falhar na validação de estoque, nenhuma alteração é salva.

---

## Paginação

**Estratégia**: offset-based com parâmetros `page` e `pageSize`.

**Parâmetros de query**:
- `page`: número da página, começa em 1 (padrão: 1).
- `pageSize`: itens por página (padrão: 20, máximo: 100).

**Formato da resposta**:
```json
{
  "items": [...],
  "totalCount": 150,
  "page": 2,
  "pageSize": 20,
  "totalPages": 8,
  "hasNextPage": true,
  "hasPreviousPage": true
}
```

**Como paginar**: para obter a próxima página, incremente `page`. Repita até `page >= totalPages` ou `hasNextPage == false`.

**Trade-off**: offset-based é simples e amplamente compreendido, mas sofre com "page drift" em conjuntos de dados que mudam rapidamente (novos itens inseridos antes da página atual). Para um MVP de gestão de pedidos, essa limitação é aceitável. Cursor-based pagination (keyset) resolveria isso para grandes volumes.

---

## Validações

**Camada Application (FluentValidation)**:
- Nome obrigatório (clientes e produtos)
- E-mail com formato válido
- Documento obrigatório + validação algorítmica de CPF/CNPJ
- Preço > 0
- Estoque ≥ 0
- Pedido com ao menos um item, quantidade > 0

**Camada Domain/Service (exceções)**:
- E-mail duplicado entre clientes ativos
- Documento duplicado entre clientes ativos
- Cliente inativo não cria pedidos
- Produto inativo não entra em pedidos
- Estoque insuficiente ao criar pedido
- Transições de status inválidas

**CPF/CNPJ**: validação pelo algoritmo oficial dos dígitos verificadores, não apenas por máscara ou comprimento.

---

## Testes automatizados

### Tipos priorizados
**Testes unitários** — sem banco de dados, sem I/O. Dependências são substituídas por mocks (Moq).

### Justificativa
As regras de negócio mais críticas estão no Domain (entidades puras) e nos Application Services. Esses são os pontos onde erros têm maior impacto — transições de status inválidas, débito de estoque errado, preço errado no pedido. Testes unitários cobrem essas regras de forma rápida, determinística e sem infraestrutura.

Testes de integração (com banco real) seriam valiosos para validar as queries EF Core e o comportamento de locking, mas foram priorizados como "fora do escopo" desta entrega.

### Como executar

```bash
dotnet test
# Com cobertura (requer coverlet):
dotnet test --collect:"XPlat Code Coverage"
```

### Cobertura por área

| Área | O que está coberto |
|---|---|
| `CpfCnpjValidator` | CPF/CNPJ válidos, inválidos, sequências repetidas, tamanhos errados |
| `Order` (domínio) | Todas as transições válidas e inválidas, idempotência, cálculo de total, arredondamento |
| `Product` (domínio) | Débito, retorno e zeramento de estoque; ativação/desativação; alteração de preço |
| `CustomerService` | Criação, duplicidade de e-mail/documento, busca, atualização de status |
| `OrderService` | Criação completa, cliente/produto inativo, estoque insuficiente, snapshot de preço, cancelamento com retorno de estoque, idempotência de status |

---

## Pontos abertos e decisões documentadas

### Status igual ao atual (idempotência)
O enunciado diz que o comportamento deve ser "tratado de forma consistente e documentado". **Decisão**: retorna `200 OK` com o estado atual do pedido, sem criar registro de histórico e sem erro. Isso permite que clientes da API façam retries sem efeitos colaterais.

### Registro inicial de histórico
O enunciado diz que a criação pode ou não gerar registro inicial. **Decisão**: gera um registro com `PreviousStatus = null` e `NewStatus = Created`. Isso facilita auditorias (é possível saber exatamente quando o pedido foi criado pelo histórico).

### Preço unitário enviado pelo cliente
O enunciado deixa explícito que a API não deve aceitar `unitPrice` como fonte confiável do cliente. **Decisão**: o DTO de criação de pedido não possui campo de preço — ele é sempre obtido do cadastro do produto no momento da criação.

### Pedido pago → cancelado
O enunciado não lista `Pago → Cancelado` como transição permitida. **Decisão**: não é permitido. O fluxo `Criado → Cancelado` existe; uma vez pago, o pedido só avança.

### Remoção física de produtos
"Produtos vinculados a pedidos não devem ser removidos fisicamente." **Decisão**: não foi exposto endpoint de exclusão de produto. A inativação via `PATCH /produtos/{id}/status` é o único mecanismo de "remoção".

---

## Fora do escopo

Com mais tempo, eu abordaria:

- **Testes de integração**: usando `WebApplicationFactory` + banco in-memory ou Testcontainers para SQL Server. Cobriria os repositórios EF, os lock hints e o comportamento transacional completo.
- **Autenticação/autorização**: JWT com claims de permissão (ex: apenas admin pode inativar produto).
- **Filtros de listagem**: filtrar pedidos por status, clientes por ativo/inativo, produtos por faixa de preço.
- **Outbox pattern**: para garantir publicação de eventos (ex: "pedido criado") de forma eventual e consistente sem acoplamento síncrono.
- **Health checks**: endpoint `/health` com verificação de conectividade com o banco.
- **Rate limiting**: proteção contra abuso das APIs de criação.
- **Dockerfile + docker-compose**: facilitar execução sem configuração local de SQL Server.
