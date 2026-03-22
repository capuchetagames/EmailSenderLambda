# 📧 EmailSenderLambda

Função AWS Lambda em C# (.NET 8) responsável pelo envio de e-mails transacionais da plataforma **FiapStore**.

Atualmente expõe dois endpoints:
- `POST /api/emails/welcome` — E-mail de boas-vindas após criação de usuário
- `POST /api/emails/payment-status` — E-mail de notificação de status de pagamento

---

## 📋 Pré-requisitos

Certifique-se de ter instalado na sua máquina:

| Ferramenta | Versão mínima | Download |
|---|---|---|
| .NET SDK | 8.0 | https://dotnet.microsoft.com/en-us/download |
| AWS CLI | v2 | https://aws.amazon.com/cli |
| Docker | Qualquer recente | https://www.docker.com/products/docker-desktop |
| LocalStack | via Docker | https://docs.localstack.cloud/getting-started |

---

## 🚀 Passo a Passo: Subindo a Lambda no LocalStack

### 1. Subir o LocalStack via Docker

```bash
docker run --rm -it \
  -p 4566:4566 \
  -e SERVICES=lambda,iam,logs \
  -v /var/run/docker.sock:/var/run/docker.sock \
  localstack/localstack
```

> Aguarde até ver a mensagem `Ready.` no terminal do LocalStack.

---

### 2. Criar a Role IAM (necessária para o Lambda)

```bash
aws --endpoint-url=http://localhost:4566 iam create-role \
  --role-name lambda-ex \
  --assume-role-policy-document '{
    "Version": "2012-10-17",
    "Statement": [{
      "Effect": "Allow",
      "Principal": { "Service": "lambda.amazonaws.com" },
      "Action": "sts:AssumeRole"
    }]
  }'
```

---

### 3. Restaurar as dependências do projeto

Navegue até a pasta do projeto e restaure os pacotes NuGet:

```bash
cd EmailSenderLambda
dotnet restore
```

---

### 4. Compilar e gerar o pacote de publicação

> **Importante:** compile sempre para `linux-arm64` se estiver em um Mac com Apple Silicon (M1/M2/M3).
> Use `linux-x64` se estiver em uma máquina Intel/AMD.

```bash
dotnet publish -c Release -o ./publish -r linux-arm64 --self-contained false
```

---

### 5. Criar o arquivo ZIP para upload

```bash
cd publish
zip -r ../EmailSenderLambda.zip *
cd ..
```

---

### 6. Criar a função Lambda no LocalStack

```bash
aws --endpoint-url=http://localhost:4566 lambda create-function \
  --function-name EmailSenderLambda \
  --runtime dotnet8 \
  --architectures arm64 \
  --timeout 30 \
  --role arn:aws:iam::000000000000:role/lambda-ex \
  --handler "EmailSenderApi::EmailSenderApi.Function::FunctionHandler" \
  --zip-file fileb://EmailSenderLambda.zip
```

> Se já tiver criado antes e quiser **atualizar** o código:
> ```bash
> aws --endpoint-url=http://localhost:4566 lambda update-function-code \
>   --function-name EmailSenderLambda \
>   --zip-file fileb://EmailSenderLambda.zip
> ```

---

### 6.1 Atualizar o código na AWS (após alterações)

Sempre que fizer mudanças no código, repita os passos abaixo para gerar um novo ZIP e enviar para a AWS:

```bash
# 1. Limpa e publica novamente
rm -rf publish
dotnet publish -c Release -o ./publish -r linux-arm64 --self-contained false

# 2. Gera o ZIP
cd publish
zip -r ../EmailSenderLambda.zip *
cd ..

# 3. Envia para a AWS
aws lambda update-function-code \
  --function-name EmailSenderLambda \
  --zip-file fileb://EmailSenderLambda.zip
```

> Para o **LocalStack**, adicione `--endpoint-url=http://localhost:4566` em todos os comandos `aws`.

---

