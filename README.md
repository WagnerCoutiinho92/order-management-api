# Order Management API

**Order Management API** é uma API REST desenvolvida em .NET 10 para gerenciamento de pedidos, clientes, produtos e estoque. O projeto foi construído com foco em boas práticas de arquitetura.

**Principais características:**
- Clean Architecture com separação em camadas (Domain, Application, Infrastructure, API)
- Autenticação e autorização via JWT com perfis Admin e Customer
- Rate limiting por IP nas APIs de criação e autenticação
- Controle de estoque com concorrência pessimista (UPDLOCK) para evitar double-debit
- Transições de status com regras de negócio e histórico auditável
- Testes unitários e de integração com SQL Server real
- Health check com verificação de conectividade com o banco

---

## Sumário

1. [Como executar](#como-executar)
2. [Autenticação e autorização](#autenticação-e-autorização)
3. [Rate limiting](#rate-limiting)
4. [Estrutura da solução](#estrutura-da-solução)
5. [Tecnologias utilizadas](#tecnologias-utilizadas)
6. [Decisões técnicas e trade-offs](#decisões-técnicas-e-trade-offs)
7. [Valores monetários e arredondamento](#valores-monetários-e-arredondamento)
8. [Datas, UTC e fuso horário](#datas-utc-e-fuso-horário)
9. [Estratégia de estoque e concorrência](#estratégia-de-estoque-e-concorrência)
10. [Paginação](#paginação)
11. [Validações](#validações)
12. [Testes automatizados](#testes-automatizados)
13. [Pontos abertos e decisões documentadas](#pontos-abertos-e-decisões-documentadas)


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

Acesse `https://localhost:5001/swagger` para a documentação interativa. Todos os endpoints protegidos exigem o token JWT — clique em **Authorize**, cole o token obtido no login e confirme.

### Executar os testes

```bash
dotnet test
```

---

## Autenticação e autorização

A API utiliza **JWT Bearer** para autenticação. Todos os endpoints (exceto `/api/auth/*` e `/health`) exigem um token válido.

### Perfis

| Perfil | Registro | Acesso |
|---|---|---|
| **Admin** | Criado via seed no startup (Development) | Acesso total |
| **Customer** | `POST /api/auth/registrar` | GET de produtos e clientes; todas as operações de pedidos |

### Credenciais do admin (Development)

```
E-mail:  admin@orderapi.com
Senha:   Admin@123
```

### Endpoints

| Método | Rota | Descrição |
|---|---|---|
| `POST` | `/api/auth/registrar` | Cria conta com perfil Customer |
| `POST` | `/api/auth/login` | Autentica e retorna token JWT |

**Exemplo de login:**
```json
POST /api/auth/login
{
  "email": "admin@orderapi.com",
  "password": "Admin@123"
}
```

**Resposta:**
```json
{
  "token": "eyJhbGci...",
  "name": "Administrador",
  "email": "admin@orderapi.com",
  "role": "Admin",
  "expiresAt": "2026-06-25T21:00:00Z"
}
```

Use o campo `token` no header de todas as requisições subsequentes:
```
Authorization: Bearer eyJhbGci...
```

### Permissões por endpoint

| Operação | Admin | Customer |
|---|---|---|
| GET produtos / clientes | ✅ | ✅ |
| Criar / editar produto, preço, estoque, status | ✅ | ❌ |
| Criar cliente / atualizar status de cliente | ✅ | ❌ |
| Criar pedido / atualizar status de pedido | ✅ | ✅ |

### Implementação

- **Hash de senha**: PBKDF2 com SHA-256, 100.000 iterações e salt aleatório de 16 bytes — implementado com `Rfc2898DeriveBytes.Pbkdf2` nativo do .NET, sem dependências externas.
- **Token**: `HS256` com claims `sub`, `email`, `name`, `role` e `jti`. Expiração configurável via `appsettings.json` (padrão: 60 minutos).
- **Configuração JWT** em `appsettings.json`:
  ```json
  "Jwt": {
    "SecretKey": "...",
    "Issuer": "OrderManagementApi",
    "Audience": "OrderManagementApi",
    "ExpirationMinutes": 60
  }
  ```

---

## Rate limiting

Proteção contra abuso nas APIs de criação e autenticação, usando o rate limiter nativo do ASP.NET Core (sem pacotes externos).

### Políticas

| Política | Endpoints | Limite padrão |
|---|---|---|
| `auth` | `POST /api/auth/registrar`, `POST /api/auth/login` | 10 req/min por IP |
| `create` | `POST /api/clientes`, `POST /api/produtos`, `POST /api/pedidos` | 30 req/min por IP |

Endpoints de leitura (`GET`) não possuem limite.

### Resposta ao exceder o limite

```
HTTP 429 Too Many Requests
```
```json
{
  "code": "RATE_LIMIT_EXCEEDED",
  "message": "Muitas requisições. Aguarde antes de tentar novamente."
}
```

### Configuração em `appsettings.json`

```json
"RateLimiting": {
  "Auth": {
    "PermitLimit": 10,
    "WindowSeconds": 60
  },
  "Create": {
    "PermitLimit": 30,
    "WindowSeconds": 60
  }
}
```

---

## Estrutura da solução

```
src/
  OrderManagement.Domain/          # Entidades, enums, exceções, interfaces, helpers
  OrderManagement.Application/     # DTOs, validators, services, interfaces de aplicação
  OrderManagement.Infrastructure/  # EF Core, repositórios, UoW, JWT, serviços de infraestrutura
  OrderManagement.API/             # Controllers, middleware, Program.cs
tests/
  OrderManagement.Tests/           # Testes unitários (domínio + application)
  OrderManagement.IntegrationTests/ # Testes de integração com SQL Server real
```

A separação em camadas segue **Clean Architecture**:

- **Domain**: sem dependências externas. Contém as regras de negócio puras.
- **Application**: orquestra casos de uso. Depende apenas de Domain.
- **Infrastructure**: implementa interfaces do Domain/Application. Conhece EF Core, SQL Server e JWT.
- **API**: camada de entrada HTTP. Conhece Application e Infrastructure (apenas para DI).

---

## Tecnologias utilizadas

| Tecnologia | Versão | Finalidade |
|---|---|---|
| .NET | 10 | Runtime e SDK |
| ASP.NET Core | 10 | API REST, autenticação JWT, rate limiting |
| Entity Framework Core | 9.x | ORM |
| Microsoft.Data.SqlClient | via EF | SQL Server |
| Microsoft.AspNetCore.Authentication.JwtBearer | 9.x | Validação de tokens JWT |
| System.IdentityModel.Tokens.Jwt | 8.x | Geração de tokens JWT |
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

### Hash de senha sem dependências externas
Em vez de BCrypt (pacote externo), foi usado `Rfc2898DeriveBytes.Pbkdf2` nativo do .NET. O PBKDF2 com SHA-256 e 100.000 iterações atende às recomendações do NIST e não adiciona dependência ao projeto.

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
- Senha com no mínimo 6 caracteres (registro)

**Camada Domain/Service (exceções)**:
- E-mail duplicado entre clientes ativos
- Documento duplicado entre clientes ativos
- E-mail já cadastrado (registro de usuário)
- Credenciais inválidas (login)
- Cliente inativo não cria pedidos
- Produto inativo não entra em pedidos
- Estoque insuficiente ao criar pedido
- Transições de status inválidas

**CPF/CNPJ**: validação pelo algoritmo oficial dos dígitos verificadores, não apenas por máscara ou comprimento.

---

## Testes automatizados

### Tipos implementados

**Testes unitários** — sem banco de dados, sem I/O. Dependências são substituídas por mocks (Moq). Cobrem regras de domínio, application services e validações.

**Testes de integração** — usam SQL Server real (banco dedicado `OrderManagementDb_Test`). Validam os endpoints HTTP de ponta a ponta: roteamento, serialização, queries EF Core, locking pessimista, autenticação JWT e rate limiting.

### Como executar

```bash
dotnet test
# Com cobertura (requer coverlet):
dotnet test --collect:"XPlat Code Coverage"
```

### Cobertura por área

| Área | Tipo | O que está coberto |
|---|---|---|
| `CpfCnpjValidator` | Unitário | CPF/CNPJ válidos, inválidos, sequências repetidas, tamanhos errados |
| `Order` (domínio) | Unitário | Todas as transições válidas e inválidas, idempotência, cálculo de total, arredondamento |
| `Product` (domínio) | Unitário | Débito, retorno e zeramento de estoque; ativação/desativação; alteração de preço |
| `CustomerService` | Unitário | Criação, duplicidade de e-mail/documento, busca, atualização de status |
| `OrderService` | Unitário | Criação completa, cliente/produto inativo, estoque insuficiente, snapshot de preço, cancelamento com retorno de estoque, idempotência de status |
| `AuthService` | Unitário | Registro, e-mail duplicado, login com credenciais válidas e inválidas |
| Endpoints de clientes | Integração | CRUD completo, validações, duplicidade, autorização por perfil |
| Endpoints de pedidos | Integração | Criação, listagem, busca por ID, atualização de status, cancelamento — fluxo HTTP completo com banco real |
| Endpoints de auth | Integração | Registro (201, 400, 422), login (200, 422), 401 sem token, 403 com perfil insuficiente |
| Rate limiting | Integração | 429 ao exceder limite nas políticas `auth` e `create`; GET sem limite |

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
