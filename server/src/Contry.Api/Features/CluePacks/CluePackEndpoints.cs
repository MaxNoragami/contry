using System.Text.Json;
using Contry.Api.Common.EndpointFilters;
using Contry.Api.Common.Security;
using Contry.Application.Auth;
using Contry.Application.Errors;
using Contry.Domain.Clues;
using Contry.Infrastructure.Persistence;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Contry.Api.Features.CluePacks;

public static class CluePackEndpoints
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new(JsonSerializerDefaults.Web);

    public static IEndpointRouteBuilder MapCluePackEndpoints(this IEndpointRouteBuilder app)
    {
        var cluePacks = app.MapGroup("/clue-packs")
            .WithTags("Clue Packs");

        cluePacks.MapGet(string.Empty, ListCluePacksAsync)
            .WithName("ListCluePacks")
            .WithSummary("List published clue packs.")
            .WithDescription("Returns paginated published clue packs with optional search and owner filtering.")
            .Produces<ListCluePacksResponse>();

        cluePacks.MapGet("/{id:guid}", GetCluePackByIdAsync)
            .WithName("GetCluePackById")
            .WithSummary("Get a clue pack by id.")
            .Produces<CluePackDetailResponse>()
            .Produces(StatusCodes.Status404NotFound);

        cluePacks.MapPost(string.Empty, CreateCluePackAsync)
            .WithValidation<UpsertCluePackRequest>()
            .RequireAuthorization()
            .RequireXsrf()
            .WithName("CreateCluePack")
            .WithSummary("Create a published clue pack.")
            .Produces<CluePackDetailResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .Produces(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status409Conflict);

        cluePacks.MapPut("/{id:guid}", UpdateCluePackAsync)
            .WithValidation<UpsertCluePackRequest>()
            .RequireAuthorization()
            .RequireXsrf()
            .WithName("UpdateCluePack")
            .WithSummary("Replace a clue pack.")
            .Produces<CluePackDetailResponse>()
            .ProducesValidationProblem()
            .Produces(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);

        cluePacks.MapDelete("/{id:guid}", DeleteCluePackAsync)
            .RequireAuthorization()
            .RequireXsrf()
            .WithName("DeleteCluePack")
            .WithSummary("Delete a clue pack.")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);

        return app;
    }

    private static async Task<IResult> ListCluePacksAsync(
        HttpContext httpContext,
        ContryDbContext dbContext,
        int page = 1,
        int pageSize = 10,
        string? q = null,
        string? ownerId = null,
        string? visibility = null,
        string? sort = null,
        string? order = null,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 50);

        var hasIdentity = AccessTokenIdentityResolver.TryResolve(httpContext.User, out var identity) && identity is not null;
        var isAdmin = hasIdentity && string.Equals(identity!.Role, "ADMIN", StringComparison.Ordinal);
        var requestedOwnerId = Guid.TryParse(ownerId, out var parsedOwnerId) ? parsedOwnerId : (Guid?)null;
        var requestedVisibility = NormalizeVisibility(visibility);

        var query = dbContext.CluePacks
            .AsNoTracking()
            .Join(
                dbContext.Users.AsNoTracking(),
                pack => pack.OwnerId,
                user => user.Id,
                (pack, user) => new { Pack = pack, Owner = user });

        query = query.Where(entry =>
            entry.Pack.Visibility == "public"
            || (hasIdentity && entry.Pack.OwnerId == identity!.UserId)
            || isAdmin);

        if (requestedOwnerId is not null)
        {
            query = query.Where(entry => entry.Pack.OwnerId == requestedOwnerId.Value);
        }

        if (!string.IsNullOrWhiteSpace(requestedVisibility))
        {
            query = query.Where(entry => entry.Pack.Visibility == requestedVisibility);
        }

        if (!string.IsNullOrWhiteSpace(q))
        {
            var search = q.Trim().ToLowerInvariant();
            query = query.Where(entry =>
                entry.Pack.DatasetId.ToLower().Contains(search)
                || entry.Pack.Label.ToLower().Contains(search)
                || entry.Pack.Description.ToLower().Contains(search)
                || entry.Owner.Username.ToLower().Contains(search));
        }

        var normalizedSort = string.Equals(sort, "label", StringComparison.OrdinalIgnoreCase) ? "label" : "updatedAt";
        var descending = !string.Equals(order, "asc", StringComparison.OrdinalIgnoreCase);

        query = normalizedSort switch
        {
            "label" when descending => query.OrderByDescending(entry => entry.Pack.Label).ThenBy(entry => entry.Owner.Username),
            "label" => query.OrderBy(entry => entry.Pack.Label).ThenBy(entry => entry.Owner.Username),
            _ when descending => query.OrderByDescending(entry => entry.Pack.UpdatedAtUtc).ThenBy(entry => entry.Pack.Label),
            _ => query.OrderBy(entry => entry.Pack.UpdatedAtUtc).ThenBy(entry => entry.Pack.Label)
        };

        var totalCount = await query.CountAsync(cancellationToken);
        var rows = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var items = rows
            .Select(entry => ToListItem(entry.Pack, entry.Owner.Username, identity))
            .ToList();

        return TypedResults.Ok(new ListCluePacksResponse(items, totalCount, page, pageSize));
    }

    private static async Task<IResult> GetCluePackByIdAsync(
        Guid id,
        HttpContext httpContext,
        ContryDbContext dbContext,
        CancellationToken cancellationToken)
    {
        AccessTokenIdentityResolver.TryResolve(httpContext.User, out var identity);

        var entry = await dbContext.CluePacks
            .AsNoTracking()
            .Join(
                dbContext.Users.AsNoTracking(),
                pack => pack.OwnerId,
                user => user.Id,
                (pack, user) => new { Pack = pack, Owner = user })
            .SingleOrDefaultAsync(item => item.Pack.Id == id, cancellationToken);

        if (entry is null || !CanRead(entry.Pack, identity))
        {
            return TypedResults.NotFound();
        }

        return TypedResults.Ok(ToDetail(entry.Pack, entry.Owner.Username, identity));
    }

    private static async Task<IResult> CreateCluePackAsync(
        UpsertCluePackRequest request,
        HttpContext httpContext,
        ContryDbContext dbContext,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var identity = RequireIdentity(httpContext);
        var normalizedDatasetId = request.DatasetId.Trim();

        var collision = await dbContext.CluePacks.AnyAsync(
            pack => pack.OwnerId == identity.UserId && pack.DatasetId == normalizedDatasetId,
            cancellationToken);

        if (collision)
        {
            throw new CluePackConflictException("You already published a clue pack with this dataset id.");
        }

        var now = timeProvider.GetUtcNow();
        var pack = new CluePack
        {
            Id = Guid.NewGuid(),
            OwnerId = identity.UserId,
            DatasetId = normalizedDatasetId,
            Label = request.Label.Trim(),
            Description = request.Description.Trim(),
            Type = request.Type,
            Comparator = request.Comparator,
            UnitSymbol = NormalizeUnitSymbol(request.UnitSymbol),
            Icon = request.Icon.Trim(),
            CategoriesJson = SerializeCategories(request.Categories),
            RowsJson = SerializeRows(request.Rows),
            Visibility = NormalizeVisibility(request.Visibility) ?? "public",
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };

        dbContext.CluePacks.Add(pack);
        await dbContext.SaveChangesAsync(cancellationToken);

        var ownerUsername = await dbContext.Users
            .Where(user => user.Id == identity.UserId)
            .Select(user => user.Username)
            .SingleAsync(cancellationToken);

        return TypedResults.Created($"/clue-packs/{pack.Id}", ToDetail(pack, ownerUsername, identity));
    }

    private static async Task<IResult> UpdateCluePackAsync(
        Guid id,
        UpsertCluePackRequest request,
        HttpContext httpContext,
        ContryDbContext dbContext,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var identity = RequireIdentity(httpContext);
        var entry = await dbContext.CluePacks
            .Join(dbContext.Users, pack => pack.OwnerId, user => user.Id, (pack, user) => new { Pack = pack, Owner = user })
            .SingleOrDefaultAsync(item => item.Pack.Id == id, cancellationToken);

        if (entry is null)
        {
            return TypedResults.NotFound();
        }

        EnsureCanWrite(entry.Pack, identity);

        var normalizedDatasetId = request.DatasetId.Trim();
        var collision = await dbContext.CluePacks.AnyAsync(
            pack => pack.Id != id && pack.OwnerId == entry.Pack.OwnerId && pack.DatasetId == normalizedDatasetId,
            cancellationToken);

        if (collision)
        {
            throw new CluePackConflictException("The clue pack owner already has another clue pack with this dataset id.");
        }

        entry.Pack.DatasetId = normalizedDatasetId;
        entry.Pack.Label = request.Label.Trim();
        entry.Pack.Description = request.Description.Trim();
        entry.Pack.Type = request.Type;
        entry.Pack.Comparator = request.Comparator;
        entry.Pack.UnitSymbol = NormalizeUnitSymbol(request.UnitSymbol);
        entry.Pack.Icon = request.Icon.Trim();
        entry.Pack.CategoriesJson = SerializeCategories(request.Categories);
        entry.Pack.RowsJson = SerializeRows(request.Rows);
        entry.Pack.Visibility = NormalizeVisibility(request.Visibility) ?? entry.Pack.Visibility;
        entry.Pack.UpdatedAtUtc = timeProvider.GetUtcNow();

        await dbContext.SaveChangesAsync(cancellationToken);
        return TypedResults.Ok(ToDetail(entry.Pack, entry.Owner.Username, identity));
    }

    private static async Task<IResult> DeleteCluePackAsync(
        Guid id,
        HttpContext httpContext,
        ContryDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var identity = RequireIdentity(httpContext);
        var pack = await dbContext.CluePacks.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (pack is null)
        {
            return TypedResults.NotFound();
        }

        EnsureCanWrite(pack, identity);
        dbContext.CluePacks.Remove(pack);
        await dbContext.SaveChangesAsync(cancellationToken);
        return TypedResults.NoContent();
    }

    private static bool CanRead(CluePack pack, AccessTokenIdentity? identity)
        => pack.Visibility == "public"
           || (identity is not null && pack.OwnerId == identity.UserId)
           || (identity is not null && string.Equals(identity.Role, "ADMIN", StringComparison.Ordinal));

    private static void EnsureCanWrite(CluePack pack, AccessTokenIdentity identity)
    {
        var isAdmin = string.Equals(identity.Role, "ADMIN", StringComparison.Ordinal);
        if (!isAdmin && pack.OwnerId != identity.UserId)
        {
            throw new CluePackForbiddenException("You do not have permission to modify this clue pack.");
        }
    }

    private static AccessTokenIdentity RequireIdentity(HttpContext httpContext)
    {
        if (!AccessTokenIdentityResolver.TryResolve(httpContext.User, out var identity) || identity is null)
        {
            throw new InvalidAccessTokenException();
        }

        return identity;
    }

    private static string SerializeCategories(IReadOnlyList<string>? categories)
        => JsonSerializer.Serialize(categories?.Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()).ToArray() ?? [], JsonSerializerOptions);

    private static string SerializeRows(IReadOnlyList<CluePackRowRequest> rows)
        => JsonSerializer.Serialize(rows.Select(row => new CluePackRowPayload(row.CountryId.Trim(), row.Value)).ToArray(), JsonSerializerOptions);

    private static string? NormalizeVisibility(string? visibility)
    {
        if (string.IsNullOrWhiteSpace(visibility)) return null;
        return string.Equals(visibility.Trim(), "private", StringComparison.OrdinalIgnoreCase) ? "private" : "public";
    }

    private static string? NormalizeUnitSymbol(string? unitSymbol)
        => string.IsNullOrWhiteSpace(unitSymbol) ? null : unitSymbol.Trim();

    private static CluePackListItemResponse ToListItem(CluePack pack, string ownerUsername, AccessTokenIdentity? identity)
        => new(
            pack.Id,
            pack.DatasetId,
            pack.Label,
            pack.Description,
            pack.Type,
            pack.Comparator,
            pack.UnitSymbol,
            pack.Icon,
            pack.OwnerId,
            ownerUsername,
            pack.Visibility,
            pack.UpdatedAtUtc,
            identity is not null && (pack.OwnerId == identity.UserId || string.Equals(identity.Role, "ADMIN", StringComparison.Ordinal)));

    private static CluePackDetailResponse ToDetail(CluePack pack, string ownerUsername, AccessTokenIdentity? identity)
    {
        var categories = JsonSerializer.Deserialize<List<string>>(pack.CategoriesJson ?? "[]", JsonSerializerOptions) ?? [];
        var rows = JsonSerializer.Deserialize<List<CluePackRowPayload>>(pack.RowsJson, JsonSerializerOptions) ?? [];

        return new CluePackDetailResponse(
            pack.Id,
            pack.DatasetId,
            pack.Label,
            pack.Description,
            pack.Type,
            pack.Comparator,
            pack.UnitSymbol,
            pack.Icon,
            categories,
            rows.Select(row => new CluePackRowResponse(row.CountryId, row.Value)).ToArray(),
            pack.OwnerId,
            ownerUsername,
            pack.Visibility,
            pack.CreatedAtUtc,
            pack.UpdatedAtUtc,
            identity is not null && (pack.OwnerId == identity.UserId || string.Equals(identity.Role, "ADMIN", StringComparison.Ordinal)));
    }
}

