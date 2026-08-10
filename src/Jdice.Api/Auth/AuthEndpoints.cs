using System.Security.Claims;
using Jdice.Application.Users;
using Jdice.Domain.Users;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Jdice.Api.Auth;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder builder)
    {
        var group = builder.MapGroup("/api/auth").WithTags("Auth");

        group.MapPost("/login", LoginAsync)
            .AllowAnonymous()
            .RequireRateLimiting(LoginRateLimiting.PolicyName);
        group.MapPost("/logout", Logout).AllowAnonymous();
        group.MapGet("/me", GetCurrentUserAsync).RequireAuthorization();
        group.MapPost("/me/password", ChangePasswordAsync).RequireAuthorization();

        // Criar conta é operação de administração. No projeto original este
        // endpoint era público e aceitava a role no corpo — qualquer pessoa
        // criava um admin.
        group.MapPost("/users", CreateUserAsync).RequireAuthorization(AuthorizationPolicies.AdminOnly);
        group.MapGet("/users", ListUsersAsync).RequireAuthorization(AuthorizationPolicies.AdminOnly);
        group.MapDelete("/users/{id:guid}", DeactivateUserAsync)
            .RequireAuthorization(AuthorizationPolicies.AdminOnly);
        group.MapPost("/users/{id:guid}/reactivate", ReactivateUserAsync)
            .RequireAuthorization(AuthorizationPolicies.AdminOnly);

        return builder;
    }

    private static async Task<Results<NoContent, UnauthorizedHttpResult>> LoginAsync(
        [FromBody] LoginRequest request,
        AuthenticationService authentication,
        HttpContext httpContext,
        IWebHostEnvironment environment,
        CancellationToken cancellationToken)
    {
        var token = await authentication.LoginAsync(request.Email, request.Senha, cancellationToken);

        if (token is null)
        {
            // Mensagem única para e-mail inexistente e senha errada: distinguir
            // os dois casos entrega ao atacante a lista de quem tem conta.
            return TypedResults.Unauthorized();
        }

        httpContext.Response.Cookies.Append(
            AuthCookie.Name,
            token.Value,
            AuthCookie.Options(environment.IsDevelopment(), token.ExpiresAt));

        return TypedResults.NoContent();
    }

    private static NoContent Logout(HttpContext httpContext, IWebHostEnvironment environment)
    {
        httpContext.Response.Cookies.Delete(
            AuthCookie.Name,
            AuthCookie.Options(environment.IsDevelopment()));

        return TypedResults.NoContent();
    }

    private static async Task<Results<Ok<CurrentUserResponse>, UnauthorizedHttpResult>> GetCurrentUserAsync(
        ClaimsPrincipal principal,
        UserService userService,
        CancellationToken cancellationToken)
    {
        var subject = principal.FindFirstValue(JwtRegisteredClaimNames.Sub);

        if (!Guid.TryParse(subject, out var userId))
        {
            return TypedResults.Unauthorized();
        }

        var user = await userService.FindByIdAsync(userId, cancellationToken);

        // Token válido mas conta removida depois da emissão.
        if (user is null)
        {
            return TypedResults.Unauthorized();
        }

        return TypedResults.Ok(new CurrentUserResponse(user.Id, user.Email, user.Role.ToString()));
    }

    private static async Task<Results<NoContent, UnauthorizedHttpResult, BadRequest<ProblemDetails>>>
        ChangePasswordAsync(
            [FromBody] ChangePasswordRequest request,
            ClaimsPrincipal principal,
            UserService userService,
            CancellationToken cancellationToken)
    {
        var subject = principal.FindFirstValue(JwtRegisteredClaimNames.Sub);

        if (!Guid.TryParse(subject, out var userId))
        {
            return TypedResults.Unauthorized();
        }

        try
        {
            await userService.ChangePasswordAsync(
                userId,
                request.SenhaAtual,
                request.NovaSenha,
                cancellationToken);

            return TypedResults.NoContent();
        }
        catch (Exception exception)
            when (exception is InvalidCurrentPasswordException or ArgumentException)
        {
            // Senha atual errada e nova senha fraca são os dois erros de quem
            // chamou; a mensagem diz qual foi, sem vazar nada sensível.
            return TypedResults.BadRequest(Problema(
                StatusCodes.Status400BadRequest, "Não foi possível trocar a senha", exception.Message));
        }
        catch (UserNotFoundException)
        {
            // Token válido, conta sumiu depois da emissão.
            return TypedResults.Unauthorized();
        }
    }

    private static async Task<Results<Created<CreatedUserResponse>, Conflict<ProblemDetails>>> CreateUserAsync(
        [FromBody] CreateUserRequest request,
        UserService userService,
        CancellationToken cancellationToken)
    {
        try
        {
            var user = await userService.CreateAsync(
                request.Email,
                request.Senha,
                request.Role,
                cancellationToken);

            return TypedResults.Created(
                $"/api/auth/users/{user.Id}",
                new CreatedUserResponse(user.Id, user.Email, user.Role.ToString()));
        }
        catch (EmailAlreadyInUseException exception)
        {
            return TypedResults.Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "E-mail já cadastrado",
                Detail = exception.Message
            });
        }
    }

    private static async Task<Ok<IReadOnlyList<UserListItemResponse>>> ListUsersAsync(
        UserService userService,
        CancellationToken cancellationToken)
    {
        var users = await userService.ListAsync(cancellationToken);

        IReadOnlyList<UserListItemResponse> resposta = [.. users.Select(user =>
            new UserListItemResponse(
                user.Id,
                user.Email,
                user.Role.ToString(),
                user.IsActive,
                user.CreatedAt,
                user.DeactivatedAt))];

        return TypedResults.Ok(resposta);
    }

    private static async Task<Results<NoContent, NotFound<ProblemDetails>, Conflict<ProblemDetails>>>
        DeactivateUserAsync(
            Guid id,
            ClaimsPrincipal principal,
            UserService userService,
            CancellationToken cancellationToken)
    {
        var subject = principal.FindFirstValue(JwtRegisteredClaimNames.Sub);

        if (!Guid.TryParse(subject, out var requestedBy))
        {
            return TypedResults.NotFound(Problema(
                StatusCodes.Status404NotFound,
                "Sessão inválida",
                "Não foi possível identificar quem fez o pedido."));
        }

        try
        {
            await userService.DeactivateAsync(id, requestedBy, cancellationToken);

            return TypedResults.NoContent();
        }
        catch (UserNotFoundException exception)
        {
            return TypedResults.NotFound(Problema(
                StatusCodes.Status404NotFound, "Conta não encontrada", exception.Message));
        }
        catch (CannotDeactivateSelfException exception)
        {
            return TypedResults.Conflict(Problema(
                StatusCodes.Status409Conflict, "Operação não permitida", exception.Message));
        }
        catch (LastAdministratorException exception)
        {
            return TypedResults.Conflict(Problema(
                StatusCodes.Status409Conflict, "Último administrador", exception.Message));
        }
    }

    private static async Task<Results<NoContent, NotFound<ProblemDetails>>> ReactivateUserAsync(
        Guid id,
        UserService userService,
        CancellationToken cancellationToken)
    {
        try
        {
            await userService.ReactivateAsync(id, cancellationToken);

            return TypedResults.NoContent();
        }
        catch (UserNotFoundException exception)
        {
            return TypedResults.NotFound(Problema(
                StatusCodes.Status404NotFound, "Conta não encontrada", exception.Message));
        }
    }

    private static ProblemDetails Problema(int status, string titulo, string detalhe) => new()
    {
        Status = status,
        Title = titulo,
        Detail = detalhe
    };
}
