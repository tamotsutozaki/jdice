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

        // Criar conta é operação de administração. No projeto original este
        // endpoint era público e aceitava a role no corpo — qualquer pessoa
        // criava um admin.
        group.MapPost("/users", CreateUserAsync).RequireAuthorization(AuthorizationPolicies.AdminOnly);

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
}
