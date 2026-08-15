using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using SundayLeague.Api.Data;
using SundayLeague.Api.Domain;

namespace SundayLeague.Api.Auth;

public sealed class BoardAuthorizationService(LeagueDbContext db)
{
    public Task<bool> CanConfigureAsync(ClaimsPrincipal user, Guid leagueId) => HasRoleAsync(user, leagueId, BoardRole.Owner, BoardRole.CompetitionAdmin);
    public Task<bool> CanEditResultsAsync(ClaimsPrincipal user, Guid leagueId) => HasRoleAsync(user, leagueId, BoardRole.Owner, BoardRole.CompetitionAdmin, BoardRole.ResultsEditor);
    public Task<bool> IsOwnerAsync(ClaimsPrincipal user) => HasRoleAsync(user, null, BoardRole.Owner);

    public async Task<bool> IsBoardMemberAsync(ClaimsPrincipal user)
    {
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        return userId is not null && await db.BoardMemberships.AnyAsync(membership => membership.UserId == userId);
    }

    public async Task<bool> HasRoleAsync(ClaimsPrincipal user, Guid? leagueId, params BoardRole[] allowedRoles)
    {
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null) return false;
        return await db.BoardMemberships.AnyAsync(membership => membership.UserId == userId && allowedRoles.Contains(membership.Role) &&
            (membership.Role == BoardRole.Owner || membership.LeagueId == leagueId));
    }
}