public sealed record ListCluePacksResponse(
    IReadOnlyList<CluePackListItemResponse> Items,
    int TotalCount,
    int Page,
    int PageSize);

public sealed record CluePackListItemResponse(
    Guid Id,
    string DatasetId,
    string Label,
    string Description,
    string Type,
    string Comparator,
    string? UnitSymbol,
    string Icon,
    Guid OwnerId,
    string OwnerUsername,
    string Visibility,
    DateTimeOffset UpdatedAtUtc,
    bool CanEdit);

public sealed record CluePackDetailResponse(
    Guid Id,
    string DatasetId,
    string Label,
    string Description,
    string Type,
    string Comparator,
    string? UnitSymbol,
    string Icon,
    IReadOnlyList<string> Categories,
    IReadOnlyList<CluePackRowResponse> Rows,
    Guid OwnerId,
    string OwnerUsername,
    string Visibility,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    bool CanEdit);

public sealed record CluePackRowResponse(string CountryId, object? Value);

public sealed record CluePackRowRequest(string CountryId, object? Value);

internal sealed record CluePackRowPayload(string CountryId, object? Value);

public sealed record UpsertCluePackRequest(
    string DatasetId,
    string Label,
    string Description,
    string Type,
    string Comparator,
    string? UnitSymbol,
    string Icon,
    IReadOnlyList<string>? Categories,
    IReadOnlyList<CluePackRowRequest> Rows,
    string? Visibility);

