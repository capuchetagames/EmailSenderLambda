using System.Text.Json;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace EmailSenderApi;

public class Function
{
    public APIGatewayHttpApiV2ProxyResponse FunctionHandler(
        APIGatewayHttpApiV2ProxyRequest request,
        ILambdaContext context)
    {
        var path = request.RawPath ?? "";

        if (request.RequestContext?.Http?.Method == "POST" && path == "/api/emails/welcome")
        {
            var payload = JsonSerializer.Deserialize<WelcomeEmailRequest>(request.Body ?? "{}",
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (payload is null)
                return BadRequest("Payload inválido.");

            context.Logger.LogInformation(
                $"📧 Welcome email para >> {payload.UserId} | {payload.Name} | {payload.Email}");

            return Ok(new { message = "E-mail de boas-vindas acionado pela Lambda com sucesso!" });
        }

        if (request.RequestContext?.Http?.Method == "POST" && path == "/api/emails/payment-status")
        {
            var payload = JsonSerializer.Deserialize<PaymentEmailRequest>(request.Body ?? "{}",
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (payload is null)
                return BadRequest("Payload inválido.");

            context.Logger.LogInformation(
                $"💳 Mensagem de Status da Compra : {payload.Status} | {payload.Name} | {payload.Email}");

            return Ok(new { message = "E-mail de status de pagamento acionado pela Lambda com sucesso!" });
        }

        return NotFound();
    }

    private static APIGatewayHttpApiV2ProxyResponse Ok(object body) => new()
    {
        StatusCode = 200,
        Headers = new Dictionary<string, string> { { "Content-Type", "application/json" } },
        Body = JsonSerializer.Serialize(body)
    };

    private static APIGatewayHttpApiV2ProxyResponse BadRequest(string msg) => new()
    {
        StatusCode = 400,
        Body = JsonSerializer.Serialize(new { error = msg })
    };

    private static APIGatewayHttpApiV2ProxyResponse NotFound() => new()
    {
        StatusCode = 404,
        Body = JsonSerializer.Serialize(new { error = "Rota não encontrada." })
    };
}

public record WelcomeEmailRequest(int UserId, string Name, string Email);
public record PaymentEmailRequest(string Status, string Name, string Email);
