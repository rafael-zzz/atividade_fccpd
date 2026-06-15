# Mini E-commerce Distribuído — Instruções de Execução

## Pré-requisitos

**Opção A (Docker — recomendado):**
- Docker e Docker Compose instalados
- `openssl` instalado (para gerar certificado)

**Opção B (Local):**
- .NET 9 SDK ou superior
- `openssl` instalado (para gerar certificado)

---

## Execução com Docker (recomendado)

```bash
# 1. Gerar certificado TLS autoassinado
chmod +x certs/generate-cert.sh
./certs/generate-cert.sh

# 2. Subir todos os serviços
docker compose up --build

# 3. Acessar
# Gateway:   https://localhost:5050
# Dashboard: https://localhost:5050/dashboard
```

### Executar testes end-to-end automatizados

```bash
docker compose --profile test up --build
```

O container de teste executa 8 verificações automaticamente e exibe o resultado no terminal.

---

## Execução Local (sem Docker)

```bash
# 1. Gerar certificado TLS
chmod +x certs/generate-cert.sh
./certs/generate-cert.sh

# 2. Restaurar dependências
dotnet restore

# 3. Em terminais separados, executar cada serviço:

# Terminal 1 — Serviço de Usuários
cd src/Users && dotnet run

# Terminal 2 — Serviço de Produtos
cd src/Products && dotnet run

# Terminal 3 — Serviço de Pedidos
cd src/Orders && dotnet run

# Terminal 4 — API Gateway
cd src/Gateway && dotnet run
```

> **Nota:** Na execução local, os serviços usam `https://localhost` por padrão.

---

## Credenciais Padrão

| Usuário | Email | Senha | Role |
|---|---|---|---|
| Admin (criado automaticamente) | `admin@admin.com` | `admin123` | admin |

O usuário admin é criado automaticamente na primeira inicialização do serviço de usuários.

---

## Exemplos de Uso (curl)

> Use `-k` para aceitar o certificado autoassinado.

### 1. Login como admin

```bash
curl -k -X POST https://localhost:5050/users/login \
  -H "Content-Type: application/json" \
  -d '{"email":"admin@admin.com","password":"admin123"}'
```

Resposta:
```json
{
  "token": "eyJhbGciOiJIUzI1NiI...",
  "userId": "abc-123",
  "email": "admin@admin.com",
  "role": "admin"
}
```

### 2. Criar produto (como admin)

```bash
TOKEN="<token do login>"

curl -k -X POST https://localhost:5050/products \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $TOKEN" \
  -d '{"name":"Notebook Gamer","description":"RTX 4090, 32GB RAM","price":8999.90,"stock":5}'
```

### 3. Listar produtos (sem autenticação)

```bash
curl -k https://localhost:5050/products
```

### 4. Registrar usuário comum

```bash
curl -k -X POST https://localhost:5050/users/register \
  -H "Content-Type: application/json" \
  -d '{"name":"João Silva","email":"joao@email.com","password":"senha123"}'
```

### 5. Criar pedido

```bash
USER_TOKEN="<token do usuário>"
USER_ID="<id do usuário>"
PRODUCT_ID="<id do produto>"

curl -k -X POST https://localhost:5050/orders \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $USER_TOKEN" \
  -d "{\"userId\":\"$USER_ID\",\"items\":[{\"productId\":\"$PRODUCT_ID\",\"quantity\":2}]}"
```

### 6. Listar pedidos de um usuário

```bash
curl -k https://localhost:5050/orders/$USER_ID \
  -H "Authorization: Bearer $USER_TOKEN"
```

---

## Testando Heartbeat e Tolerância a Falhas

```bash
# Com Docker:

# 1. Derrubar o serviço de pedidos
docker compose stop orders

# 2. Observar logs do gateway (deve mostrar mensagem de FALHA)
docker compose logs -f gateway

# 3. Tentar acessar pedidos (deve retornar 503)
curl -k https://localhost:5050/orders/teste

# 4. Religar o serviço
docker compose start orders

# 5. Observar logs (deve mostrar RECUPERAÇÃO)
```

O dashboard em `https://localhost:5050/dashboard` exibe o status em tempo real.

---

## Portas dos Serviços

| Serviço | Porta | URL |
|---|---|---|
| API Gateway | 5050 (host) → 5000 (container) | `https://localhost:5050` |
| Usuários | 5001 | `https://localhost:5001` |
| Produtos | 5002 | `https://localhost:5002` |
| Pedidos | 5003 | `https://localhost:5003` |
| Dashboard | 5050 | `https://localhost:5050/dashboard` |

---

## Estrutura do Projeto

```
├── src/
│   ├── Shared/       ← Modelos, DTOs, JWT helper, JSON store
│   ├── Gateway/      ← API Gateway + Heartbeat + Dashboard
│   ├── Users/        ← Serviço de Usuários
│   ├── Products/     ← Serviço de Produtos (com replicação)
│   └── Orders/       ← Serviço de Pedidos
├── tests/            ← Container de teste end-to-end
├── certs/            ← Certificados TLS
├── Dockerfile        ← Build multi-stage dos serviços
└── docker-compose.yml
```