public sealed class UpsertCluePackRequestValidator : AbstractValidator<UpsertCluePackRequest>
{
    public UpsertCluePackRequestValidator()
    {
        RuleFor(request => request.DatasetId).NotEmpty().MaximumLength(96);
        RuleFor(request => request.Label).NotEmpty().MaximumLength(120);
        RuleFor(request => request.Description).NotEmpty().MaximumLength(120);
        RuleFor(request => request.Type).Must(value => value is "numeric" or "categorical");
        RuleFor(request => request.Comparator).Must(value => value is "higher_lower" or "exact");
        RuleFor(request => request.Icon).NotEmpty().MaximumLength(64);
        RuleFor(request => request.UnitSymbol).MaximumLength(32);
        RuleFor(request => request.Visibility).Must(value => value is null or "public" or "private");
        RuleFor(request => request.Rows).NotEmpty();
        RuleForEach(request => request.Rows).SetValidator(new CluePackRowRequestValidator());
    }
}

public sealed class CluePackRowRequestValidator : AbstractValidator<CluePackRowRequest>
{
    public CluePackRowRequestValidator()
    {
        RuleFor(row => row.CountryId).NotEmpty().MaximumLength(16);
    }
}

file sealed class CluePackConflictException(string detail) : ConflictException(
    "https://contry.app/problems/clue-pack-conflict",
    "Clue pack conflict",
    detail);

file sealed class CluePackForbiddenException(string detail) : AppException(
    "https://contry.app/problems/clue-pack-forbidden",
    "Forbidden",
    StatusCodes.Status403Forbidden,
    detail);
