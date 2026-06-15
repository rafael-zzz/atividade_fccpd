#!/bin/bash
# Não usar set -e pois alguns curls falham intencionalmente (ex: teste 403)

BASE_URL="${BASE_URL:-https://gateway:5000}"
CURL="curl -sk --fail-with-body"
PASSED=0
FAILED=0

pass() { echo "  ✅ $1"; PASSED=$((PASSED+1)); }
fail() { echo "  ❌ $1"; FAILED=$((FAILED+1)); }

echo ""
echo "════════════════════════════════════════"
echo " Teste End-to-End — Mini E-commerce"
echo "════════════════════════════════════════"
echo ""

# 1. Health check
echo "[1/8] Verificando saúde do gateway..."
if $CURL $BASE_URL/health | jq -e '.status == "ok"' > /dev/null 2>&1; then
  pass "Gateway healthy"
else
  fail "Gateway não respondeu"
fi
echo ""

# 2. Login como admin (seed)
echo "[2/8] Login como admin..."
ADMIN_RESPONSE=$($CURL -X POST $BASE_URL/users/login \
  -H "Content-Type: application/json" \
  -d '{"email":"admin@admin.com","password":"admin123"}')
ADMIN_TOKEN=$(echo $ADMIN_RESPONSE | jq -r '.token')
if [ "$ADMIN_TOKEN" != "null" ] && [ -n "$ADMIN_TOKEN" ]; then
  pass "Login admin retornou JWT"
  echo "  Token: ${ADMIN_TOKEN:0:20}..."
else
  fail "Login admin falhou"
  echo "  Resposta: $ADMIN_RESPONSE"
fi
echo ""

# 3. Criar produto como admin
echo "[3/8] Criando produto como admin..."
PRODUCT_RESPONSE=$($CURL -X POST $BASE_URL/products \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $ADMIN_TOKEN" \
  -d '{"name":"Notebook Gamer","description":"RTX 4090, 32GB RAM","price":8999.90,"stock":5}')
PRODUCT_ID=$(echo $PRODUCT_RESPONSE | jq -r '.id')
if [ "$PRODUCT_ID" != "null" ] && [ -n "$PRODUCT_ID" ]; then
  pass "Produto criado: $PRODUCT_ID"
else
  fail "Criação de produto falhou"
fi
echo ""

# 4. Listar produtos (sem auth)
echo "[4/8] Listando produtos (sem auth)..."
PRODUCTS=$($CURL $BASE_URL/products)
COUNT=$(echo $PRODUCTS | jq 'length')
if [ "$COUNT" -ge 1 ]; then
  pass "Listagem retornou $COUNT produto(s)"
else
  fail "Listagem vazia"
fi
echo ""

# 5. Registrar usuário comum
echo "[5/8] Registrando usuário comum..."
USER_RESPONSE=$($CURL -X POST $BASE_URL/users/register \
  -H "Content-Type: application/json" \
  -d '{"name":"João Silva","email":"joao@email.com","password":"senha123"}')
USER_ID=$(echo $USER_RESPONSE | jq -r '.id')
if [ "$USER_ID" != "null" ] && [ -n "$USER_ID" ]; then
  pass "Usuário criado: $USER_ID"
else
  fail "Registro de usuário falhou"
fi
echo ""

# 6. Testar controle de acesso (user tentando criar produto)
echo "[6/8] Testando controle de acesso (403)..."
USER_LOGIN=$($CURL -X POST $BASE_URL/users/login \
  -H "Content-Type: application/json" \
  -d '{"email":"joao@email.com","password":"senha123"}')
USER_TOKEN=$(echo $USER_LOGIN | jq -r '.token')
HTTP_CODE=$(curl -sk -o /dev/null -w "%{http_code}" -X POST $BASE_URL/products \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $USER_TOKEN" \
  -d '{"name":"Teste","description":"Proibido","price":1,"stock":1}')
if [ "$HTTP_CODE" = "403" ]; then
  pass "Acesso negado corretamente (HTTP 403)"
else
  fail "Esperava 403, recebeu $HTTP_CODE"
fi
echo ""

# 7. Criar pedido
echo "[7/8] Criando pedido..."
ORDER_RESPONSE=$($CURL -X POST $BASE_URL/orders \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $USER_TOKEN" \
  -d "{\"userId\":\"$USER_ID\",\"items\":[{\"productId\":\"$PRODUCT_ID\",\"quantity\":2}]}")
ORDER_ID=$(echo $ORDER_RESPONSE | jq -r '.id')
if [ "$ORDER_ID" != "null" ] && [ -n "$ORDER_ID" ]; then
  pass "Pedido criado: $ORDER_ID"
  TOTAL=$(echo $ORDER_RESPONSE | jq -r '.total')
  echo "  Total: R$ $TOTAL"
else
  fail "Criação de pedido falhou"
fi
echo ""

# 8. Listar pedidos do usuário
echo "[8/8] Listando pedidos do usuário..."
ORDERS=$($CURL $BASE_URL/orders/$USER_ID \
  -H "Authorization: Bearer $USER_TOKEN")
ORDER_COUNT=$(echo $ORDERS | jq 'length')
if [ "$ORDER_COUNT" -ge 1 ]; then
  pass "Listagem retornou $ORDER_COUNT pedido(s)"
else
  fail "Listagem de pedidos vazia"
fi
echo ""

# Resultado final
echo "════════════════════════════════════════"
echo " Resultado: $PASSED aprovados, $FAILED falhos"
echo "════════════════════════════════════════"

if [ $FAILED -gt 0 ]; then
  exit 1
fi
exit 0
