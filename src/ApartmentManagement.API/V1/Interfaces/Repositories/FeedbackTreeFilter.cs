namespace ApartmentManagement.API.V1.Interfaces.Repositories;

/// <param name="AllRootThreads">True: all top-level threads; false: subtree from <see cref="ScopeRootId"/>.</param>
/// <param name="ScopeRootId">Anchor feedback id when <see cref="AllRootThreads"/> is false.</param>
/// <param name="RestrictToActingUserOnly">When listing all roots, limit to threads owned by <see cref="ActingUserId"/>.</param>
/// <param name="ActingUserId">Current user for ownership filter.</param>
public readonly record struct FeedbackTreeFilter(
    bool AllRootThreads,
    Guid ScopeRootId,
    bool RestrictToActingUserOnly,
    Guid ActingUserId);