### 7. Criar a Function URL (necessária para chamadas HTTP)

```bash
aws --endpoint-url=http://localhost:4566 lambda create-function-url-config \
  --function-name EmailSenderLambda \
  --auth-type NONE
```

Anote o valor de `FunctionUrl` retornado. Exemplo:
```
http://abc123.lambda-url.us-east-1.localhost.localstack.cloud:4566/
```

---

## 🧪 Testando os Endpoints

### Boas-Vindas (Welcome)

```bash
curl -X POST {FunctionUrl}/api/emails/welcome \
  -H "Content-Type: application/json" \
  -d '{
    "UserId": 105,
    "Name": "Allan Silva",
    "Email": "allan.silva@teste.com"
  }'
```

**Resposta esperada:**
```json
{ "message": "E-mail de boas-vindas acionado pela Lambda com sucesso!" }
```

---

### Status de Pagamento

```bash
curl -X POST {FunctionUrl}/api/emails/payment-status \
  -H "Content-Type: application/json" \
  -d '{
    "Status": "Aprovado",
    "Name": "Allan Silva",
    "Email": "allan.silva@teste.com"
  }'
```

**Resposta esperada:**
```json
{ "message": "E-mail de status de pagamento acionado pela Lambda com sucesso!" }
```

---

## 🔧 Comandos Úteis

| Objetivo | Comando |
|---|---|
| Ver logs da Lambda | `aws --endpoint-url=http://localhost:4566 logs filter-log-events --log-group-name /aws/lambda/EmailSenderLambda` |
| Verificar configuração da função | `aws --endpoint-url=http://localhost:4566 lambda get-function-configuration --function-name EmailSenderLambda` |
| Atualizar timeout | `aws --endpoint-url=http://localhost:4566 lambda update-function-configuration --function-name EmailSenderLambda --timeout 30` |
| Invocar diretamente (sem HTTP) | `aws --endpoint-url=http://localhost:4566 lambda invoke --function-name EmailSenderLambda --payload '{}' response.json` |

---

## 📁 Estrutura do Projeto

```
EmailSenderLambda/
├── Function.cs                  # Handler principal da Lambda com os dois endpoints
├── EmailSenderApi.csproj        # Definição do projeto (.NET 8, SDK Lambda)
├── appsettings.json
└── appsettings.Development.json
```

---

## 📦 Dependências NuGet

| Pacote | Versão | Descrição |
|---|---|---|
| `Amazon.Lambda.Core` | 2.5.0 | Runtime base do Lambda |
| `Amazon.Lambda.APIGatewayEvents` | 2.7.3 | Modelos de request/response do API Gateway |
| `Amazon.Lambda.Serialization.SystemTextJson` | 2.4.4 | Serialização automática JSON |

---

## 🛠️ Troubleshooting

### ❌ `Execution environment timed out during startup`

**Sintoma:** A chamada ao endpoint retorna `500` e o log do LocalStack exibe:
```
ERROR: exception during call chain: Execution environment timed out during startup.
```

Este foi o principal problema encontrado durante a configuração. Existem três causas possíveis e suas respectivas correções:

---

#### Causa 1: Timeout padrão muito curto (3 segundos)

O timeout padrão de qualquer Lambda na AWS é **3 segundos**. No LocalStack rodando localmente, o cold start do .NET pode facilmente ultrapassar esse limite.

**Solução:** Aumente o timeout para pelo menos 30 segundos:
```bash
aws --endpoint-url=http://localhost:4566 lambda update-function-configuration \
  --function-name EmailSenderLambda \
  --timeout 30
```
> **Atenção:** Se receber o erro `ResourceConflictException: An update is in progress`, aguarde alguns segundos e tente novamente.

---

#### Causa 2: Arquitetura incompatível (Mac Apple Silicon vs Lambda Linux)

Ao compilar em um Mac com processador **M1/M2/M3** sem especificar a arquitetura alvo, o binário gerado pode ser incompatível com o ambiente Linux x64 do LocalStack.

**Solução:** Sempre utilize o parâmetro `-r linux-arm64` ao publicar:
```bash
dotnet publish -c Release -o ./publish -r linux-arm64 --self-contained false
```

E ao criar/atualizar a função, especifique a arquitetura:
```bash
aws --endpoint-url=http://localhost:4566 lambda update-function-code \
  --function-name EmailSenderLambda \
  --architectures arm64 \
  --zip-file fileb://EmailSenderLambda.zip
```

---

#### Causa 3: Overhead do ASP.NET Core (abordagem errada para Lambda)

Usar `Amazon.Lambda.AspNetCoreServer.Hosting` sobe um servidor web ASP.NET Core completo (Kestrel + pipeline de middleware), que demora entre 10~15 segundos para inicializar, tornando inviável o uso no Lambda (que tem máximo de 15 min de execução e cold start pesado).

**Solução:** Este projeto foi reescrito para usar o modelo **Lambda puro** com `Amazon.Lambda.Core` + `Amazon.Lambda.APIGatewayEvents`, que inicializa em milissegundos sem nenhum servidor web embutido. O `csproj` também foi migrado de `Microsoft.NET.Sdk.Web` para `Microsoft.NET.Sdk`.

---

### ❌ Chamada direta para `localhost:4566/api/emails/welcome` retorna 200 vazio

**Sintoma:** O curl bate em `http://localhost:4566/api/emails/welcome` e retorna HTTP 200, mas sem body de resposta.

**Causa:** Você está chamando a porta principal do LocalStack (`4566`) diretamente, sem passar por uma **Function URL**. O LocalStack recebe a requisição genérica e não sabe qual Lambda invocar.

**Solução:** Crie uma Function URL (Passo 7 deste guia) e use a URL gerada com o hash único para suas chamadas. Exemplo:
```bash
# ❌ Errado - chama o LocalStack diretamente
curl -X POST http://localhost:4566/api/emails/welcome

# ✅ Correto - chama via Function URL específica da Lambda
curl -X POST http://abc123.lambda-url.us-east-1.localhost.localstack.cloud:4566/api/emails/welcome
```

---

## 🖥️ Dica: LocalStack Desktop (Dashboard Visual)

Para acompanhar e gerenciar os recursos criados no LocalStack de forma visual (sem precisar ficar rodando comandos AWS CLI), utilize o **LocalStack Desktop**.

Com ele você consegue:
- 📊 Ver todas as funções Lambda criadas e seus status
- 📋 Acompanhar os logs de execução em tempo real
- 🔍 Inspecionar configurações de IAM, S3, SQS e outros serviços
- ▶️ Invocar funções Lambda diretamente pela interface

**Download e documentação oficial:**
👉 [https://docs.localstack.cloud/user-guide/tools/localstack-desktop/](https://docs.localstack.cloud/user-guide/tools/localstack-desktop/)

---

### ❌ `{"Message":"Forbidden"}` ao chamar a Function URL

**Sintoma:** O cURL ou Postman retorna `403 Forbidden` ao tentar chamar o endpoint da Lambda.

**Causa:** A Function URL foi criada com `AuthType = AWS_IAM`, que exige autenticação com assinatura Sigv4 em cada requisição. Como a nossa Lambda é pública, o tipo deve ser `NONE`.

**Solução:** Atualize a configuração da Function URL via CLI:

```bash
# AWS real
aws lambda update-function-url-config \
  --function-name EmailSenderLambda \
  --auth-type NONE

# LocalStack
aws --endpoint-url=http://localhost:4566 lambda update-function-url-config \
  --function-name EmailSenderLambda \
  --auth-type NONE
```

Ou pelo **Console AWS**:
1. Lambda → `EmailSenderLambda` → aba **Configuration**
2. Menu lateral **Function URL** → **Edit**
3. Altere **Auth type** de `AWS_IAM` para **`NONE`** → Salve
